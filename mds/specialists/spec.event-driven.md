# EVENT-DRIVEN ARCHITECTURE

Events:
- Immutable
- Append-only

Design:
- Events describe facts, not commands

Consumers:
- Must be idempotent

Coupling:
- Producers don’t know consumers

Storage:
- Event store = source of truth

Replay:
- System must support replay

Schema:
- Version events