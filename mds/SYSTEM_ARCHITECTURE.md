# Rolling Summary + Facts Architecture for WhatsApp CRM

## Overview

This document defines the architecture for a **conversation processing system** designed to handle WhatsApp messages for a CRM platform. The system maintains conversational context efficiently using:

- Rolling summaries
- Message buffers
- Structured fact extraction
- Local fast memory
- Asynchronous persistence

The architecture is optimized for:

- Low latency
- Low LLM token usage
- High scalability
- Efficient DynamoDB access using a **Single Table Design Pattern**

---

## Estratégia de Dados Híbrida e Visão de Futuro


## Hybrid Data Strategy and Vision for the Future
The project was designed under a hybrid data strategy:

1.  **DynamoDB (Single Table Design)**: Currently utilized for high-volume memory and event processing (messages, facts, summaries). It is horizontally scalable and ideal for the continuous data flow of conversations.
2.  **SQLite (Future Real Estate CRM)**: In a future expansion, the system will integrate a full real estate CRM. Structured relational data (property listings, owners, contracts, and administrative management) will be stored in **SQLite**, ensuring the referential integrity required for a CRM, while DynamoDB remains the "brain" for scalable conversations.

---

# Core Conversation Model

The system maintains the conversational state using four main components:
```
Rolling Summary
+
Recent Message Buffer
+
Extracted Facts
+
Incoming Message
```


- **Rolling Summary** → condensed representation of the conversation history  
- **Message Buffer** → recent unsummarized messages  
- **Facts** → structured information extracted from conversation  
- **Incoming Message** → newest message received from WhatsApp  

---

# High-Level System Architecture

```
WhatsApp Webhook
│
▼
Message Normalization
│
▼
Message Deduplication
│
▼
Event Queue (NATS JetStream)
│
▼
Message Worker
│
▼
Conversation State (Local Memory Cache)
│
├── Update Message Buffer
├── Extract Facts
└── Trigger Summary
│
▼
Async Persistence Worker
│
▼
DynamoDB (Single Table)

```

---

# 1. Message Ingestion

Incoming messages are received via **WhatsApp Webhooks**.

Responsibilities:

- Receive message payload
- Validate input
- Forward message to the processing pipeline

Typical flow:

```
WhatsApp Webhook
↓
API Gateway
↓
Message Queue (NATS JetStream)
````


Event-driven messaging prevents the system from needing to poll for new messages.

---

# 2. Message Normalization

All incoming messages must be normalized into a consistent internal format before processing.

### Normalized Message Structure

```
NormalizedMessage

conversation_id
broker_id
customer_id
sender_type ("broker" | "customer")
text
timestamp
message_hash
```

### Conversation ID Generation

The `conversation_id` is constructed deterministically from the broker and customer phone numbers. This ensures all messages between the same pair are routed to the same conversation state.

```
conversation_id = <broker_number> + "-" + <customer_number>
```

**Example:**

```
conversation_id = 4798913312-47839948
```

(Concatenate the broker's number, a hyphen, and the customer's number.)


### Example Normalized Message

```json
{
"conversation_id": "conv_98123",
"broker_id": "broker_77",
"customer_id": "cust_456",
"sender_type": "customer",
"text": "I'm looking for an apartment downtown",
"timestamp": "2026-03-10T14:22:01Z",
"message_hash": "b7af...912"
}
```


### Normalization Rules

```
text = trim(text)
text = collapse_multiple_spaces(text)
text = normalize_newlines(text)
```


Other normalization tasks:

- Convert to UTF-8
- Standardize timestamps
- Normalize emoji if required

---

# 3. Message Deduplication

Webhook providers sometimes resend messages.

To prevent duplicate processing a deterministic hash is generated.

### Hash Formula

`hash = SHA256(timestamp + broker_id + customer_id + message)`


If the same hash already exists:
`ignore message`
The system stores the last processed hash:
`last_message_hash``


---

# 4. User Classification

When a message arrives the system determines whether the sender is a **new lead**.

### Classification Logic
```
if conversation_id not found in database:
user_type = new_lead
else:
user_type = existing_lead
```

If the user is a **new lead**, initialize conversation state:
```
rolling_summary = ""
facts = {}
buffer = []
```

---

# 4.1 Conversation Mode (Agent Mode / Listening Mode Flag)

The system supports a **Conversation Mode** flag to control the behavior of the conversation processing. This flag determines whether the system operates in **Listening Mode** only or in **Agent Mode** (listening + responding).

### Conversation Mode Options

- **OnlyListening**: The system only listens to messages, extracts facts, and maintains conversation state without generating responses. Useful for passive data collection.
- **AgentAndListening**: The system listens to messages, extracts facts, and actively responds as an AI agent using the LLM.

### Implementation

The mode is configured per broker via the `RealStateBroker` entity:
```
public ConversationMode Mode { get; set; } = ConversationMode.OnlyListening;
```

This flag is checked during message processing to decide whether to trigger LLM responses or just update the state.

### Use Cases

- **Listening Mode**: For initial data gathering or when human agents handle responses.
- **Agent Mode**: For automated customer interactions, lead qualification, and instant responses.

---

# 5. Local Conversation State (Fast Memory)

To minimize database reads, the system stores conversation state in **local fast memory**.

Possible implementations:

- In-process LRU cache
- Redis
- Dedicated memory store

### Conversation State Structure

```
ConversationState

conversation_id
rolling_summary
facts
buffer
buffer_chars
last_message_hash
last_message_timestamp
last_activity_timestamp
 ```


---

# 6. Loading Conversation State

When a worker processes a message:

1. Check local cache  
2. If not present → load from DynamoDB  
3. Store in local cache  

Example cache storage:
`cache[conversation_id] = ConversationState`


### Cache Expiration
`10 minutes after last activity`


This prevents uncontrolled memory growth.

---

# 7. Message Buffer

The buffer stores recent messages that have not yet been summarized.

Purpose: prevent summarizing fragmented messages such as:

```
I'm looking for
An apartment downtown
But north zone is also fine
```

### Example Buffer

```
buffer = [
"I'm looking for",
"An apartment downtown",
"But north zone is also fine"
]
```


### Buffer Limits

```
max_messages = 6
max_chars = 500
timeout = 30 seconds
expiration = 5 minutes
```

If the buffer reaches expiration without triggering a summary, it is flushed.

---

# 8. Fact Extraction

Facts represent structured knowledge extracted from messages.

Example facts:
```
property_type = apartment
location_preference = downtown
budget_max = 600000
bedrooms = 2
garage_required = true
```

Facts are initially stored in **local memory**.

### Facts Retention Policy

`facts TTL = 10 minutes after last message`


If no new messages arrive during that period:

- Facts are persisted to DynamoDB
- Local memory entry is released

---

# 9. Rolling Summary

Rolling summaries compress conversation history while preserving key context.

### Summary Trigger Conditions

```
buffer_messages >= 5
buffer_chars >= 400
time_since_last_message > 30 seconds
```

### LLM Input
Previous summary:
User is searching for a property.

New messages:

Looking downtown

Budget around 600k

Two bedrooms preferred

### Example LLM Output


User is searching for a two-bedroom apartment downtown with a budget around 600k.

After updating the summary:

```
buffer = []
```


---

# 10. LLM Context Construction

When interacting with an LLM the system builds the context using:

```
Rolling Summary
+
Relevant Facts
+
Recent Buffer
+
Incoming Message
```


Example context:
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

# 11. Efficient Worker Architecture

Workers operate in an **event-driven architecture**.

They **do not poll the database**.

### Event Flow

```
Webhook
    ↓
Publish Message Event
    ↓
NATS JetStream Stream
    ↓
Message Worker
```


Workers subscribe to streams and process messages as they arrive.

Benefits:

- No polling loops
- Lower CPU usage
- Horizontal scalability

---

# 12. Asynchronous Persistence

Permanent storage operations occur asynchronously.

The message worker emits events:
```
persist_message
persist_summary
persist_facts
```

These events are processed by a **Persistence Worker**.

Advantages:

- Faster webhook responses
- Write batching possible
- Improved reliability

---

# 13. DynamoDB Storage (Single Table Design)

All persistent data is stored in a single DynamoDB table.

### Table Name
`crm_memory`


### Primary Keys
```
PK
SK
```

---

## Conversation Metadata

```
PK = CONV#<conversation_id>
SK = META
```

Example record:

```
{
"PK": "CONV#123",
"SK": "META",
"rolling_summary": "User searching for apartment downtown",
"last_message_hash": "ad98ab3",
"last_message_timestamp": "2026-03-10T14:22:01Z",
"broker_id": "broker_77",
"customer_id": "cust_456"
}
```

---

```
## Messages
PK = CONV#<conversation_id>
SK = MSG#<timestamp>
```

Example message:

```
{
"PK": "CONV#123",
"SK": "MSG#2026-03-10T14:22:01Z",
"sender": "customer",
"text": "I'm looking for an apartment downtown",
"hash": "ab2939c"
}
```

---

## Facts

```
PK = CONV#<conversation_id>
SK = FACT#<fact_name>
```

Example fact:

```
{
"PK": "CONV#123",
"SK": "FACT#budget",
"value": 600000,
"confidence": 0.92,
"updated_at": "2026-03-10T14:23:00Z"
}
```

---

# 14. Local Cache Refresh

If conversation state is not found in memory:

- Load from DynamoDB
- Store in cache
- Resume processing

Cache expiration rule:

10 minutes after last activity


---

# 15. Permanent Memory Persistence

The following data must be stored permanently:

```
messages
rolling_summary
facts
last_message_hash
```

Persistence occurs through **asynchronous persistence workers**.

---

```
# Complete Data Flow
WhatsApp Webhook
    ↓
Message Normalization
    ↓
Message Deduplication (hash)
    ↓
Publish Event
    ↓
NATS JetStream
    ↓   
Message Worker
    ↓
Load Conversation State (Cache / DynamoDB)
    ↓
Update Buffer
    ↓
Extract Facts
    ↓
Trigger Rolling Summary
    ↓
Publish Persistence Events
    ↓
Async Persistence Worker
    ↓
DynamoDB (Single Table)
```

---

# Advantages of This Architecture

### Low Latency

Most reads occur from **local memory cache**.

### Low Token Usage

Rolling summaries prevent sending the full conversation history to the LLM.

### High Scalability

Event-driven workers allow horizontal scaling.

### Efficient DynamoDB Usage

Single-table design provides predictable access patterns and low query cost.

### Fault Tolerance

Queues allow retries and resilience against transient failures.

---

# Summary

This architecture enables scalable WhatsApp CRM automation by combining:

- Rolling summaries for contextual compression  
- Message buffers for fragmented messages  
- Fact extraction for structured knowledge  
- Local fast memory for low latency  
- Asynchronous persistence for scalability  
- DynamoDB single-table design for efficient storage


