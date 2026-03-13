# Message Worker Implementation Tasks

## Dependencies
- **Requires:** tasks-02 (message normalization)
- **Requires:** tasks-03 (deduplication logic)
- **Requires:** tasks-04 (user classification)
- **Requires:** tasks-05 (cache access)
- **Requires:** tasks-06 (buffer management)
- **Requires:** tasks-07 (fact extraction)
- **Requires:** tasks-08 (summary generation)
- **Requires:** tasks-09 (LLM context construction)
- **Requires:** tasks-13 (NATS configuration)
- **Required by:** tasks-31 (core intelligence integration - orchestrates all components)
- **Related:** tasks-20 (worker enhancements)

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
12. Monitor worker performance and throughput.