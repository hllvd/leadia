# Workers Reference — LeadIa (WhatsApp CRM)

This document describes the two asynchronous worker services that power the conversation processing pipeline: `message-worker` and `persistence-worker`.

See also:
- [System Architecture](./SYSTEM_ARCHITECTURE.md)
- [Queue Reference](./QUEUE.md) — NATS JetStream streams and consumer configuration
- [LLM Reference](./LLM.md) — fact extraction and rolling summary logic
- [Cache Reference](./CACHE.md) — conversation state and memory management
- [Docker Setup](./DOCKER.md) — containerization and deployment

---

## Table of Contents

1. [Architecture Overview](#1-architecture-overview)
2. [Message Worker](#2-message-worker)
3. [Persistence Worker](#3-persistence-worker)
4. [Event-Driven Design Principles](#4-event-driven-design-principles)
5. [Horizontal Scaling](#5-horizontal-scaling)
6. [Worker Startup Sequence](#6-worker-startup-sequence)
7. [Failure Handling](#7-failure-handling)
8. [Observability](#8-observability)

---

## 1. Architecture Overview

```
WhatsApp Webhook
      │
      ▼
 api-gateway
      │  PUBLISH message.received
      ▼
 NATS JetStream (stream: messages)
      │  PUSH to queue group
      ▼
 message-worker  ──────────────────────────────────────────────┐
      │                                                         │
      │  Manages ConversationState (cache + LLM)               │
      │                                                         │
      │  PUBLISH persist.message                               │
      │  PUBLISH persist.summary  (if summary updated)         │
      │  PUBLISH persist.facts    (if facts changed)           │
      ▼                                                         │
 NATS JetStream (stream: persistence_events)                   │
      │  PUSH to queue group                                    │
      ▼                                                         │
 persistence-worker                                            │
      │                                                         │
      │  Writes to DynamoDB (crm_memory)                       │
      └───────────────────────────────────────────────────────-┘
```

The `api-gateway` is only responsible for receiving, validating, normalizing, and publishing messages — it does **no heavy processing**. All stateful computation is delegated to the workers.

---

## 2. Message Worker

### Responsibilities

The `message-worker` is the heart of the system. It:

1. Consumes `message.received` events from NATS
2. Loads `ConversationState` from cache (or DynamoDB on cache miss)
3. Checks for duplicate messages via `message_hash`
4. Classifies the user (new lead vs existing lead)
5. Appends the message to the conversation buffer
6. Triggers fact extraction + rolling summary if buffer thresholds are met (via LLM)
7. Publishes persistence events for async storage
8. ACKs the NATS message

### Processing Steps (per message)

```
1.  Receive message.received from NATS
2.  Load ConversationState from cache
      └── Cache miss? Load from DynamoDB → store in cache
3.  Check conversation_id in database
      └── Not found? Initialize new ConversationState (new lead)
4.  Verify message_hash != last_message_hash
      └── Match? Skip (duplicate) → ACK immediately
5.  Append message text to buffer
6.  Update buffer_chars count
7.  Update last_message_hash + last_message_timestamp
8.  Check buffer thresholds (messages, chars, timeout)
      └── Met? → Call LLM (see LLM.md)
            ├── Update rolling_summary
            ├── Merge new facts into ConversationState.facts
            └── Clear buffer
9.  Publish persist.message
10. If summary updated → Publish persist.summary
11. If facts changed  → Publish persist.facts
12. Update ConversationState in cache
13. ACK message.received
```

### NATS Consumer Config

| Property           | Value                  |
|--------------------|------------------------|
| Stream             | `messages`             |
| Consumer name      | `message-worker-group` |
| Queue group        | `message-workers`      |
| Ack policy         | Explicit               |
| Max deliver        | 5                      |
| Ack wait           | 30 seconds             |

### Environment Variables

| Variable                    | Description                              |
|-----------------------------|------------------------------------------|
| `NATS_URL`                  | NATS server connection string            |
| `NATS_STREAM_MESSAGES`      | Stream name for inbound messages         |
| `NATS_STREAM_PERSISTENCE`   | Stream name for persistence events       |
| `REDIS_URL`                 | Redis connection string for cache        |
| `OPENAI_API_KEY`            | LLM API key                             |
| `LLM_MODEL`                 | Model identifier (e.g. `gpt-4o`)        |
| `CACHE_TTL_SECONDS`         | Conversation state TTL (default: 600)    |
| `BUFFER_MAX_MESSAGES`       | Buffer message limit (default: 6)        |
| `BUFFER_MAX_CHARS`          | Buffer char limit (default: 500)         |
| `BUFFER_TIMEOUT_SECONDS`    | Buffer flush timeout (default: 30)       |
| `BUFFER_EXPIRATION_SECONDS` | Buffer max lifetime (default: 300)       |
| `SUMMARY_TRIGGER_MESSAGES`  | Trigger threshold in messages (default: 5) |
| `SUMMARY_TRIGGER_CHARS`     | Trigger threshold in chars (default: 400)|
| `FACTS_TTL_SECONDS`         | Facts TTL in local memory (default: 600) |

---

## 3. Persistence Worker

### Responsibilities

The `persistence-worker` is intentionally thin. It:

1. Consumes `persist.*` events from NATS
2. Translates each event into the appropriate DynamoDB write
3. ACKs each event after a successful write

It has **no business logic** — it is purely a database write adapter.

### Event Handlers

| Subject           | DynamoDB operation                          | Key pattern                            |
|-------------------|---------------------------------------------|----------------------------------------|
| `persist.message` | `PutItem`                                   | `PK=CONV#<id>`, `SK=MSG#<timestamp>`  |
| `persist.summary` | `UpdateItem` (metadata record)              | `PK=CONV#<id>`, `SK=META`             |
| `persist.facts`   | `BatchWriteItem` (one item per fact)        | `PK=CONV#<id>`, `SK=FACT#<fact_name>` |

### Processing Steps (per event)

```
1. Receive persist.* event from NATS
2. Identify event type
3. Map payload to DynamoDB operation
4. Execute write (with exponential backoff on throttling)
5. ACK event on success
   └── Write fails after retries? → NAK → NATS re-delivers
```

### NATS Consumer Config

| Property           | Value                       |
|--------------------|-----------------------------|
| Stream             | `persistence_events`        |
| Consumer name      | `persistence-worker-group`  |
| Queue group        | `persistence-workers`       |
| Ack policy         | Explicit                    |
| Max deliver        | 10                          |
| Ack wait           | 60 seconds                  |

> Longer `ack_wait` and higher `max_deliver` than the message worker accommodate DynamoDB throttling retries.

### Environment Variables

| Variable                  | Description                                    |
|---------------------------|------------------------------------------------|
| `NATS_URL`                | NATS server connection string                  |
| `NATS_STREAM_PERSISTENCE` | Stream name for persistence events             |
| `DYNAMODB_REGION`         | AWS region                                     |
| `DYNAMODB_TABLE`          | Table name (`crm_memory`)                      |
| `DYNAMODB_ENDPOINT`       | Override endpoint for local dev                |
| `AWS_ACCESS_KEY_ID`       | AWS credentials (use IAM role in production)   |
| `AWS_SECRET_ACCESS_KEY`   | AWS credentials (use IAM role in production)   |

---

## 4. Event-Driven Design Principles

### No Database Polling

Workers **never poll** DynamoDB or any other database. They only react to events pushed by NATS JetStream. This eliminates:
- Unnecessary CPU cycles
- Wasteful database read costs
- Tight coupling between services

### At-Least-Once Delivery

NATS JetStream guarantees that each message is delivered **at least once**. Workers must therefore be **idempotent**:

- `persist.message` — `PutItem` with the same key is a safe overwrite
- `persist.summary` — `UpdateItem` on META record is safe on repeated calls
- `persist.facts`   — `PutItem` per fact key is safe on repeated calls

Duplicate `message.received` events are caught by the `message_hash` check before any processing occurs.

### Separation of Concerns

| Worker              | Knows about         | Does NOT know about    |
|---------------------|---------------------|------------------------|
| `message-worker`    | Cache, LLM, buffer  | Database write details |
| `persistence-worker`| DynamoDB schema     | LLM, cache, buffer     |

---

## 5. Horizontal Scaling

Both workers support **horizontal scaling** via NATS queue groups. Adding more instances of a worker automatically load-balances message delivery — no configuration changes required.

```
NATS JetStream (stream: messages)
        │
        ├──▶ message-worker instance 1  (queue group: message-workers)
        ├──▶ message-worker instance 2
        └──▶ message-worker instance 3
```

Each `conversation_id` will be processed by whichever instance picks up the message first. Because conversation state is stored in a **shared cache (Redis)**, any instance can load the state for any conversation.

### Scaling Recommendations

| Traffic level        | message-worker replicas | persistence-worker replicas |
|----------------------|-------------------------|-----------------------------|
| < 1 000 msg/day      | 1                       | 1                           |
| 1 000 – 50 000/day   | 2–3                     | 2                           |
| > 50 000/day         | 5+                      | 3–5                         |

> **Note:** Scaling the persistence worker beyond ~5 replicas may require increasing DynamoDB capacity or switching to on-demand billing mode.

---

## 6. Worker Startup Sequence

On startup, each worker performs:

```
1. Connect to NATS
2. Verify JetStream stream exists (create if not — idempotent)
3. Register durable consumer with queue group
4. Connect to Redis (message-worker only)
5. Connect to DynamoDB (persistence-worker: verify table exists)
6. Begin consuming messages
```

If NATS or Redis is unavailable at startup, the worker should **crash and restart** (managed by Docker's `restart: unless-stopped` or a Kubernetes liveness probe).

---

## 7. Failure Handling

### Message Worker

| Failure                        | Behaviour                                                            |
|--------------------------------|----------------------------------------------------------------------|
| Cache miss                     | Load from DynamoDB → store in cache → continue                       |
| DynamoDB load failure          | Error logged; message NAK'd for re-delivery                          |
| LLM call failure               | Logged; summary/facts skipped; buffer **retained** for next trigger  |
| NATS publish failure (persist) | Error logged; message NAK'd so nothing is lost                       |
| Message ACK timeout            | NATS re-delivers; `message_hash` check prevents double-processing    |

### Persistence Worker

| Failure                        | Behaviour                                                            |
|--------------------------------|----------------------------------------------------------------------|
| DynamoDB throttling            | Exponential backoff retry (up to `max_deliver` attempts)             |
| DynamoDB unavailable           | NAK → NATS holds the event and re-delivers                           |
| Invalid event schema           | Logged; event sent to dead-letter subject (`persist.DEAD`)           |
| Max deliver exceeded           | Event moved to dead-letter for manual inspection                     |

---

## 8. Observability

### Recommended Metrics

| Metric                                   | Source            |
|------------------------------------------|-------------------|
| Messages processed per second            | message-worker    |
| LLM calls per minute                     | message-worker    |
| LLM call latency (p50, p95, p99)         | message-worker    |
| Buffer flush rate                        | message-worker    |
| Cache hit rate                           | message-worker    |
| DynamoDB writes per second               | persistence-worker|
| DynamoDB write error rate                | persistence-worker|
| NATS consumer pending message count      | NATS monitoring   |
| Dead-letter event count                  | NATS monitoring   |

### Structured Log Fields

Each worker log entry should include:

```json
{
  "timestamp":       "2026-03-10T23:10:00Z",
  "level":           "info",
  "service":         "message-worker",
  "conversation_id": "4798913312-47839948",
  "event_type":      "message.received",
  "duration_ms":     42,
  "llm_called":      true,
  "cache_hit":       true
}
```
