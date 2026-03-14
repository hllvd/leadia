# Persistence Worker Enhancement Tasks

## Dependencies
- **Requires:** tasks-11 (base persistence worker implementation)
- **Integrates with:** tasks-31 (core intelligence integration - persistence handlers)
- **Related:** tasks-12 (DynamoDB connection)
- **Related:** tasks-13 (NATS connection)
- **Related:** tasks-19 (monitoring and metrics)
- **Related:** tasks-29 (dead-letter queue handling)

## Small Tasks

1. Implement worker startup sequence: connect to NATS, verify streams, verify DynamoDB table.
2. Implement crash-and-restart behavior when NATS or DynamoDB unavailable at startup.
3. Add environment variable configuration for DynamoDB settings.
4. Implement exponential backoff retry for DynamoDB throttling errors.
5. Implement retry logic with max_deliver limit (10 attempts).
6. Implement dead-letter handling for events exceeding max_deliver.
7. Implement dead-letter handling for invalid event schemas.
8. Add structured logging with required fields: event_type, conversation_id, duration_ms, retry_count.
9. Add metrics for DynamoDB writes per second.
10. Add metrics for DynamoDB write error rate.
11. Add metrics for DynamoDB write latency (p50, p95, p99).
12. Add metrics for dead-letter event count.
13. Implement idempotent write operations for all event types.
14. Test worker behavior with DynamoDB throttling scenarios.
15. Test worker behavior with DynamoDB unavailability.
16. Test worker behavior with invalid event schemas.
17. Test worker behavior when max_deliver is exceeded.

---

## Unit Tests

1. Test worker exits on startup if NATS is unreachable.
2. Test worker exits on startup if DynamoDB is unreachable.
3. Test exponential backoff increases delay on each retry.
4. Test invalid event schema routes to dead-letter.
5. Test structured log includes event_type, conversation_id, duration_ms, retry_count.
6. Test idempotent write: same persist.message twice produces one DynamoDB item.

## Integration Tests

1. Test DynamoDB throttling triggers retry and eventually succeeds.
2. Test event exceeding max_deliver is routed to persist.DEAD.
3. Test worker restarts automatically after crash.
4. Test DynamoDB write latency metric is recorded per operation.
5. Test dead-letter count metric increments on failed events.
6. Test worker resumes processing after DynamoDB recovers from outage.
