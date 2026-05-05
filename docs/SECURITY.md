# Security

## Threat model

This project is a take-home assignment with a single trusted operator running locally. The threat model is therefore narrower than a production deployment, but the controls are sized so that the same code could be promoted to a real environment without rearchitecting the security boundary.

| Asset                 | Concern                                                                              | In-scope here? |
|-----------------------|--------------------------------------------------------------------------------------|----------------|
| Postgres data         | SQL injection via API inputs                                                         | Yes            |
| API responses         | Reflected XSS via stored CSV payloads (`<script>…</script>` in `notes`)              | Yes            |
| Job queue / DB writes | DoS via unbounded payloads                                                           | Yes            |
| Connection metadata   | Header-injection / response-splitting via crafted user input                         | Yes            |
| Static assets         | Click-jacking / mime-sniffing                                                        | Yes            |
| Operator traffic      | Network eavesdropping (HTTPS / HSTS)                                                 | Deferred       |
| Authn / Authz         | Multi-user access control                                                            | Deferred       |
| Abuse                 | IP / per-user rate limiting                                                          | Deferred       |

## Controls in place

### Input validation
- **Status / priority** are checked against a fixed allow-list (`OrderStatuses`, `OrderPriorities`). Anything else returns 400 with `{error, code}`.
- **`PATCH /api/orders/:id`** rejects empty bodies with `code: "no_fields"` (400). At least one of `status`, `priority`, `notes` is required.
- **`notes`** is capped at 4096 characters; oversize → 400 `notes_too_long`. Notes content itself is stored verbatim — see "Output encoding" below for why that's safe.
- **Bulk `reason`** is capped at 4096 characters → 400 `reason_too_long`.
- **Bulk `orderIds`** must be non-empty and ≤ 10000 → 400.
- **List filters** parse ISO-8601 dates with explicit `AssumeUniversal | AdjustToUniversal`; bad strings → 400 `invalid_date`.
- **Pagination** clamps `limit` to `[1, 1000]` (defaults to 20) and `offset` to `[0, ∞)` so negative/oversize values don't throw or DoS the database. See `Infrastructure/PagedResult.cs`.
- **Sort field** is mapped through a static allow-list (`OrderRepository.SortColumns`); unknown values fall back to `id`. SQL identifiers are never interpolated from user input.

### Database access
- **EF Core 9** parameterizes every LINQ-generated query.
- The five `FromSqlRaw` / `SqlQueryRaw` / `ExecuteSqlRawAsync` call sites (anomaly array projection, recursive category CTE, `FOR UPDATE SKIP LOCKED` chunk read, per-row chunk UPDATE / `INSERT ... ON CONFLICT DO NOTHING`) all use `NpgsqlParameter` with explicit `NpgsqlDbType` and `DBNull.Value` for nullable parameters. **No string concatenation of user input into SQL exists.**
- Connection string lives in `appsettings.json` and is overridable via `ConnectionStrings__Postgres` env var; secrets are never committed.

### Output encoding
- All responses set `Content-Type: application/json`. The default `System.Text.Json` encoder escapes the JSON-significant characters (`"`, `\`, control codes); browsers will not interpret a JSON response as HTML, so XSS payloads stored in `notes` round-trip as inert string literals.
- The frontend renders `notes` only via React's `{value}` interpolation (auto-escaped) and never via `dangerouslySetInnerHTML`.

### HTTP hardening
Set on every response by `SecurityHeadersMiddleware` (registered before the exception handler so error responses inherit them):
- `X-Content-Type-Options: nosniff` — prevents browsers from MIME-sniffing JSON as HTML.
- `X-Frame-Options: DENY` — defends against click-jacking via embedded iframes.
- `Referrer-Policy: no-referrer` — keeps API URLs out of cross-origin Referer headers.
- `Cross-Origin-Resource-Policy: same-origin` — opts the API out of cross-origin reads from foreign documents.

### CORS
- Named policy `frontend-dev` allows the Vite dev origin (`http://localhost:5173`) by default. Origins are config-driven via `Cors:AllowedOrigins` in `appsettings.json` so deployments can override without code changes.
- `AllowAnyHeader().AllowAnyMethod()` for simplicity; the surface is already constrained by the controllers' verb decorators.

### Error envelope
- Every error is rendered through `ExceptionHandlerMiddleware` as `{ "error": "...", "code": "..." }` with the appropriate status. Stack traces are logged via `ILogger` but never echoed to clients.

### Concurrency control
- `Order.Version` is mapped as `IsConcurrencyToken()`; concurrent PATCHes resolve to one 200 + one 409. This is correctness more than security, but it neutralizes a class of write-tampering races.

### Resource limits
- **Request body size** is capped at 1 MB via `KestrelServerOptions.Limits.MaxRequestBodySize`. Oversize requests return 413.
- **Bulk batch size** is capped at 10000 IDs per job (assignment requirement).
- **SSE subscriber channel** is bounded at 256 events per client with `BoundedChannelFullMode.DropOldest`, so a stalled consumer cannot wedge publishers (`Features/Events/EventHub.cs`).

### Logging
- Structured logging (`_log.LogInformation("msg {OrderId}", id)`) — message templates are constants; user input goes through structured properties, not format strings, so log injection / format vulnerabilities do not apply.

## Known limitations

| Limitation                                          | Why deferred                                                                                                                                                |
|-----------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------|
| No authentication or authorization                  | Out of README scope. Single trusted operator on local Docker.                                                                                               |
| No rate limiting                                    | Out of scope. Would need IP-keyed token-bucket via Redis to be useful behind a load balancer.                                                               |
| No HTTPS / HSTS                                     | Local dev runs HTTP only. Reverse proxy (nginx / caddy / cloud LB) would terminate TLS in production.                                                       |
| `Cors:AllowedOrigins` defaults to one dev origin    | Production deploys must override via env var or appsettings.                                                                                                |
| 413 from Kestrel doesn't go through the JSON envelope | Body-size violations short-circuit the pipeline before our exception middleware runs. Acceptable: clients see a 413 with framework-default text content. |
| No audit trail beyond the `jobs` table              | The `jobs` mirror is per-bulk; single PATCHes are not durably logged. A production deployment would add structured audit events.                            |

## What we'd add for production

- ASP.NET Core authentication (JWT bearer or OIDC); per-route `[Authorize]` policies.
- Rate limiting middleware (`Microsoft.AspNetCore.RateLimiting`) keyed on IP and authenticated principal, backed by Redis.
- HTTPS enforcement (`UseHttpsRedirection`) + `Strict-Transport-Security` header at the proxy.
- ASP.NET Core Data Protection with key persistence in Redis or an external KMS.
- Content Security Policy at the FE host (out of scope for the API, but governs how `notes` ultimately renders).
- Structured audit log (e.g. `Serilog` to Elasticsearch) for every state-changing call.
- Production CORS allow-list with explicit origins; `WithExposedHeaders` reduced to what the FE actually reads.

## Where things live

| Concern                  | File                                                                          |
|--------------------------|-------------------------------------------------------------------------------|
| Status/priority/notes validation | `src/OrderOps.Api/Features/Orders/OrdersService.cs`                   |
| Allowed enums            | `Features/Orders/OrderStatuses.cs`, `Features/Orders/OrderPriorities.cs`     |
| Bulk size + reason caps  | `src/OrderOps.Api/Features/Bulk/BulkService.cs`                              |
| Sort field whitelist     | `src/OrderOps.Api/Features/Orders/OrderRepository.cs` (`SortColumns`)        |
| Error envelope           | `src/OrderOps.Api/Infrastructure/ExceptionHandlerMiddleware.cs`              |
| Security headers         | `src/OrderOps.Api/Infrastructure/SecurityHeadersMiddleware.cs`               |
| CORS policy              | `src/OrderOps.Api/Infrastructure/ServiceCollectionExtensions.cs` + `Program.cs` |
| Body-size cap            | `src/OrderOps.Api/Program.cs` (`ConfigureKestrel`)                           |
| Pagination clamp         | `src/OrderOps.Api/Infrastructure/PagedResult.cs`                             |
| SSE channel bound        | `src/OrderOps.Api/Features/Events/EventHub.cs`                               |
