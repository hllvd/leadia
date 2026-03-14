# DynamoDB Operational Excellence Tasks

## Small Tasks

1. Enable Point-in-Time Recovery (PITR) for crm_memory table.
2. Enable encryption at rest with AWS KMS for crm_memory table.
3. Configure IAM roles with least-privilege access for workers.
4. Remove hardcoded AWS credentials from service code.
5. Implement CloudWatch alarms for ThrottledRequests metric.
6. Implement CloudWatch alarms for SystemErrors metric.
7. Implement CloudWatch alarms for consumed capacity > 80%.
8. Document backup and restore procedures.
9. Implement monitoring for hot partition detection.
10. Document item size limits (400 KB max) and validation.
11. Evaluate and document Global Tables requirements for multi-region.
12. Create runbook for DynamoDB operational issues.
13. Test backup and restore procedures in staging environment.
14. Document capacity planning and cost optimization strategies.

---

## Unit Tests

1. Test IAM policy allows only required DynamoDB actions (least-privilege).
2. Test item size validation rejects items > 400KB.
3. Test CloudWatch alarm thresholds are correctly configured.

## Integration Tests

1. Test PITR is enabled and a restore point can be created.
2. Test backup restore produces identical table data.
3. Test CloudWatch alarm fires on ThrottledRequests.
4. Test CloudWatch alarm fires on consumed capacity > 80%.
5. Test workers use IAM role credentials (no hardcoded keys in env).
