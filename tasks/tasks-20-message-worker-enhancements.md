# Message Worker Enhancement Tasks

## Dependencies
- **Requires:** tasks-10 (base message worker implementation)
- **Integrates with:** tasks-31 (core intelligence integration)
- **Related:** tasks-05 (Redis connection)
- **Related:** tasks-13 (NATS connection)
- **Related:** tasks-19 (monitoring and metrics)

## Small Tasks

1. Implement worker startup sequence: connect to NATS, verify streams, register consumer.
2. Add Redis connection verification on worker startup.
3. Implement crash-and-restart behavior when NATS or Redis unavailable at startup.
4. Add environment variable configuration for all buffer thresholds.
5. Add environment variable configuration for cache TTL settings.
6. Add environment variable configuration for LLM settings.
7. Implement structured logging with required fields: conversation_id, event_type, duration_ms, llm_called, cache_hit.
8. Add metrics for messages processed per second.
9. Add metrics for LLM calls per minute.
10. Add metrics for LLM call latency (p50, p95, p99).
11. Add metrics for buffer flush rate.
12. Add metrics for cache hit rate.
13. Implement exponential backoff for DynamoDB load failures.
14. Implement buffer retention when LLM call fails (don't clear buffer).
15. Implement NAK behavior for recoverable errors.
16. Test worker behavior with cache miss scenarios.
17. Test worker behavior with LLM failures.
18. Test worker behavior with NATS publish failures.

---

## Unit Tests

1. Test worker exits on startup if NATS is unreachable.
2. Test worker exits on startup if Redis is unreachable.
3. Test buffer is retained when LLM call fails.
4. Test NAK is sent for recoverable errors.
5. Test structured log includes conversation_id, duration_ms, llm_called, cache_hit.
6. Test all buffer threshold env vars override defaults correctly.

## Integration Tests

1. Test worker restarts automatically after crash (Docker restart policy).
2. Test cache miss triggers DynamoDB load and caches result.
3. Test LLM failure does not lose the message (NAK → redelivery).
4. Test NATS publish failure triggers NAK and redelivery.
5. Test cache hit rate metric is recorded correctly.
6. Test LLM latency metric is recorded per call.
