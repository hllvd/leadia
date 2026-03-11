# Data Flow — LeadIa (WhatsApp CRM)

This document provides a complete end-to-end walkthrough of how data moves through the system — from a WhatsApp message arriving at the webhook to being persisted in DynamoDB. It covers three key scenarios.

See also:
- [System Architecture](./SYSTEM_ARCHITECTURE.md) — architecture overview
- [API Reference](./API.md) — schemas and event payloads
- [Queue Reference](./QUEUE.md) — NATS streams and consumers
- [Workers Reference](./WORKERS.md) — worker processing logic
- [Cache Reference](./CACHE.md) — conversation state management
- [LLM Reference](./LLM.md) — fact extraction and summarization
- [DynamoDB Reference](./DYNAMODB.md) — persistence layer

---

## Table of Contents

1. [System Component Map](#1-system-component-map)
2. [Scenario A — New Lead, First Message](#2-scenario-a--new-lead-first-message)
3. [Scenario B — Returning Lead, Normal Message](#3-scenario-b--returning-lead-normal-message)
4. [Scenario C — Buffer Threshold Reached (LLM Trigger)](#4-scenario-c--buffer-threshold-reached-llm-trigger)
5. [Scenario D — Duplicate Message Detected](#5-scenario-d--duplicate-message-detected)
6. [Complete Data Flow Diagram](#6-complete-data-flow-diagram)
7. [Latency Profile](#7-latency-profile)
8. [Data Residency Summary](#8-data-residency-summary)

---

## 1. System Component Map

```
┌────────────────────────────────────────────────────────────────┐
│                        External                                │
│   WhatsApp Cloud API  ──────▶  POST /webhook/whatsapp          │
└──────────────────────────────────────┬─────────────────────────┘
                                       │
┌──────────────────────────────────────▼─────────────────────────┐
│  api-gateway                                                   │
│  • HMAC signature verification                                 │
│  • Message normalization                                       │
│  • Deduplication (hash check)                                  │
│  • Publish → NATS (message.received)                           │
│  • Return HTTP 200                                             │
└──────────────────────────────────────┬─────────────────────────┘
                                       │
              ┌────────────────────────▼────────────────┐
              │  NATS JetStream                          │
              │  stream: messages                        │
              │  subject: message.received               │
              └────────────────────────┬────────────────┘
                                       │
┌──────────────────────────────────────▼─────────────────────────┐
│  message-worker  (queue group: message-workers)                │
│  • Load ConversationState (cache → DynamoDB)                   │
│  • Update message buffer                                       │
│  • Extract facts + trigger rolling summary (LLM)              │
│  • Publish → NATS (persist.*)                                  │
│  • Update cache                                                │
└──────────────────┬──────────────────────────┬──────────────────┘
                   │                          │
         ┌─────────▼────────┐    ┌────────────▼────────────────┐
         │  Redis / LRU     │    │  NATS JetStream              │
         │  (fast memory)   │    │  stream: persistence_events  │
         └──────────────────┘    │  subjects: persist.*         │
                                 └────────────┬────────────────-┘
                                              │
              ┌───────────────────────────────▼────────────────┐
              │  persistence-worker                            │
              │  (queue group: persistence-workers)            │
              │  • Write to DynamoDB                           │
              └───────────────────────────────┬────────────────┘
                                              │
                              ┌───────────────▼──────────────┐
                              │  DynamoDB (crm_memory)        │
                              │  Single Table Design          │
                              └──────────────────────────────┘
```

---

## 2. Scenario A — New Lead, First Message

A brand new customer contacts a broker for the first time.

```
Step  Component           Action
────  ──────────────────  ─────────────────────────────────────────────────
 1    WhatsApp            POST /webhook/whatsapp (raw payload)
 2    api-gateway         Verify HMAC signature → pass
 3    api-gateway         Normalize payload into NormalizedMessage
                          conversation_id = "4798913312-47839948"
 4    api-gateway         Compute message_hash = SHA256(ts+broker+customer+text)
 5    api-gateway         Check local last_message_hash → no match (first msg)
 6    api-gateway         Publish message.received to NATS
 7    api-gateway         Return HTTP 200 to WhatsApp ← end of sync path

 8    message-worker      Consume message.received
 9    message-worker      GET cache["4798913312-47839948"] → MISS
10    message-worker      Query DynamoDB CONV#... META → not found
11    message-worker      Initialize new ConversationState:
                          { rolling_summary: "", facts: {}, buffer: [] }
12    message-worker      Append message text to buffer
13    message-worker      buffer thresholds NOT met (1 message, < 400 chars)
14    message-worker      Publish persist.message
15    message-worker      SET cache["4798913312-47839948"] (TTL=600s)
16    message-worker      ACK message.received

17    persistence-worker  Consume persist.message
18    persistence-worker  PutItem:  PK=CONV#..., SK=MSG#<timestamp>
19    persistence-worker  Also PutItem:  PK=CONV#..., SK=META  (new record)
20    persistence-worker  ACK persist.message
```

**DynamoDB state after Scenario A:**
```
PK=CONV#4798913312-47839948  SK=META     → { rolling_summary: "", broker_id: ..., customer_id: ... }
PK=CONV#4798913312-47839948  SK=MSG#...  → { sender: "customer", text: "...", hash: "..." }
```

---

## 3. Scenario B — Returning Lead, Normal Message

The same customer sends a follow-up message. The conversation state is already cached.

```
Step  Component           Action
────  ──────────────────  ─────────────────────────────────────────────────
 1–7  api-gateway         Same as Scenario A (normalize → publish → 200)

 8    message-worker      Consume message.received
 9    message-worker      GET cache["4798913312-47839948"] → HIT
10    message-worker      Verify message_hash != last_message_hash → OK
11    message-worker      Append message text to buffer (now 2 messages)
12    message-worker      buffer thresholds NOT met yet
13    message-worker      Publish persist.message
14    message-worker      Update last_message_hash + last_message_timestamp
15    message-worker      SET cache (refresh TTL)
16    message-worker      ACK message.received

17    persistence-worker  Consume persist.message
18    persistence-worker  PutItem:  SK=MSG#<new_timestamp>
19    persistence-worker  ACK persist.message
```

**Cache state after Scenario B:**
```json
{
  "buffer": ["I'm looking for an apartment downtown", "Something with parking please"],
  "buffer_chars": 76,
  "rolling_summary": "",
  "facts": {}
}
```

---

## 4. Scenario C — Buffer Threshold Reached (LLM Trigger)

After 5 messages (or 400+ chars, or 30s timeout), the buffer threshold is met.

```
Step  Component           Action
────  ──────────────────  ─────────────────────────────────────────────────
 1–7  api-gateway         Normalize + publish message.received

 8    message-worker      Consume message.received
 9    message-worker      GET cache → HIT
10    message-worker      Append message to buffer → threshold met!
                          buffer: 5 messages, 420 chars

11    message-worker      Assemble LLM context:
                          [SUMMARY] "" + [FACTS] {} + [BUFFER] 5 msgs + [MSG] new msg

12    message-worker      Call LLM API (gpt-4o, temp=0.2, json_object)
                          → returns: { summary: "...", facts: { intent: ..., budget: ... } }

13    message-worker      Parse LLM response:
                          • Update rolling_summary
                          • Merge facts into ConversationState.facts
                          • Clear buffer (buffer=[], buffer_chars=0)

14    message-worker      Publish persist.message
15    message-worker      Publish persist.summary  (summary was updated)
16    message-worker      Publish persist.facts    (facts were extracted)
17    message-worker      SET cache (updated state, refresh TTL)
18    message-worker      ACK message.received

19    persistence-worker  Consume persist.message  → PutItem MSG#...
20    persistence-worker  Consume persist.summary  → UpdateItem META (rolling_summary, last_message_hash)
21    persistence-worker  Consume persist.facts    → BatchWriteItem FACT#intent, FACT#budget, ...
22    persistence-worker  ACK all events
```

**DynamoDB state after Scenario C:**
```
SK=META          → { rolling_summary: "User searching for 2-bed apartment downtown, budget 600k" }
SK=MSG#ts1..5    → individual message records
SK=FACT#intent   → { value: "buy", confidence: 0.97 }
SK=FACT#budget   → { value: 600000, confidence: 0.88 }
SK=FACT#location → { value: "downtown", confidence: 0.92 }
```

---

## 5. Scenario D — Duplicate Message Detected

WhatsApp resends a message (common with webhook retries).

```
Step  Component           Action
────  ──────────────────  ─────────────────────────────────────────────────
 1    WhatsApp            POST /webhook/whatsapp (same message resent)
 2    api-gateway         Verify HMAC → pass
 3    api-gateway         Normalize → same message_hash as before
 4    api-gateway         Publish message.received to NATS
 5    api-gateway         Return HTTP 200

 6    message-worker      Consume message.received
 7    message-worker      GET cache → HIT
 8    message-worker      Check: message_hash == last_message_hash → MATCH
 9    message-worker      SKIP processing (duplicate detected)
10    message-worker      ACK message.received (no publish of persist.*)
```

No database writes occur. No LLM is called. The duplicate is silently discarded.

---

## 6. Complete Data Flow Diagram

```
WhatsApp Cloud API
        │  POST /webhook/whatsapp
        ▼
┌───────────────────┐
│   api-gateway     │──── HTTP 200 ────────────────────────────────▶ WhatsApp
│                   │
│ 1. Verify HMAC    │
│ 2. Normalize msg  │
│ 3. Hash check     │
│ 4. Publish event  │
└────────┬──────────┘
         │ message.received
         ▼
┌───────────────────────────────────────────────────────────────────┐
│  NATS JetStream — stream: messages                                │
└───────────────────────────────────────────────────────────────────┘
         │ PUSH (queue group: message-workers)
         ▼
┌───────────────────┐          ┌─────────────────┐
│  message-worker   │◀────────▶│  Redis / LRU    │
│                   │  GET/SET │  (fast memory)  │
│ 1. Load state     │          └─────────────────┘
│ 2. Update buffer  │                   ▲ cache miss
│ 3. LLM (if ready) │                   │
│ 4. Publish events │          ┌─────────────────┐
└────────┬──────────┘          │    DynamoDB     │
         │                     │  (crm_memory)   │
         │ persist.*           └─────────────────┘
         ▼                              ▲
┌───────────────────────────────────────│──────────────────────────┐
│  NATS JetStream — stream: persistence_events                     │
└───────────────────────────────────────│──────────────────────────┘
         │ PUSH (queue group: persistence-workers)
         ▼
┌───────────────────┐                   │
│ persistence-worker│───────────────────┘
│                   │  PutItem / UpdateItem / BatchWrite
│ 1. Route event    │
│ 2. Write DynamoDB │
│ 3. ACK event      │
└───────────────────┘
```

---

## 7. Latency Profile

| Path segment                          | Typical latency |
|---------------------------------------|-----------------|
| WhatsApp → api-gateway (network)      | 20–100 ms       |
| api-gateway processing + publish      | 3–10 ms         |
| api-gateway → HTTP 200                | **< 50 ms total** |
| NATS delivery to message-worker       | < 1 ms          |
| Cache hit + buffer update             | 1–5 ms          |
| Cache miss + DynamoDB load            | 5–25 ms         |
| LLM call (when triggered)             | 500–3 000 ms    |
| NATS delivery to persistence-worker   | < 1 ms          |
| DynamoDB write                        | 5–20 ms         |

**Webhook response time** (what WhatsApp observes): typically **< 50 ms**, independent of LLM and database latency because all heavy work is asynchronous.

---

## 8. Data Residency Summary

| Data                  | Where it lives                          | Retention         |
|-----------------------|-----------------------------------------|-------------------|
| Raw webhook payload   | Never stored — normalized immediately   | —                 |
| NormalizedMessage     | NATS (transient until ACK)             | Until ACK'd       |
| ConversationState     | Redis / LRU cache                       | 10 min TTL        |
| message buffer        | Cache only (not persisted)              | 10 min TTL        |
| rolling_summary       | Cache + DynamoDB META record            | 1 year (DynamoDB) |
| message records       | DynamoDB MSG# items                     | 90 days           |
| facts                 | Cache + DynamoDB FACT# items            | 1 year (DynamoDB) |
| last_message_hash     | Cache + DynamoDB META record            | 1 year (DynamoDB) |
| persist.* events      | NATS (WorkQueue — deleted on ACK)       | Until ACK'd       |
