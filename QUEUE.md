# Queue Reference — WhatsApp CRM (Second Brain)

This document describes the **NATS JetStream** message queue infrastructure used in the WhatsApp CRM system. It covers stream configuration, subject naming conventions, consumer setup, message lifecycle, retry policies, and monitoring.

See also:
- [System Architecture](./SYSTEM_ARCHITECTURE.md)
- [API Reference](./API.md)
- [Docker Setup](./DOCKER.md)

---

## Table of Contents

1. [Why NATS JetStream?](#1-why-nats-jetstream)
2. [NATS Server Configuration](#2-nats-server-configuration)
3. [Streams](#3-streams)
4. [Subjects & Message Schemas](#4-subjects--message-schemas)
5. [Consumers](#5-consumers)
6. [Message Lifecycle](#6-message-lifecycle)
7. [Retry & Error Policy](#7-retry--error-policy)
8. [Producer Reference](#8-producer-reference)
9. [Consumer Reference](#9-consumer-reference)
10. [Monitoring](#10-monitoring)
11. [Local Development](#11-local-development)

---

## 1. Why NATS JetStream?

The system uses **NATS JetStream** (persistent, at-least-once delivery) as its event bus because:

| Requirement                     | How NATS JetStream Meets It                                           |
|---------------------------------|-----------------------------------------------------------------------|
| No database polling             | Workers subscribe to subjects; messages are pushed to them            |
| At-least-once delivery          | JetStream persists messages and re-delivers on consumer acknowledgment timeout |
| Horizontal scaling              | Multiple workers join the same **queue group** — NATS load-balances   |
| Low latency                     | In-process pub/sub with microsecond delivery                          |
| Durability                      | Messages are stored to disk until acknowledged                        |
| Replay / audit                  | Consumers can replay from any sequence number                         |

---

## 2. NATS Server Configuration

### `infra/nats/nats-server.conf`

```conf
# nats-server.conf — JetStream enabled

server_name: crm-nats

# Client connections
port: 4222

# HTTP monitoring
http_port: 8222

# JetStream storage
jetstream {
  store_dir: /data/jetstream
  max_memory_store: 1GB
  max_file_store:   10GB
}

# Cluster settings (production — add more routes for HA)
# cluster {
#   name: crm-cluster
#   listen: 0.0.0.0:6222
#   routes: [
#     nats-route://nats-2:6222
#     nats-route://nats-3:6222
#   ]
# }
```

The Docker container mounts this file at `/etc/nats/nats-server.conf` (see [DOCKER.md §4](./DOCKER.md#4-docker-compose)).

---

## 3. Streams

Two JetStream streams handle all message passing in the system.

### 3.1 `messages` — Inbound Message Stream

| Property          | Value                            |
|-------------------|----------------------------------|
| **Stream name**   | `messages`                       |
| **Subjects**      | `message.received`               |
| **Storage**       | File (disk-backed)               |
| **Retention**     | `LimitsPolicy` — 24 hours / 1M msgs |
| **Max message size** | 64 KB                         |
| **Replicas**      | 1 (dev) / 3 (production)        |

**Purpose:** Carries normalized, deduplicated WhatsApp messages from the `api-gateway` to `message-worker` instances.

---

### 3.2 `persistence_events` — Async Persistence Stream

| Property          | Value                                    |
|-------------------|------------------------------------------|
| **Stream name**   | `persistence_events`                     |
| **Subjects**      | `persist.message`, `persist.summary`, `persist.facts` |
| **Storage**       | File (disk-backed)                       |
| **Retention**     | `WorkQueuePolicy` — deleted on ack       |
| **Max message size** | 256 KB                               |
| **Replicas**      | 1 (dev) / 3 (production)               |

**Purpose:** Decouples DynamoDB writes from the main processing loop. The `message-worker` publishes here; the `persistence-worker` consumes and writes to DynamoDB asynchronously.

---

## 4. Subjects & Message Schemas

### 4.1 `message.received`

Published by the `api-gateway` after normalization and deduplication of a WhatsApp message.

```json
{
  "type": "message.received",
  "payload": {
    "conversation_id": "4798913312-47839948",
    "broker_id":       "broker_77",
    "customer_id":     "cust_456",
    "sender_type":     "customer",
    "text":            "I'm looking for an apartment downtown",
    "timestamp":       "2026-03-10T14:22:01Z",
    "message_hash":    "b7af3e2c..."
  }
}
```

---

### 4.2 `persist.message`

Published by the `message-worker` to durably store a single message in DynamoDB.

```json
{
  "type": "persist.message",
  "payload": {
    "conversation_id": "4798913312-47839948",
    "timestamp":       "2026-03-10T14:22:01Z",
    "sender_type":     "customer",
    "text":            "I'm looking for an apartment downtown",
    "hash":            "b7af3e2c..."
  }
}
```

DynamoDB target key:
```
PK = CONV#4798913312-47839948
SK = MSG#2026-03-10T14:22:01Z
```

See [API.md §6.2](./API.md#62-messages) for the full DynamoDB record structure.

---

### 4.3 `persist.summary`

Published by the `message-worker` when the rolling summary is updated after a buffer flush.

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

DynamoDB target key:
```
PK = CONV#4798913312-47839948
SK = META
```

See [API.md §6.1](./API.md#61-conversation-metadata) for the full DynamoDB record structure.

---

### 4.4 `persist.facts`

Published by the `message-worker` when facts are extracted or updated.

```json
{
  "type": "persist.facts",
  "payload": {
    "conversation_id": "4798913312-47839948",
    "facts": [
      { "name": "property_type", "value": "apartment", "confidence": 0.95 },
      { "name": "location",      "value": "downtown",  "confidence": 0.92 },
      { "name": "budget",        "value": 600000,      "confidence": 0.88 }
    ],
    "updated_at": "2026-03-10T14:25:00Z"
  }
}
```

DynamoDB target key per fact:
```
PK = CONV#4798913312-47839948
SK = FACT#<fact_name>
```

See [API.md §6.3](./API.md#63-facts) for the full DynamoDB record structure.

---

## 5. Consumers

### 5.1 `message-worker` Consumer

| Property              | Value                          |
|-----------------------|--------------------------------|
| **Stream**            | `messages`                     |
| **Consumer name**     | `message-worker-group`         |
| **Deliver policy**    | `DeliverAllPolicy`             |
| **Ack policy**        | `AckExplicit`                  |
| **Max deliver**       | `5`                            |
| **Ack wait**          | `30s`                          |
| **Queue group**       | `message-workers`              |

Multiple replicas of `message-worker` join the `message-workers` queue group. NATS delivers each message to **exactly one** worker instance.

---

### 5.2 `persistence-worker` Consumer

| Property              | Value                           |
|-----------------------|---------------------------------|
| **Stream**            | `persistence_events`            |
| **Consumer name**     | `persistence-worker-group`      |
| **Deliver policy**    | `DeliverAllPolicy`              |
| **Ack policy**        | `AckExplicit`                   |
| **Max deliver**       | `10`                            |
| **Ack wait**          | `60s`                           |
| **Queue group**       | `persistence-workers`           |

Higher `max deliver` and longer `ack wait` accommodate potential DynamoDB throttling retries.

---

## 6. Message Lifecycle

```
api-gateway
  │
  │  PUBLISH message.received
  ▼
NATS JetStream (stream: messages)
  │
  │  PUSH to queue group: message-workers
  ▼
message-worker
  ├── Updates local conversation state (cache)
  ├── Runs fact extraction
  ├── Triggers rolling summary if thresholds met
  │
  │  PUBLISH persist.message
  │  PUBLISH persist.summary   (if summary updated)
  │  PUBLISH persist.facts     (if facts changed)
  │
  │  ACK message.received
  ▼
NATS JetStream (stream: persistence_events)
  │
  │  PUSH to queue group: persistence-workers
  ▼
persistence-worker
  ├── Writes to DynamoDB
  └── ACK persist.*
```

**Key property:** The `api-gateway` returns `200 OK` to WhatsApp **before** any database writes occur. All heavy work is deferred to the asynchronous consumer chain.

---

## 7. Retry & Error Policy

### Message Worker Retry

| Scenario                             | Behaviour                                          |
|--------------------------------------|----------------------------------------------------|
| Worker crashes before ACK            | NATS re-delivers after `ack_wait` (30s)            |
| LLM call fails                       | Message ACKed; summary generation skipped          |
| Cache miss (state not in memory)     | State loaded from DynamoDB before processing       |
| Max deliver exceeded (5 attempts)    | Message moved to a **dead-letter** subject: `message.received.DEAD` |

### Persistence Worker Retry

| Scenario                             | Behaviour                                          |
|--------------------------------------|----------------------------------------------------|
| DynamoDB throttling (ProvisionedThroughputExceeded) | Worker retries with exponential backoff |
| Worker crashes before ACK            | NATS re-delivers after `ack_wait` (60s)            |
| Max deliver exceeded (10 attempts)   | Message moved to dead-letter: `persist.DEAD`       |

### Dead-Letter Handling

Dead-letter subjects should be monitored and alerted on. Failed messages should be re-driven manually after the root cause is resolved:

```bash
# Re-drive dead letters (example using nats CLI)
nats stream get messages --last | nats publish message.received
```

---

## 8. Producer Reference

### Publishing `message.received` (api-gateway)

```typescript
import { connect, StringCodec } from "nats";

const nc = await connect({ servers: process.env.NATS_URL });
const js = nc.jetstream();
const sc = StringCodec();

const event = {
  type: "message.received",
  payload: normalizedMessage,   // NormalizedMessage
};

await js.publish("message.received", sc.encode(JSON.stringify(event)));
```

### Publishing persistence events (message-worker)

```typescript
// After processing a message
await js.publish("persist.message",  sc.encode(JSON.stringify(persistMsgEvent)));
await js.publish("persist.summary",  sc.encode(JSON.stringify(persistSummaryEvent)));
await js.publish("persist.facts",    sc.encode(JSON.stringify(persistFactsEvent)));
```

---

## 9. Consumer Reference

### Subscribing in a queue group (message-worker)

```typescript
import { connect, AckPolicy, DeliverPolicy } from "nats";

const nc = await connect({ servers: process.env.NATS_URL });
const js = nc.jetstream();
const jsm = await nc.jetstreamManager();

// Ensure consumer exists
await jsm.consumers.add("messages", {
  durable_name: "message-worker-group",
  deliver_group: "message-workers",
  deliver_subject: nc.inbox(),
  ack_policy: AckPolicy.Explicit,
  deliver_policy: DeliverPolicy.All,
  max_deliver: 5,
  ack_wait: 30_000_000_000,  // nanoseconds
});

const consumer = await js.consumers.get("messages", "message-worker-group");

for await (const msg of await consumer.consume()) {
  try {
    const event = JSON.parse(msg.string());
    await processMessage(event.payload);
    msg.ack();
  } catch (err) {
    msg.nak();   // trigger re-delivery
  }
}
```

---

## 10. Monitoring

NATS exposes an HTTP monitoring endpoint at port `8222`.

### Useful endpoints

| Endpoint                         | Description                             |
|----------------------------------|-----------------------------------------|
| `GET http://nats:8222/healthz`   | Health check                            |
| `GET http://nats:8222/varz`      | Server variables (uptime, connections)  |
| `GET http://nats:8222/connz`     | Active connections                      |
| `GET http://nats:8222/subsz`     | Active subscriptions                    |
| `GET http://nats:8222/jsz`       | JetStream status (streams + consumers)  |

### NATS CLI quick reference

```bash
# Install nats CLI
brew install nats-io/nats-tools/nats

# Connect to local server
nats context add local --server nats://localhost:4222

# List streams
nats stream ls

# Inspect a stream
nats stream info messages

# Inspect a consumer
nats consumer info messages message-worker-group

# Peek at pending messages (without consuming)
nats stream view messages

# Publish a test message
nats publish message.received '{"type":"message.received","payload":{}}'
```

### Key metrics to alert on

| Metric                         | Alert threshold    |
|--------------------------------|--------------------|
| Consumer pending messages      | > 1 000            |
| Consumer redelivery count      | > 3 per message    |
| Dead-letter message count      | > 0                |
| JetStream storage used         | > 80%              |
| NATS server disconnections     | Any                |

---

## 11. Local Development

### Starting NATS with Docker Compose

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up nats
```

### Create streams manually (first run)

```bash
# Create messages stream
nats stream add messages \
  --subjects="message.received" \
  --storage=file \
  --retention=limits \
  --max-age=24h \
  --max-msgs=1000000 \
  --replicas=1

# Create persistence_events stream
nats stream add persistence_events \
  --subjects="persist.message,persist.summary,persist.facts" \
  --storage=file \
  --retention=workqueue \
  --max-msgs=500000 \
  --replicas=1
```

> **Tip:** In production, provision streams via infrastructure-as-code (Terraform + NATS Terraform provider, or a startup script) rather than manually.
