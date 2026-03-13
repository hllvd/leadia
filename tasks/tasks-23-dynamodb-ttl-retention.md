# DynamoDB TTL and Retention Tasks

## Dependencies
- **Requires:** tasks-12 (DynamoDB table setup)
- **Required by:** tasks-11 (persistence worker adds TTL to writes)
- **Related:** tasks-24 (operational excellence)
- **Related:** tasks-27 (data residency and compliance)

## Small Tasks

1. Enable TTL attribute on DynamoDB table with attribute name 'ttl'.
2. Implement TTL calculation function: TTL = current_time + retention_days * 86400.
3. Set conversation META records TTL to 1 year (365 days).
4. Set message records TTL to 90 days.
5. Set fact records TTL to 1 year (365 days).
6. Add TTL attribute to all PutItem operations for messages.
7. Add TTL attribute to all PutItem operations for facts.
8. Add TTL attribute to all PutItem/UpdateItem operations for META records.
9. Document TTL deletion behavior (eventual, up to 48h delay).
10. Test TTL expiration with short retention periods in development.
11. Monitor TTL deletion metrics in production.
12. Implement manual cleanup script for expired items if needed.
