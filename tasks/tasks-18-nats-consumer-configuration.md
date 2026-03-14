# NATS Consumer Configuration Tasks

## Small Tasks

1. Configure message-worker consumer with durable name 'message-worker-group'.
2. Set message-worker queue group to 'message-workers' for load balancing.
3. Configure message-worker AckPolicy to Explicit.
4. Set message-worker max_deliver to 5 attempts.
5. Set message-worker ack_wait to 30 seconds.
6. Configure persistence-worker consumer with durable name 'persistence-worker-group'.
7. Set persistence-worker queue group to 'persistence-workers' for load balancing.
8. Configure persistence-worker AckPolicy to Explicit.
9. Set persistence-worker max_deliver to 10 attempts.
10. Set persistence-worker ack_wait to 60 seconds.
11. Implement consumer registration on worker startup with idempotent checks.
12. Add consumer health monitoring (pending messages, redelivery count).
13. Test consumer behavior with message acknowledgment scenarios.
14. Test consumer behavior when max_deliver is exceeded.

---

## Unit Tests

1. Test message-worker consumer config has durable name 'message-worker-group'.
2. Test persistence-worker consumer config has durable name 'persistence-worker-group'.
3. Test AckPolicy is set to Explicit for both consumers.
4. Test message-worker max_deliver is 5, ack_wait is 30s.
5. Test persistence-worker max_deliver is 10, ack_wait is 60s.
6. Test consumer registration is idempotent.

## Integration Tests

1. Test unacknowledged message-worker message is redelivered after 30s.
2. Test unacknowledged persistence-worker message is redelivered after 60s.
3. Test message is routed to dead-letter after 5 failed message-worker attempts.
4. Test message is routed to dead-letter after 10 failed persistence-worker attempts.
5. Test two message-worker instances each receive different messages.
6. Test consumer survives worker restart and resumes from last position.
