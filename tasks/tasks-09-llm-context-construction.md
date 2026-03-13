# LLM Context Construction Tasks

## Dependencies
- **Requires:** tasks-05 (cache for conversation state)
- **Requires:** tasks-06 (buffer contents)
- **Required by:** tasks-07 (fact extraction needs context)
- **Required by:** tasks-08 (summary generation needs context)
- **Required by:** tasks-31 (core intelligence integration - LLM context assembly)

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
10. Optimize context for LLM prompt effectiveness.