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
| DB driver | **Npgsql** | Postgres ADO.NET driver, used as the data source under EF Core. |
| ORM | **Entity Framework Core 9** (`Npgsql.EntityFrameworkCore.PostgreSQL`) | Used for entity model, navigation properties, change tracking, and `IsConcurrencyToken()` optimistic locking. LINQ where expressible; `FromSqlRaw` / `SqlQueryRaw` / `ExecuteSqlRawAsync` only for the genuinely Postgres-specific patterns (`generate_series`, recursive CTE, `FOR UPDATE SKIP LOCKED`, `ON CONFLICT DO NOTHING`, `array_remove(ARRAY[CASE …])`). |
| Cache / Pub-Sub / Queue | **StackExchange.Redis** | Used for async job state and event fan-out |
| Database | **Postgres 16** | Provided by `docker-compose.yml` |
| Frontend | **Vite + React + TypeScript** | Tailwind v3.4 + **shadcn/ui** for components, TanStack Query for server state, Recharts for analytics charts, React Router for the 4 views. SPA only — no SSR. |

**NuGet packages already installed in `src/OrderOps.Api`:**
`Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `Npgsql` (transitive), `StackExchange.Redis`, `Microsoft.AspNetCore.OpenApi` (default).

**NuGet packages installed in `src/OrderOps.Importer`:**
`Npgsql`, `CsvHelper`, `Microsoft.Extensions.Hosting`.

**npm packages installed in `src/OrderOps.Web`** (pinned to majors):

| Package | Version | Role |
|---|---|---|
| `vite` + `@vitejs/plugin-react` | ^5.4 | Dev server + bundler |
| `react`, `react-dom` | ^18.3 | UI library |
| `typescript` | ^5.5 | Type system |
| `tailwindcss` + `postcss` + `autoprefixer` | ^3.4 / ^8.4 / ^10.4 | Utility CSS toolchain |
| `tailwindcss-animate` | ^1.0 | shadcn animation peer |
| `class-variance-authority`, `clsx`, `tailwind-merge` | latest | shadcn variant + `cn()` helper |
| `lucide-react` | ^0.460 | shadcn icon set |
| `@tanstack/react-query` | ^5.59 | Server state + caching |
| `recharts` | ^2.13 | Analytics charts |
| `react-router-dom` | ^6.26 | Routing across 4 views |
| `vite-tsconfig-paths` | ^5.0 | `@/*` → `src/*` path alias |

shadcn/ui is **not** an npm dep — its components (`button`, `card`, `skeleton`) are committed at `src/components/ui/`. Adding new primitives in future slices: `npx shadcn@latest add <name>`.

**Theme:** slate base + emerald accent, 10px radius, Inter typeface, soft shadows, 150ms transitions. Tri-state (light / dark / system) toggle in the nav, persisted to `localStorage`. Full rules in [`docs/ui-design.md`](./ui-design.md) — binding for any UI change, on the same gate as `coding-principles.md`.

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
│   ├── ui-design.md           ← binding rules for any frontend visual change
│   ├── ARCHITECTURE.md        ← (future) deliverable for human reviewers
│   ├── ANOMALY_STRATEGY.md    ← (future) deliverable for anomaly rules + severity
│   └── pending-decisions.md   ← provisional choices needing PO review
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
    │   ├── schema.sql         ← canonical DDL (DROP + CREATE); owned by Api,
    │   │                        applied by Importer via msbuild <Link>
    │   ├── appsettings.json   ← Postgres + Redis connection strings
    │   └── Properties/launchSettings.json
    ├── OrderOps.Importer/     ← console app: applies schema, COPYs CSVs, idempotent
    │   ├── Program.cs         ← reads ../OrderOps.Api/schema.sql via output Link
    │   ├── appsettings.json
    │   └── OrderOps.Importer.csproj
    └── OrderOps.Web/          ← Vite + React + TS + Tailwind + shadcn/ui SPA
        ├── package.json       ← scripts: dev, build, typecheck
        ├── vite.config.ts     ← /api/* → http://localhost:3000 dev proxy
        ├── tsconfig.json + tsconfig.app.json + tsconfig.node.json
        ├── tailwind.config.js + postcss.config.js
        ├── components.json    ← shadcn config (slate base, CSS variables)
        ├── index.html
        └── src/
            ├── main.tsx       ← QueryClientProvider + RouterProvider
            ├── App.tsx        ← top-nav layout + <Outlet />
            ├── routes.tsx     ← /orders (default), /stats, /suppliers, /suppliers/:id
            ├── index.css      ← @tailwind + shadcn CSS variables
            ├── vite-env.d.ts
            ├── lib/           ← cn() helper + queryClient
            ├── api/           ← fetch wrapper + ApiError + Paginated<T>
            ├── components/ui/ ← shadcn primitives (button, card so far)
            └── features/      ← orders/, stats/, suppliers/ — group-by-feature
```

SOLID boundaries come from feature folders inside `OrderOps.Api`, not extra csprojs. The Importer is a separate project so reviewers can see CSV ingestion as a deliberate, isolated tool runnable via `dotnet run --project src/OrderOps.Importer`. The frontend mirrors the same group-by-feature shape under `src/OrderOps.Web/src/features/`.

---

## 5. Data Model (CSV → Postgres)

The CSV columns are fixed; type choices below are confirmed against the actual data:

| Table | CSV columns | Notes / edge cases (from README §Tips + data inspection) |
|---|---|---|
| `orders` | `id, supplier_id, product_id, quantity, unit_price, total_price, status, priority, created_at, updated_at, warehouse, notes` | ~2% have `total_price ≠ quantity*unit_price`; **208 rows** have `updated_at < created_at`; **507 rows** have `quantity < 0` (returns, down to −49); **1,512 rows** have empty `warehouse` (loaded as NULL); XSS payloads (`<script>…`, `onmouseover=…`) in `notes` |
| `suppliers` | `id, name, email, rating, country, active, created_at` | `active` is `true`/`false` text → boolean; ~37 country codes including non-ISO variants (DEN, GER, BRZ); duplicate name variations |
| `products` | `id, name, category_id, sku, price` | `price` = catalog/base price; used by `price_consistency` & `price_spike` rules. **81 rows reference non-existent `cat_200`** — the importer NULL-coerces these `category_id`s at parse time so the FK stays enforced for genuine bugs (see §8.4) |
| `categories` | `id, name, parent_id` | Hierarchical; `parent_id` empty for roots; **real cycle in seed at `cat_150↔151↔152`** — must guard the recursive descent (Postgres self-FKs do NOT prevent cycles). `expected-values.json` asserts `counts.categories: 193` (vs. 195 rows in CSV); the API surface must filter 2 rows out — exact rule is TBD when category endpoint is implemented |

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

CREATE TABLE order_flags (
  order_id      varchar(16) PRIMARY KEY REFERENCES orders(id) ON DELETE CASCADE,
  flagged_at    timestamptz NOT NULL DEFAULT now(),
  source_job_id varchar(32) REFERENCES jobs(id),
  reason        text
);
```

- `version` is **internal**: never accepted on PATCH input, never emitted in responses. Used by the conditional UPDATE in §8.1.
- `parent_id` FK is `DEFERRABLE INITIALLY DEFERRED` — load order in COPY is unconstrained inside one tx; orphan parent_ids are rejected at commit. Cycles in the seed data still load (FKs don't prevent them) and must be guarded query-side.
- `jobs` row is the **mirror** of Redis live state — see §8.2.
- `order_flags` records manual flags placed via the bulk `flag` action. PK is `order_id` (one flag per order; re-flagging is a no-op via `ON CONFLICT DO NOTHING`). Independent from automatic anomaly detection — `GET /api/orders/anomalies` does not consider this table.

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
| GET | `/api/orders` | Paginated; default `limit=20`; supports filters (§6.2). Each row also carries the joined `product_name` (search filter operates on this column). |
| GET | `/api/orders/:id` | Joined `supplier_name`, `product_name`, and nullable `flagged_at` (set when the order has been manually flagged via bulk action); 404 if missing |
| PATCH | `/api/orders/:id` | Updates status/priority. 400 invalid status; 409 if already cancelled OR optimistic-lock conflict |
| GET | `/api/suppliers` | Paginated |
| GET | `/api/suppliers/:id` | Includes computed `order_count`, `total_revenue`; 404 if missing |
| GET | `/api/products` | Paginated; `?category=cat_001` filters recursively into children |

### 6.2 Filters on `GET /api/orders`

`status` (single or comma-list), `priority`, `supplier_id`, `warehouse`, `date_from`, `date_to` (inclusive on `created_at`), `min_total`, `search` (case-insensitive on joined `product_name`), `sort` + `order` (asc/desc), `limit`, `offset`. All combinable.

### 6.3 Aggregations

- `GET /api/orders/stats` — `total_orders`, `total_revenue`, `avg_order_value`, `by_status` (per-status `count` + `total_value`), `by_month` (24 entries chronological), `top_suppliers` (exactly 10), `by_warehouse` (5 warehouses + `"unassigned"` bucket).
- `GET /api/suppliers/:id/performance` — `avg_delivery_days` (mean of `updated_at − created_at` over delivered orders only), `rejection_rate` (rejected / total over all of the supplier's orders), `avg_order_value`, `monthly_trend` (24 entries zero-filled across the global date range), `price_consistency` (fraction of orders whose `unit_price` is within ±20% of the catalog `products.price`). 404 if supplier id is unknown.

### 6.4 Anomalies

`GET /api/orders/anomalies` returns `{ data: [{ order_id, anomaly_types[], severity }] }`.

**Required rules:** `price_mismatch`, `inactive_supplier`, `negative_quantity`, `timestamp_anomaly`.
**Bonus rules:** `price_spike`, `after_hours`, `risky_supplier`.

`severity` ∈ `{low, medium, high}` — classification logic is our call but must be deterministic and documented (will end up in `ANOMALY_STRATEGY.md`).

### 6.5 Bulk

- `POST /api/orders/bulk-action` → 202 `{ jobId }` in <500ms; async processing; actions: `approve`, `reject`, `flag`. 400 on empty `orderIds`, invalid action, or batch >10,000. Failures: non-existent IDs, already-cancelled orders.
- `GET /api/jobs/:id` → `{ status: processing|completed|failed, progress: { total, completed, failed } }`.

**Action semantics.** `approve` and `reject` change `orders.status` to `approved` / `rejected` via the same optimistic-lock UPDATE used by single PATCH (§8.1). `flag` writes a row to `order_flags`; it does NOT mutate `orders.*` and does NOT bump the order's `version`. The flag is exposed only on `GET /api/orders/:id` (as `flagged_at`); it is intentionally **not** projected into `/api/orders/anomalies`. Manual flagging is a separate concern from automatic anomaly detection — orthogonal persistence keeps both contracts clean.

**Three URLs, two wire formats — provisional, see `docs/pending-decisions.md` §1.** The bulk endpoint is exposed at `POST /api/orders/bulk-action` (camelCase, README §1.5 + `bulk-operations.test.ts`), `POST /api/orders/bulk` (camelCase, `realtime.test.ts` for the `bulk_completed` flow), and `POST /api/orders/bulk-actions` (snake_case, `concurrency.test.ts`). All three routes call the same `BulkService`. This dual-shape exists because three test files disagree on URL — and the README, on casing; all are authoritative. Pending PO review to collapse to one URL + one style.

### 6.6 Real-time (`/api/events`)

SSE-only (per §8.3); the test client (`tests/helpers/events-client.ts`) tries WebSocket first with a 3s timeout, then falls back to SSE — so SSE alone passes the suite. Response: `text/event-stream`, with `Cache-Control: no-cache, no-transform`, `Connection: keep-alive`, `X-Accel-Buffering: no`. Each frame is `data: <JSON>\n\n`; an initial `retry: 5000\n\n` hint and a `: ping\n\n` keepalive every 15 s keep proxies/load-balancers from severing idle connections.

Events:
- `order_updated` — `{ id, old_status, new_status, updated_at }` (snake_case, matches the rest of the API).
- `bulk_completed` — `{ jobId }` (**camelCase** — `realtime.test.ts:96` asserts `event.data.jobId` literally; tracked alongside the dual-casing wart in `docs/pending-decisions.md` §1).

Filter via `?supplier_id=...`. The filter applies to `order_updated` only (gated by the affected order's supplier_id). `bulk_completed` is broadcast to every subscriber regardless of filter, since a bulk job may legitimately span multiple suppliers and there is no obvious narrowing rule.

Implementation lives under `Features/Events/`: a singleton `EventHub` (bounded `Channel<EventEnvelope>(256)` per subscriber, `BoundedChannelFullMode.DropOldest` so a stalled client never wedges publishers, `IAsyncDisposable` for graceful shutdown) and `EventsController.Stream` which races a `PeriodicTimer(15s)` heartbeat against `ChannelReader.WaitToReadAsync` on a single `Response.Body` writer to avoid interleaving. The PATCH path (`OrdersService.UpdateStatusAsync`) publishes `order_updated` only when `oldStatus != newStatus`. The bulk worker publishes `bulk_completed` from `TryFinalize` on every terminal job state (success or failure). Both publish calls are wrapped in try/catch so an event-hub failure cannot break the write path.

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

- **Single PATCH:** `Order.Version` is mapped as `IsConcurrencyToken()`. The service loads the tracked entity, mutates `Status`/`UpdatedAt` and bumps `Version++`, then calls `SaveChangesAsync`. EF generates `UPDATE orders SET status=@p, updated_at=@p, version=@p_new WHERE id=@p AND version=@p_orig`. If 0 rows affected, EF throws `DbUpdateConcurrencyException`, which the repo catches and translates to `OptimisticConcurrencyException` (`409`). The version is **never** part of the API contract — tests send no version, and responses don't expose it.
- **Bulk worker:** for each batch chunk, the repository opens an EF transaction, runs `_db.Database.SqlQueryRaw<LockedRow>("SELECT id, status, version FROM orders WHERE id = ANY({0}) FOR UPDATE SKIP LOCKED", ids)` (no LINQ equivalent for the lock hint), then issues parameterized `ExecuteSqlRawAsync` UPDATE / INSERT … ON CONFLICT DO NOTHING per row. Orders held by an overlapping job's transaction are silently skipped — they are then counted as `completed` because the other job has already applied the same action. This satisfies the "each order processed exactly once across overlapping batches" invariant. No Redis lock on the write path.

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

- Schema is **owned by `schema.sql`**. EF Core reads/writes the existing tables through `AppDbContext` (mapped via `IEntityTypeConfiguration<T>` per entity) but does NOT own DDL — no migrations, no `EnsureCreated`. The Importer is the sole DDL author.
- Run as `dotnet run --project src/OrderOps.Importer`.
- Steps:
  1. Apply `schema.sql` — `DROP TABLE IF EXISTS … CASCADE` followed by `CREATE TABLE …`. The drop-and-create shape (rather than `CREATE TABLE IF NOT EXISTS` + `TRUNCATE`) means schema changes during development always take effect on the next import; no migration framework is needed for a take-home where the only data source is the CSVs themselves. **Schema ownership:** the file lives in `src/OrderOps.Api/schema.sql` (canonical home for DB DDL). The Importer's csproj uses `<None Include="..\OrderOps.Api\schema.sql" Link="schema.sql" CopyToOutputDirectory="Always" />` to ship it next to its binary. The Api itself never reads the file at runtime — it consumes the resulting tables.
  2. Open a single transaction and `SET CONSTRAINTS ALL DEFERRED` so the deferred `categories.parent_id` self-FK is only checked at commit (CSV row order is unconstrained).
  3. `COPY` each CSV via Npgsql `BeginBinaryImportAsync` in FK-safe order: categories → suppliers → products → orders. Fastest path for 50k orders. Empty CSV string fields → `NULL` for all nullable columns; `version` is seeded at `1`.
  4. Commit. Orphan `parent_id` rejection runs here. Cycles in seed data (`cat_150↔151↔152`) load fine — they remain a query-time concern (§5.1).
  5. `SELECT COUNT(*)` per table; exit non-zero if any count mismatches what was parsed.
- CSV parsing uses **CsvHelper** so quoted fields containing the seeded XSS payloads (`<script>…`, `onmouseover=…`) round-trip correctly.
- **Pre-COPY orphan fixup (products → categories):** before the products `COPY`, the importer builds a `HashSet<string>` of valid category IDs and rewrites any orphan `category_id` to `NULL`. The current seed has 81 such rows (all pointing at the missing `cat_200`); the importer logs the count. This preserves correctness — the FK remains enforced and the DB never holds dangling pointers — at the price of erasing the original literal `cat_200` reference for those 81 products. The alternatives (drop the FK; insert a sentinel `cat_200` row) were rejected because the first weakens future bug-catching and the second invents data and would inflate the categories row count.
- Connection string is sourced from the Importer's `appsettings.json` (`ConnectionStrings:Postgres`); standard `Host.CreateApplicationBuilder` env-var override (`ConnectionStrings__Postgres`) applies.
- Re-running is safe and idempotent against the data files.

### 8.5 Validation & Errors
- Single error envelope `{ error, code }` rendered by `ExceptionHandlerMiddleware`. Stack traces are logged via `ILogger`, never echoed to clients.
- Input validation: query params (numbers, ISO-8601 dates, enum allow-lists), path params (ID format), JSON body shapes, length bounds (`notes` ≤ 4096, bulk `reason` ≤ 4096), `PATCH /api/orders/:id` requires at least one of `status` / `priority` / `notes` (`code: no_fields`).
- Sort field is mapped through a static allow-list (`OrderRepository.SortColumns`) — SQL identifiers are never interpolated from user input.
- Pagination clamps `limit` to `[1, 1000]`, `offset` to `[0, ∞)`. Negative or oversize values return 200 with the clamped page rather than 400.
- Unknown query params are ignored (forgiving). Unknown JSON body fields are also ignored — `System.Text.Json` skips them by default.
- All error responses set `Content-Type: application/json` and inherit the security headers from §8.6.

### 8.6 Security

Full threat model + control inventory in [`docs/SECURITY.md`](./SECURITY.md). Summary of controls:

- **Input validation** — enum allow-lists for `status` and `priority`, length caps on `notes` and bulk `reason`, ISO-8601 date parsing, pagination clamp.
- **SQL injection** — EF Core 9 parameterizes LINQ-generated queries; the five `FromSqlRaw` / `SqlQueryRaw` / `ExecuteSqlRawAsync` call sites all use `NpgsqlParameter` with explicit types. No string concatenation of user input into SQL exists in the codebase.
- **XSS** — CSV-imported XSS payloads in `notes` are stored verbatim and round-tripped as inert JSON string literals. The FE renders `notes` only via React's auto-escaped `{value}` interpolation.
- **HTTP hardening** — `SecurityHeadersMiddleware` sets `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Cross-Origin-Resource-Policy: same-origin` on every response (registered before the exception handler so error responses inherit them).
- **CORS** — named policy `frontend-dev` allows the Vite dev origin (`http://localhost:5173` by default; configurable via `Cors:AllowedOrigins`).
- **Body size** — Kestrel `MaxRequestBodySize = 1 MB`. 10000-element bulk requests are ~250 KB worst case, comfortably within the cap.
- **Resource bounds** — bulk batch ≤ 10000 IDs; SSE per-subscriber `Channel<EventEnvelope>(256)` with `DropOldest` so a stalled client cannot wedge publishers.
- **Concurrency** — `Order.Version` `IsConcurrencyToken` prevents lost-update races (one 200 / one 409 on simultaneous PATCH).
- **Out of scope** — auth/authz, rate limiting, HTTPS/HSTS — see `docs/SECURITY.md` for "what we'd add for production".

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
- [x] **2026-05-05** — `OrderOps.Importer` shipped; schema applied; 195 categories / 500 suppliers / 5,000 products / 50,000 orders loaded into Postgres via binary COPY. CSV parsing via CsvHelper.
- [x] **2026-05-05** — `OrderOps.Web` scaffold shipped; Vite + React + TS + Tailwind v3.4 + shadcn/ui + TanStack Query + Recharts + React Router. Vite dev proxy `/api → :3000` verified end-to-end through `/api/healthz` (added to API alongside `/healthz`). Placeholder pages render at all 4 routes; `npm run build` and `tsc --noEmit` pass clean.
- [x] **2026-05-05** — Theme + design rules locked. `docs/ui-design.md` written (binding); slate + emerald palette, 10px radius, Inter typeface, soft shadows, 150ms transitions. `ThemeProvider` + tri-state toggle (light/dark/system) in the nav with `localStorage` persistence and pre-paint flash-prevention script in `index.html`. `Skeleton` primitive added; `OrdersPage` uses it for the loading state.
- [x] **2026-05-05** — Basic-CRUD slice landed. Backend foundation in place: feature folders (`Features/Orders|Suppliers|Products`) + `Infrastructure/`, snake_case JSON, error-envelope middleware (`{error, code}`), DI for `NpgsqlDataSource` (singleton, pooled) and `IConnectionMultiplexer` (lazy reconnect, no consumers yet), Dapper `MatchNamesWithUnderscores=true`. 7 endpoints implemented per §6.1 (orders list/detail/PATCH with full §8.1 optimistic-lock; suppliers list/detail with computed `order_count`/`total_revenue`; products list with cycle-guarded recursive category descent — `id::text` cast on the CTE path to satisfy PG type unification). `tests/basic-crud.test.ts` 15/15 green; build clean.
- [x] **2026-05-05** — Filtering slice landed on `GET /api/orders`. Bound `OrderListRequest` (snake_case via `[FromQuery(Name=…)]`); service parses status comma-list, ISO dates (`AssumeUniversal | AdjustToUniversal`), trims string filters, normalizes pagination; repository builds parameterized WHERE dynamically (`status = ANY(@statuses)`, `priority`, `supplier_id`, `warehouse`, `created_at >= @date_from`, `created_at < @date_to + interval '1 day'`, `total_price >= @min_total`, `p.name ILIKE '%' || @search || '%'`). Sort whitelist (`id, created_at, updated_at, total_price, unit_price, quantity, status, priority, supplier_id, warehouse`) maps API field → SQL column; unknown values fall back to `o.id`. List rows now include `product_name` (always-on `JOIN products`); count query joins products only when `search` is set. `tests/filtering.test.ts` 10/10 green; basic-crud 15/15 still green; build clean.
- [x] **2026-05-05** — Aggregations slice landed. Two endpoints added: `GET /api/orders/stats` (single `QueryMultipleAsync` returning totals + `by_status` map + 24-month series + top-10 suppliers + `by_warehouse` with `unassigned` bucket via `COALESCE(warehouse, 'unassigned')`) and `GET /api/suppliers/{id}/performance` (per-supplier `total_orders`, `avg_delivery_days = avg(updated_at - created_at) FILTER (status='delivered')`, `rejection_rate = rejected/total`, `avg_order_value`, 24-entry `monthly_trend` zero-filled via `generate_series` over the global date range, `price_consistency = within ±20% of catalog `products.price` / total`). Frontend `StatsPage` replaced its placeholder with a Recharts dashboard (KPI tiles, monthly line, status donut, warehouse horizontal bar, top-10 supplier list). Caching deferred to the performance slice per §8.7. `tests/aggregations.test.ts` 12/12 green against a fresh seed; basic-crud 15/15 + filtering 10/10 still green; both backend and frontend builds clean. **Test-ordering caveat:** `basic-crud.test.ts` PATCHes the first pending order to `approved` per run, so running it before `test:agg` skews counts by 1 — re-import between full-suite runs.
- [x] **2026-05-05** — Anomalies slice landed (backend). `GET /api/orders/anomalies` returns `{ data: [{ order_id, anomaly_types, severity }] }` per README §1.4 (no extras). Single SQL pass joins orders + suppliers + products and projects matched rules into a `text[]` via `array_remove(ARRAY[CASE…], NULL)`. Seven rules: 4 required (`price_mismatch` with 0.01 tolerance, `negative_quantity`, `timestamp_anomaly`, `inactive_supplier`) + 3 bonus (`price_spike` at >1.5× catalog, `after_hours` at UTC hour <8 OR ≥18, `risky_supplier` at `rating ≤ 1.5`). Severity computed in C# from the matched-types array per §8.8 rubric. `AnomalyRow` is a settable-property class because Dapper rejects positional-record binding when Npgsql exposes the `text[]` column as `System.Array`. Full strategy + observed seed counts written to `docs/ANOMALY_STRATEGY.md`. `tests/anomalies.test.ts` 8/8; basic-crud 15/15, filtering 10/10, aggregations 12/12 still green against fresh seed; build clean. Caching deferred to performance slice per §8.7.
- [x] **2026-05-05** — Performance slice landed (no code changes). All 8 tests in `tests/performance.test.ts` pass against the EF-refactored data layer with no Redis caching layer added: `/api/orders` default <100ms p95, `?status=pending&sort=created_at` <200ms, `?search=hydraulic` <300ms (trigram index on `products.name`), `/api/orders/stats` ~570ms peak with 500ms p95 budget (just under), `/api/suppliers/sup_042/performance` <500ms, `/api/orders/anomalies` ~1500ms peak with 1000ms p95 (single SQL scan with array projection). Data-completeness tests confirm `orders.total = 50000`, `suppliers.total = 500`. Caching layer per §8.7 remains documented but deferred — Postgres + the existing indexes (status, priority, supplier_id, warehouse, created_at, total_price btree + name gin trgm) carry the budgets without it. **73/83 automated tests passing.**

- [x] **2026-05-05** — Concurrency slice landed (no new mechanism). The optimistic-lock + `FOR UPDATE SKIP LOCKED` machinery from prior slices already covered the 10 tests in `tests/concurrency.test.ts`; the only code change was a second route alias `POST /api/orders/bulk-actions` (plural, snake_case) on `BulkController` to satisfy the test suite's choice of URL/format, alongside the existing `POST /api/orders/bulk-action` (singular, camelCase). New `BulkActionsRequest` / `BulkActionsResponse` DTOs use the global snake-case policy with no `[JsonPropertyName]` overrides. Both routes share `BulkService.EnqueueAsync`. Decision documented in `docs/pending-decisions.md` §1 for PO review. `tests/concurrency.test.ts` 10/10; basic 15/15, filter 10/10, agg 12/12, anomaly 8/8, bulk 10/10 still green against fresh seed; build clean. **65/73 automated tests passing.**

- [x] **2026-05-05** — Security slice landed. All 5 tests in `tests/security.test.ts` were already green against the EF-refactored code (every assertion hits an existing control: `OrderStatuses.IsValid`, `Pagination.Normalize` clamp, `BulkService.MaxBatchSize`, JSON error envelope, unbounded `reason` accepted as 202). The slice instead added the production hygiene that satisfies the qualitative-review column: (1) `PATCH /api/orders/:id` extended to accept `priority` and `notes` per README §6.1, with new `OrderPriorities` allow-list (`critical, high, medium, low`), 4 KB cap on `notes`, "at least one field required" rule (`code: no_fields`); `OrderRepository.UpdateAsync` captures `oldStatus` once and applies all provided fields under a single optimistic-lock UPDATE; `order_updated` event still publishes only when `oldStatus != newStatus`. (2) Bulk `reason` field length-capped at 4 KB. (3) `Infrastructure/SecurityHeadersMiddleware` sets `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Cross-Origin-Resource-Policy: same-origin` on every response — registered *before* `ExceptionHandlerMiddleware` so error envelopes inherit them. (4) Kestrel `MaxRequestBodySize = 1 MB`. (5) Named CORS policy `frontend-dev` reading `Cors:AllowedOrigins` from configuration (defaults to `http://localhost:5173`); applied globally between `UseRouting` and `MapControllers`. New `docs/SECURITY.md` deliverable documents the threat model, controls, known limitations, and production follow-ups. `tests/security.test.ts` 5/5; full regression 78/78 still green against fresh seed; build clean. **83/83 automated tests passing.**

- [x] **2026-05-05** — Realtime slice landed. New `Features/Events/` folder: singleton `EventHub` (bounded per-subscriber `Channel<EventEnvelope>(256)`, `DropOldest` overflow policy, `IAsyncDisposable` for graceful shutdown) + `EventsController.Stream` serving SSE at `GET /api/events`. Headers set per spec (`Content-Type: text/event-stream`, `Cache-Control: no-cache, no-transform`, `Connection: keep-alive`, `X-Accel-Buffering: no`); initial `retry: 5000` hint; `: ping` heartbeat every 15s; single-writer event/heartbeat race via `Task.WhenAny`; client cancellation linked with `IHostApplicationLifetime.ApplicationStopping`; subscriber removed in a `finally` block on every exit path. PATCH path publishes `order_updated` (snake_case payload) when `oldStatus != newStatus`; `UpdateStatusOutcome.Updated` extended with `OldStatus` so the prior status survives the EF mutation. Bulk worker publishes `bulk_completed` (`{ jobId }` — camelCase via `[JsonPropertyName("jobId")]`) from `TryFinalize` on every terminal state. Third bulk route alias `POST /api/orders/bulk` added to satisfy `realtime.test.ts:89`; reuses canonical camelCase `BulkActionRequest`. Pending-decisions §1 expanded to the three-URL reality. `tests/realtime.test.ts` 5/5; basic 15/15, filter 10/10, agg 12/12, anomaly 8/8, bulk 10/10, concurrency 10/10, perf 8/8 still green; build clean. **78/83 automated tests passing.**

- [x] **2026-05-05** — Data layer migrated from Dapper to **Entity Framework Core 9** (`Npgsql.EntityFrameworkCore.PostgreSQL` 9.0). New `Data/AppDbContext.cs` with `DbSet<Order|Supplier|Product|Category|Job|OrderFlag>`; entity classes in `Data/Entities/*` and explicit column maps in `Data/Configurations/*`. `Order.Version` is configured `IsConcurrencyToken()`, replacing the manual `WHERE version=@v` UPDATE pattern; `OrderRepository.UpdateStatusAsync` now bumps `Version++` and catches `DbUpdateConcurrencyException` → `OptimisticConcurrencyException`. Repositories rewritten LINQ-first; `FromSqlRaw` / `SqlQueryRaw` / `ExecuteSqlRawAsync` retained only for: anomaly array projection, recursive category descent CTE, `FOR UPDATE SKIP LOCKED` chunk read, and `ON CONFLICT DO NOTHING` flag insert. `SupplierRepository.GetPerformanceAsync` now does the 24-month zero-fill in C# (the seed range is locked to 2023-01 → 2024-12 per §6.3) instead of `generate_series`. `OrderRepository.GetStatsAsync` is 5 sequential LINQ queries (was 1 Dapper `QueryMultipleAsync` with 5 result sets) — same Postgres work, +4 round trips; perf-slice Redis cache will neutralize this. `IOrderRepository` / `ISupplierRepository` / `IProductRepository` / `IBulkRepository` interfaces unchanged — services don't move. `Dapper` package removed (`dotnet remove package Dapper`); `MatchNamesWithUnderscores` flag deleted; column mapping is now explicit per property. Build clean (0 warnings). All 5 prior suites still 55/55 against fresh seed: basic 15/15, filter 10/10, agg 12/12, anomaly 8/8, bulk 10/10. Importer is unaffected (still uses Npgsql binary `COPY`; `schema.sql` remains the DDL source per §8.4).

- [x] **2026-05-05** — Bulk operations slice landed (backend). Two endpoints: `POST /api/orders/bulk-action` (202 `{ jobId }` in well under 500ms — validates `orderIds` non-empty, ≤10000, `action` ∈ {approve, reject, flag}; INSERTs `jobs` row + `HSET job:{id}` + writes to in-process `Channel<BulkJob>`; returns) and `GET /api/jobs/:id` (Redis-primary read with Postgres `jobs` mirror fallback, 404 if neither has it). `BulkWorker : BackgroundService` drains the channel one job at a time, processes in chunks of 200 with per-chunk transactions: `SELECT id FROM orders WHERE id = ANY(@ids)` (existence) → `SELECT id, status, version FROM orders WHERE id = ANY(@ids) FOR UPDATE SKIP LOCKED` (lock) → per-row UPDATE for approve/reject (version-conditional, +1 on each), or `INSERT INTO order_flags … ON CONFLICT DO NOTHING` for flag. Buckets: completed (rowcount=1) / completed_overlap (locked-by-other-tx) / completed (flag) → `progress.completed`; cancelled / nonexistent / version-conflict → `progress.failed`. Final job status: `failed` iff `completed==0 && failed==total`, else `completed`. New `order_flags` table (PK = `order_id`, FKs to orders + jobs); `OrderDetail` DTO gains `FlaggedAt`; `OrderRepository.GetByIdAsync` and the post-update SELECT in `UpdateStatusAsync` LEFT JOIN `order_flags`. `IConnectionMultiplexer` gets its first consumer (`RedisJobStateStore`). Bulk DTOs use `[JsonPropertyName]` for camelCase (only endpoint that does). `tests/bulk-operations.test.ts` 10/10; basic 15/15, filter 10/10, agg 12/12, anomaly 8/8 still green against fresh seed; build clean. Caching deferred to perf slice per §8.7. Worker resumes from a graceful restart only via the Postgres mirror (live Redis state for in-flight jobs is not recovered) — acceptable per §8.2.

- [x] **2026-05-05** — Suppliers FE wiring landed. `features/suppliers/api.ts` added with `listSuppliers`/`getSupplier`/`getSupplierPerformance` (snake_case types matching server). `/suppliers` is now a paged table (25/page) with id/name/country/email/rating/active columns, full skeleton/error/empty states, row click → `/suppliers/:id`. `/suppliers/:id` shows a header card (name, id, country, email, active badge, rating, total orders, total revenue), a performance card (5-stat grid + Recharts monthly-trend line), and a recent-orders table (top 10 by `created_at desc` via `listOrders({supplier_id})`) that opens the existing `OrderDetailSheet`. Cross-page navigation: `OrderDetailSheet` supplier_id is now a `<Link>` to `/suppliers/:id` (closes the sheet on click); `StatsPage` top-suppliers rows are clickable to detail. 404 on `getSupplier` renders a "Supplier not found" card; React Router router definitions unchanged. **Polish in the same slice:** OrdersFiltersBar now uses a real supplier picker (`<Select>` populated by `listSuppliers({ limit: 500 })`, name-sorted, 5-min `staleTime`) instead of a free-text supplier_id input. `OrderDetailSheet` gained an "Update status" section: `useMutation` calling `patchOrder`, options exclude the current status, on success it `setQueryData` for `["order", id]` and invalidates `["orders"]`; PATCH errors (409 version_conflict, already_cancelled, validation) render inline as `${message} (${code})` per ui-design §2.4. No backend changes; frontend `tsc -b` + `vite build` pass clean. **Still outstanding per README Part 2:** multi-select rows + bulk-action UX on the Orders Table — gated on the bulk-operations backend slice.

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
