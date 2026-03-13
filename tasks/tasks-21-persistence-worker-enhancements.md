# Persistence Worker Enhancement Tasks

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
