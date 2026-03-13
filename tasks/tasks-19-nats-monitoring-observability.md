# NATS Monitoring and Observability Tasks

## Small Tasks

1. Integrate NATS HTTP monitoring endpoint (port 8222) into health checks.
2. Implement monitoring for consumer pending messages with alert threshold > 1000.
3. Implement monitoring for consumer redelivery count with alert threshold > 3 per message.
4. Implement monitoring for dead-letter message count with alert threshold > 0.
5. Implement monitoring for JetStream storage used with alert threshold > 80%.
6. Implement monitoring for NATS server disconnections.
7. Create dashboard for NATS metrics: /varz, /connz, /subsz, /jsz endpoints.
8. Implement structured logging for NATS events (publish, consume, ack, nak).
9. Add metrics for messages processed per second.
10. Add metrics for NATS publish latency (p50, p95, p99).
11. Add metrics for NATS consume latency (p50, p95, p99).
12. Document NATS CLI commands for debugging and monitoring.
13. Create alerting rules for critical NATS metrics.
14. Test monitoring with simulated failure scenarios.
