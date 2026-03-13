# NATS Stream Management Tasks

## Small Tasks

1. Implement stream creation logic with idempotent checks (create if not exists).
2. Configure 'messages' stream with LimitsPolicy retention (24 hours / 1M msgs).
3. Configure 'persistence_events' stream with WorkQueuePolicy retention (deleted on ack).
4. Set max message size to 64 KB for 'messages' stream.
5. Set max message size to 256 KB for 'persistence_events' stream.
6. Configure stream replicas: 1 for dev, 3 for production.
7. Implement dead-letter subject configuration: 'message.received.DEAD' and 'persist.DEAD'.
8. Add stream monitoring endpoints integration (jsz endpoint).
9. Implement stream health checks on worker startup.
10. Create stream backup/restore procedures for production.
11. Document stream configuration in infrastructure-as-code format.
12. Test stream creation with various configuration scenarios.
