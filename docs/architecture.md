# Architecture (AI Context)

> **Purpose.** Living context document for AI assistance on this project. Read this first.
> Distinct from the deliverable `ARCHITECTURE.md` required by the assignment — that one is written for human reviewers at the end. This one is a working document and may contain "PENDING" markers for decisions still to be made together with the human owner.

---

## 1. Project Goal

Build a procurement order-management dashboard:

- **Backend:** REST API on **port 3000** that serves orders, suppliers, products, categories, aggregations, anomaly detection, async bulk operations, and real-time events.
- **Frontend:** React + TypeScript dashboard consuming the API.
- **Data:** CSV seed files (50,000 orders, 500 suppliers, 5,000 products, 195 categories). Must be imported into Postgres before the API can serve anything.
- **Grading:** 83 automated tests (115 pts) in `tests/` + qualitative review (30 pts). Tests are authoritative — when README disagrees with a test, the test wins.

`tests/` and `data/` are read-only. All implementation lives in `src/`.

---

## 2. Hard Constraints (do not violate)

| Constraint | Source |
|---|---|
| Server listens on `http://localhost:3000` | README §Quick Start |
| Paginated list shape: `{ data, total, limit, offset }` | README §1 |
| Error shape: `{ error, code }` | README §1 |
| All responses set `Content-Type: application/json` | README §1 |
| Bulk action responds in `<500ms` regardless of batch size (≤10,000) | README §1.5 |
| `top_suppliers` exactly 10 entries, sorted desc by revenue | README §1.3 |
| `by_warehouse` includes `"unassigned"` for null/empty warehouse | README §1.3 |
| `by_month` format `"YYYY-MM"`, chronological, 24 entries (2023-01 → 2024-12) | README §1.3 |
| Product category filter must descend recursively into child categories | README §1.1 |
| Concurrent PATCH on same order: one wins (`200`), other gets `409` | README §1.6 |
| Bulk overlap: each order processed exactly once across overlapping batches | README §1.6 |
| Reads during bulk operations: consistent, no corrupt status | README §1.6 |
| Real-time stream at `/api/events` (WebSocket OR SSE) | README §1.7 |
| Filtered subscription via `?supplier_id=...` | README §1.7 |

---

## 3. Tech Stack (decided)

| Layer | Choice | Notes |
|---|---|---|
| Runtime | **.NET 9** (`net9.0`) | SDK 10.0.201 also installed locally |
| Web framework | **ASP.NET Core (Controllers)** | MVC pattern per coding-principles §6 |
| DB driver | **Npgsql** | Direct ADO.NET; no EF Core (perf + control over aggregation SQL) |
| Query helper | **Dapper** | Thin micro-ORM; keeps SQL explicit, mapping ergonomic |
| Cache / Pub-Sub / Queue | **StackExchange.Redis** | Used for async job state and event fan-out |
| Database | **Postgres 16** | Provided by `docker-compose.yml` |
| Frontend | **React + TypeScript** | PENDING toolchain choice (Vite vs Next vs CRA — see §10) |

**NuGet packages already installed in `src/OrderOps.Api`:**
`Npgsql`, `Dapper`, `StackExchange.Redis`, `Microsoft.AspNetCore.OpenApi` (default).

---

## 4. Repository Layout (current)

```
candidate-package/
├── README.md                  ← assignment spec (read-only intent; do not modify)
├── docker-compose.yml         ← Postgres + Redis (read-only intent)
├── .env.example / .env        ← PORT, DATABASE_URL, REDIS_URL
├── docs/                      ← ALL project docs live here
│   ├── architecture.md        ← THIS FILE — AI working context
│   ├── coding-principles.md   ← binding rules the AI must follow
│   ├── ARCHITECTURE.md        ← (future) deliverable for human reviewers
│   └── ANOMALY_STRATEGY.md    ← (future) deliverable for anomaly rules + severity
├── .claude/
│   └── commands/              ← project-scoped slash commands (/bootstrap, /commit)
├── data/                      ← READ-ONLY CSV seeds
│   ├── orders.csv             50,000 rows
│   ├── suppliers.csv          500 rows
│   ├── products.csv           5,000 rows
│   └── categories.csv         195 rows
├── tests/                     ← READ-ONLY test suite (vitest, 83 tests)
└── src/
    ├── OrderOps.slnx          ← solution (XML format, .NET 10 default)
    └── OrderOps.Api/          ← ASP.NET Core Web API project
        ├── Program.cs         ← bootstraps on port 3000
        ├── Controllers/       ← (empty — to be populated)
        ├── appsettings.json   ← Postgres + Redis connection strings
        └── Properties/launchSettings.json
```

`src/` structure beyond `OrderOps.Api` is **PENDING** — see §10.

---

## 5. Data Model (CSV → Postgres)

Schema is **PENDING** (planning phase). The CSV columns are fixed:

| Table | CSV columns | Notes / edge cases (from README §Tips) |
|---|---|---|
| `orders` | `id, supplier_id, product_id, quantity, unit_price, total_price, status, priority, created_at, updated_at, warehouse, notes` | ~2% have `total_price ≠ quantity*unit_price`; ~200 have `updated_at < created_at`; some `quantity < 0` (returns); some `warehouse` null/empty; XSS payloads in `notes` |
| `suppliers` | `id, name, email, rating, country, active, created_at` | `active` is `true`/`false` text; some inactive suppliers still have orders; duplicate name variations |
| `products` | `id, name, category_id, sku, price` | `price` = catalog/base price; used by `price_consistency` & `price_spike` rules |
| `categories` | `id, name, parent_id` | Hierarchical; `parent_id` empty for roots; **circular references possible** (must guard the recursive descent) |

**Valid order statuses:** `pending, approved, rejected, shipped, delivered, cancelled`.

**Required fields the schema must support that aren't in CSV:**
- `version` (or equivalent) on `orders` for optimistic locking → returns `409` on stale PATCH.
- A jobs table or Redis hash for bulk-action job tracking.

Indexing strategy is **PENDING** but at minimum needs to cover: `status`, `priority`, `supplier_id`, `warehouse`, `created_at`, and joined search on `products.name`.

---

## 6. API Surface

All paths under `/api`. List endpoints return the paginated shape; errors return the error shape.

### 6.1 CRUD

| Method | Path | Notes |
|---|---|---|
| GET | `/api/orders` | Paginated; default `limit=20`; supports filters (§6.2) |
| GET | `/api/orders/:id` | Joined `supplier_name`, `product_name`; 404 if missing |
| PATCH | `/api/orders/:id` | Updates status/priority. 400 invalid status; 409 if already cancelled OR optimistic-lock conflict |
| GET | `/api/suppliers` | Paginated |
| GET | `/api/suppliers/:id` | Includes computed `order_count`, `total_revenue`; 404 if missing |
| GET | `/api/products` | Paginated; `?category=cat_001` filters recursively into children |

### 6.2 Filters on `GET /api/orders`

`status` (single or comma-list), `priority`, `supplier_id`, `warehouse`, `date_from`, `date_to` (inclusive on `created_at`), `min_total`, `search` (case-insensitive on joined `product_name`), `sort` + `order` (asc/desc), `limit`, `offset`. All combinable.

### 6.3 Aggregations

- `GET /api/orders/stats` — `total_orders`, `total_revenue`, `avg_order_value`, `by_status` (per-status `count` + `total_value`), `by_month` (24 entries chronological), `top_suppliers` (exactly 10), `by_warehouse` (5 warehouses + `"unassigned"` bucket).
- `GET /api/suppliers/:id/performance` — `avg_delivery_days`, `rejection_rate`, `avg_order_value`, `monthly_trend`, `price_consistency` (fraction of orders within 20% of catalog price).

### 6.4 Anomalies

`GET /api/orders/anomalies` returns `{ data: [{ order_id, anomaly_types[], severity }] }`.

**Required rules:** `price_mismatch`, `inactive_supplier`, `negative_quantity`, `timestamp_anomaly`.
**Bonus rules:** `price_spike`, `after_hours`, `risky_supplier`.

`severity` ∈ `{low, medium, high}` — classification logic is our call but must be deterministic and documented (will end up in `ANOMALY_STRATEGY.md`).

### 6.5 Bulk

- `POST /api/orders/bulk-action` → 202 `{ jobId }` in <500ms; async processing; actions: `approve`, `reject`, `flag`. 400 on empty `orderIds`, invalid action, or batch >10,000. Failures: non-existent IDs, already-cancelled orders.
- `GET /api/jobs/:id` → `{ status: processing|completed|failed, progress: { total, completed, failed } }`.

### 6.6 Real-time (`/api/events`)

Choose **SSE or WS** — tests auto-detect. Events:
- `order_updated` — `{ id, old_status, new_status, updated_at }`
- `bulk_completed` — `{ jobId }`

Filter via `?supplier_id=...`. All connected clients receive matching events.

### 6.7 Concurrency

| Scenario | Required behavior |
|---|---|
| 2× simultaneous PATCH same order | One `200`, other `409` |
| 2× bulk actions w/ overlapping IDs | Both jobs complete; each order processed exactly once |
| GET during bulk run | Consistent reads; `total_orders` stays at 50,000; status always valid |

### 6.8 Performance

p95 budgets enforced in `tests/performance.test.ts`. Need DB indexes; aggregation endpoints likely benefit from Redis caching with invalidation on order writes.

---

## 7. Test Suite Mapping

The tests are the spec. When implementing, run the matching suite to validate:

| File | Tests | Pts | Run |
|---|---|---|---|
| `basic-crud.test.ts` | 15 | 15 | `npm run test:basic` |
| `filtering.test.ts` | 10 | 10 | `npm run test:filter` |
| `aggregations.test.ts` | 12 | 20 | `npm run test:agg` |
| `anomalies.test.ts` | 8 | 15 | `npm run test:anomaly` |
| `bulk-operations.test.ts` | 10 | 15 | `npm run test:bulk` |
| `concurrency.test.ts` | 10 | 15 | `npm run test:concurrent` |
| `performance.test.ts` | 8 | 10 | `npm run test:perf` |
| `realtime.test.ts` | 5 | 10 | `npm run test:realtime` |
| `security.test.ts` | 5 | 5 | `npm run test:security` |

Run from `tests/` directory. `tests/expected-values.json` is likely the source of truth for aggregation numbers — read it when implementing §1.3.

Implementation order: **basic → filtering → aggregations → anomalies → bulk → concurrency → performance → realtime → security** (easy → hard, mirrors test categories).

---

## 8. Cross-Cutting Concerns (PENDING design)

These are decisions to make together in planning. Do not implement before alignment.

### 8.1 Concurrency
- **Optimistic locking:** add a `version` column on `orders`, increment on every update, require client to send the version (or read-modify-write pattern: re-check the version inside `UPDATE … WHERE id=? AND version=?`).
- **Bulk overlap:** server must dedupe across overlapping batches. Options: (a) row-level lock with `SELECT … FOR UPDATE SKIP LOCKED`; (b) Redis-based lock per `order_id`; (c) idempotent action via the version column itself.
- **PENDING:** which approach to standardize on.

### 8.2 Background Jobs
- Bulk action MUST return 202 in <500ms — so processing must be off the request path.
- Options:
  1. In-process `Channel<T>` worker with state in Postgres (`jobs` table)
  2. Redis-backed list/stream with state in Redis hash
  3. Hosted background service (`IHostedService`) consuming from a channel/queue
- README says Redis is "optional" — option 1 is acceptable. **PENDING.**

### 8.3 Real-time
- **SSE preferred** unless we have a concrete reason to use WS:
  - Tests auto-detect, so functional outcome is identical.
  - SSE is one-way (server → client) which matches the spec.
  - Easier to implement in ASP.NET (just a streamed `IAsyncEnumerable` response with `text/event-stream`).
- Event source: a single in-process pub/sub (or Redis pub/sub if we anticipate multiple instances). Order-update emitter sits inside the PATCH handler / bulk worker.
- **PENDING:** SSE vs WS, and in-proc vs Redis pub/sub.

### 8.4 CSV Import
- **PENDING:** standalone `dotnet run --project Tools/Importer` or first-run hook in the API.
- Postgres `COPY` is the fastest path. Streaming COPY via Npgsql's `BeginBinaryImport` is preferred over row-by-row INSERT.
- Must be **idempotent** so re-running tests doesn't poison state — either truncate-then-load, or skip if the row count already matches the CSV.

### 8.5 Validation & Errors
- Single error envelope `{ error, code }`. Do not expose stack traces.
- Input validation: query params (numbers, ISO-8601 dates, enums), path params (ID format), JSON body shapes.
- Reject unknown query params? README doesn't say — default to ignoring (forgiving).
- All 4xx responses must still set `Content-Type: application/json`.

### 8.6 Security
- Notes/text fields contain XSS payloads — store as-is; never echo through HTML; ensure JSON encoding is correct.
- SQL injection: Dapper parameterizes by default; never string-concatenate user input into SQL.
- Sort field whitelist: `sort=...` must be checked against an allow-list of column names, never substituted directly.

---

## 9. Done So Far (state checkpoint)

- [x] `docker compose up -d` — Postgres + Redis healthy
- [x] `tests/` deps installed (`npm install`)
- [x] `.env` created from `.env.example`
- [x] CSV headers verified
- [x] .NET solution + `OrderOps.Api` Web API scaffold
- [x] NuGet: Npgsql, Dapper, StackExchange.Redis
- [x] Server listens on port 3000 (`GET /healthz` returns `{"status":"ok"}`)
- [x] `appsettings.json` has `ConnectionStrings.Postgres` and `ConnectionStrings.Redis`

---

## 10. Open Decisions (require human alignment per coding-principles §7)

1. **Project structure inside `src/`** — single project vs. split (`OrderOps.Api`, `OrderOps.Domain`, `OrderOps.Infrastructure`, `OrderOps.Importer`).
2. **Database schema** — exact tables, types, constraints, indexes; how `version` column works.
3. **CSV import strategy** — separate tool vs. startup hook; idempotency mechanism.
4. **Concurrency mechanism** — version column vs. row locks vs. Redis locks (likely combination).
5. **Background job runtime** — in-process channel vs. Redis stream.
6. **Real-time transport** — SSE (likely) vs. WS, and pub/sub mechanism.
7. **Frontend toolchain** — Vite + React + TS is the lean default; confirm before scaffolding.
8. **Anomaly severity rubric** — needs to be agreed before implementing §1.4 so it's defensible in `ANOMALY_STRATEGY.md`.

---

## 11. Working Agreement

See `coding-principles.md`. Summary: small increments, explicit human review and commit after each step, no inventing of design decisions, end-of-session checks (typecheck, code review, confidence/risk, edge cases).
