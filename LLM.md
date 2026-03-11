# LLM Reference — LeadIa (WhatsApp CRM)

This document describes how the system uses Large Language Models (LLMs) for:
- **Fact extraction** — structured data derived from conversation messages
- **Rolling summary generation** — compressed conversation history
- **Context assembly** — the prompt payload sent to the LLM on each interaction

See also:
- [System Architecture](./SYSTEM_ARCHITECTURE.md)
- [API Reference](./API.md) — event schemas and data structures
- [Workers Reference](./WORKERS.md) — when and how LLM calls are triggered

---

## Table of Contents

1. [Overview](#1-overview)
2. [Fact Extraction](#2-fact-extraction)
3. [Rolling Summary](#3-rolling-summary)
4. [Context Construction](#4-context-construction)
5. [LLM Call Lifecycle](#5-llm-call-lifecycle)
6. [Prompt Design](#6-prompt-design)
7. [Model Configuration](#7-model-configuration)
8. [Token Budget](#8-token-budget)
9. [Failure Handling](#9-failure-handling)

---

## 1. Overview

The LLM is not called on every message. It is invoked **asynchronously** by the `message-worker` only when specific conditions are met, keeping token costs low and latency minimal.

```
Incoming Message
      │
      ▼
Append to Buffer
      │
      ▼
  Thresholds met?
   ├── No  → wait for next message
   └── Yes → Call LLM
              ├── Extract / update Facts
              └── Regenerate Rolling Summary
```

Two LLM tasks always run together when triggered:
1. **Fact extraction** — updates the structured fact store
2. **Summary regeneration** — compresses the buffer into the rolling summary

---

## 2. Fact Extraction

### What Are Facts?

Facts are structured, named key-value pairs drawn from the conversation. They represent the user's intent, preferences, and qualifications.

### Supported Fact Keys

| Key                    | Type              | Example value          | Description                          |
|------------------------|-------------------|------------------------|--------------------------------------|
| `intent`               | `string`          | `"buy"` / `"rent"`     | Primary goal of the user             |
| `property_type`        | `string`          | `"apartment"`          | Desired property type                |
| `location`             | `string`          | `"downtown"`           | Preferred area or neighborhood       |
| `price_range_min`      | `number`          | `500000`               | Minimum budget                       |
| `price_range_max`      | `number`          | `700000`               | Maximum budget                       |
| `budget`               | `number`          | `600000`               | Stated total budget                  |
| `bedrooms`             | `number`          | `2`                    | Number of bedrooms required          |
| `garage_required`      | `boolean`         | `true`                 | Whether a garage is needed           |
| `financing_preapproved`| `boolean`         | `false`                | Pre-approval status                  |
| `purpose`              | `string`          | `"residence"`          | Intended use                         |
| `purchase_timeline`    | `string`          | `"up_to_3_months"`     | How soon the user wants to buy       |
| `visit_interest`       | `boolean`         | `true`                 | Interest in scheduling a visit       |
| `mentioned_property_id`| `string` / `null` | `"prop_1234"`          | A specific property referenced       |
| `lead_score`           | `number`          | `42`                   | Computed engagement score (0–100)    |
| `confidence`           | `number`          | `0.82`                 | Overall confidence across facts      |

### Fact Schema

```typescript
interface FactEntry {
  value:       string | number | boolean | null;
  confidence:  number;   // 0.0 – 1.0
  updated_at:  string;   // ISO 8601
}
```

### Fact Update Policy

- Facts are **merged** on each extraction — new values overwrite old ones only when `confidence` is higher or equal.
- Facts not mentioned in the new messages are **preserved** as-is.
- The entire fact set is persisted to DynamoDB after extraction (via `persist.facts` event).

```
new_fact.confidence >= existing_fact.confidence
  → overwrite
else
  → keep existing
```

---

## 3. Rolling Summary

### Purpose

The rolling summary prevents unbounded growth of the LLM context. Rather than sending the full conversation history on every call, only the compressed summary + recent buffer are sent.

### Summary Structure

```
[Previous Summary] + [Buffer Messages] → LLM → [New Summary]
```

The buffer is cleared after summarization.

### Trigger Conditions

The summary is regenerated when **any one** of these conditions is true:

| Condition                     | Threshold       |
|-------------------------------|-----------------|
| Buffer message count          | ≥ 5 messages    |
| Buffer character count        | ≥ 400 characters|
| Time since last message       | > 30 seconds    |
| Buffer expiration (no trigger)| 5 minutes       |

On buffer expiration without a natural trigger, the buffer is **force-flushed** and the summary is regenerated.

### Example

**Previous summary:**
```
User is searching for a property.
```

**Buffer (new messages):**
```
Looking downtown
Budget around 600k
Two bedrooms preferred
```

**New summary (LLM output):**
```
User is searching for a two-bedroom apartment downtown with a budget around 600k.
```

**After summarization:**
```
buffer = []
buffer_chars = 0
```

---

## 4. Context Construction

When making any LLM call, the system assembles a structured prompt context:

```
SUMMARY
<rolling_summary>

FACTS
<key: value pairs from fact store>

RECENT MESSAGES
<buffer contents>

NEW MESSAGE
<incoming message text>
```

### Example Context Payload

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

### Context Assembly Order

1. `rolling_summary` — always included
2. All facts from the local `ConversationState.facts` — serialized as `key: value` pairs
3. `buffer` — all messages in the current buffer joined by newlines
4. Incoming message text

---

## 5. LLM Call Lifecycle

```
message-worker receives message
        │
        ▼
Append message to buffer
        │
        ▼
Check trigger conditions
        │
   Met? │
   Yes ─┤
        ▼
Assemble context (summary + facts + buffer + message)
        │
        ▼
Call LLM API (fact extraction + summary)
        │
        ▼
Parse LLM response
   ├── Extract updated facts → merge into ConversationState.facts
   └── Extract new summary  → overwrite ConversationState.rolling_summary
        │
        ▼
Clear buffer (buffer = [], buffer_chars = 0)
        │
        ▼
Publish persist.summary + persist.facts events
```

---

## 6. Prompt Design

### System Prompt (fact extraction + summary)

```
You are an assistant analyzing WhatsApp conversations between real estate brokers and leads.

Your tasks:
1. Extract or update structured facts from the latest messages.
2. Generate an updated one-paragraph summary of the conversation so far.

Rules:
- Only update facts that are clearly supported by the messages.
- Do not invent or assume facts not mentioned.
- Preserve existing facts if not contradicted.
- The summary must be concise (1–3 sentences).
- Respond in JSON format only.
```

### Expected LLM Response Format

```json
{
  "summary": "User is searching for a two-bedroom apartment downtown with a budget around 600k.",
  "facts": {
    "intent":          { "value": "buy",       "confidence": 0.97 },
    "property_type":   { "value": "apartment", "confidence": 0.95 },
    "location":        { "value": "downtown",  "confidence": 0.92 },
    "budget":          { "value": 600000,      "confidence": 0.88 },
    "bedrooms":        { "value": 2,           "confidence": 0.91 }
  }
}
```

> The `updated_at` timestamp is added by the worker after parsing, not by the LLM.

---

## 7. Model Configuration

| Parameter       | Value / Notes                                            |
|-----------------|----------------------------------------------------------|
| `model`         | Configured via `LLM_MODEL` env var (e.g. `gpt-4o`)      |
| `temperature`   | `0.2` — low randomness for consistent structured output  |
| `max_tokens`    | `1024` — sufficient for summary + facts JSON             |
| `response_format` | `{ type: "json_object" }` — enforces JSON output       |
| `timeout`       | `10 seconds` — failure falls back gracefully             |

Environment variable:
```dotenv
LLM_MODEL=gpt-4o
OPENAI_API_KEY=sk-...
```

---

## 8. Token Budget

One of the primary design goals is **low token usage**. The rolling summary pattern achieves this by replacing the full message history with a short paragraph.

### Estimated Token Usage Per Call

| Component          | Approx. tokens |
|--------------------|----------------|
| System prompt      | ~120           |
| Rolling summary    | ~50            |
| Facts block        | ~100           |
| Buffer (≤5 msgs)   | ~150           |
| New message        | ~30            |
| **Total input**    | **~450**       |
| LLM response       | ~200           |
| **Total per call** | **~650**       |

Without rolling summaries, a 50-message conversation could easily exceed 3 000+ tokens per call. With summaries, the cost stays roughly constant regardless of conversation length.

---

## 9. Failure Handling

| Failure scenario              | Behaviour                                                       |
|-------------------------------|-----------------------------------------------------------------|
| LLM API timeout (> 10s)       | Error logged; summary and fact extraction skipped for this cycle; buffer is **not** cleared |
| LLM returns invalid JSON      | Error logged; response discarded; buffer retained for next cycle |
| LLM returns partial facts     | Only valid fact keys are merged; invalid keys are discarded     |
| LLM confidence below 0.5      | Fact is not merged into the store                               |
| Network error calling LLM API | Retried once immediately; if still failing, cycle is skipped    |

> **Key principle:** LLM failures are **non-fatal**. The system continues processing and persisting messages even if the LLM call fails. The summary and facts will be updated on the next successful trigger.
