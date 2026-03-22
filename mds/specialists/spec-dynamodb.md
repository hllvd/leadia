# DYNAMODB BEST PRACTICES

Design:
- Design by access patterns FIRST
- Single-table preferred

Keys:
- PK = entity or aggregate
- SK = hierarchy or time

Indexes:
- Use GSIs for alternative access
- Avoid over-indexing

Writes:
- Always idempotent
- Use conditional writes when needed

Queries:
- Query > Scan ALWAYS

Events:
- Store immutable events
- Derive views asynchronously

Hot Partitions:
- Distribute keys properly

Naming:
- Use predictable prefixes (USER#, ORDER#, MSG#) 