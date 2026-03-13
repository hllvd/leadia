# Message Buffer Management Tasks

## Dependencies
- **Requires:** tasks-05 (cache to store buffer state)
- **Required by:** tasks-08 (rolling summary triggered by buffer thresholds)
- **Required by:** tasks-31 (core intelligence integration - buffer management)
- **Related:** tasks-09 (LLM context includes buffer contents)

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
11. ~~Ensure buffer is persisted in cache and recovered on cache miss.~~