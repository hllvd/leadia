# Horizontal Scaling Implementation Tasks

## Dependencies
- **Requires:** tasks-05 (shared cache for multi-instance state)
- **Requires:** tasks-10 (message worker with queue groups)
- **Requires:** tasks-11 (persistence worker with queue groups)
- **Requires:** tasks-13 (NATS queue group configuration)
- **Related:** tasks-14 (Docker containerization)
- **Related:** tasks-22 (Kubernetes deployment)

## Small Tasks

1. Verify queue group load balancing works with multiple message-worker instances.
2. Verify queue group load balancing works with multiple persistence-worker instances.
3. Implement shared cache (Redis) access for conversation state across worker instances.
4. Test conversation state consistency across multiple worker instances.
5. Document scaling recommendations for different traffic levels (< 1K, 1K-50K, > 50K msg/day).
6. Implement worker instance identification in logs and metrics.
7. Test horizontal scaling with 2-3 message-worker replicas.
8. Test horizontal scaling with 2-3 persistence-worker replicas.
9. Implement graceful shutdown to prevent message loss during scaling down.
10. Test worker behavior during rolling deployments.
11. Document Kubernetes deployment configuration for horizontal pod autoscaling.
12. Test cache contention scenarios with multiple workers accessing same conversation.

---

## Unit Tests

1. Test queue group configuration is identical across all worker instances.
2. Test worker instance ID is included in logs and metrics.
3. Test graceful shutdown drains in-flight messages before stopping.

## Integration Tests

1. Test 3 message-worker instances each receive different messages (no duplicates).
2. Test 3 persistence-worker instances each receive different events.
3. Test conversation state is consistent when two workers process same conversation sequentially.
4. Test no messages are lost during rolling deployment (one instance down).
5. Test cache contention: two workers loading same conversation_id return same state.
6. Test system handles 3x normal load with 3 worker replicas.
