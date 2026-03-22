# REACT FRONTEND BEST PRACTICES

State:
- Server state ≠ UI state
- Use React Query / SWR for server state

Components:
- Small, pure, composable
- No logic in JSX

Data Flow:
- Top-down only
- Avoid prop drilling → use context selectively

Side Effects:
- Isolate in hooks
- Never mix fetching + rendering logic

Forms:
- Use controlled inputs
- Validate at boundaries

Performance:
- Avoid unnecessary re-renders
- Memo only when needed

UX:
- Always handle loading, error, empty states