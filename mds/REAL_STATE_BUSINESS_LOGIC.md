# Real Estate Business Logic — LeadIa

This document describes the business logic used to detect conversational signals and manage tasks within the system.

## 1. Conversational Task Management

Tasks are actionable items derived from conversation signals. They serve as the source of truth for the conversation state and the broker's to-do list.

### 1.1 Question Tasks (`type: "question"`)

*   **Trigger**: A question is detected in the latest message (`has_new_question: true`).
*   **Behavior**:
    *   **Customer Question**: If the customer asks, the task is assigned to the broker (`owner: "broker"`) with status `open`. The verbatim question is stored in `metadata.user_question`.
    *   **Broker Question**: If the broker asks, the task is assigned to the customer (`owner: "customer"`) with status `open`. This represents that the broker is waiting for a response.
*   **Completion**: When all questions in recent messages are answered (`has_unanswered_question: false`), the status changes to `completed`.

### 1.2 Follow-up Tasks (`type: "followup"`)

*   **Trigger**: The broker commits to a future action or a request remains unfulfilled.
*   **Status**: Defaults to `pending` when active.
*   **Example Conversation**:
    > **Customer**: "Gostei muito do apartamento, mas preciso saber se o condomínio aceita pets."
    >
    > **Broker**: "**Vou verificar essa informação com a administração do prédio e te aviso ainda hoje.**"
*   **Signal**: The phrase *"Vou verificar... e te aviso"* triggers `needs_followup: true`.

### 1.3 Visit Tasks (`type: "visit"`)

*   **Trigger**: A property visit is suggested or discussed.
*   **Signals**: `visit_suggested`, `has_pending_visit`, `visit_confirmed`.
*   **Metadata**: Stores `proposed_date` and `proposed_time`.

### 1.4 Documents Task (`type: "documents"`)

*   **Trigger**: Documents or financial information are requested or discussed.
*   **Signal**: `has_pending_documents`.
*   **Metadata**: Includes a description of the requested items.

## 2. Signal Detection Rules

Signals are deterministic and based on explicit conversation flow, extracted by the LLM using the `analysis_system.md` prompt.

| Signal | Description |
|--------|-------------|
| `has_new_question` | Set to true if the latest message contains a clear question. |
| `needs_followup` | Set to true if someone promises a future action or a request remains unanswered. |
| `customer_engaged` | Set to true if the customer is actively interacting. |
| `customer_unresponsive` | Set to true if the customer fails to reply to a message that required a response. |
