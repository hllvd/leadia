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

*   **Status**: `pending` when active, `completed` when all signals are resolved.
*   **Owner**: Always `broker` — they are responsible for keeping the conversation moving.
*   **Trigger**: Active when ANY of the following signals is true:
    *   `has_unanswered_question: true` — a question was asked with no reply
    *   `has_pending_visit: true` — a visit was suggested but not confirmed
    *   `has_pending_call: true` — a call or meeting was suggested but not confirmed
    *   `has_pending_documents: true` — documents were requested but not sent
    *   `needs_followup: true` — an explicit commitment was made to respond later
*   **Completion**: Automatically set to `completed` once all of the above signals are cleared (questions answered, visit confirmed/cancelled, call confirmed/cancelled, documents delivered).

*   **Example Conversation that triggers follow-up**:
    > **Customer**: "Gostei muito do apartamento no Leblon, mas preciso saber se o condomínio aceita pets."
    >
    > *(Broker has not replied yet → `has_unanswered_question: true` → follow-up: `pending`)*

    > **Broker**: "Boa pergunta! Vou verificar e te aviso."
    >
    > *(Broker promised but hasn't confirmed → `needs_followup: true` → follow-up remains `pending`)*

    > **Broker**: "Confirmado! O condomínio aceita pets de até 15kg."
    >
    > *(Question resolved → all signals false → follow-up: `completed`)*

### 1.3 Visit Tasks (`type: "visit"`)

*   **Trigger**: A property visit is suggested or discussed.
*   **Signals**: `visit_suggested`, `has_pending_visit`, `visit_confirmed`.
*   **Metadata**: Stores `proposed_date` and `proposed_time`.

### 1.4 Documents Task (`type: "documents"`)

*   **Trigger**: Documents or financial information are requested or discussed.
*   **Signal**: `has_pending_documents`.
*   **Metadata**: Includes a description of the requested items.

### 1.5 Call / Meeting Tasks (`type: "call"`)

*   **Trigger**: A phone call, video call, or in-person meeting is proposed.
*   **Signals**: `call_suggested`, `has_pending_call`, `call_confirmed`.
*   **Metadata**: Stores `proposed_date`, `proposed_time`, and `type` (`"phone"`, `"video"`, or `"in-person"`).
*   **Flow**:
    *   `open`: When a call is suggested or remains pending (`call_suggested || has_pending_call`).
    *   `completed`: When both parties agree on the call (`call_confirmed`).

## 2. Signal Detection Rules

Signals are deterministic and based on explicit conversation flow, extracted by the LLM using the `analysis_system.md` prompt.

| Signal | Description |
|--------|-------------|
| `has_new_question` | Set to true if the latest message contains a clear question. |
| `needs_followup` | Set to true if someone promises a future action or a request remains unanswered. |
| `customer_engaged` | Set to true if the customer is actively interacting. |
| `customer_unresponsive` | Set to true if the customer fails to reply to a message that required a response. |
