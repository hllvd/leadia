# DynamoDB Reference — LeadIa (WhatsApp CRM)

This document describes the DynamoDB **single-table design** used to persist all conversation data. It covers the table schema, all item types, access patterns, write operations, TTL configuration, and local development setup.

See also:
- [System Architecture](./SYSTEM_ARCHITECTURE.md)
- [API Reference](./API.md) — high-level access patterns
- [Workers Reference](./WORKERS.md) — which events trigger writes
- [Cache Reference](./CACHE.md) — DynamoDB as the persistent fallback

---

## Table of Contents

1. [Design Philosophy](#1-design-philosophy)
2. [Table Schema](#2-table-schema)
3. [Item Types](#3-item-types)
4. [Access Patterns](#4-access-patterns)
5. [Write Operations](#5-write-operations)
6. [TTL Configuration](#6-ttl-configuration)
7. [Capacity Planning](#7-capacity-planning)
8. [Indexes](#8-indexes)
9. [Local Development](#9-local-development)
10. [Operational Notes](#10-operational-notes)

---

## 1. Design Philosophy

All entity types live in one DynamoDB table (`crm_memory`) — a **Single Table Design**. Benefits:

| Benefit                    | Detail                                                          |
|----------------------------|-----------------------------------------------------------------|
| **Fewer round trips**      | Fetch metadata + facts in one query                             |
| **Predictable costs**      | Access patterns are known upfront; no surprise scan costs       |
| **Low operational overhead** | One table to monitor, back up, and tune                      |

---

## 2. Table Schema

**Table name:** `crm_memory`

### Primary Key

| Attribute | Type   | Role          |
|-----------|--------|---------------|
| `PK`      | String | Partition key |
| `SK`      | String | Sort key      |

### Key Prefix Convention

| Prefix  | Entity              |
|---------|---------------------|
| `CONV#` | Conversation (PK)   |
| `META`  | Conversation metadata |
| `MSG#`  | Message record      |
| `FACT#` | Extracted fact      |

---

## 3. Item Types

### 3.1 Conversation Metadata

```
PK = CONV#<conversation_id>
SK = META
```

| Attribute                | Type   | Description                          |
|--------------------------|--------|--------------------------------------|
| `rolling_summary`        | String | Latest compressed summary            |
| `last_message_hash`      | String | SHA-256 hash for deduplication       |
| `last_message_timestamp` | String | ISO 8601                             |
| `broker_id`              | String | Broker identifier                    |
| `customer_id`            | String | Customer identifier                  |
| `created_at`             | String | ISO 8601                             |
| `updated_at`             | String | ISO 8601                             |
| `ttl`                    | Number | Unix epoch (DynamoDB TTL)            |

**Example:**
```json
{
  "PK":                    "CONV#4798913312-47839948",
  "SK":                    "META",
  "rolling_summary":       "User is searching for a two-bedroom apartment downtown with a budget around 600k.",
  "last_message_hash":     "b7af3e2c...",
  "last_message_timestamp":"2026-03-10T14:22:01Z",
  "broker_id":             "broker_77",
  "customer_id":           "cust_456",
  "created_at":            "2026-03-01T09:00:00Z",
  "updated_at":            "2026-03-10T14:25:00Z",
  "ttl":                   1773000000
}
```

---

### 3.2 Message Records

```
PK = CONV#<conversation_id>
SK = MSG#<timestamp_iso8601>
```

| Attribute    | Type   | Description                  |
|--------------|--------|------------------------------|
| `sender`     | String | `"broker"` or `"customer"`   |
| `text`       | String | Normalized message text      |
| `hash`       | String | SHA-256 message hash         |
| `created_at` | String | ISO 8601                     |
| `ttl`        | Number | Unix epoch (DynamoDB TTL)    |

**Example:**
```json
{
  "PK":         "CONV#4798913312-47839948",
  "SK":         "MSG#2026-03-10T14:22:01Z",
  "sender":     "customer",
  "text":       "I'm looking for an apartment downtown",
  "hash":       "ab2939c...",
  "created_at": "2026-03-10T14:22:01Z",
  "ttl":        1773000000
}
```

---

### 3.3 Fact Records

```
PK = CONV#<conversation_id>
SK = FACT#<fact_name>
```

| Attribute    | Type               | Description               |
|--------------|--------------------|---------------------------|
| `value`      | String/Number/Bool | Extracted fact value      |
| `confidence` | Number             | 0.0–1.0                   |
| `updated_at` | String             | ISO 8601                  |
| `ttl`        | Number             | Unix epoch (DynamoDB TTL) |

**Example:**
```json
{
  "PK":         "CONV#4798913312-47839948",
  "SK":         "FACT#budget",
  "value":      600000,
  "confidence": 0.92,
  "updated_at": "2026-03-10T14:23:00Z",
  "ttl":        1773000000
}
```

---

## 4. Access Patterns

### AP-1: Load conversation metadata (cache miss)
```
GetItem:  PK=CONV#<id>  SK=META
```

### AP-2: Load all facts for a conversation (cache miss)
```
Query:  PK=CONV#<id>  AND SK begins_with "FACT#"
```

> AP-1 and AP-2 together reconstruct the full `ConversationState` from DynamoDB in 2 round trips.

### AP-3: Load all messages (audit / replay)
```
Query:  PK=CONV#<id>  AND SK begins_with "MSG#"
ScanIndexForward: true  ← chronological order
```

Messages can be time-ranged:
```
SK BETWEEN "MSG#2026-03-10T00:00:00Z" AND "MSG#2026-03-10T23:59:59Z"
```

### AP-4: Full conversation export
```
Query:  PK=CONV#<id>
```
Returns all item types (META + MSG# + FACT#) in one query.

### AP-5: Load a single fact
```
GetItem:  PK=CONV#<id>  SK=FACT#<fact_name>
```

---

## 5. Write Operations

### New message (`persist.message`)
```
PutItem:
  PK=CONV#<id>  SK=MSG#<timestamp>
  sender, text, hash, created_at, ttl
```

### Update conversation metadata (`persist.summary`)

For **existing** conversation:
```
UpdateItem:
  Key: PK=CONV#<id>  SK=META
  SET rolling_summary, last_message_hash, last_message_timestamp, updated_at, ttl
  ConditionExpression: attribute_exists(PK)
```

For **new** conversation (first message), use `PutItem` with all attributes including `broker_id`, `customer_id`, `created_at`.

### Update facts (`persist.facts`)
```
BatchWriteItem (up to 25 items per batch):
  Per fact → PutItem:
    PK=CONV#<id>  SK=FACT#<fact_name>
    value, confidence, updated_at, ttl
```

---

## 6. TTL Configuration

**TTL attribute name:** `ttl` (Number, Unix epoch seconds)

| Entity              | Retention | Rationale                          |
|---------------------|-----------|------------------------------------|
| Conversation META   | 1 year    | Long-term lead record              |
| Messages            | 90 days   | Audit trail                        |
| Facts               | 1 year    | Key CRM data                       |

```typescript
const TTL = (days: number) => Math.floor(Date.now() / 1000) + days * 86400;
```

> TTL deletion is eventual — items may persist up to 48 h after expiry.

---

## 7. Capacity Planning

**Recommended billing mode:** `PAY_PER_REQUEST` (on-demand) — switch to provisioned with auto-scaling once traffic stabilizes.

### Estimated Units Per Message

| Operation                     | Type | Units |
|-------------------------------|------|-------|
| GetItem META (cache miss)     | RCU  | 0.5   |
| Query FACT# (cache miss)      | RCU  | 0.5–1 |
| PutItem message               | WCU  | 1     |
| UpdateItem META               | WCU  | 1     |
| BatchWrite 5 facts            | WCU  | 5     |
| **Total per message**         |      | **~9 WCU, ~1 RCU** |

> Cache hits eliminate almost all RCU cost. Reads are dominated by cold starts and TTL-evicted entries.

---

## 8. Indexes

The base table covers all production access patterns. Future GSIs to consider:

| Potential GSI              | Use case                              |
|----------------------------|---------------------------------------|
| `broker_id + created_at`   | List all conversations for a broker   |
| `customer_id + created_at` | Look up a customer across brokers     |

> Add indexes only when a concrete access pattern requires them — each GSI adds WCU cost on every write.

---

## 9. Local Development

### DynamoDB Local (Docker Compose)

```yaml
# docker-compose.dev.yml
dynamodb-local:
  image: amazon/dynamodb-local:latest
  ports:
    - "8000:8000"
  command: ["-jar", "DynamoDBLocal.jar", "-sharedDb", "-inMemory"]
  networks:
    - crm-net
```

### Bootstrap Table

```bash
#!/usr/bin/env bash
# infra/dynamodb/init.sh
AWS_ACCESS_KEY_ID=local \
AWS_SECRET_ACCESS_KEY=local \
aws dynamodb create-table \
  --endpoint-url http://localhost:8000 \
  --region us-east-1 \
  --table-name crm_memory \
  --attribute-definitions \
      AttributeName=PK,AttributeType=S \
      AttributeName=SK,AttributeType=S \
  --key-schema \
      AttributeName=PK,KeyType=HASH \
      AttributeName=SK,KeyType=RANGE \
  --billing-mode PAY_PER_REQUEST

echo "✅ Table crm_memory created"
```

### Useful Local Commands

```bash
# Describe table
aws dynamodb describe-table \
  --endpoint-url http://localhost:8000 --region us-east-1 \
  --table-name crm_memory

# Scan all items
aws dynamodb scan \
  --endpoint-url http://localhost:8000 --region us-east-1 \
  --table-name crm_memory

# Get conversation metadata
aws dynamodb get-item \
  --endpoint-url http://localhost:8000 --region us-east-1 \
  --table-name crm_memory \
  --key '{"PK":{"S":"CONV#4798913312-47839948"},"SK":{"S":"META"}}'
```
_(All local commands require `AWS_ACCESS_KEY_ID=local AWS_SECRET_ACCESS_KEY=local` env vars.)_

---

## 10. Operational Notes

| Concern              | Recommendation                                                              |
|----------------------|-----------------------------------------------------------------------------|
| **Backups**          | Enable Point-in-Time Recovery (PITR)                                        |
| **Encryption**       | Enable encryption at rest with AWS KMS                                      |
| **IAM**              | Use IAM roles with least-privilege — never embed AWS keys in service code   |
| **Monitoring**       | Alert on `ThrottledRequests`, `SystemErrors`, consumed capacity > 80%       |
| **Hot partitions**   | `CONV#` prefix per conversation eliminates hot-key risk                     |
| **Item size limit**  | DynamoDB max item size is 400 KB; summaries and facts are well within limits |
| **Global Tables**    | Use DynamoDB Global Tables if multi-region availability is required          |
