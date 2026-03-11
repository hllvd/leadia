# Docker — WhatsApp CRM (Second Brain)

This document describes how to containerize, configure, and run the WhatsApp CRM system using Docker and Docker Compose. The architecture consists of several independent services that communicate through NATS JetStream and persist data to DynamoDB.

See also:
- [System Architecture](./SYSTEM_ARCHITECTURE.md)
- [API Reference](./API.md)
- [Queue Reference](./QUEUE.md) — NATS stream and consumer configuration

---

## Table of Contents

1. [Services Overview](#1-services-overview)
2. [Project Structure](#2-project-structure)
3. [Environment Variables](#3-environment-variables)
4. [Docker Compose](#4-docker-compose)
5. [Individual Dockerfiles](#5-individual-dockerfiles)
6. [Networking](#6-networking)
7. [Volumes & Persistence](#7-volumes--persistence)
8. [Health Checks](#8-health-checks)
9. [Local Development](#9-local-development)
10. [Production Considerations](#10-production-considerations)

---

## 1. Services Overview

The system is composed of the following containerized services:

| Service                  | Description                                                           | Exposes       |
|--------------------------|-----------------------------------------------------------------------|---------------|
| `api-gateway`            | Receives WhatsApp webhooks, normalizes messages, publishes to NATS    | `8080`        |
| `message-worker`         | Consumes `messages` stream, manages conversation state and buffer     | internal      |
| `persistence-worker`     | Consumes `persistence_events` stream, writes to DynamoDB             | internal      |
| `nats`                   | NATS JetStream message broker                                         | `4222`, `8222`|
| `dynamodb-local`         | Local DynamoDB emulator (development only)                           | `8000`        |
| `redis`                  | Optional fast-memory cache for conversation state                     | `6379`        |

---

## 2. Project Structure

```
second-brain-ia/
├── docker-compose.yml
├── docker-compose.dev.yml      # Overrides for local development
├── .env.example
├── services/
│   ├── api-gateway/
│   │   └── Dockerfile
│   ├── message-worker/
│   │   └── Dockerfile
│   └── persistence-worker/
│       └── Dockerfile
├── infra/
│   ├── nats/
│   │   └── nats-server.conf   # JetStream config
│   └── dynamodb/
│       └── init.sh            # Table bootstrap script
└── DOCKER.md
```

---

## 3. Environment Variables

Copy `.env.example` to `.env` before running.

```bash
cp .env.example .env
```

### `.env.example`

```dotenv
# ─── App ──────────────────────────────────────────────
APP_ENV=development                  # development | production
LOG_LEVEL=info                       # debug | info | warn | error

# ─── API Gateway ──────────────────────────────────────
API_PORT=8080
WEBHOOK_SECRET=your_webhook_hmac_secret

# ─── NATS ─────────────────────────────────────────────
NATS_URL=nats://nats:4222
NATS_STREAM_MESSAGES=messages
NATS_STREAM_PERSISTENCE=persistence_events

# ─── DynamoDB ─────────────────────────────────────────
DYNAMODB_REGION=us-east-1
DYNAMODB_TABLE=crm_memory
DYNAMODB_ENDPOINT=http://dynamodb-local:8000   # local dev only
AWS_ACCESS_KEY_ID=local                        # local dev only
AWS_SECRET_ACCESS_KEY=local                    # local dev only

# ─── Redis (optional cache) ───────────────────────────
REDIS_URL=redis://redis:6379

# ─── LLM ──────────────────────────────────────────────
OPENAI_API_KEY=sk-...
LLM_MODEL=gpt-4o

# ─── Conversation State ───────────────────────────────
CACHE_TTL_SECONDS=600              # 10 minutes
BUFFER_MAX_MESSAGES=6
BUFFER_MAX_CHARS=500
BUFFER_TIMEOUT_SECONDS=30
BUFFER_EXPIRATION_SECONDS=300
SUMMARY_TRIGGER_MESSAGES=5
SUMMARY_TRIGGER_CHARS=400
FACTS_TTL_SECONDS=600
```

---

## 4. Docker Compose

### `docker-compose.yml` (production-ready base)

```yaml
version: "3.9"

services:

  # ── API Gateway ────────────────────────────────────────
  api-gateway:
    build:
      context: ./services/api-gateway
      dockerfile: Dockerfile
    ports:
      - "8080:8080"
    environment:
      - APP_ENV=${APP_ENV}
      - API_PORT=${API_PORT}
      - WEBHOOK_SECRET=${WEBHOOK_SECRET}
      - NATS_URL=${NATS_URL}
      - LOG_LEVEL=${LOG_LEVEL}
    depends_on:
      nats:
        condition: service_healthy
    restart: unless-stopped
    networks:
      - crm-net

  # ── Message Worker ─────────────────────────────────────
  message-worker:
    build:
      context: ./services/message-worker
      dockerfile: Dockerfile
    environment:
      - APP_ENV=${APP_ENV}
      - NATS_URL=${NATS_URL}
      - NATS_STREAM_MESSAGES=${NATS_STREAM_MESSAGES}
      - NATS_STREAM_PERSISTENCE=${NATS_STREAM_PERSISTENCE}
      - REDIS_URL=${REDIS_URL}
      - DYNAMODB_REGION=${DYNAMODB_REGION}
      - DYNAMODB_TABLE=${DYNAMODB_TABLE}
      - OPENAI_API_KEY=${OPENAI_API_KEY}
      - LLM_MODEL=${LLM_MODEL}
      - CACHE_TTL_SECONDS=${CACHE_TTL_SECONDS}
      - BUFFER_MAX_MESSAGES=${BUFFER_MAX_MESSAGES}
      - BUFFER_MAX_CHARS=${BUFFER_MAX_CHARS}
      - BUFFER_TIMEOUT_SECONDS=${BUFFER_TIMEOUT_SECONDS}
      - SUMMARY_TRIGGER_MESSAGES=${SUMMARY_TRIGGER_MESSAGES}
      - SUMMARY_TRIGGER_CHARS=${SUMMARY_TRIGGER_CHARS}
      - FACTS_TTL_SECONDS=${FACTS_TTL_SECONDS}
    depends_on:
      nats:
        condition: service_healthy
      redis:
        condition: service_healthy
    restart: unless-stopped
    networks:
      - crm-net

  # ── Persistence Worker ────────────────────────────────
  persistence-worker:
    build:
      context: ./services/persistence-worker
      dockerfile: Dockerfile
    environment:
      - APP_ENV=${APP_ENV}
      - NATS_URL=${NATS_URL}
      - NATS_STREAM_PERSISTENCE=${NATS_STREAM_PERSISTENCE}
      - DYNAMODB_REGION=${DYNAMODB_REGION}
      - DYNAMODB_TABLE=${DYNAMODB_TABLE}
      - AWS_ACCESS_KEY_ID=${AWS_ACCESS_KEY_ID}
      - AWS_SECRET_ACCESS_KEY=${AWS_SECRET_ACCESS_KEY}
      - LOG_LEVEL=${LOG_LEVEL}
    depends_on:
      nats:
        condition: service_healthy
    restart: unless-stopped
    networks:
      - crm-net

  # ── NATS JetStream ────────────────────────────────────
  nats:
    image: nats:2.10-alpine
    command: ["-c", "/etc/nats/nats-server.conf"]
    ports:
      - "4222:4222"
      - "8222:8222"   # monitoring UI
    volumes:
      - ./infra/nats/nats-server.conf:/etc/nats/nats-server.conf:ro
      - nats-data:/data/jetstream
    healthcheck:
      test: ["CMD", "nats-server", "--healthz"]
      interval: 5s
      timeout: 5s
      retries: 5
    restart: unless-stopped
    networks:
      - crm-net

  # ── Redis ─────────────────────────────────────────────
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 3s
      retries: 5
    restart: unless-stopped
    networks:
      - crm-net

networks:
  crm-net:
    driver: bridge

volumes:
  nats-data:
  redis-data:
```

### `docker-compose.dev.yml` (local overrides + DynamoDB Local)

```yaml
version: "3.9"

services:

  api-gateway:
    build:
      target: development
    volumes:
      - ./services/api-gateway:/app   # hot-reload

  message-worker:
    build:
      target: development
    volumes:
      - ./services/message-worker:/app

  persistence-worker:
    build:
      target: development
    environment:
      - DYNAMODB_ENDPOINT=${DYNAMODB_ENDPOINT}
    volumes:
      - ./services/persistence-worker:/app

  dynamodb-local:
    image: amazon/dynamodb-local:latest
    ports:
      - "8000:8000"
    command: ["-jar", "DynamoDBLocal.jar", "-sharedDb", "-inMemory"]
    networks:
      - crm-net
```

Run locally with both files merged:

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build
```

---

## 5. Individual Dockerfiles

### API Gateway

```dockerfile
# services/api-gateway/Dockerfile
FROM node:20-alpine AS base
WORKDIR /app
COPY package*.json ./
RUN npm ci --omit=dev

FROM base AS development
RUN npm ci
COPY . .
CMD ["npm", "run", "dev"]

FROM base AS production
COPY . .
CMD ["node", "src/index.js"]
```

### Message Worker

```dockerfile
# services/message-worker/Dockerfile
FROM node:20-alpine AS base
WORKDIR /app
COPY package*.json ./
RUN npm ci --omit=dev

FROM base AS development
RUN npm ci
COPY . .
CMD ["npm", "run", "dev"]

FROM base AS production
COPY . .
CMD ["node", "src/worker.js"]
```

### Persistence Worker

```dockerfile
# services/persistence-worker/Dockerfile
FROM node:20-alpine AS base
WORKDIR /app
COPY package*.json ./
RUN npm ci --omit=dev

FROM base AS development
RUN npm ci
COPY . .
CMD ["npm", "run", "dev"]

FROM base AS production
COPY . .
CMD ["node", "src/worker.js"]
```

---

## 6. Networking

All services join a single bridge network `crm-net`. Services communicate by **service name** (Docker internal DNS).

| From                  | To                    | Port   | Protocol |
|-----------------------|-----------------------|--------|----------|
| `api-gateway`         | `nats`                | `4222` | TCP      |
| `message-worker`      | `nats`                | `4222` | TCP      |
| `message-worker`      | `redis`               | `6379` | TCP      |
| `message-worker`      | `dynamodb-local`      | `8000` | HTTP     |
| `persistence-worker`  | `nats`                | `4222` | TCP      |
| `persistence-worker`  | `dynamodb-local`      | `8000` | HTTP     |

The webhook entry point `api-gateway:8080` is the **only port exposed to the host** in production.

---

## 7. Volumes & Persistence

| Volume       | Used by    | Contents                              |
|--------------|------------|---------------------------------------|
| `nats-data`  | `nats`     | JetStream message store               |
| `redis-data` | `redis`    | Conversation state cache snapshots    |

> **Note:** In local development with `dynamodb-local -inMemory`, DynamoDB data is lost on container restart. Remove `-inMemory` and mount a volume if you need persistence locally.

---

## 8. Health Checks

| Service    | Check command              | Interval | Retries |
|------------|----------------------------|----------|---------|
| `nats`     | `nats-server --healthz`    | 5 s      | 5       |
| `redis`    | `redis-cli ping`           | 5 s      | 5       |

Worker services use `depends_on` with `condition: service_healthy` so they only start after NATS and Redis pass their health checks.

---

## 9. Local Development

### Startup Order

> **Important**: The following must be done in order on first run:
>
> 1. Start infrastructure services first (`nats`, `redis`, `dynamodb-local`)
> 2. Bootstrap the DynamoDB table (see below)
> 3. Create NATS JetStream streams (see [QUEUE.md §11](./QUEUE.md#11-local-development))
> 4. Start workers (`message-worker`, `persistence-worker`) and `api-gateway`

### Starting everything

```bash
# First-time setup
cp .env.example .env

# Build and start all services
docker compose -f docker-compose.yml -f docker-compose.dev.yml up --build

# Start in background
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --build
```

### Bootstrap DynamoDB table (local)

```bash
./infra/dynamodb/init.sh
```

Example `init.sh`:

```bash
#!/usr/bin/env bash
set -e
AWS_ACCESS_KEY_ID=local \
AWS_SECRET_ACCESS_KEY=local \
aws dynamodb create-table \
  --endpoint-url http://localhost:8000 \
  --region us-east-1 \
  --table-name crm_memory \
  --attribute-definitions \
      AttributeName=PK,AttributeType=S \
      AttributeName=SK,AttributeType=S \
  --key-schema \
      AttributeName=PK,KeyType=HASH \
      AttributeName=SK,KeyType=RANGE \
  --billing-mode PAY_PER_REQUEST
```

### Useful commands

```bash
# View logs for a specific service
docker compose logs -f message-worker

# Restart a single service
docker compose restart persistence-worker

# Open a shell in a running container
docker compose exec api-gateway sh

# Stop and remove containers (keep volumes)
docker compose down

# Stop and remove containers AND volumes
docker compose down -v
```

---

## 10. Production Considerations

| Concern             | Recommendation                                                                 |
|---------------------|--------------------------------------------------------------------------------|
| **DynamoDB**        | Remove `dynamodb-local`; use AWS DynamoDB with IAM roles (not hardcoded keys)  |
| **Secrets**         | Use AWS Secrets Manager or Docker Secrets — never commit `.env` to git         |
| **Scaling workers** | Deploy `message-worker` and `persistence-worker` as multiple replicas behind NATS queue groups |
| **NATS clustering** | Use a NATS cluster with at least 3 nodes for high availability                 |
| **Redis HA**        | Use Redis Sentinel or ElastiCache for managed failover                         |
| **TLS**             | Terminate TLS at the load balancer or use Let's Encrypt with a reverse proxy (e.g. Traefik) |
| **Logging**         | Ship container logs to a centralized system (CloudWatch, Datadog, etc.)        |
| **Resource limits** | Set `mem_limit` and `cpus` on each service in `docker-compose.yml`             |
