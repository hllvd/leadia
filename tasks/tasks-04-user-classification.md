# User Classification Tasks

## Small Tasks

1. ~~Check if conversation_id exists in the local cache or DynamoDB.~~
2. ~~If conversation_id not found, classify as new_lead.~~
3. ~~Initialize ConversationState for new leads: empty rolling_summary, empty facts object, empty buffer.~~
4. ~~Set initial last_message_hash and timestamps.~~
5. ~~If conversation_id exists, classify as existing_lead and load existing state.~~
6. ~~Update last_activity_timestamp on every message for existing conversations.~~
7. Handle race conditions when multiple messages arrive for the same new conversation simultaneously.
8. ~~Test classification logic with new and existing conversation IDs.~~
9. ~~Ensure new lead initialization includes all required fields.~~