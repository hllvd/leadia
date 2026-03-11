# Cache Reference — LeadIa (WhatsApp CRM)

This document describes the **local fast-memory cache** used to store and manage `ConversationState` objects in the WhatsApp CRM system. The cache layer is the primary performance lever — it avoids DynamoDB reads on every message.

See also:
- [System Architecture](./SYSTEM_ARCHITECTURE.md)
- [API Reference](./API.md) — ConversationState and Fact schemas
- [Workers Reference](./WORKERS.md) — how workers interact with the cache
- [DynamoDB Reference](./DYNAMODB.md) — persistent fallback storage

---

## Table of Contents

1. [Purpose](#1-purpose)
2. [ConversationState Structure](#2-conversationstate-structure)
3. [Cache Backends](#3-cache-backends)
4. [Cache Key Design](#4-cache-key-design)
5. [Cache Lifecycle](#5-cache-lifecycle)
6. [TTL and Eviction Policy](#6-ttl-and-eviction-policy)
7. [Cache Miss Flow](#7-cache-miss-flow)
8. [New Lead Initialization](#8-new-lead-initialization)
9. [Redis Configuration](#9-redis-configuration)
10. [Failure Handling](#10-failure-handling)

---

## 1. Purpose

The cache stores the complete conversational state for each active conversation in fast memory. This provides:

| Benefit             | Detail                                                          |
|---------------------|-----------------------------------------------------------------|
| **Low latency**     | Avoids DynamoDB reads on every inbound message                  |
| **Low cost**        | Reduces DynamoDB read capacity consumption                      |
| **Stateful workers**| Workers can process messages without querying the database      |
| **Scalability**     | Shared Redis cache allows multiple worker instances to share state |

---

## 2. ConversationState Structure

The `ConversationState` is the central in-memory object for a conversation.

```typescript
interface ConversationState {
  conversation_id:          string;   // "<broker_number>-<customer_number>"
  rolling_summary:          string;   // compressed conversation history
  facts:                    Record<string, FactEntry>;  // structured extracted data
  buffer:                   string[]; // recent unsummarized messages
  buffer_chars:             number;   // total character count in buffer
  last_message_hash:        string;   // SHA-256 hex for deduplication
  last_message_timestamp:   string;   // ISO 8601
  last_activity_timestamp:  string;   // ISO 8601 — used for TTL decisions
}
```

### FactEntry

```typescript
interface FactEntry {
  value:       string | number | boolean | null;
  confidence:  number;   // 0.0 – 1.0
  updated_at:  string;   // ISO 8601
}
```

### Example State Object

```json
{
  "conversation_id":         "4798913312-47839948",
  "rolling_summary":         "User is searching for a two-bedroom apartment downtown with a budget around 600k.",
  "facts": {
    "intent":       { "value": "buy",       "confidence": 0.97, "updated_at": "2026-03-10T14:20:00Z" },
    "property_type":{ "value": "apartment", "confidence": 0.95, "updated_at": "2026-03-10T14:20:00Z" },
    "location":     { "value": "downtown",  "confidence": 0.92, "updated_at": "2026-03-10T14:20:00Z" },
    "budget":       { "value": 600000,      "confidence": 0.88, "updated_at": "2026-03-10T14:22:00Z" }
  },
  "buffer":                  ["Does it have parking?"],
  "buffer_chars":            22,
  "last_message_hash":       "b7af3e2c...",
  "last_message_timestamp":  "2026-03-10T14:22:01Z",
  "last_activity_timestamp": "2026-03-10T14:22:01Z"
}
```

---

## 3. Cache Backends

Two backend options are supported:

### Option A — In-Process LRU Cache

Simple in-memory map with an LRU eviction strategy. Suitable for single-instance deployments.

| Property          | Value                                    |
|-------------------|------------------------------------------|
| Latency           | Sub-millisecond                          |
| Persistence       | None — lost on process restart           |
| Sharing           | Not shared between worker instances      |
| Max entries       | Configurable (recommended: 5 000)        |

### Option B — Redis (Recommended)

External Redis instance shared between all worker replicas.

| Property          | Value                                    |
|-------------------|------------------------------------------|
| Latency           | ~1–2 ms                                  |
| Persistence       | Optional (RDB snapshots / AOF)           |
| Sharing           | ✅ All worker instances share state      |
| Eviction policy   | `volatile-lru` (evicts keys with TTL)    |

> **Use Redis in production** whenever more than one `message-worker` replica is deployed, to ensure consistent state across instances.

---

## 4. Cache Key Design

```
cache key = conversation_id
```

**Example:**
```
4798913312-47839948  →  ConversationState { ... }
```

In Redis, the key is stored as a JSON-serialized string:

```bash
SET "4798913312-47839948" '{ "rolling_summary": "...", ... }' EX 600
```

---

## 5. Cache Lifecycle

```
Inbound message arrives at message-worker
          │
          ▼
   GET cache[conversation_id]
          │
    Found?│
     Yes ─┼─────────────────────────────────────────────┐
          │                                              │
     No   ▼                                             │
   Load from DynamoDB                                   │
          │                                             │
   Found? │                                             │
     Yes  ▼                                             │
   SET cache[conversation_id] (with TTL)                │
          │                                             │
     No   ▼                                             │
   Initialize new ConversationState (new lead)          │
          │                                             │
          └──────────────────────────────────────────── ▼
                                          Process message
                                                  │
                                                  ▼
                                    Update ConversationState
                                                  │
                                                  ▼
                                    SET cache[conversation_id] (refresh TTL)
```

---

## 6. TTL and Eviction Policy

### Conversation State TTL

```
TTL = 10 minutes after last_activity_timestamp
```

Every time a new message is processed, the TTL is **refreshed** by updating `last_activity_timestamp` and resetting the cache expiry:

```bash
# Redis
SET "4798913312-47839948" '<state_json>' EX 600
```

When the TTL expires:
- The entry is evicted from Redis automatically
- The next message for that conversation triggers a DynamoDB load

### Facts TTL

Facts follow the same TTL as the conversation state (10 minutes). When the conversation state is evicted:
- A `persist.facts` event should have already been emitted during the last active session
- Facts are always available in DynamoDB as the source of truth

### Buffer Expiration

If no summary trigger occurs, the buffer is force-flushed after 5 minutes:

```
buffer_expiration = last_message_timestamp + 300 seconds
```

This prevents stale messages from accumulating in memory indefinitely.

---

## 7. Cache Miss Flow

When a conversation state is not found in cache:

```
1. Query DynamoDB for CONV#<id> META record → load rolling_summary, last_message_hash
2. Query DynamoDB for all FACT#* records    → reconstruct facts map
3. Reconstruct ConversationState with:
      buffer = []
      buffer_chars = 0
   (Buffer is not persisted — it is always reconstructed fresh)
4. Store in cache with TTL = 600s
5. Continue processing the current message
```

> The buffer is **intentionally not persisted** to DynamoDB. Only the summary, facts, and last message hash are persistent. A cache miss after worker restart simply means the next message may trigger a summary sooner than expected, which is acceptable.

---

## 8. New Lead Initialization

When a `conversation_id` is not found in either cache **or** DynamoDB:

```typescript
const newState: ConversationState = {
  conversation_id:         conversationId,
  rolling_summary:         "",
  facts:                   {},
  buffer:                  [],
  buffer_chars:            0,
  last_message_hash:       "",
  last_message_timestamp:  "",
  last_activity_timestamp: new Date().toISOString(),
};

cache.set(conversationId, newState, ttl=600);
```

A `persist.message` event is still emitted, so the conversation record appears in DynamoDB after the very first message.

---

## 9. Redis Configuration

### Recommended `redis.conf` settings

```conf
# Memory limit — adjust based on expected active conversations
maxmemory 512mb

# Evict least-recently-used keys that have a TTL set
maxmemory-policy volatile-lru

# Persistence (optional — reduces cold-start cache misses after Redis restart)
save 900 1
save 300 10

# Append-only log for durability
appendonly yes
appendfsync everysec
```

### Docker Compose snippet (from DOCKER.md)

```yaml
redis:
  image: redis:7-alpine
  volumes:
    - redis-data:/data
  command: redis-server --maxmemory 512mb --maxmemory-policy volatile-lru
```

### Memory Estimation

Each `ConversationState` JSON object is approximately **1–3 KB**.

| Active conversations | Estimated Redis memory |
|----------------------|------------------------|
| 1 000                | ~3 MB                  |
| 10 000               | ~30 MB                 |
| 100 000              | ~300 MB                |

---

## 10. Failure Handling

| Scenario                        | Behaviour                                                        |
|---------------------------------|------------------------------------------------------------------|
| Redis unavailable at startup    | Worker fails to start; Docker restarts it                        |
| Redis read timeout              | Fall through to DynamoDB load; log warning                       |
| Redis write timeout             | State update skipped for this cycle; logged; non-fatal           |
| DynamoDB unavailable on miss    | Message NAK'd; re-delivered by NATS; retried                     |
| Corrupted cache entry           | Deserialization error → entry deleted; DynamoDB reload triggered |
| Redis evicts entry early        | Treated as cache miss; DynamoDB load on next message             |
