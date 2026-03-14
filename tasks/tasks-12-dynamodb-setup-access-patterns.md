# DynamoDB Setup and Access Patterns Tasks

## Dependencies
- **Required by:** tasks-04 (user classification needs state lookup)
- **Required by:** tasks-05 (cache fallback to DynamoDB)
- **Required by:** tasks-11 (persistence worker writes)
- **Required by:** tasks-31 (core intelligence integration - state persistence)
- **Related:** tasks-23 (TTL configuration)
- **Related:** tasks-24 (operational excellence)
- **Related:** tasks-25 (GSI implementation)

## Small Tasks

1. ~~Create DynamoDB table 'crm_memory' with PK and SK as string keys.~~
2. ~~Set up single table design with patterns: CONV#<id> for PK, META/SK for metadata, MSG#<ts> for messages, FACT#<name> for facts.~~
3. ~~Implement query for conversation metadata: PK=CONV#<id>, SK=META.~~
4. ~~Implement query for all messages: PK=CONV#<id>, SK begins_with MSG#.~~
5. ~~Implement query for all facts: PK=CONV#<id>, SK begins_with FACT#.~~
6. Set up DynamoDB local for development with sharedDb and inMemory options.
7. ~~Configure AWS credentials and region for production.~~
8. ~~Implement error handling for DynamoDB operations.~~
9. ~~Test access patterns with sample data.~~
10. Optimize read/write capacity for expected load.
11. Implement pagination for large result sets.

---

## Unit Tests

1. Test GetItem for META record returns correct ConversationState fields.
2. Test Query for FACT# prefix returns all facts for a conversation.
3. Test Query for MSG# prefix returns messages in chronological order.
4. Test PutItem for new conversation creates all required attributes.
5. Test UpdateItem for META record updates only specified fields.
6. Test error handling returns appropriate exception on DynamoDB failure.

## Integration Tests

1. Test full conversation lifecycle: create META, write MSG#, write FACT#, read all.
2. Test Query with begins_with MSG# returns only messages.
3. Test Query with begins_with FACT# returns only facts.
4. Test GetItem on non-existent conversation returns null.
5. Test concurrent writes to same conversation do not corrupt state.
6. Test DynamoDB local setup matches production table schema.