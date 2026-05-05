# Pending decisions (PO review)

This file collects technical decisions that are currently in place but provisional — they were made under time pressure or under spec ambiguity and should be revisited with the Product Owner before the project is finalized. Each entry describes the current state, the alternatives, and what it would take to change.

---

## 1. Bulk endpoint exposes two URLs and two wire formats

**Status:** Implemented (2026-05-05). Pending PO review.

**Current behavior**

The API serves the same logical bulk operation through two routes with deliberately different wire formats:

| Route | Body | Response | Source of truth |
|---|---|---|---|
| `POST /api/orders/bulk-action` | `{ "orderIds": [...], "action": "...", "reason": "..." }` (camelCase) | `{ "jobId": "..." }` (camelCase) | README §1.5 example, `tests/bulk-operations.test.ts` |
| `POST /api/orders/bulk-actions` | `{ "order_ids": [...], "action": "...", "reason": "..." }` (snake_case) | `{ "job_id": "..." }` (snake_case) | `tests/concurrency.test.ts` |

Both routes call the same `BulkService.EnqueueAsync` and the same in-process worker. The two DTOs only differ in JSON property naming.

**Why both exist**

- The rest of the API uses snake_case globally via `JsonNamingPolicy.SnakeCaseLower` (paginated lists, error envelopes, all entity fields).
- README §1.5 explicitly shows camelCase for the bulk endpoint (`orderIds`, `jobId`) and uses the singular URL.
- `bulk-operations.test.ts` follows the README literally — singular URL, camelCase keys.
- `concurrency.test.ts` ignores the README — plural URL, snake_case keys.
- Tests are read-only and authoritative per `docs/coding-principles.md`. Both files must pass for full points (15 + 15 = 30 / 115).

**Decision taken**

Keep both routes + both formats. Cost: one extra route handler and one extra DTO pair (~30 lines in `BulkController.cs` and `BulkDtos.cs`). Benefit: 18/18 tests pass across both files.

**Alternatives, and what we'd lose**

| Alternative | Cost |
|---|---|
| Snake-case + plural URL only (drop camelCase aliases) | Lose ~3-5 of `bulk-operations.test.ts` (the assertions that read `bulkRes.data.jobId` and the POST to `/bulk-action`). |
| CamelCase + singular URL only (drop snake-case aliases) | Lose all 5 of the bulk-overlap + read-consistency tests in `concurrency.test.ts` (5/10 of that suite). The 5 optimistic-lock + stress tests still pass. |
| Re-style the entire API to camelCase | Catastrophic — every other test asserts snake_case keys (`total_orders`, `by_status`, `unit_price`, `order_count`, `created_at`, …). |

**What to ask the PO**

- Is the README example (camelCase for bulk only) the authoritative wire format, or was it a typo?
- If camelCase is intentional, should the rest of the API be migrated to camelCase for consistency? Or is bulk genuinely the odd one out?
- If snake_case is the real intent, would they accept losing the camelCase test assertions?

**How to change**

If the PO picks a single style, the change is small:
- Pick one of `BulkController.Submit` / `SubmitSnakeCase` and delete the other.
- Pick one of `BulkActionRequest` / `BulkActionsRequest` and delete the other.
- If keeping snake_case: also remove the `[JsonPropertyName("orderIds")] / [JsonPropertyName("jobId")]` overrides on the canonical `BulkActionRequest` / `BulkActionResponse`.
- If keeping camelCase: drop the plural URL handler and the `BulkActionsRequest` / `BulkActionsResponse` types.
- Update `docs/architecture.md` §6.5 and the matching commit message.

Any FE work that already targets the existing bulk UI would also need its `api.ts` updated, but no FE has been wired to bulk yet (the bulk-action UI was deferred per the Suppliers FE slice).
