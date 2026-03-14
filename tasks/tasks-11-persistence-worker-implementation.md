# Persistence Worker Implementation Tasks

## Dependencies
- **Requires:** tasks-12 (DynamoDB setup and access patterns)
- **Requires:** tasks-13 (NATS configuration)
- **Required by:** tasks-31 (core intelligence integration - persistence handlers)
- **Related:** tasks-21 (worker enhancements)
- **Related:** tasks-23 (TTL configuration)

## Small Tasks

1. ~~Subscribe to NATS stream 'persistence_events' with subjects 'persist.message', 'persist.summary', 'persist.facts'.~~
2. ~~Use queue group 'persistence-workers' for load balancing.~~
3. ~~Handle persist.message: write message record to DynamoDB with PK=CONV#<id>, SK=MSG#<timestamp>.~~
4. ~~Handle persist.summary: update META record with new rolling_summary and last_message_hash.~~
5. ~~Handle persist.facts: write or update FACT#<name> records for each fact.~~
6. Implement batch writes for efficiency.
7. ~~Handle DynamoDB write failures with retries.~~
8. ~~Acknowledge events after successful persistence.~~
9. Implement idempotent operations to handle duplicate events.
10. ~~Test persistence with various event types.~~
11. Monitor persistence latency and error rates.

---

## Unit Tests

1. Test persist.message writes correct PK=CONV#<id>, SK=MSG#<timestamp>.
2. Test persist.summary updates META record with rolling_summary and last_hash.
3. Test persist.facts writes one FACT#<name> record per fact.
4. Test unknown event type is handled without crash.
5. Test ACK is sent after successful DynamoDB write.
6. Test NAK is sent on DynamoDB write failure.

## Integration Tests

1. Test persist.message event creates correct DynamoDB item.
2. Test persist.summary event updates existing META record.
3. Test persist.facts event writes all facts to DynamoDB.
4. Test DynamoDB throttling triggers retry with backoff.
5. Test duplicate events produce same DynamoDB state (idempotency).
6. Test two persistence-worker instances share load via queue group.
7. Test worker recovers and reprocesses events after crash.