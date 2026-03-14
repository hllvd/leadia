# Core Intelligence & Integration Tasks

## Overview
This task file serves as the **"glue"** that integrates all scattered components into a cohesive implementation. It consolidates logic from multiple task files into end-to-end flows.

## Dependencies
- **Integrates:** tasks-03 (deduplication)
- **Integrates:** tasks-04 (user classification)
- **Integrates:** tasks-05 (cache-aside pattern)
- **Integrates:** tasks-06 (buffer management)
- **Integrates:** tasks-07 (fact extraction)
- **Integrates:** tasks-08 (summary generation)
- **Integrates:** tasks-09 (LLM context construction)
- **Integrates:** tasks-10 (message worker orchestration)
- **Integrates:** tasks-11 (persistence handlers)
- **Integrates:** tasks-12 (DynamoDB patterns)
- **Related:** tasks-20 (message worker enhancements)
- **Related:** tasks-21 (persistence worker enhancements)
- **Related:** tasks-26 (data flow optimization)

## Small Tasks

### Message Worker - State Management
1. Implement cache-aside pattern: check Redis first, fallback to DynamoDB on miss.
2. Implement state loader with error handling for both cache and database failures.
3. Implement state writer that updates both cache and publishes persistence events.
4. Add cache warming strategy for frequently accessed conversations.
5. Implement cache invalidation strategy when state is updated.
6. Test cache-aside pattern with various scenarios (hit, miss, failure).

### Message Worker - Deduplication Flow
1. Integrate message hash generation into the processing pipeline.
2. Implement deduplication check before any processing begins.
3. Add metrics for duplicate message detection rate.
4. Test deduplication with rapid duplicate message scenarios.

### Message Worker - LLM Integration Flow
1. Implement complete LLM context assembly pipeline (Summary + Facts + Buffer + New Message).
2. Integrate LLM API call with timeout and retry logic.
3. Implement LLM response parsing with schema validation.
4. Implement fact extraction from LLM response with confidence filtering (>= 0.5).
5. Implement summary update from LLM response.
6. Implement fact merging logic (higher confidence overwrites).
7. Add error handling for malformed LLM responses.
8. Test LLM integration with various conversation scenarios.

### Message Worker - Event Publishing Flow
1. Implement persist.message event publishing after every message.
2. Implement persist.summary event publishing only when summary is updated.
3. Implement persist.facts event publishing only when facts change.
4. Add event publishing error handling and retry logic.
5. Test event publishing with NATS failures.

### Persistence Worker - Event Routing
1. Implement event type detection and routing logic.
2. Add event schema validation before processing.
3. Implement error handling for unknown event types.
4. Test event routing with all event types.

### Persistence Worker - DynamoDB Write Handlers
1. Implement persist.message handler with PK=CONV#<id>, SK=MSG#<timestamp> pattern.
2. Implement persist.summary handler with UpdateItem on META record.
3. Implement persist.facts handler with BatchWriteItem for multiple facts.
4. Add conditional writes to prevent race conditions.
5. Implement write retry logic with exponential backoff.
6. Add metrics for write success/failure rates per event type.
7. Test all handlers with various payload scenarios.

### End-to-End Integration
1. Implement complete message flow: webhook → normalization → worker → LLM → persistence.
2. Add distributed tracing to track messages through entire pipeline.
3. Implement integration tests for complete flow scenarios.
4. Test error recovery at each stage of the pipeline.
5. Document the complete integration architecture.

---

## Unit Tests

1. Test cache-aside: Redis hit returns state without DynamoDB call.
2. Test cache-aside: Redis miss triggers DynamoDB load and caches result.
3. Test cache-aside: DynamoDB failure propagates error correctly.
4. Test deduplication check runs before buffer update or LLM trigger.
5. Test LLM context includes summary + facts + buffer + new message.
6. Test LLM response parser extracts facts with correct confidence values.
7. Test fact merge: higher confidence overwrites, lower does not.
8. Test persist.summary is only published when summary actually changes.
9. Test persist.facts is only published when facts actually change.
10. Test persist.message handler writes correct PK/SK pattern.
11. Test persist.summary handler uses UpdateItem on META record.
12. Test persist.facts handler writes one item per fact.

## Integration Tests

1. Test complete flow: webhook → NATS → worker → LLM → persist.* → DynamoDB.
2. Test cache-aside under load: state is consistent across 100 sequential messages.
3. Test deduplication prevents double-processing under rapid retries.
4. Test LLM failure at any stage does not corrupt conversation state.
5. Test all persist.* events are written to DynamoDB correctly after LLM trigger.
6. Test distributed trace spans entire pipeline for a single message.
7. Test error recovery: system resumes correctly after each component failure.
