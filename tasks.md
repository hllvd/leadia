# Authentication and Webhook Security Tasks

## Small Tasks

1. ~~Implement HMAC-SHA256 signature verification for webhook requests using the WEBHOOK_SECRET environment variable.~~
2. ~~Add constant-time comparison function to prevent timing attacks during signature validation.~~
3. ~~Return HTTP 401 for invalid webhook signatures.~~
4. ~~Ensure webhook endpoint responds with 200 as fast as possible after signature verification.~~
5. Log invalid signature attempts for monitoring and security auditing.
6. ~~Test signature verification with valid and invalid signatures.~~
7. Document the webhook secret rotation process for production deployments.# Message Ingestion and Normalization Tasks

## Small Tasks

1. ~~Create the POST /webhook/whatsapp endpoint in the API gateway.~~
2. ~~Parse the raw WhatsApp payload from the request body.~~
3. ~~Extract broker phone number, customer phone number, sender type, message text, and timestamp from the payload.~~
4. ~~Generate conversation_id as <broker_number>-<customer_number>.~~
5. ~~Apply text normalization rules: trim, collapse multiple spaces, normalize newlines, encode as UTF-8.~~
6. ~~Validate sender_type is either "broker" or "customer", return 400 otherwise.~~
7. ~~Convert timestamp to ISO 8601 format.~~
8. ~~Create NormalizedMessage object with all required fields.~~
9. ~~Publish the normalized message to NATS stream 'messages' with subject 'message.received'.~~
10. ~~Handle and log any errors during normalization, return appropriate HTTP status codes.~~
11. ~~Test normalization with various WhatsApp payload formats.~~
12. ~~Ensure the endpoint handles malformed payloads gracefully.~~# Message Deduplication Tasks

## Small Tasks

1. ~~Generate message_hash using SHA256(timestamp + broker_id + customer_id + text).~~
2. ~~Check if the generated hash matches the last_message_hash stored in the conversation state.~~
3. ~~If hash matches, silently ignore the message and return 200 to webhook provider.~~
4. ~~If hash does not match, proceed with processing and update last_message_hash.~~
5. ~~Store the hash in the conversation state after successful processing.~~
6. Handle hash collisions gracefully (though unlikely with SHA256).
7. ~~Test deduplication with duplicate messages.~~
8. ~~Ensure deduplication works across restarts by persisting last_message_hash in DynamoDB.~~# User Classification Tasks

## Small Tasks

1. ~~Check if conversation_id exists in the local cache or DynamoDB.~~
2. ~~If conversation_id not found, classify as new_lead.~~
3. ~~Initialize ConversationState for new leads: empty rolling_summary, empty facts object, empty buffer.~~
4. ~~Set initial last_message_hash and timestamps.~~
5. ~~If conversation_id exists, classify as existing_lead and load existing state.~~
6. ~~Update last_activity_timestamp on every message for existing conversations.~~
7. Handle race conditions when multiple messages arrive for the same new conversation simultaneously.
8. ~~Test classification logic with new and existing conversation IDs.~~
9. ~~Ensure new lead initialization includes all required fields.~~# Conversation State Cache Tasks

## Small Tasks

1. Implement local fast memory cache (LRU or Redis) for ConversationState objects.
2. Use conversation_id as cache key.
3. Set cache TTL to 10 minutes after last activity.
4. Load conversation state from cache first, fallback to DynamoDB if cache miss.
5. Update cache on every state change (buffer, facts, summary).
6. Implement cache eviction policy to prevent memory leaks.
7. Handle cache serialization/deserialization for complex objects.
8. Test cache performance and hit/miss ratios.
9. Ensure cache is thread-safe for concurrent access.
10. Monitor cache usage and adjust TTL based on usage patterns.# Message Buffer Management Tasks

## Small Tasks

1. ~~Append incoming message text to the buffer array.~~
2. ~~Update buffer_chars count with the length of the new message.~~
3. ~~Check buffer limits: max_messages = 6, max_chars = 500.~~
4. ~~If limits exceeded, trigger summary generation immediately.~~
5. ~~Implement timeout check: if time since last message > 30 seconds, trigger summary.~~
6. ~~Implement expiration: if buffer not flushed after 5 minutes, force flush and trigger summary.~~
7. ~~Clear buffer after successful summary generation.~~
8. ~~Reset buffer_chars to 0 after clearing.~~
9. Handle fragmented messages properly in the buffer.
10. ~~Test buffer limits and timeout behavior.~~
11. ~~Ensure buffer is persisted in cache and recovered on cache miss.~~# Fact Extraction Tasks

## Small Tasks

1. ~~Implement fact extraction logic using LLM or rule-based system.~~
2. ~~Extract structured facts from incoming message text (e.g., property_type, location, budget).~~
3. ~~Assign confidence scores to extracted facts (0.0 to 1.0).~~
4. ~~Update existing facts if new information has higher confidence.~~
5. ~~Store facts in the facts Record<string, FactEntry> in conversation state.~~
6. ~~Set updated_at timestamp for each fact update.~~
7. ~~Implement facts TTL: persist to DynamoDB after 10 minutes of inactivity.~~
8. ~~Publish persist.facts event when facts are updated.~~
9. ~~Handle conflicting facts (e.g., different budget values).~~
10. ~~Test fact extraction accuracy with sample messages.~~
11. ~~Ensure facts are included in LLM context construction.~~# Rolling Summary Generation Tasks

## Small Tasks

1. ~~Check summary trigger conditions: buffer_messages >= 5, buffer_chars >= 400, or time_since_last > 30s.~~
2. ~~Construct LLM prompt with previous summary, recent buffer messages, and incoming message.~~
3. ~~Call LLM to generate updated rolling summary.~~
4. ~~Update rolling_summary in conversation state with LLM response.~~
5. ~~Clear the message buffer after summary update.~~
6. ~~Publish persist.summary event with updated summary.~~
7. ~~Handle LLM call failures gracefully (log and skip summary update).~~
8. Optimize prompt to minimize token usage.
9. ~~Test summary generation with various conversation scenarios.~~
10. ~~Ensure summaries preserve key context while compressing history.~~# LLM Context Construction Tasks

## Small Tasks

1. ~~Build LLM context payload with sections: SUMMARY, FACTS, RECENT MESSAGES, NEW MESSAGE.~~
2. ~~Include rolling_summary in SUMMARY section.~~
3. ~~Format facts as key: value pairs in FACTS section.~~
4. ~~Include current buffer contents in RECENT MESSAGES section.~~
5. ~~Add incoming message text in NEW MESSAGE section.~~
6. ~~Ensure proper formatting and separators between sections.~~
7. Limit context size to prevent token limits.
8. Prioritize most relevant facts for context.
9. ~~Test context construction with complete conversation states.~~
10. Optimize context for LLM prompt effectiveness.# Message Worker Implementation Tasks

## Small Tasks

1. ~~Subscribe to NATS stream 'messages' with subject 'message.received'.~~
2. ~~Use queue group 'message-workers' for load balancing.~~
3. ~~Load or initialize conversation state for each message.~~
4. ~~Update message buffer with incoming message.~~
5. ~~Trigger fact extraction on the new message.~~
6. ~~Check and trigger rolling summary if conditions met.~~
7. ~~Publish persistence events: persist.message, persist.summary (if updated), persist.facts (if updated).~~
8. ~~Acknowledge message after processing.~~
9. ~~Handle errors and implement retry logic via NATS.~~
10. Implement graceful shutdown and message reprocessing.
11. ~~Test worker with simulated message events.~~
12. Monitor worker performance and throughput.# Persistence Worker Implementation Tasks

## Small Tasks

1. ~~Subscribe to NATS stream 'persistence_events' with subjects 'persist.message', 'persist.summary', 'persist.facts'.~~
2. ~~Use queue group 'persistence-workers' for load balancing.~~
3. ~~Handle persist.message: write message record to DynamoDB with PK=CONV#<id>, SK=MSG#<timestamp>.~~
4. ~~Handle persist.summary: update META record with new rolling_summary and last_message_hash.~~
5. ~~Handle persist.facts: write or update FACT#<name> records for each fact.~~
6. Implement batch writes for efficiency.
7. ~~Handle DynamoDB write failures with retries.~~
8. ~~Acknowledge events after successful persistence.~~
9. Implement idempotent operations to handle duplicate events.
10. ~~Test persistence with various event types.~~
11. Monitor persistence latency and error rates.# DynamoDB Setup and Access Patterns Tasks

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
11. Implement pagination for large result sets.# NATS JetStream Configuration Tasks

## Small Tasks

1. ~~Set up NATS server with JetStream enabled.~~
2. ~~Create 'messages' stream with subject 'message.received'.~~
3. ~~Create 'persistence_events' stream with subjects 'persist.message', 'persist.summary', 'persist.facts'.~~
4. ~~Configure consumer queue groups: 'message-workers', 'persistence-workers'.~~
5. Set up retry policies and dead letter queues.
6. Configure message retention and storage limits.
7. Implement monitoring for stream and consumer metrics.
8. ~~Test stream publishing and consuming.~~
9. Set up clustering for high availability in production.
10. ~~Document stream configuration and subject patterns.~~# Docker Containerization Tasks

## Small Tasks

1. ~~Create Dockerfile for api-gateway service with Node.js base image.~~ : Actually .NET, but implemented
2. ~~Create Dockerfile for message-worker service.~~
3. ~~Create Dockerfile for persistence-worker service.~~
4. ~~Set up multi-stage builds for development and production.~~
5. ~~Configure docker-compose.yml with all services: api-gateway, message-worker, persistence-worker, nats, redis, dynamodb-local.~~ : No Redis, but DynamoDB local
6. ~~Set up environment variables in docker-compose files.~~
7. ~~Configure health checks for services.~~
8. ~~Set up volumes for NATS data and Redis persistence.~~ : NATS data yes, Redis no
9. ~~Implement service dependencies and startup order.~~
10. ~~Test container builds and service communication.~~
11. ~~Document local development setup with docker-compose.dev.yml.~~ : No dev file, but basic setup
12. Prepare production considerations: secrets, scaling, TLS.# Testing and Validation Tasks

## Small Tasks

1. ~~Create unit tests for message normalization logic.~~
2. ~~Create unit tests for deduplication hash generation.~~
3. ~~Create unit tests for user classification.~~
4. ~~Create integration tests for message processing pipeline.~~
5. ~~Create tests for DynamoDB access patterns.~~
6. ~~Create tests for NATS publishing and consuming.~~
7. ~~Test webhook signature verification.~~
8. Test conversation state cache operations.
9. ~~Test buffer limits and summary triggers.~~
10. ~~Test fact extraction accuracy.~~
11. ~~Test end-to-end message flow from webhook to persistence.~~
12. Set up CI/CD pipeline for automated testing.# Future CRM Integration Tasks

## Small Tasks

1. Design SQLite schema for real estate CRM: real_state_agency, real_state_broker tables.
2. Implement foreign key relationships between tables.
3. Create migration scripts for SQLite database setup.
4. Integrate SQLite with existing .NET application.
5. Implement CRUD operations for properties and brokers.
6. Design data flow between DynamoDB (conversations) and SQLite (CRM data).
7. Implement data synchronization mechanisms.
8. Create API endpoints for CRM operations.
9. Test referential integrity in SQLite.
10. Plan migration strategy from current system to full CRM.
11. Document SQLite vs DynamoDB usage patterns.# NATS Stream Management Tasks

## Small Tasks

1. Implement stream creation logic with idempotent checks (create if not exists).
2. Configure 'messages' stream with LimitsPolicy retention (24 hours / 1M msgs).
3. Configure 'persistence_events' stream with WorkQueuePolicy retention (deleted on ack).
4. Set max message size to 64 KB for 'messages' stream.
5. Set max message size to 256 KB for 'persistence_events' stream.
6. Configure stream replicas: 1 for dev, 3 for production.
7. Implement dead-letter subject configuration: 'message.received.DEAD' and 'persist.DEAD'.
8. Add stream monitoring endpoints integration (jsz endpoint).
9. Implement stream health checks on worker startup.
10. Create stream backup/restore procedures for production.
11. Document stream configuration in infrastructure-as-code format.
12. Test stream creation with various configuration scenarios.
# NATS Consumer Configuration Tasks

## Small Tasks

1. Configure message-worker consumer with durable name 'message-worker-group'.
2. Set message-worker queue group to 'message-workers' for load balancing.
3. Configure message-worker AckPolicy to Explicit.
4. Set message-worker max_deliver to 5 attempts.
5. Set message-worker ack_wait to 30 seconds.
6. Configure persistence-worker consumer with durable name 'persistence-worker-group'.
7. Set persistence-worker queue group to 'persistence-workers' for load balancing.
8. Configure persistence-worker AckPolicy to Explicit.
9. Set persistence-worker max_deliver to 10 attempts.
10. Set persistence-worker ack_wait to 60 seconds.
11. Implement consumer registration on worker startup with idempotent checks.
12. Add consumer health monitoring (pending messages, redelivery count).
13. Test consumer behavior with message acknowledgment scenarios.
14. Test consumer behavior when max_deliver is exceeded.
# NATS Monitoring and Observability Tasks

## Small Tasks

1. Integrate NATS HTTP monitoring endpoint (port 8222) into health checks.
2. Implement monitoring for consumer pending messages with alert threshold > 1000.
3. Implement monitoring for consumer redelivery count with alert threshold > 3 per message.
4. Implement monitoring for dead-letter message count with alert threshold > 0.
5. Implement monitoring for JetStream storage used with alert threshold > 80%.
6. Implement monitoring for NATS server disconnections.
7. Create dashboard for NATS metrics: /varz, /connz, /subsz, /jsz endpoints.
8. Implement structured logging for NATS events (publish, consume, ack, nak).
9. Add metrics for messages processed per second.
10. Add metrics for NATS publish latency (p50, p95, p99).
11. Add metrics for NATS consume latency (p50, p95, p99).
12. Document NATS CLI commands for debugging and monitoring.
13. Create alerting rules for critical NATS metrics.
14. Test monitoring with simulated failure scenarios.
# Message Worker Enhancement Tasks

## Small Tasks

1. Implement worker startup sequence: connect to NATS, verify streams, register consumer.
2. Add Redis connection verification on worker startup.
3. Implement crash-and-restart behavior when NATS or Redis unavailable at startup.
4. Add environment variable configuration for all buffer thresholds.
5. Add environment variable configuration for cache TTL settings.
6. Add environment variable configuration for LLM settings.
7. Implement structured logging with required fields: conversation_id, event_type, duration_ms, llm_called, cache_hit.
8. Add metrics for messages processed per second.
9. Add metrics for LLM calls per minute.
10. Add metrics for LLM call latency (p50, p95, p99).
11. Add metrics for buffer flush rate.
12. Add metrics for cache hit rate.
13. Implement exponential backoff for DynamoDB load failures.
14. Implement buffer retention when LLM call fails (don't clear buffer).
15. Implement NAK behavior for recoverable errors.
16. Test worker behavior with cache miss scenarios.
17. Test worker behavior with LLM failures.
18. Test worker behavior with NATS publish failures.
# Persistence Worker Enhancement Tasks

## Small Tasks

1. Implement worker startup sequence: connect to NATS, verify streams, verify DynamoDB table.
2. Implement crash-and-restart behavior when NATS or DynamoDB unavailable at startup.
3. Add environment variable configuration for DynamoDB settings.
4. Implement exponential backoff retry for DynamoDB throttling errors.
5. Implement retry logic with max_deliver limit (10 attempts).
6. Implement dead-letter handling for events exceeding max_deliver.
7. Implement dead-letter handling for invalid event schemas.
8. Add structured logging with required fields: event_type, conversation_id, duration_ms, retry_count.
9. Add metrics for DynamoDB writes per second.
10. Add metrics for DynamoDB write error rate.
11. Add metrics for DynamoDB write latency (p50, p95, p99).
12. Add metrics for dead-letter event count.
13. Implement idempotent write operations for all event types.
14. Test worker behavior with DynamoDB throttling scenarios.
15. Test worker behavior with DynamoDB unavailability.
16. Test worker behavior with invalid event schemas.
17. Test worker behavior when max_deliver is exceeded.
# Horizontal Scaling Implementation Tasks

## Small Tasks

1. Verify queue group load balancing works with multiple message-worker instances.
2. Verify queue group load balancing works with multiple persistence-worker instances.
3. Implement shared cache (Redis) access for conversation state across worker instances.
4. Test conversation state consistency across multiple worker instances.
5. Document scaling recommendations for different traffic levels (< 1K, 1K-50K, > 50K msg/day).
6. Implement worker instance identification in logs and metrics.
7. Test horizontal scaling with 2-3 message-worker replicas.
8. Test horizontal scaling with 2-3 persistence-worker replicas.
9. Implement graceful shutdown to prevent message loss during scaling down.
10. Test worker behavior during rolling deployments.
11. Document Kubernetes deployment configuration for horizontal pod autoscaling.
12. Test cache contention scenarios with multiple workers accessing same conversation.
# DynamoDB TTL and Retention Tasks

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
# Data Flow Optimization Tasks

## Small Tasks

1. Optimize webhook response time to consistently achieve < 50ms target.
2. Implement latency tracking for each data flow segment.
3. Add metrics for api-gateway processing time (normalization + publish).
4. Add metrics for NATS delivery latency (< 1ms target).
5. Add metrics for cache hit vs cache miss latency.
6. Add metrics for end-to-end message processing time.
7. Implement performance profiling for LLM call latency (500-3000ms).
8. Optimize cache access patterns to maximize hit rate.
9. Implement request tracing across all components (distributed tracing).
10. Add correlation IDs to track messages through entire pipeline.
11. Document latency SLAs for each component.
12. Test system performance under various load scenarios.
13. Identify and optimize bottlenecks in the data flow.
14. Create performance benchmarking suite.
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
# Event Schema Validation Tasks

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
# Dead-Letter Queue Handling Tasks

## Small Tasks

1. Configure dead-letter subject 'message.received.DEAD' for failed message processing.
2. Configure dead-letter subject 'persist.DEAD' for failed persistence events.
3. Implement automatic routing to DLQ when max_deliver is exceeded.
4. Implement DLQ consumer for monitoring and alerting.
5. Create dashboard for dead-letter message counts and trends.
6. Implement alerting when dead-letter count > 0.
7. Create manual re-drive procedure for dead-letter messages.
8. Implement dead-letter message inspection tools.
9. Add metadata to dead-letter messages: original subject, failure reason, retry count.
10. Document dead-letter handling procedures in runbook.
11. Implement automated re-drive for transient failures after root cause resolution.
12. Test dead-letter routing with simulated failures.
13. Create dead-letter message retention policy.
14. Monitor dead-letter queue size and alert on growth.
# NATS Clustering and High Availability Tasks

## Small Tasks

1. Design NATS cluster configuration with 3+ nodes for production.
2. Configure cluster routes between NATS nodes.
3. Set up cluster name and listen ports for inter-node communication.
4. Configure JetStream replicas to 3 for production streams.
5. Test failover behavior when one NATS node goes down.
6. Test split-brain scenarios and cluster recovery.
7. Implement health checks for cluster status.
8. Monitor cluster connectivity and node status.
9. Document cluster deployment architecture.
10. Implement automated cluster recovery procedures.
11. Test worker reconnection behavior during NATS node failures.
12. Document cluster upgrade procedures with zero downtime.
13. Implement cluster backup and disaster recovery procedures.
14. Test cluster performance under high load.
