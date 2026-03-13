# Event Schema Validation Tasks

## Dependencies
- **Requires:** tasks-02 (message.received schema)
- **Requires:** tasks-10 (persist.* event schemas)
- **Required by:** tasks-11 (persistence worker validates events)
- **Required by:** tasks-31 (core intelligence integration validates all events)
- **Related:** tasks-29 (invalid events go to DLQ)

## Small Tasks

1. Define JSON schema for message.received event payload.
2. Define JSON schema for persist.message event payload.
3. Define JSON schema for persist.summary event payload.
4. Define JSON schema for persist.facts event payload.
5. Implement schema validation in api-gateway before publishing message.received.
6. Implement schema validation in message-worker before publishing persist.* events.
7. Implement schema validation in persistence-worker when consuming persist.* events.
8. Add error handling for schema validation failures.
9. Send invalid events to dead-letter queue with validation error details.
10. Document all event schemas with examples.
11. Implement schema versioning strategy for backward compatibility.
12. Test schema validation with valid and invalid payloads.
13. Monitor schema validation error rates.
14. Create schema migration guide for breaking changes.
