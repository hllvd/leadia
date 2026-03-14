# Docker Containerization Tasks

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
12. Prepare production considerations: secrets, scaling, TLS.

---

## Unit Tests

1. Test Dockerfile builds successfully for each service.
2. Test multi-stage build produces minimal production image.
3. Test environment variables are correctly passed to containers.
4. Test health check endpoints respond correctly.

## Integration Tests

1. Test docker-compose up starts all services without errors.
2. Test api-gateway can reach NATS after startup.
3. Test message-worker can reach NATS and DynamoDB after startup.
4. Test persistence-worker can reach NATS and DynamoDB after startup.
5. Test service startup order: NATS healthy before workers start.
6. Test container restart policy recovers crashed services.
7. Test end-to-end message flow across all containers.