# API Reference — WhatsApp CRM (LeadIa)

This document describes the API surface of the WhatsApp CRM conversation-processing system. It covers the external webhook endpoint, internal event schemas, data structures, and DynamoDB access patterns derived from the [System Architecture](./SYSTEM_ARCHITECTURE.md).

See also:
- [Docker Setup](./DOCKER.md) — containerization, environment variables, and service wiring
- [Queue Reference](./QUEUE.md) — NATS JetStream streams, consumers, retry, and monitoring

---

## Table of Contents

1. [Webhook Endpoint](#1-webhook-endpoint)
2. [Normalized Message Schema](#2-normalized-message-schema)
3. [Event Bus (NATS JetStream)](#3-event-bus-nats-jetstream)
4. [Conversation State](#4-conversation-state)
5. [Fact Schema](#5-fact-schema)
6. [DynamoDB Access Patterns](#6-dynamodb-access-patterns)
7. [LLM Context Payload](#7-llm-context-payload)
8. [Error Handling](#8-error-handling)
9. [End-to-End Flow Summary](#9-end-to-end-flow-summary)

---

## 1. Webhook Endpoint

Entry point for all inbound WhatsApp messages.

```
POST /webhook/whatsapp
```

### Request Headers

| Header           | Value                      | Required |
|------------------|----------------------------|----------|
| `Content-Type`   | `application/json`         | ✅       |
| `X-Hub-Signature-256` | `sha256=<hmac>`       | ✅       |

### Request Body

Raw WhatsApp payload forwarded by the provider (e.g. Meta Cloud API). The system normalizes this internally — see [§2 Normalized Message Schema](#2-normalized-message-schema).

### Signature Verification

The `X-Hub-Signature-256` header must be validated before processing any payload.

```
signature = HMAC-SHA256(key=WEBHOOK_SECRET, message=raw_request_body)
expected  = "sha256=" + hex(signature)
if not constant_time_compare(expected, request.headers["X-Hub-Signature-256"]):
    return HTTP 401
```

> Use a **constant-time comparison** to prevent timing attacks.

### Response

| Status | Meaning                                           |
|--------|---------------------------------------------------|
| `200`  | Message accepted and queued for processing        |
| `400`  | Malformed payload or missing required fields      |
| `401`  | Invalid webhook signature                         |
| `409`  | Duplicate message detected (hash already seen)    |
| `500`  | Internal server error                             |

> **Note:** Always respond with `200` as fast as possible. Heavy processing occurs asynchronously via NATS JetStream workers after the webhook acknowledges receipt.

---

## 2. Normalized Message Schema

All incoming messages are normalized into this internal structure before being published to the event queue.

```typescript
interface NormalizedMessage {
  conversation_id:  string;   // "<broker_number>-<customer_number>"
  broker_id:        string;
  customer_id:      string;
  sender_type:      "broker" | "customer";  // any other value → HTTP 400
  text:             string;
  timestamp:        string;   // ISO 8601, e.g. "2026-03-10T14:22:01Z"
  message_hash:     string;   // SHA-256 hex — used for deduplication
}
```

### Conversation ID Format

```
conversation_id = <broker_phone_number> + "-" + <customer_phone_number>
```

**Example:**

```
4798913312-47839948
```

### Normalization Rules Applied to `text`

```
text = trim(text)
text = collapse_multiple_spaces(text)
text = normalize_newlines(text)
text = encode_as_utf8(text)
```

### Example Payload

```json
{
  "conversation_id": "4798913312-47839948",
  "broker_id":       "broker_77",
  "customer_id":     "cust_456",
  "sender_type":     "customer",
  "text":            "I'm looking for an apartment downtown",
  "timestamp":       "2026-03-10T14:22:01Z",
  "message_hash":    "b7af3e2c019d4a8e7f123abc456def7890123456789012345678901234567890"
}
```

### Deduplication

```
message_hash = SHA256(timestamp + broker_id + customer_id + text)
```

If `message_hash` matches `last_message_hash` stored in the conversation state, the message is **silently ignored**.

---

## 3. Event Bus (NATS JetStream)

Internal events are transmitted via **NATS JetStream**. Workers subscribe to streams rather than polling databases.

> For full stream configuration, consumer setup, retry policies, and monitoring, see [QUEUE.md](./QUEUE.md).

### Streams

| Stream Name            | Subject(s)                                          | Consumer queue group       |
|------------------------|-----------------------------------------------------|----------------------------|
| `messages`             | `message.received`                                  | `message-workers`          |
| `persistence_events`   | `persist.message`, `persist.summary`, `persist.facts` | `persistence-workers`    |

### Published Events

#### `message.received`

Published by the webhook handler after normalization and deduplication.

```json
{
  "type":    "message.received",
  "payload": { /* NormalizedMessage */ }
}
```

#### `persist.message`

Emitted by the message worker to durably store a message.

```json
{
  "type": "persist.message",
  "payload": {
    "conversation_id": "4798913312-47839948",
    "timestamp":       "2026-03-10T14:22:01Z",
    "sender_type":     "customer",
    "text":            "I'm looking for an apartment downtown",
    "hash":            "b7af..."
  }
}
```

#### `persist.summary`

Emitted after a rolling summary is updated.

```json
{
  "type": "persist.summary",
  "payload": {
    "conversation_id":   "4798913312-47839948",
    "rolling_summary":   "User is searching for a two-bedroom apartment downtown with a budget around 600k.",
    "last_message_hash": "b7af3e2c...",
    "updated_at":        "2026-03-10T14:25:00Z"
  }
}
```

#### `persist.facts`

Emitted after facts are extracted or updated.

```json
{
  "type": "persist.facts",
  "payload": {
    "conversation_id": "4798913312-47839948",
    "facts": [
      { "name": "property_type", "value": "apartment",  "confidence": 0.95 },
      { "name": "location",      "value": "downtown",   "confidence": 0.92 },
      { "name": "budget",        "value": 600000,       "confidence": 0.88 }
    ],
    "updated_at": "2026-03-10T14:25:00Z"
  }
}
```

---

## 4. Conversation State

The local fast-memory cache holds one `ConversationState` object per active conversation.

```typescript
interface ConversationState {
  conversation_id:          string;
  rolling_summary:          string;
  facts:                    Record<string, FactEntry>;
  buffer:                   string[];   // recent unsummarized messages
  buffer_chars:             number;
  last_message_hash:        string;
  last_message_timestamp:   string;    // ISO 8601
  last_activity_timestamp:  string;    // ISO 8601
}
```

### User Classification on First Message

If `conversation_id` is **not found** in the database, it is a **new lead** and state is initialized as:

```json
{
  "rolling_summary": "",
  "facts":           {},
  "buffer":          []
}
```

### Cache Rules

| Parameter         | Value                        |
|-------------------|------------------------------|
| Storage           | In-process LRU / Redis       |
| Key               | `conversation_id`            |
| TTL               | 10 minutes after last activity |

---

## 5. Fact Schema

Facts represent structured data extracted from the conversation.

```typescript
interface FactEntry {
  value:       string | number | boolean | null;
  confidence:  number;   // 0.0 – 1.0
  updated_at:  string;   // ISO 8601
}
```

### Example Facts

```json
{
  "intent":             { "value": "buy",         "confidence": 0.97, "updated_at": "..." },
  "property_type":      { "value": "apartment",   "confidence": 0.95, "updated_at": "..." },
  "location":           { "value": "downtown",    "confidence": 0.92, "updated_at": "..." },
  "budget":             { "value": 600000,         "confidence": 0.88, "updated_at": "..." },
  "bedrooms":           { "value": 2,              "confidence": 0.91, "updated_at": "..." },
  "garage_required":    { "value": true,           "confidence": 0.80, "updated_at": "..." },
  "lead_score":         { "value": 42,             "confidence": 0.82, "updated_at": "..." }
}
```

### Message Buffer Limits

| Parameter       | Limit        |
|-----------------|--------------|
| `max_messages`  | 6 messages   |
| `max_chars`     | 500 chars    |
| `timeout`       | 30 seconds   |
| `expiration`    | 5 minutes    |

### Rolling Summary Trigger Conditions

| Condition                        | Threshold      |
|----------------------------------|----------------|
| Buffer message count             | ≥ 5            |
| Buffer character count           | ≥ 400          |
| Time since last message          | > 30 seconds   |

### Facts TTL

Facts are evicted from local memory and persisted to DynamoDB after:

```
10 minutes of inactivity (no new messages)
```

---

## 6. DynamoDB Access Patterns

**Table name:** `crm_memory` — Single Table Design.

### Key Schema

| Attribute | Type   | Role            |
|-----------|--------|-----------------|
| `PK`      | String | Partition key   |
| `SK`      | String | Sort key        |

---

### 6.1 Conversation Metadata

```
PK = CONV#<conversation_id>
SK = META
```

**Example record:**

```json
{
  "PK":                    "CONV#4798913312-47839948",
  "SK":                    "META",
  "rolling_summary":       "User searching for apartment downtown",
  "last_message_hash":     "ad98ab3",
  "last_message_timestamp":"2026-03-10T14:22:01Z",
  "broker_id":             "broker_77",
  "customer_id":           "cust_456"
}
```

---

### 6.2 Messages

```
PK = CONV#<conversation_id>
SK = MSG#<timestamp_iso8601>
```

**Example record:**

```json
{
  "PK":     "CONV#4798913312-47839948",
  "SK":     "MSG#2026-03-10T14:22:01Z",
  "sender": "customer",
  "text":   "I'm looking for an apartment downtown",
  "hash":   "ab2939c"
}
```

**Query all messages for a conversation:**

```
PK = CONV#<conversation_id>   AND   SK begins_with "MSG#"
```

---

### 6.3 Facts

```
PK = CONV#<conversation_id>
SK = FACT#<fact_name>
```

**Example record:**

```json
{
  "PK":         "CONV#4798913312-47839948",
  "SK":         "FACT#budget",
  "value":      600000,
  "confidence": 0.92,
  "updated_at": "2026-03-10T14:23:00Z"
}
```

**Query all facts for a conversation:**

```
PK = CONV#<conversation_id>   AND   SK begins_with "FACT#"
```

---

## 7. LLM Context Payload

When building the prompt context to send to the LLM, the system assembles:

```
[SUMMARY]          ← rolling_summary
[FACTS]            ← relevant extracted facts
[RECENT MESSAGES]  ← current buffer contents
[NEW MESSAGE]      ← incoming message text
```

**Example:**

```
SUMMARY
User is searching for a two-bedroom apartment downtown with a budget around 600k.

FACTS
intent: buy
property_type: apartment
location: downtown
price_range_min: 500000
price_range_max: 700000
budget: 600000
bedrooms: 2
financing_preapproved: false
purpose: residence
purchase_timeline: up_to_3_months
visit_interest: true
mentioned_property_id: null
lead_score: 42
confidence: 0.82

RECENT MESSAGES
Does it have parking?

NEW MESSAGE
Can you send photos?
```

---

## 8. Error Handling

| Scenario                         | Behaviour                                              |
|----------------------------------|--------------------------------------------------------|
| Duplicate message (same hash)    | Silently ignored; `200` returned to webhook provider  |
| Invalid webhook signature        | Request rejected with `401` immediately               |
| State not in cache               | Loaded from DynamoDB, then cached                      |
| DynamoDB write failure           | Retried by persistence worker via NATS retry policy    |
| Buffer expiration without flush  | Buffer force-flushed and summary triggered             |
| LLM call failure                 | Logged; summary generation skipped for this cycle      |
| Unknown `sender_type`            | Message rejected with `400`                            |
| NATS publish failure             | Webhook returns `500`; WhatsApp provider will retry   |

---

## 9. End-to-End Flow Summary

```
1.  WhatsApp sends POST /webhook/whatsapp
2.  api-gateway verifies HMAC signature
3.  Payload is normalized into NormalizedMessage
4.  SHA-256 hash checked against last_message_hash → skip if duplicate
5.  User classification: new lead → initialize ConversationState
6.  api-gateway publishes message.received to NATS (stream: messages)
7.  api-gateway returns HTTP 200 ← end of synchronous path

──── Asynchronous path (message-worker) ────

8.  message-worker consumes message.received
9.  ConversationState loaded from Redis/LRU cache (or DynamoDB if cache miss)
10. Message appended to buffer
11. Fact extraction runs against new message
12. If buffer thresholds met → LLM called → rolling_summary updated → buffer cleared
13. message-worker publishes:
      persist.message  → always
      persist.summary  → if summary was updated
      persist.facts    → if facts changed
14. message-worker ACKs message.received

──── Asynchronous path (persistence-worker) ────

15. persistence-worker consumes persist.* events
16. Writes to DynamoDB crm_memory table
17. persistence-worker ACKs each event
```
 and
---

## 10. CRM Data Model (Relational)

In future phases, administrative and relational data will be stored in **SQLite**, while the conversation memory remains in DynamoDB.

### 10.1 Structured Tables Requirements

#### Table: `real_state_agency`
Stores property-specific information.
- `id`: Primary Key (GUID)
- `name`: Title/Name of the property
- `address`: Full location string
- `description`: Detailed property text

#### Table: `real_state_broker`
Manages the relationship between properties and responsible brokers.
- `id`: Primary Key
- `real_state_id`: Foreign Key to `real_state`
#### Table: `broker_data`
A flexible key-value store for broker-specific information (phones, emails, etc.)
- `id`: Primary Key
- `broker_id`: Foreign Key to `broker` profile
- `broken_name`: String (the name of the broker), same as data_key but for humans
- `data_key`: String (e.g., "phone", "email", "whatsapp", "instagram")
- `data_value`: String (the actual contact detail or value)
- `is_preferred`: Boolean (marks the primary/preferred entry for a specific key)
- `description`: String (description of the data) if needed
- `created_at`: DateTime
- `updated_at`: DateTime

### 10.2 Administrative Roles

The system must support a **Super Admin** role with full **CRUD (Create, Read, Update, Delete)** capabilities over the CRM tables:
- Ability to manually add/edit properties (`real_state`).
- Ability to assign properties to brokers (`real_state_broker`).
- Ability to manage arbitrary broker data (`broker_data`), allowing for multiple phones, emails, and preference settings.
- Direct access to manage the relational state through a dedicated admin interface.
