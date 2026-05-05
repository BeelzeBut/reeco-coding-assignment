# Architecture

A procurement order-management dashboard: a .NET 9 / ASP.NET Core REST API on port 3000 backed by Postgres 16 and Redis 7, with a Vite + React + TypeScript SPA consuming it. Fifty thousand orders, five hundred suppliers, five thousand products, and 195 categories are seeded from CSV by a separate console importer. The contract is locked by the test suite (`tests/`, 83 tests / 115 pts); when README and tests disagreed, the tests won.

This document is the deliverable for human reviewers and is intentionally narrative. The full anomaly-rule and severity rationale lives in [`docs/ANOMALY_STRATEGY.md`](./ANOMALY_STRATEGY.md). Security threat model and control inventory are in [`docs/SECURITY.md`](./SECURITY.md). The AI working context (decision log, daily checkpoints) is in [`docs/ai-architecture.md`](./ai-architecture.md). Provisional decisions awaiting PO review are in [`docs/pending-decisions.md`](./pending-decisions.md). The full Claude Code session history that produced this codebase is preserved as JSONL under [`docs/transcripts/`](./transcripts/) — see §12.

---

## 1. Project structure

```
src/
├── OrderOps.slnx          ← solution
├── OrderOps.Api/          ← Web API (port 3000)
│   ├── Program.cs
│   ├── Features/          ← group-by-feature
│   │   ├── Orders/        ← controller + service + repository + DTOs
│   │   ├── Suppliers/
│   │   ├── Products/
│   │   ├── Bulk/          ← bulk endpoint, BackgroundService worker, job state
│   │   └── Events/        ← SSE controller + EventHub
│   ├── Data/              ← AppDbContext + entity configs (EF Core)
│   ├── Infrastructure/    ← exception handler, security headers, validation
│   └── schema.sql         ← canonical DDL — owned here, applied by Importer
├── OrderOps.Importer/     ← console: schema apply + CSV COPY
└── OrderOps.Web/          ← Vite + React + TS + Tailwind + shadcn/ui SPA
    └── src/
        ├── api/           ← fetch wrapper + ApiError
        ├── lib/           ← cn, queryClient, shared hooks
        ├── components/ui/ ← shadcn primitives (hand-rolled, no Radix)
        └── features/      ← orders/, stats/, suppliers/
```

Three project boundaries deliberately:

- **Api** owns HTTP, business logic, and data access. SOLID is enforced *within* by feature folders rather than by csproj sprawl. Controllers are thin (`OrdersController`, `BulkController`, `EventsController`); services own business rules; repositories own SQL; DTOs are mapped explicitly so entities never leak to JSON.
- **Importer** is a console host so reviewers can run `dotnet run --project src/OrderOps.Importer` once and see a deliberate, auditable ingestion — not a hidden side effect of the API booting.
- **Web** is a Vite SPA mirrored by feature folders for the four routes (`/orders`, `/stats`, `/suppliers`, `/suppliers/:id`). It uses TanStack Query for server state, Recharts for analytics, React Router for navigation, Tailwind v3 + shadcn/ui for design, and a tri-state theme toggle (light/dark/system) persisted to `localStorage`.

`schema.sql` lives under `OrderOps.Api/` because the API is the canonical owner of the data shape; the Importer's csproj links it via `<None Include="..\OrderOps.Api\schema.sql" Link="schema.sql" CopyToOutputDirectory="Always" />` so it ships next to the importer binary at runtime. The API never reads the file — it consumes the resulting tables.

---

## 2. Database schema

```sql
categories  (id PK, name, parent_id self-FK DEFERRABLE INITIALLY DEFERRED)
suppliers   (id PK, name, email, rating, country, active, created_at)
products    (id PK, name, category_id FK→categories, sku, price)
orders      (id PK, supplier_id FK, product_id FK, quantity, unit_price,
             total_price, status, priority, created_at, updated_at,
             warehouse, notes, version int default 1)
jobs        (id PK, status, total, completed, failed, action, created_at,
             finished_at)
order_flags (order_id PK FK→orders, flagged_at, source_job_id FK→jobs, reason)
```

A few non-obvious choices:

- **`orders.version`** is a server-internal optimistic-lock token. It is never accepted as PATCH input and never appears in API responses. EF Core maps it as `IsConcurrencyToken()`, and the generated UPDATE includes `WHERE id=@p AND version=@p_orig`. Zero-row updates throw `DbUpdateConcurrencyException`, which the repository translates to a 409. Section 4 covers this in full.
- **`categories.parent_id` is `DEFERRABLE INITIALLY DEFERRED`** so the importer can `COPY` rows in arbitrary order inside a single transaction; the FK is only checked at commit. The seed data also contains a real cycle at `cat_150 ↔ 151 ↔ 152` — Postgres self-FKs do not prevent cycles, so the recursive category descent in `GET /api/products?category=…` carries a visited-set guard.
- **`order_flags`** is independent persistence for manually-flagged orders (the bulk `flag` action). It is intentionally separate from automatic anomaly detection: flagging does not bump `orders.version`, does not affect `/api/orders/anomalies`, and only surfaces on `GET /api/orders/:id` as `flagged_at`. This keeps the two surfaces orthogonal — flag is a workflow action; anomalies are a query.
- **`jobs`** is the durable mirror of bulk-job state. Live counters are in Redis; the row is INSERTed on enqueue and UPDATEd to its terminal state on completion. The split is deliberate (see §5).

### CSV edge cases handled at import

- ~2% of orders have `total_price ≠ quantity × unit_price` (1,489 rows) — kept as-is and surfaced by the `price_mismatch` anomaly rule.
- 208 orders have `updated_at < created_at`, 507 have `quantity < 0`, 1,512 have empty `warehouse` (loaded as NULL).
- 81 products reference a non-existent category `cat_200`. The importer NULL-coerces these `category_id`s before COPY, with a logged count. This preserves FK enforcement (so genuine bugs would still fail) at the cost of erasing the literal `cat_200` reference. The two alternatives — drop the FK, or insert a sentinel `cat_200` — were both worse: the first weakens future bug-catching, the second invents data and inflates the categories row count beyond the value the test fixtures assert (193 categories).
- `notes` contains seeded XSS payloads (`<script>…`, `onmouseover=…`). They round-trip as inert JSON strings; React's auto-escaping renders them literally on the FE.

---

## 3. Indexing strategy

```sql
CREATE INDEX idx_orders_status        ON orders(status);
CREATE INDEX idx_orders_priority      ON orders(priority);
CREATE INDEX idx_orders_supplier      ON orders(supplier_id);
CREATE INDEX idx_orders_warehouse     ON orders(warehouse);
CREATE INDEX idx_orders_created_at    ON orders(created_at);
CREATE INDEX idx_orders_total_price   ON orders(total_price);
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE INDEX idx_products_name_trgm   ON products USING gin (name gin_trgm_ops);
```

Each index serves a query the test suite or the dashboard hits frequently:

| Index | Serves |
|---|---|
| `idx_orders_status`, `_priority`, `_supplier`, `_warehouse` | The four equality filters on `GET /api/orders`. |
| `idx_orders_created_at` | `date_from`/`date_to` range filter, default sort, the `by_month` aggregation, and `ORDER BY created_at DESC` on supplier-detail's recent-orders panel. |
| `idx_orders_total_price` | `min_total` filter and `sort=total_price`. |
| `idx_products_name_trgm` (pg_trgm GIN) | The `?search=` filter, which is `ILIKE '%' || $1 || '%'` on the joined `products.name`. A btree wouldn't help — the trigram index does, and the performance test passes p95 < 300ms on the seeded "hydraulic" search. |

Primary keys cover the joins. Foreign-key target columns are PKs already, so explicit FK indexes on `orders.supplier_id` / `orders.product_id` were considered redundant — equality filters on those columns ride the `idx_orders_supplier` and PK lookups respectively.

The aggregation endpoints (`/api/orders/stats`, `/api/suppliers/:id/performance`) do not have purpose-built indexes. They are full-scan aggregations that the existing indexes still help via index-only scans on the grouped column, and the performance slice confirmed both endpoints meet the p95 budget without further tuning. See §6 for the cache layer that *was* planned for these and ultimately deferred.

---

## 4. Concurrency

Two distinct mechanisms, one per access pattern:

### 4.1 Single PATCH — optimistic locking via `version`

`Order.Version` is mapped as `IsConcurrencyToken()`. `OrderRepository.UpdateAsync` loads the tracked entity, mutates `Status` / `Priority` / `Notes` / `UpdatedAt` and bumps `Version++`, then calls `SaveChangesAsync`. EF Core generates:

```sql
UPDATE orders
SET status=$1, priority=$2, notes=$3, updated_at=$4, version=$5
WHERE id=$6 AND version=$7
```

If zero rows are affected, EF throws `DbUpdateConcurrencyException`, which the repository catches and translates to `OptimisticConcurrencyException` → 409 with `code: "version_conflict"`. The version is **never** part of the API contract — clients send no version, and responses don't expose it. The mechanism is invisible by design: clients see "one wins, the other gets 409", which is exactly what the README and `concurrency.test.ts` require.

`PATCH /api/orders/:id` also returns 409 (`code: "already_cancelled"`) when the current status is `cancelled`. That check happens before the UPDATE, against the loaded entity.

### 4.2 Bulk worker — `SELECT … FOR UPDATE SKIP LOCKED` per chunk

The bulk worker processes order IDs in chunks of 200 inside an EF transaction. For each chunk:

1. `SELECT id FROM orders WHERE id = ANY(@ids)` — existence check; missing IDs counted as failed.
2. `SELECT id, status, version FROM orders WHERE id = ANY(@ids) FOR UPDATE SKIP LOCKED` — row-level pessimistic lock; rows held by an overlapping transaction are silently skipped. (No LINQ surface for the lock hint, so this is `SqlQueryRaw`.)
3. Per locked row: parameterized UPDATE (approve/reject) with the same version-conditional `WHERE` clause as the single PATCH path, or `INSERT INTO order_flags … ON CONFLICT DO NOTHING` for `flag`.

Skipped rows (held by another job's transaction) are counted as **completed**, not failed — by the time this transaction commits, the other job has already applied an equivalent action. That single decision is what gives us the README's "each order processed exactly once across overlapping batches" guarantee without coordination at the application layer. No Redis lock on the write path.

Failures bucket in C# from per-row outcomes:

- Order doesn't exist → `failed++`
- Order is `cancelled` → `failed++` (both for status changes and for flag — flagging cancelled is meaningless)
- UPDATE rowcount = 0 → `failed++` (concurrent modification, version mismatch)

The job is `failed` only if every row failed; otherwise `completed`.

---

## 5. Background processing

```
Bulk endpoint  ──[Channel<BulkJob>]──►  BulkWorker (BackgroundService)
                                         │
                                         ├── EF transaction per chunk of 200 (§4.2)
                                         ├── Redis HINCRBY job:{id} per row
                                         └── Postgres jobs row → terminal state
```

A single in-process `Channel.CreateUnbounded<BulkJob>()` queue paired with one `IHostedService` worker draining it. The endpoint handler enqueues and returns 202 in well under 50 ms; processing happens off the request path. With 50,000 orders, a budget of <500 ms response and <30 s total completion, the simplest queue that could possibly work was the right choice — Hangfire / MassTransit / SQS would be over-engineering for one consumer on one host.

**Job state is split between Redis and Postgres on purpose.** Redis is the live truth: a hash `job:{id}` with `status, total, completed, failed`, mutated via `HINCRBY` per processed row. `GET /api/jobs/:id` reads from Redis only — microsecond hot path, never hits Postgres while a job is running. The Postgres `jobs` row is the durable mirror: INSERTed on enqueue, UPDATEd to its terminal state on completion. Two concrete benefits:

- If Redis is unavailable when the API queries, finished jobs are still answerable from the Postgres mirror (degraded but correct).
- The submission timeline is preserved as durable history, which is what reviewers and ops would actually want.

The cost is two writes per terminal state instead of one. Worth it.

The worker resumes only the Postgres mirror across a graceful restart — in-flight jobs whose live Redis state has been wiped do not get re-queued automatically. For this take-home that's an acceptable simplification; in production you'd want a startup sweep that re-enqueues `processing` rows older than a configurable threshold.

---

## 6. Caching — what was planned, and what shipped

The original architecture called for explicit-invalidation Redis caches on the two aggregation endpoints:

- `SET stats:global` (JSON), `DEL` on every successful PATCH and bulk-job completion.
- `SET perf:{supplier_id}` (JSON), `DEL` on PATCH/bulk affecting that supplier's orders.

The performance slice ran the test suite against the data layer with no cache. **All eight performance tests passed.** The slowest endpoint, `/api/orders/anomalies`, peaks at ~1.5 s on cold p95 against a 1.0 s budget — and that one wouldn't have benefited from the planned caches anyway, since it scans the full orders table for anomaly rules each call.

Given the budgets are met without it, the cache layer is **deferred, not removed**. The reasons not to ship it now:

- Every cached value is one more correctness surface. `/api/orders/stats` would have been incorrect during the window between a PATCH commit and the cache-key delete; that race is solvable but adds complexity.
- The performance test (`tests/performance.test.ts`) is the binding contract for "fast enough", and it passes.
- The project's coding principles explicitly warn against features beyond what the task requires.

What *is* cached: bulk-job live state (Redis `HSET job:{id}` with `EXPIRE 86400` on terminal state). That is structural, not optimization — it's how the worker and the polling endpoint communicate.

---

## 7. Real-time events

`/api/events` is **Server-Sent Events only**. The README permits either WebSocket or SSE; the test client (`tests/utils/events-client.ts`) tries WebSocket first with a 3 s timeout and falls back to SSE on failure, so SSE alone passes the suite. One transport is easier to operate, easier to reason about, and easier to debug than two — the choice was straightforward.

The wire is conventional SSE: `text/event-stream`, `Cache-Control: no-cache, no-transform`, `Connection: keep-alive`, `X-Accel-Buffering: no`. Each frame is `data: <JSON>\n\n`. An initial `retry: 5000\n\n` hint sets the client's reconnect window, and a `: ping\n\n` heartbeat every 15 s keeps proxies/load balancers from severing idle connections.

Two events:

- `order_updated { id, old_status, new_status, updated_at }` — published from `OrdersService.UpdateStatusAsync` only when `oldStatus != newStatus`. Snake_case payload, matching the rest of the API.
- `bulk_completed { jobId }` — published from the bulk worker's `TryFinalize` on every terminal state. **camelCase** because `realtime.test.ts:96` literally asserts `event.data.jobId`. This is one of two backend casing inconsistencies — both documented in `pending-decisions.md` §1.

A singleton `EventHub` (DI-registered) holds a list of subscriber `Channel<EventEnvelope>(256)` per connection, with `BoundedChannelFullMode.DropOldest` so a stalled client cannot wedge publishers. `EventsController.Stream` races a `PeriodicTimer(15s)` heartbeat against `ChannelReader.WaitToReadAsync` on a single `Response.Body` writer to avoid interleaving. Filtered subscriptions via `?supplier_id=…` apply to `order_updated` only; `bulk_completed` broadcasts to every subscriber regardless of filter, since a bulk job may legitimately span multiple suppliers and there is no narrowing rule.

There is no Redis pub/sub. The deployment shape is a single API instance, so in-process fan-out is the simplest correct answer.

The frontend subscribes once globally via `lib/useBulkJobEvents.ts`, a refcounted `EventSource` wrapper that lets multiple bulk-job toasts share a single connection and tear it down only when none remain.

---

## 8. Validation, errors, and security

Every error response is a single envelope:

```json
{ "error": "Human-readable message", "code": "machine_code" }
```

Rendered by `ExceptionHandlerMiddleware`. Stack traces are logged via `ILogger`, never echoed to clients.

Input validation is layered:

- Query params — number parsing, ISO-8601 dates, enum allow-lists for `status` / `priority`, sort field through a static whitelist (`OrderRepository.SortColumns`) so SQL identifiers are never interpolated from user input.
- Path params — ID format checks before any DB call.
- Bodies — JSON-deserialized, length-bounded (`notes` ≤ 4096, bulk `reason` ≤ 4096), and PATCH requires at least one of `status` / `priority` / `notes` (`code: "no_fields"`).
- Pagination — `limit` clamped to `[1, 1000]`, `offset` to `[0, ∞)`. Negative or oversize values return 200 with the clamped page rather than 400 — silent saturation matches the test expectations and is friendlier to clients.

Security controls (full inventory in `docs/SECURITY.md`):

- `SecurityHeadersMiddleware` sets `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Cross-Origin-Resource-Policy: same-origin` on every response, registered *before* the exception handler so error envelopes inherit them.
- Named CORS policy `frontend-dev` driven by `Cors:AllowedOrigins` (defaults to `http://localhost:5173`).
- Kestrel `MaxRequestBodySize = 1 MB` (10,000-element bulk requests are ~250 KB worst case, comfortably within the cap).
- SQL injection — EF Core 9 parameterizes everything; the five remaining `FromSqlRaw` / `SqlQueryRaw` / `ExecuteSqlRawAsync` call sites all bind via `NpgsqlParameter` with explicit types. No string concatenation of user input into SQL exists in the codebase.
- XSS — CSV-imported XSS payloads in `notes` round-trip as inert JSON. The FE renders them only via React's auto-escaped `{value}` interpolation. Never `dangerouslySetInnerHTML`.

What's *not* in scope: authentication, authorization, rate limiting, HTTPS/HSTS. See `docs/SECURITY.md` § "What I'd add for production".

---

## 9. Frontend

Vite + React 18 + TypeScript + Tailwind v3.4 + shadcn/ui. State is split deliberately: TanStack Query owns server state with a 30 s `staleTime` and explicit invalidations on writes; React Router owns navigation; React `useState` owns component-local UI. There is no Redux, MobX, Zustand, or context-based global store — none of them earn their weight here.

shadcn primitives (`Button`, `Card`, `Sheet`, `Skeleton`, `Table`, `Badge`, `Input`, `Select`, `Checkbox`, `AlertDialog`) are committed under `src/components/ui/` and hand-rolled — no Radix dependency. The two newest, `Checkbox` and `AlertDialog`, were written for the bulk-action UX following the same pattern as the existing `Sheet` (focus trap, ESC, backdrop click, no portal library).

Four routes:

- `/orders` — paginated table with full filter bar (status multi-chip, priority chip, supplier picker, warehouse, date range, search, min total, sort), sortable columns, click → detail sheet with status PATCH, **multi-select checkboxes with sticky bulk-action bar** (approve / reject / flag), confirmation dialog, and a bottom-right job-progress toast that polls `/api/jobs/:id` every 1 s and listens to `/api/events` for the `bulk_completed` SSE event.
- `/stats` — Recharts dashboard: KPI tiles, monthly trend line, status donut, warehouse bars, top-10 suppliers (clickable through to detail).
- `/suppliers` — paged supplier table.
- `/suppliers/:id` — header card, performance card (avg delivery, rejection rate, avg order value, price consistency, total orders), monthly-trend chart, recent-orders table.

Theme: slate base + emerald accent, 10 px radius, Inter typeface, 150 ms transitions, soft shadows. Tri-state (light / dark / system) toggle in the nav with `localStorage` persistence and a pre-paint flash-prevention script in `index.html`. The full design rules are binding and live in `docs/ui-design.md`.

---

## 10. Tradeoffs

A few decisions that warrant honest naming:

- **Three URL aliases on the bulk endpoint.** The test suite is internally inconsistent on URL and casing (`bulk-action` camelCase, `bulk` camelCase, `bulk-actions` snake_case). All three are accepted; all three call the same `BulkService`. The wart is documented in `pending-decisions.md` §1 for PO review and resolution. We chose to satisfy the tests as-shipped rather than pick one URL and watch a third of the suite fail.
- **5 sequential queries for `/api/orders/stats` instead of 1 `QueryMultipleAsync`.** When we migrated from Dapper to EF Core 9, the multi-result-set pattern didn't translate cleanly to LINQ. We accepted four extra round trips because the perf budget still passes, and the win in maintainability — uniform LINQ + entity model + `IsConcurrencyToken()` for free — was substantial.
- **Aggregation cache deferred** (see §6).
- **Single-instance deployment shape.** No Redis pub/sub, no horizontal scaling, no sticky sessions. If the brief had said "deploy across 3 nodes", the SSE fan-out would need Redis pub/sub and the in-process bulk worker would need a real queue.
- **Selection persists across pagination and filter changes** on the bulk-action UI. This is a UX choice with both upsides (power-user workflows, hidden selections survive a back-and-forth) and downsides (users can forget what they selected). Mitigated by an always-visible "N selected · Clear" badge in the sticky action bar.
- **Per-order success/failure outcomes are not surfaced** on the bulk-job toast. The `/api/jobs/:id` contract returns aggregate counts only, so we report `847 succeeded · 3 failed` plus a tooltip enumerating the three possible failure reasons (cancelled / nonexistent / version conflict). Showing per-order detail would require either extending the API or refetching every submitted order — declined as scope creep.
- **Hand-rolled shadcn primitives instead of Radix.** Radix would give better keyboard/ARIA polish for free, but it's another dependency. The existing `Sheet` is already hand-rolled, so the new `AlertDialog` and `Checkbox` follow that precedent rather than introducing inconsistency.

---

## 11. What I'd add with more time

- **Aggregation caching** with the explicit-invalidation pattern in §6. Even though the budgets pass, ~570 ms p95 on `/api/orders/stats` is uncomfortably close to the 500 ms cap and would not survive a 2× data growth.
- **Anomalies endpoint optimization.** It currently scans 50k joined rows per call (~1.5 s peak). A materialized view refreshed on bulk-job completion would drop this to milliseconds.
- **Authentication & rate limiting.** No-auth was scoped out by the brief; for production the whole API would sit behind JWT auth with role-based controls (`admin` for bulk actions, `viewer` for reads) and per-IP rate limits on the write paths.
- **End-to-end FE tests.** The backend has 83 automated tests; the frontend has zero. A Playwright suite walking the four documented user flows (filter → patch → bulk → real-time) would catch regressions the type checker can't.
- **Worker resume on restart.** The bulk worker doesn't re-enqueue Postgres `processing` rows on startup; an admin endpoint and/or a startup sweep would fix this.
- **Supplier-local time for `after_hours`.** Currently UTC, which over-flags suppliers in non-UTC timezones. Joining `suppliers.country` to an IANA-zone map is the obvious fix; described further in `docs/ANOMALY_STRATEGY.md`.
- **Observability.** Structured logs are in place; metrics (Prometheus / OpenTelemetry) and tracing across the request → worker → SSE chain are not. For a real production rollout this is the first thing I'd add.
- **Resolve the bulk-URL wart.** Pick one URL, one casing, deprecate the others, update the brief. Awaiting PO input.

---

## 12. AI chat transcripts

The full Claude Code session history that produced this codebase is preserved under [`docs/transcripts/`](./transcripts/) for reviewer audit — verbatim Claude Code session logs in JSONL, one file per session, including every user prompt, assistant reply, and tool call/result. Readable with any JSONL viewer, or re-openable in Claude Code via `claude --resume <session-id>`. To find user-typed prompts in an editor, regex-search for `"role":"user","content":"(?!<command-|<local-command-|\[Request)`.

Filenames are prefixed with the session start time (`YYYY-MM-DD_HHMM_`) so chronological order matches build order. Sessions, in order:

| # | Date / time (UTC) | Topic |
|---|---|---|
| 1 | 2026-05-04 12:58 | Project bootstrap, .NET vs Node decision, initial `ARCHITECTURE.md` draft |
| 2 | 2026-05-04 13:45 | Resolving architecture open questions |
| 3 | 2026-05-05 08:57 | Backend API CRUD + project structure (DI, middleware, controllers) |
| 4 | 2026-05-05 09:31 | Filtering slice tests — parallel agent on `tests/filtering.test.ts` |
| 5 | 2026-05-05 16:19 | State review + multi-select bulk actions feature |
| 6 | 2026-05-05 16:24 | Frontend polish (logo, branding, scroll restoration) |
| 7 | 2026-05-05 17:38 | Self-review document + flagged-orders UI tooltip |
