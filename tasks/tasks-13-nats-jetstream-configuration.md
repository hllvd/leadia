# NATS JetStream Configuration Tasks

## Dependencies
- **Required by:** tasks-02 (webhook publishes to NATS)
- **Required by:** tasks-10 (message worker consumes from NATS)
- **Required by:** tasks-11 (persistence worker consumes from NATS)
- **Required by:** tasks-31 (core intelligence integration - event flow)
- **Related:** tasks-17 (stream management)
- **Related:** tasks-18 (consumer configuration)
- **Related:** tasks-19 (monitoring)
- **Related:** tasks-30 (clustering)

## Small Tasks

1. ~~Set up NATS server with JetStream enabled.~~
2. ~~Create 'messages' stream with subject 'message.received'.~~
3. ~~Create 'persistence_events' stream with subjects 'persist.message', 'persist.summary', 'persist.facts'.~~
4. ~~Configure consumer queue groups: 'message-workers', 'persistence-workers'.~~
5. Set up retry policies and dead letter queues.
6. Configure message retention and storage limits.
7. Implement monitoring for stream and consumer metrics.
8. ~~Test stream publishing and consuming.~~
9. Set up clustering for high availability in production.
10. ~~Document stream configuration and subject patterns.~~

---

## Unit Tests

1. Test stream creation is idempotent (no error if stream already exists).
2. Test consumer creation is idempotent.
3. Test message publish to 'message.received' subject succeeds.
4. Test message publish to 'persist.*' subjects succeeds.
5. Test queue group delivers each message to exactly one consumer.

## Integration Tests

1. Test 'messages' stream receives and delivers message.received events.
2. Test 'persistence_events' stream receives and delivers persist.* events.
3. Test unacknowledged message is redelivered after ack_wait timeout.
4. Test message is routed to dead-letter after max_deliver exceeded.
5. Test NATS server restart does not lose unacknowledged messages.
6. Test two consumers in same queue group each receive different messages.