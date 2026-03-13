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
