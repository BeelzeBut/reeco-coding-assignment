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
| Frontend | **Vite + React + TypeScript** | Tailwind for layout, TanStack Query for server state, Recharts for analytics charts. SPA only — no SSR. |

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
    ├── OrderOps.Api/          ← ASP.NET Core Web API project
    │   ├── Program.cs         ← bootstraps on port 3000
    │   ├── Features/          ← group-by-feature: Orders/, Suppliers/, Products/,
    │   │                        Anomalies/, Bulk/, Events/ — each with controller +
    │   │                        service + repository + DTOs
    │   ├── Infrastructure/    ← Npgsql connection factory, Redis client, error
    │   │                        middleware, cache invalidation
    │   ├── schema.sql         ← canonical schema, applied by Importer
    │   ├── appsettings.json   ← Postgres + Redis connection strings
    │   └── Properties/launchSettings.json
    ├── OrderOps.Importer/     ← console app: applies schema, COPYs CSVs, idempotent
    │   ├── Program.cs
    │   └── OrderOps.Importer.csproj
    └── OrderOps.Web/          ← Vite + React + TS frontend (see §3)
```

SOLID boundaries come from feature folders inside `OrderOps.Api`, not extra csprojs. The Importer is a separate project so reviewers can see CSV ingestion as a deliberate, isolated tool runnable via `dotnet run --project src/OrderOps.Importer`.

---

## 5. Data Model (CSV → Postgres)

The CSV columns are fixed; type choices below are confirmed against the actual data:

| Table | CSV columns | Notes / edge cases (from README §Tips + data inspection) |
|---|---|---|
| `orders` | `id, supplier_id, product_id, quantity, unit_price, total_price, status, priority, created_at, updated_at, warehouse, notes` | ~2% have `total_price ≠ quantity*unit_price`; ~200 have `updated_at < created_at`; some `quantity < 0` (returns, down to −49); 1 row has empty `warehouse` → NULL; XSS payloads (`<script>…`, `onmouseover=…`) in `notes` |
| `suppliers` | `id, name, email, rating, country, active, created_at` | `active` is `true`/`false` text → boolean; ~37 country codes including non-ISO variants (DEN, GER, BRZ); duplicate name variations |
| `products` | `id, name, category_id, sku, price` | `price` = catalog/base price; used by `price_consistency` & `price_spike` rules |
| `categories` | `id, name, parent_id` | Hierarchical; `parent_id` empty for roots; **real cycle in seed at `cat_150↔151↔152`** — must guard the recursive descent (Postgres self-FKs do NOT prevent cycles) |

**Valid order statuses:** `pending, approved, rejected, shipped, delivered, cancelled`.

### 5.1 Schema

```sql
CREATE TABLE categories (
  id          varchar(16) PRIMARY KEY,
  name        text NOT NULL,
  parent_id   varchar(16) NULL
    REFERENCES categories(id) DEFERRABLE INITIALLY DEFERRED
);

CREATE TABLE suppliers (
  id          varchar(16) PRIMARY KEY,
  name        text NOT NULL,
  email       text,
  rating      numeric(3,2),
  country     varchar(8),
  active      boolean NOT NULL,
  created_at  timestamptz NOT NULL
);

CREATE TABLE products (
  id          varchar(16) PRIMARY KEY,
  name        text NOT NULL,
  category_id varchar(16) REFERENCES categories(id),
  sku         text,
  price       numeric(12,2) NOT NULL
);

CREATE TABLE orders (
  id           varchar(16) PRIMARY KEY,
  supplier_id  varchar(16) NOT NULL REFERENCES suppliers(id),
  product_id   varchar(16) NOT NULL REFERENCES products(id),
  quantity     integer NOT NULL,
  unit_price   numeric(12,2) NOT NULL,
  total_price  numeric(14,2) NOT NULL,
  status       varchar(16) NOT NULL,
  priority     varchar(16) NOT NULL,
  created_at   timestamptz NOT NULL,
  updated_at   timestamptz NOT NULL,
  warehouse    varchar(32) NULL,
  notes        text,
  version      integer NOT NULL DEFAULT 1   -- server-internal optimistic lock; never in JSON
);

CREATE TABLE jobs (
  id          varchar(32) PRIMARY KEY,
  status      varchar(16) NOT NULL,
  total       integer NOT NULL,
  completed   integer NOT NULL DEFAULT 0,
  failed      integer NOT NULL DEFAULT 0,
  action      varchar(16) NOT NULL,
  created_at  timestamptz NOT NULL DEFAULT now(),
  finished_at timestamptz NULL
);
```

- `version` is **internal**: never accepted on PATCH input, never emitted in responses. Used by the conditional UPDATE in §8.1.
- `parent_id` FK is `DEFERRABLE INITIALLY DEFERRED` — load order in COPY is unconstrained inside one tx; orphan parent_ids are rejected at commit. Cycles in the seed data still load (FKs don't prevent them) and must be guarded query-side.
- `jobs` row is the **mirror** of Redis live state — see §8.2.

### 5.2 Indexes

```sql
CREATE INDEX idx_orders_status      ON orders(status);
CREATE INDEX idx_orders_priority    ON orders(priority);
CREATE INDEX idx_orders_supplier    ON orders(supplier_id);
CREATE INDEX idx_orders_warehouse   ON orders(warehouse);
CREATE INDEX idx_orders_created_at  ON orders(created_at);
CREATE INDEX idx_orders_total_price ON orders(total_price);
CREATE EXTENSION IF NOT EXISTS pg_trgm;
CREATE INDEX idx_products_name_trgm ON products USING gin (name gin_trgm_ops);
```

The trigram index on `products.name` exists because `?search=` is case-insensitive substring on the joined `product_name` — a btree wouldn't help; a `gin_trgm_ops` index does.

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

## 8. Cross-Cutting Concerns

Decisions below are confirmed and unblocked. Implementation must follow them; deviating requires re-opening the discussion per `coding-principles.md` §7.

### 8.1 Concurrency — internal `version` + `SELECT FOR UPDATE SKIP LOCKED`

- **Single PATCH:** repository runs `UPDATE orders SET status=@s, updated_at=now(), version=version+1 WHERE id=@id AND version=@v RETURNING version`. If 0 rows affected → `409`. The service reads the current `version` immediately before the UPDATE in the same connection (read-your-writes). The version is **never** part of the API contract — tests send no version, and responses don't expose it. The `concurrency.test.ts` PATCH-race test passes because two concurrent UPDATEs read `version=v` and only one wins the conditional UPDATE.
- **Bulk worker:** for each batch chunk, run `SELECT id, version FROM orders WHERE id = ANY(@ids) FOR UPDATE SKIP LOCKED` inside a transaction. Orders held by an overlapping job's transaction are silently skipped — they are then counted as `completed` because the other job has already applied the same action. This satisfies the "each order processed exactly once across overlapping batches" invariant. No Redis lock on the write path.

### 8.2 Background Jobs — in-process `Channel<BulkJob>` + Redis-primary state + Postgres mirror

- Bulk endpoint enqueues onto `Channel.CreateUnbounded<BulkJob>()`; one `IHostedService` worker drains it. Easily meets the <500ms response and 30s completion budgets (50ms enqueue, work happens off the request path).
- **Live state in Redis**: hash `job:{id}` with fields `status, total, completed, failed`. Worker uses `HINCRBY` per processed row. `GET /api/jobs/:id` reads from Redis only — microsecond latency, never hits Postgres on hot path.
- **Postgres `jobs` mirror**: a row is INSERTed when the job is enqueued and UPDATEd to its terminal state on completion. Durable audit/observability without doubling write traffic during the run.
- **Failure mode**: if Redis is unavailable mid-job, the API can fall back to the Postgres row for status (degraded but correct for already-finished jobs).

### 8.3 Real-time — SSE only, in-process `EventHub`

- SSE is sufficient: the test client (`tests/utils/events-client.ts`) tries WebSocket first (3s timeout) and falls back to SSE on failure, so SSE alone passes `realtime.test.ts`.
- Implementation: a controller streams `text/event-stream` using `IAsyncEnumerable<EventEnvelope>`.
- A singleton `EventHub` (DI-registered) holds a list of subscriber `Channel<EventEnvelope>`s. The PATCH handler and the bulk worker call `Publish()`. The controller filters by `?supplier_id=...` per subscriber.
- No Redis pub/sub (single API instance is the deployment shape).

### 8.4 CSV Import — `OrderOps.Importer` console, truncate-and-COPY, idempotent

- Run as `dotnet run --project src/OrderOps.Importer`.
- Steps inside one transaction:
  1. Apply `schema.sql` (uses `CREATE TABLE IF NOT EXISTS` form so re-runs are no-ops).
  2. `SET CONSTRAINTS ALL DEFERRED;`
  3. `TRUNCATE orders, products, suppliers, categories RESTART IDENTITY CASCADE;`
  4. `COPY` each CSV via Npgsql `BeginBinaryImport` — fastest path for 50k orders. Empty `warehouse` → `NULL` at parse time.
- Re-running is safe: tests can drop and reload state freely. Importer prints row counts on completion and exits non-zero if any count mismatches the CSV header parse.

### 8.5 Validation & Errors
- Single error envelope `{ error, code }`. Do not expose stack traces.
- Input validation: query params (numbers, ISO-8601 dates, enums), path params (ID format), JSON body shapes.
- Reject unknown query params? README doesn't say — default to ignoring (forgiving).
- All 4xx responses must still set `Content-Type: application/json`.

### 8.6 Security
- Notes/text fields contain XSS payloads — store as-is; never echo through HTML; ensure JSON encoding is correct.
- SQL injection: Dapper parameterizes by default; never string-concatenate user input into SQL.
- Sort field whitelist: `sort=...` must be checked against an allow-list of column names, never substituted directly.

### 8.7 Redis usage map

Redis is leaned on for speed; the cache layer is a **post-MVP pass** — every endpoint must be functionally correct against Postgres first, then cached.

| Use | Pattern | Invalidation |
|---|---|---|
| Bulk job state (live counters) | `HSET job:{id} status total completed failed`, `HINCRBY` per row | `EXPIRE 86400` on terminal state |
| Aggregation cache: `/api/orders/stats` | `SET stats:global` (JSON), no TTL | `DEL stats:global` on every successful PATCH and on bulk-job completion |
| Aggregation cache: `/api/suppliers/:id/performance` | `SET perf:{supplier_id}` (JSON), no TTL | `DEL perf:{supplier_id}` on PATCH/bulk that touches an order with that supplier |
| Real-time pub/sub | not used in MVP (single-instance deployment) | n/a |

Invalidation is explicit, not TTL-based — staleness during the concurrency tests would be visible. Every write path that mutates an aggregate must `DEL` the affected cache keys; this is enforced by routing all writes through a single `OrdersWriteService` that calls the cache invalidator after a successful Postgres commit.

### 8.8 Anomaly severity rubric

Severity is computed deterministically from the matched rule set on a single order:

| Bucket | Trigger |
|---|---|
| **high** | Any of: `negative_quantity`, `price_mismatch`, `timestamp_anomaly`. **OR** ≥3 anomaly types matched. **OR** order belongs to a `risky_supplier`. |
| **medium** | Any of: `inactive_supplier`, `price_spike`. **OR** exactly 2 anomaly types matched. |
| **low** | Single match of: `after_hours`. |

Rationale: financial-integrity bugs (negative quantity, price mismatch, impossible timestamps) directly affect reported revenue and audit accuracy → **high**. Governance/cost concerns (using an inactive supplier, unusual price spike) are operational risks → **medium**. After-hours alone is informational → **low**. Multi-rule matches escalate one tier because correlated anomalies on a single order are stronger evidence of underlying data corruption.

The full rubric (with worked examples) lives in `docs/ANOMALY_STRATEGY.md`, written when §1.4 is implemented.

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
- [x] **2026-05-04** — Architecture decisions §10.1–§10.8 resolved; §8.1–§8.8 promoted from PENDING; schema (§5) and indexes (§5.2) locked.

---

## 10. Open Decisions

All decisions resolved on **2026-05-04**. Each former item now has a concrete home in this document:

| Was § 10 item | Now lives in |
|---|---|
| 1. Project structure inside `src/` | §4 (Repo Layout) |
| 2. Database schema | §5.1 (Schema) + §5.2 (Indexes) |
| 3. CSV import strategy | §8.4 |
| 4. Concurrency mechanism | §8.1 |
| 5. Background job runtime | §8.2 |
| 6. Real-time transport | §8.3 |
| 7. Frontend toolchain | §3 |
| 8. Anomaly severity rubric | §8.8 (full rubric will land in `docs/ANOMALY_STRATEGY.md` with §1.4) |

New PENDING items go here as they emerge. Empty is the steady state.

---

## 11. Working Agreement

See `coding-principles.md`. Summary: small increments, explicit human review and commit after each step, no inventing of design decisions, end-of-session checks (typecheck, code review, confidence/risk, edge cases).
