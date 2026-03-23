# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]
### Added
- Implemented a robust 10-minute message flush mechanism using NATS Key-Value (KV) store for tracking session expirations securely and handling concurrent flush events.
- **Signals & Context Support**: Replaced legacy events with a deterministic `LlmSignals` and `LlmContext` matrix mapping deep conversational state. 
- **Tasks Engine**: Implemented `ConversationTask` system. Tasks (Question, Visit, Documents, Follow-up) are now deterministically upserted into DynamoDB (`SK=TASK#{Type}`) preventing duplicates.
- **NATS Notifications**: Added `PublishNotificationAsync` to fire `persist.notification.task_state` and `persist.notification.unresponsive` events to decouple the LLM worker from API listeners.
- **Raw Signals Snapshot**: Added functionality to persist the raw JSON output of intelligence signals under a single DynamoDB key (`SK=SIGNALS`).

### Removed
- **Events System**: Deprecated and completely removed the old `ConversationEvent` domain models, mapping logic, tests, and persistence repository methods.
