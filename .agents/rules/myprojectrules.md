---
trigger: always_on
---

.NET Rules (Strict)

1. Separation: Decouple business logic, I/O, and infrastructure. Keep endpoints thin.
2. Functional Core: Prefer pure, deterministic functions and immutability. Test every pure function (≥1).
3. Async: Use async/await for I/O. Never block. Propagate CancellationToken.
4. Data: Avoid N+1 and over-fetching. Prefer projections; use AsNoTracking for reads.
5. DI & HTTP: Use built-in DI with correct lifetimes. Use IHttpClientFactory (no new HttpClient).
6. Explicitness: Use clear names. Validate inputs and handle errors—no silent failures.
7. Errors & Validation: No exceptions for control flow. Use centralized handling and explicit validation.
8. Platform: Use UTC time, System.Text.Json (explicit contracts), structured logging, and typed config.
9. Runtime Safety: No fire-and-forget tasks. Ensure correct middleware order. Avoid unnecessary allocations/enumerations.
10. Simplicity & Docs: Optimize for clarity. Apply DRY, avoid premature abstraction. Document "why" (non-obvious logic). Output production-ready code with tests.

