# Message Deduplication Tasks

## Small Tasks

1. ~~Generate message_hash using SHA256(timestamp + broker_id + customer_id + text).~~
2. ~~Check if the generated hash matches the last_message_hash stored in the conversation state.~~
3. ~~If hash matches, silently ignore the message and return 200 to webhook provider.~~
4. ~~If hash does not match, proceed with processing and update last_message_hash.~~
5. ~~Store the hash in the conversation state after successful processing.~~
6. Handle hash collisions gracefully (though unlikely with SHA256).
7. ~~Test deduplication with duplicate messages.~~
8. ~~Ensure deduplication works across restarts by persisting last_message_hash in DynamoDB.~~