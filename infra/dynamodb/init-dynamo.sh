#!/usr/bin/env bash
# init-dynamo.sh — local table bootstrap
set -e

# Wait for DynamoDB Local to be ready
until curl -s http://dynamodb-local:8000 > /dev/null; do
  echo "Waiting for DynamoDB Local..."
  sleep 2
done

AWS_ACCESS_KEY_ID=local \
AWS_SECRET_ACCESS_KEY=local \
aws dynamodb create-table \
  --endpoint-url http://dynamodb-local:8000 \
  --region us-east-1 \
  --table-name crm_memory \
  --attribute-definitions \
      AttributeName=PK,AttributeType=S \
      AttributeName=SK,AttributeType=S \
  --key-schema \
      AttributeName=PK,KeyType=HASH \
      AttributeName=SK,KeyType=RANGE \
  --billing-mode PAY_PER_REQUEST

echo "DynamoDB Table 'crm_memory' created successfully."
