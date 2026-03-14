# Data Residency and Compliance Tasks

## Small Tasks

1. Document data residency for each component (NATS, Redis, DynamoDB).
2. Implement data retention policies: messages (90 days), metadata (1 year), facts (1 year).
3. Ensure raw webhook payloads are never stored (normalized immediately).
4. Implement secure deletion of expired data.
5. Document cache TTL policy (10 minutes for conversation state).
6. Implement audit logging for data access and modifications.
7. Document data encryption at rest for all storage layers.
8. Document data encryption in transit for all communication.
9. Implement GDPR compliance: right to erasure (delete conversation data).
10. Implement GDPR compliance: right to access (export conversation data).
11. Create data retention policy documentation.
12. Implement data anonymization for analytics and reporting.
13. Test data deletion workflows.
14. Document compliance requirements and implementation.

---

## Unit Tests

1. Test raw webhook payload is not stored anywhere in the pipeline.
2. Test GDPR erasure deletes all items for a given conversation_id.
3. Test GDPR export returns all data for a given conversation_id.
4. Test data anonymization replaces PII fields with placeholders.
5. Test audit log entry is created for each data access event.

## Integration Tests

1. Test data retention: MSG# items are absent after 90-day TTL.
2. Test data retention: META and FACT# items persist after 90 days.
3. Test GDPR erasure removes META, MSG#, and FACT# items from DynamoDB.
4. Test GDPR erasure also removes conversation state from cache.
5. Test audit log captures all DynamoDB write operations.
