# Testing protocol

The 83-test suite in `tests/` is the authoritative spec for the API. This document explains how to run it correctly, the test-ordering hazard that has bitten us, and the one-liner to avoid it.

> **TL;DR.** Don't `cd tests && npm test`. The suite's expected values are computed against a pristine seed; running all 9 files in one shot mutates the DB mid-suite and produces false failures. Re-import between every file.

---

## Why a single `npm test` doesn't pass cleanly

`tests/expected-values.json` was generated against the freshly-imported CSV — exact counts (`shipped=5845`, `approved=9940`, …) and exact totals.

Three test files mutate the database:

- `basic-crud.test.ts` — PATCHes one pending order → approved (test 4).
- `bulk-operations.test.ts` — submits 1000-order bulk approvals (Scale tests).
- `concurrency.test.ts` — submits multiple overlapping bulk jobs.

`realtime.test.ts` also writes (PATCH to trigger an `order_updated` SSE event).

`aggregations.test.ts` uses strict equality (`expect(stats.by_status[s].count).toBe(N)`) against the pristine values. The moment basic-crud's single PATCH lands, `approved` becomes `9941` and aggregations fails with "expected 9941 to be 9940". Cascade from there: the parallel default of vitest 2.0 lets bulk and concurrency mutate the first-page pending orders before basic-crud's PATCH test even reads them, so basic-crud also fails.

This is documented in `docs/ai-architecture.md` §9 as a known caveat. **The fix is not in the code — it's in how you invoke the suite.**

---

## Run-it-right one-liner

From the repo root, with Postgres + Redis up (`docker compose up -d`) and the API running on port 3000 (`dotnet run --project src/OrderOps.Api`):

```bash
for f in basic-crud filtering security aggregations anomalies bulk-operations performance concurrency realtime; do
  dotnet run --project src/OrderOps.Importer > /dev/null
  cd tests && npx vitest run "$f" 2>&1 | sed 's/\x1b\[[0-9;]*m//g' | grep -E "^[[:space:]]+Tests[[:space:]]+" | head -1 | sed "s|^|  $f → |"
  cd ..
done
```

Expected output (one line per file, all green):

```
  basic-crud →       Tests  15 passed (15)
  filtering →       Tests  10 passed (10)
  security →       Tests  5 passed (5)
  aggregations →       Tests  12 passed (12)
  anomalies →       Tests  8 passed (8)
  bulk-operations →       Tests  10 passed (10)
  performance →       Tests  8 passed (8)
  concurrency →       Tests  10 passed (10)
  realtime →       Tests  5 passed (5)
```

**Total: 83/83 (115/115 pts).** Wall time ≈ 35–45 s on a warm machine: ~22 s of imports + ~12–20 s of tests.

PowerShell equivalent:

```powershell
foreach ($f in 'basic-crud','filtering','security','aggregations','anomalies','bulk-operations','performance','concurrency','realtime') {
  dotnet run --project src/OrderOps.Importer | Out-Null
  Push-Location tests
  $out = npx vitest run $f 2>&1 | Out-String
  Pop-Location
  $line = ($out -replace "`e\[[0-9;]*m","") -split "`n" | Where-Object { $_ -match '^\s+Tests\s+' } | Select-Object -First 1
  Write-Host "  $f → $line"
}
```

---

## Per-category invocation

When iterating on a single feature, run only its category. No re-import is needed if you start from a fresh DB and only touch one file:

```bash
dotnet run --project src/OrderOps.Importer
cd tests
npm run test:basic       # basic-crud (15)
npm run test:filter      # filtering (10)
npm run test:agg         # aggregations (12)
npm run test:anomaly     # anomalies (8)
npm run test:bulk        # bulk-operations (10)
npm run test:concurrent  # concurrency (10)
npm run test:perf        # performance (8)
npm run test:realtime    # realtime (5)
npm run test:security    # security (5)
```

Re-import again before switching to a different category that the previous one mutated.

---

## Vitest version gotcha

`tests/package.json` pins `"vitest": "^2.0.0"`, installed in `tests/node_modules/`. Running `npx vitest` **from the repo root** (outside `tests/`) bypasses the local install and pulls the latest from the registry — currently 4.x — which has different defaults and flag names. Always invoke from inside `tests/`, or call the local binary explicitly: `tests/node_modules/.bin/vitest run …`.

---

## Common gotchas

| Symptom | Likely cause | Fix |
|---|---|---|
| `Address already in use` from `dotnet run` | An older API instance is still bound to port 3000 | `Get-Process dotnet \| Stop-Process` (PowerShell) or `lsof -i :3000` then kill |
| Tests pass individually but fail when chained | DB carrying mutations from the previous file | Re-import between files |
| `aggregations.by_status` off by 1 (e.g. 9941 vs 9940) | basic-crud ran first without re-import | Re-import |
| `aggregations.shipped` off by 21 / large diffs in approved | Manual UI bulk actions ran against the test DB | Re-import; do manual sweeps on a separate DB or after the suite, not before |
| basic-crud test 4 returns 409 instead of 200 | Something else is concurrently PATCHing the same order (vitest 2.0 file-level parallelism) | Run files one at a time; do not `cd tests && npm test` |
| `realtime.test.ts > order_updated` times out at 5 s | API stayed up across a previous failed run that stale-cached an SSE connection; or the API was off | Restart the API |
| `Job not found` on `/api/jobs/:id` polling tests | Redis was flushed between API start and test | Don't `redis-cli FLUSHDB` while a job is in flight |

---

## Smoke-only mode

If you just want a fast confidence check before committing — re-import once, run the read-mostly subset:

```bash
dotnet run --project src/OrderOps.Importer
cd tests
npx vitest run filtering security anomalies performance
```

That's 31 read-only tests in ~5 s. Add `basic-crud aggregations` if you've changed read-path code; re-import between them.

---

## When to re-generate `expected-values.json`

Don't. The file is checked in by the assignment authors and is read-only along with the rest of `tests/`. If a test failure points at a value mismatch, the bug is in the API or the seed handling — never in the expected file.
