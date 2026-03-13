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