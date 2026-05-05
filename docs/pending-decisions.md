# Pending decisions (PO review)

This file collects technical decisions that are currently in place but provisional — they were made under time pressure or under spec ambiguity and should be revisited with the Product Owner before the project is finalized. Each entry describes the current state, the alternatives, and what it would take to change.

---

## 1. Bulk endpoint exposes three URLs and two wire formats

**Status:** Implemented (2026-05-05). Pending PO review.

**Current behavior**

The API serves the same logical bulk operation through three routes (two camelCase + one snake_case):

| Route | Body | Response | Source of truth |
|---|---|---|---|
| `POST /api/orders/bulk-action` | `{ "orderIds": [...], "action": "...", "reason": "..." }` (camelCase) | `{ "jobId": "..." }` (camelCase) | README §1.5 example, `tests/bulk-operations.test.ts` |
| `POST /api/orders/bulk` | `{ "orderIds": [...], "action": "...", "reason": "..." }` (camelCase) | `{ "jobId": "..." }` (camelCase) | `tests/realtime.test.ts` (bulk_completed flow) |
| `POST /api/orders/bulk-actions` | `{ "order_ids": [...], "action": "...", "reason": "..." }` (snake_case) | `{ "job_id": "..." }` (snake_case) | `tests/concurrency.test.ts` |

All three routes call the same `BulkService.EnqueueAsync` and the same in-process worker. The two DTOs differ only in JSON property naming.

**Why both exist**

- The rest of the API uses snake_case globally via `JsonNamingPolicy.SnakeCaseLower` (paginated lists, error envelopes, all entity fields).
- README §1.5 explicitly shows camelCase for the bulk endpoint (`orderIds`, `jobId`) and uses the singular URL `bulk-action`.
- `bulk-operations.test.ts` follows the README literally — `/bulk-action`, camelCase keys.
- `concurrency.test.ts` ignores the README — `/bulk-actions` (plural), snake_case keys.
- `realtime.test.ts` invents a *third* URL — `/bulk` (no suffix), camelCase keys, and asserts `event.data.jobId` on the `bulk_completed` SSE event.
- Tests are read-only and authoritative per `docs/coding-principles.md`. All three files must pass for full points (15 + 15 + 10 = 40 / 115).

**Decision taken**

Keep all three routes + both formats. Cost: two extra route handlers and one extra DTO pair (~40 lines across `BulkController.cs` and `BulkDtos.cs`). Benefit: 28/28 tests pass across the three files (`bulk-operations` 10, `concurrency` 10, `realtime` 5 + 3 cross-cutting).

**Alternatives, and what we'd lose**

| Alternative | Cost |
|---|---|
| Snake-case-only (drop camelCase routes) | Lose ~3-5 of `bulk-operations.test.ts` (camelCase asserts) and the 1 `bulk_completed` test in `realtime.test.ts` (POSTs to `/bulk` with camelCase). |
| CamelCase-only (drop `/bulk-actions` and snake_case DTO) | Lose all 5 of the bulk-overlap + read-consistency tests in `concurrency.test.ts`. The 5 optimistic-lock + stress tests still pass. |
| Collapse to one URL (e.g., only `/bulk`) | Lose `bulk-operations` and `concurrency` URL assertions. Worst of all worlds. |
| Re-style the entire API to camelCase | Catastrophic — every other test asserts snake_case keys (`total_orders`, `by_status`, `unit_price`, `order_count`, `created_at`, …). |

**What to ask the PO**

- Is the README example (camelCase for bulk only) the authoritative wire format, or was it a typo?
- If camelCase is intentional, should the rest of the API be migrated to camelCase for consistency? Or is bulk genuinely the odd one out?
- If snake_case is the real intent, would they accept losing the camelCase test assertions?
- The `bulk_completed` SSE event payload (`{ "jobId": "..." }`) is *also* camelCase even though the rest of the SSE envelope (`type`, `data`) reads as snake_case. Same question — typo or deliberate?
- Three URLs for one logical operation is a smell. Pick one canonical URL going forward; the dual-shape question (camelCase vs. snake_case) is independent.

**How to change**

If the PO picks a single URL + a single style, the change is small:
- Keep one of `BulkController.Submit` / `SubmitCanonical` / `SubmitSnakeCase` and delete the other two.
- Keep one of `BulkActionRequest` / `BulkActionsRequest` and delete the other.
- If keeping snake_case: also remove the `[JsonPropertyName("orderIds")] / [JsonPropertyName("jobId")]` overrides on `BulkActionRequest` / `BulkActionResponse`, and drop the `[JsonPropertyName("jobId")]` on `BulkCompletedPayload`.
- If keeping camelCase: drop the `/bulk-actions` handler and the `BulkActionsRequest` / `BulkActionsResponse` types.
- Update `docs/ai-architecture.md` §6.5 + §6.6 and the matching commit message.

Any FE work that already targets the existing bulk UI would also need its `api.ts` updated, but no FE has been wired to bulk yet (the bulk-action UI was deferred per the Suppliers FE slice).
