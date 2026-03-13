# Task Dependency Map

This document visualizes the dependencies between all task files, showing which tasks must be completed before others and how they integrate together.

---

## Core Flow (Critical Path)

```
tasks-01 (Webhook Security)
    ↓
tasks-02 (Message Ingestion & Normalization)
    ↓
tasks-13 (NATS Configuration) ←──────────────┐
    ↓                                         │
tasks-03 (Deduplication) ──→ tasks-31 (Integration Glue)
    ↓                              ↓
tasks-04 (User Classification)     │
    ↓                              │
tasks-05 (Cache) ←─────────────────┤
    ↓                              │
tasks-06 (Buffer Management)       │
    ↓                              │
tasks-09 (LLM Context) ────────────┤
    ↓                              │
tasks-07 (Fact Extraction) ────────┤
tasks-08 (Summary Generation) ─────┤
    ↓                              │
tasks-10 (Message Worker) ←────────┘
    ↓
tasks-11 (Persistence Worker)
    ↓
tasks-12 (DynamoDB)
```

---

## Dependency Groups

### Group 1: Foundation (Must Complete First)
- **tasks-01** - Webhook Security
- **tasks-12** - DynamoDB Setup
- **tasks-13** - NATS Configuration
- **tasks-14** - Docker Containerization

### Group 2: Message Processing Pipeline
- **tasks-02** - Message Ingestion (requires: 01, 13)
- **tasks-03** - Deduplication (requires: 02)
- **tasks-04** - User Classification (requires: 03, 05, 12)

### Group 3: State Management
- **tasks-05** - Cache (required by: 04, 06, 09, 10, 22, 31)
- **tasks-06** - Buffer Management (requires: 05; required by: 08, 09, 31)

### Group 4: AI/LLM Intelligence
- **tasks-09** - LLM Context (requires: 05, 06; required by: 07, 08, 31)
- **tasks-07** - Fact Extraction (requires: 09; required by: 31)
- **tasks-08** - Summary Generation (requires: 06, 09; required by: 31)

### Group 5: Worker Services
- **tasks-10** - Message Worker (requires: 02-09, 13; required by: 31)
- **tasks-11** - Persistence Worker (requires: 12, 13; required by: 31)

### Group 6: Integration Layer (The "Glue")
- **tasks-31** - Core Intelligence Integration (integrates: 03-12)
  - Consolidates cache-aside pattern
  - Orchestrates deduplication flow
  - Manages LLM integration pipeline
  - Coordinates event publishing
  - Implements persistence handlers

### Group 7: Enhancements & Optimization
- **tasks-20** - Message Worker Enhancements (requires: 10; integrates with: 31)
- **tasks-21** - Persistence Worker Enhancements (requires: 11; integrates with: 31)
- **tasks-26** - Data Flow Optimization (requires: 02, 05, 10, 11, 31)

### Group 8: Infrastructure & Scaling
- **tasks-17** - NATS Stream Management (extends: 13)
- **tasks-18** - NATS Consumer Configuration (extends: 13)
- **tasks-19** - NATS Monitoring (extends: 13)
- **tasks-22** - Horizontal Scaling (requires: 05, 10, 11, 13)
- **tasks-30** - NATS Clustering (extends: 13)

### Group 9: Data Persistence & Compliance
- **tasks-23** - DynamoDB TTL (requires: 12; required by: 11)
- **tasks-24** - DynamoDB Operational Excellence (extends: 12)
- **tasks-25** - DynamoDB GSI (extends: 12)
- **tasks-27** - Data Residency & Compliance (requires: 23)

### Group 10: Error Handling & Validation
- **tasks-28** - Event Schema Validation (requires: 02, 10; required by: 11, 31)
- **tasks-29** - Dead-Letter Queue (requires: 13, 17; required by: 20, 21)

### Group 11: Testing & Quality
- **tasks-15** - Testing & Validation (tests all above tasks)

### Group 12: Future Development
- **tasks-16** - Future CRM Integration (independent)

---

## Critical Dependencies for tasks-31 (Integration Glue)

The integration task **tasks-31** is the central orchestrator that requires:

### Input Dependencies:
1. **tasks-03** - Deduplication logic
2. **tasks-04** - User classification
3. **tasks-05** - Cache-aside pattern
4. **tasks-06** - Buffer management
5. **tasks-07** - Fact extraction
6. **tasks-08** - Summary generation
7. **tasks-09** - LLM context construction
8. **tasks-10** - Message worker orchestration
9. **tasks-11** - Persistence handlers
10. **tasks-12** - DynamoDB patterns

### Integration Points:
- **Cache-aside pattern**: Implements the flow from tasks-05
- **Deduplication flow**: Integrates tasks-03 into worker pipeline
- **LLM integration**: Combines tasks-07, 08, 09 into cohesive flow
- **Event publishing**: Orchestrates tasks-10 event emission
- **Persistence handlers**: Implements tasks-11 write patterns

---

## Implementation Order Recommendation

### Phase 1: Foundation (Week 1-2)
1. tasks-01, 12, 13, 14
2. tasks-02
3. tasks-05 (basic implementation)

### Phase 2: Core Processing (Week 3-4)
4. tasks-03, 04
5. tasks-06
6. tasks-09
7. tasks-07, 08

### Phase 3: Workers (Week 5-6)
8. tasks-10 (basic implementation)
9. tasks-11 (basic implementation)
10. **tasks-31** (Integration - THE GLUE)

### Phase 4: Testing & Validation (Week 7)
11. tasks-15 (comprehensive testing)

### Phase 5: Production Readiness (Week 8-10)
12. tasks-17, 18, 19 (NATS advanced)
13. tasks-20, 21 (Worker enhancements)
14. tasks-23, 24 (DynamoDB operational)
15. tasks-28, 29 (Validation & DLQ)

### Phase 6: Scaling & Optimization (Week 11-12)
16. tasks-22 (Horizontal scaling)
17. tasks-26 (Performance optimization)
18. tasks-27 (Compliance)
19. tasks-30 (NATS clustering)

### Phase 7: Advanced Features (Future)
20. tasks-25 (GSI)
21. tasks-16 (CRM integration)

---

## Quick Reference: "What Requires What?"

| If you're working on... | You need these completed first... |
|------------------------|-----------------------------------|
| tasks-02 | tasks-01, 13 |
| tasks-03 | tasks-02 |
| tasks-04 | tasks-03, 05, 12 |
| tasks-06 | tasks-05 |
| tasks-07 | tasks-09 |
| tasks-08 | tasks-06, 09 |
| tasks-09 | tasks-05, 06 |
| tasks-10 | tasks-02, 03, 04, 05, 06, 07, 08, 09, 13 |
| tasks-11 | tasks-12, 13 |
| tasks-20 | tasks-10 |
| tasks-21 | tasks-11 |
| tasks-22 | tasks-05, 10, 11, 13 |
| tasks-23 | tasks-12 |
| tasks-26 | tasks-02, 05, 10, 11, 31 |
| tasks-28 | tasks-02, 10 |
| tasks-29 | tasks-13, 17 |
| **tasks-31** | **tasks-03, 04, 05, 06, 07, 08, 09, 10, 11, 12** |

---

## The "Glue" Concept

**tasks-31** acts as the integration layer that:
- ✅ Takes scattered implementations from tasks-03 through tasks-12
- ✅ Combines them into cohesive end-to-end flows
- ✅ Provides implementation patterns (cache-aside, event orchestration)
- ✅ Ensures all components work together seamlessly
- ✅ Adds distributed tracing across the entire pipeline

**Without tasks-31**, you have individual components but no clear integration path.
**With tasks-31**, you have a complete, working system.
