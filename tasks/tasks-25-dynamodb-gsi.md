# DynamoDB Global Secondary Indexes Tasks

## Small Tasks

1. Design GSI schema for broker_id + created_at access pattern.
2. Design GSI schema for customer_id + created_at access pattern.
3. Evaluate GSI cost impact (WCU on every write).
4. Implement GSI creation with CloudFormation/Terraform.
5. Implement query logic for listing all conversations by broker_id.
6. Implement query logic for looking up customer across brokers.
7. Add pagination support for GSI queries.
8. Test GSI query performance with large datasets.
9. Document when to use base table vs GSI queries.
10. Monitor GSI consumed capacity and throttling.
11. Implement backfill strategy for existing data when adding new GSI.
12. Document GSI maintenance and operational considerations.

---

## Unit Tests

1. Test GSI query by broker_id returns correct conversations.
2. Test GSI query by customer_id returns correct conversations.
3. Test GSI query with pagination returns correct page size.
4. Test GSI query with date range filter returns correct results.

## Integration Tests

1. Test GSI is populated when new conversation META is written.
2. Test GSI query returns all conversations for a given broker_id.
3. Test GSI query returns all conversations for a given customer_id.
4. Test GSI pagination handles > 1000 results correctly.
5. Test GSI query performance is < 50ms for typical result sets.
6. Test backfill script populates GSI for existing items.
