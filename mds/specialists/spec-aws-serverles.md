# AWS SERVERLESS BEST PRACTICES

Lambdas:
- Single responsibility per function
- Keep cold start minimal

Idempotency:
- Required for all handlers

Storage:
- S3 = source of truth for blobs
- DynamoDB = metadata + queries

Events:
- Prefer async (SNS, SQS, EventBridge)
- Never couple services directly

Retries:
- Design for retries (no side effects duplication)

Security:
- Least privilege IAM