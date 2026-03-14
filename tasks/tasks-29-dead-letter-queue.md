# Dead-Letter Queue Handling Tasks

## Dependencies
- **Requires:** tasks-13 (NATS DLQ configuration)
- **Requires:** tasks-17 (stream management with DLQ subjects)
- **Required by:** tasks-20 (message worker error handling)
- **Required by:** tasks-21 (persistence worker error handling)
- **Related:** tasks-28 (schema validation failures go to DLQ)
- **Related:** tasks-19 (DLQ monitoring)

## Small Tasks

1. Configure dead-letter subject 'message.received.DEAD' for failed message processing.
2. Configure dead-letter subject 'persist.DEAD' for failed persistence events.
3. Implement automatic routing to DLQ when max_deliver is exceeded.
4. Implement DLQ consumer for monitoring and alerting.
5. Create dashboard for dead-letter message counts and trends.
6. Implement alerting when dead-letter count > 0.
7. Create manual re-drive procedure for dead-letter messages.
8. Implement dead-letter message inspection tools.
9. Add metadata to dead-letter messages: original subject, failure reason, retry count.
10. Document dead-letter handling procedures in runbook.
11. Implement automated re-drive for transient failures after root cause resolution.
12. Test dead-letter routing with simulated failures.
13. Create dead-letter message retention policy.
14. Monitor dead-letter queue size and alert on growth.

---

## Unit Tests

1. Test dead-letter message includes original subject, failure reason, retry count.
2. Test DLQ routing is triggered when max_deliver is exceeded.
3. Test DLQ routing is triggered for invalid event schemas.
4. Test re-drive publishes message back to original subject.

## Integration Tests

1. Test message.received.DEAD receives message after 5 failed attempts.
2. Test persist.DEAD receives event after 10 failed attempts.
3. Test alert fires when dead-letter count goes from 0 to 1.
4. Test manual re-drive successfully reprocesses a dead-letter message.
5. Test dead-letter queue does not grow unboundedly (retention policy enforced).
6. Test DLQ consumer can inspect message content and metadata.
