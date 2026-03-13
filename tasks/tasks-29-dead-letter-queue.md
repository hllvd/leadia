# Dead-Letter Queue Handling Tasks

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
