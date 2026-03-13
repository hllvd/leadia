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
