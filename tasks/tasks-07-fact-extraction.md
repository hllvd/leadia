# Fact Extraction Tasks

## Dependencies
- **Requires:** tasks-09 (LLM context construction)
- **Required by:** tasks-31 (core intelligence integration - LLM response parsing)
- **Related:** tasks-08 (facts and summary updated together)
- **Related:** tasks-11 (persistence of extracted facts)

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
11. ~~Ensure facts are included in LLM context construction.~~