# Rolling Summary Generation Tasks

## Dependencies
- **Requires:** tasks-06 (buffer thresholds trigger summary)
- **Requires:** tasks-09 (LLM context construction)
- **Required by:** tasks-31 (core intelligence integration - LLM integration flow)
- **Related:** tasks-07 (facts and summary updated together)
- **Related:** tasks-11 (persistence of summary)

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
10. ~~Ensure summaries preserve key context while compressing history.~~