# Manual UI test plan

A walkthrough you can follow end-to-end to verify every functional area of the dashboard. Pair with `tests/` for the automated coverage; this doc is for the UX & polish points the test suite can't see.

> **Estimated time.** 15–20 minutes for the full sweep. Each section is independent — if you only changed one feature, jump to its section.

---

## 0. Setup

Start from a clean slate. **Do this every time** before the manual sweep, or your aggregation counts will be off.

```bash
docker compose ps                                # postgres + redis must be healthy
dotnet run --project src/OrderOps.Importer       # 50,000 orders re-imported
dotnet run --project src/OrderOps.Api            # API on http://localhost:3000
npm --prefix src/OrderOps.Web run dev            # FE on http://localhost:5173
```

Open `http://localhost:5173`. You should land on `/orders` (the index redirects there).

**Sanity:**
- Browser tab title reads `Orders · OrderOps`.
- Top nav shows the OrderOps logo (links back to `/orders`), three nav items (Orders / Analytics / Suppliers), and the theme toggle on the right.
- Header card on the page shows "50,000 total" in the top-right.

---

## 1. Theme & navigation chrome

| # | Action | Expected |
|---|---|---|
| 1 | Click the theme toggle (top-right) | Cycles light → dark → system. The active route's underline (the small primary-colored bar under the nav item) re-renders cleanly. |
| 2 | Hard-refresh the page (`Ctrl+R`) in dark mode | No white flash before the dark theme paints. (The flash-prevention script in `index.html` handles this.) |
| 3 | Click the OrderOps logo | Navigates to `/orders`, the wordmark stays highlighted in the nav. |
| 4 | Click any nav item, then back/forward in the browser | The underline animates to the active route; no full page reload. |
| 5 | Click a nav item while scrolled down a long page | After navigation, the new page is scrolled to the top. (Native back/forward preserves scroll position — only forward navigation jumps to top.) |

---

## 2. `/orders` — list, filters, sort, search

| # | Action | Expected |
|---|---|---|
| 1 | Land on `/orders` with no filters | Table renders 25 rows. Footer shows "Page 1 of 2,000". Header shows "1–25 of 50,000". |
| 2 | Click "Next" twice, then "Previous" once | Lands on page 2 of 2000, rows 26–50; "Previous" enables. |
| 3 | Click any column header (e.g. "Total") | Sort indicator (▲/▼) appears. Click again — direction flips. |
| 4 | Click a Status chip — e.g. "pending" — and another — "approved" | Both chips stay highlighted; results restrict to those two statuses; total count drops. |
| 5 | Click the active "pending" chip again | Deselects, only "approved" remains. |
| 6 | Click any Priority chip — e.g. "critical" | Combines with status filter; result count drops further. |
| 7 | Type "hydraulic" in the search box | Debounced 300ms; result count drops to ≈600 after the network round trip. |
| 8 | Open "More filters" → pick a Warehouse | Result count refines. |
| 9 | "More filters" → Supplier picker | Dropdown is sorted alphabetically by name (not by id). Pick one — count drops. |
| 10 | "More filters" → Min total = 10000 + From = 2024-06-01 + To = 2024-06-30 | All four filters compose; check the result count is sensible (low). |
| 11 | Click "Clear all" | All filters reset, count returns to 50,000. |
| 12 | Apply a filter that matches nothing — e.g. search "xxxxxxx" | Empty state renders: inbox icon + "No orders match these filters." |
| 13 | Stop the API (`Ctrl+C` on the dotnet process), then click "Next" | Red error banner: "Failed to load orders". Restart the API — the next click recovers. |

---

## 3. `/orders` — order detail sheet & single-order PATCH

| # | Action | Expected |
|---|---|---|
| 1 | Click any row | Right-side sheet slides in. Status + priority badges render at the top. |
| 2 | Verify the sheet shows: Supplier (name + clickable id), Product, Pricing (qty / unit / total), Logistics, Timeline, optional Notes | All sections rendered. Notes containing seeded `<script>…` payloads render as plain text — never as HTML. |
| 3 | Click the supplier id link in the sheet | Sheet closes; lands on `/suppliers/:id`. |
| 4 | Re-open a `pending` order, pick "approved" in the "Update status" dropdown, click Apply | Sheet refreshes — status badge updates to "approved"; the row in the table behind also updates. |
| 5 | With the same order still open, change again — "approved" → "cancelled" → "Apply" | Status changes to cancelled. |
| 6 | Now try to change "cancelled" → "approved" | Inline error in red: `Order is already cancelled (already_cancelled)`. |
| 7 | Open a different order in two browser tabs simultaneously | Patch one to `approved` in tab A. In tab B (still showing the old state), pick a different status and Apply | Tab B shows: `… (version_conflict)`. Tab A's mutation is intact. |

---

## 4. `/orders` — multi-select bulk actions (the new feature)

This is the README Part 2 piece that landed last. Spend a few minutes here.

### 4.1 Selection model

| # | Action | Expected |
|---|---|---|
| 1 | Tick the checkbox in any row | Row dims slightly. A sticky bar appears above the table: "**1** selected" + "Approve / Reject / Flag / Clear". |
| 2 | Click the header checkbox | All 25 rows on the current page get selected. Header shows a check (not a dash). Bar reads "**25** selected". |
| 3 | Click "Next" to page 2 | Header checkbox shows the **dash** state (indeterminate) on page 2 because page 2 has zero selected. The bar still shows "25 selected" — selection persists across pages. |
| 4 | Tick 3 rows on page 2 | Bar reads "28 selected". |
| 5 | Go back to page 1 | All 25 rows still ticked. |
| 6 | Apply a status filter (e.g. status=approved) | Bar still says "28 selected" even if the visible rows now mostly show empty checkboxes — selection persists across filter changes. |
| 7 | Click "Clear" in the bar | All ticks gone, bar disappears. |
| 8 | Click a row's checkbox | Row gets selected. Click the row body (not the checkbox) | Sheet opens. Confirms the checkbox cell uses `stopPropagation`. |

### 4.2 Approve happy path

| # | Action | Expected |
|---|---|---|
| 1 | Filter `status=pending`, tick 5 rows on page 1 | Bar shows "5 selected". |
| 2 | Click **Approve** | AlertDialog modal: "Approve 5 orders?" + a 4096-char-capped reason textarea. |
| 3 | Type a short reason ("smoke test") | Counter shows "10 / 4,096". Confirm button enabled. |
| 4 | Click "Confirm approve" | Dialog closes. Selection clears. Bottom-right: a toast appears with a spinning loader, "Processing bulk job", a job id (`job_…`), and live progress bar. |
| 5 | Watch the toast for 1–2 s | Progress bar fills; counters update from 0/5 to 5/5 ok. Toast turns green: "Bulk job complete". |
| 6 | After ~8 s of inactivity, the toast auto-dismisses | (If you don't interact, it disappears.) |
| 7 | Re-apply `status=pending` | The 5 orders are gone from this view (they're approved now). |

### 4.3 Reject happy path

Same as 4.2 but click **Reject**. New status: `rejected`.

### 4.4 Flag (no status change)

| # | Action | Expected |
|---|---|---|
| 1 | Pick 3 pending orders, click **Flag** → Confirm | Toast progresses to terminal state: "3 ok". |
| 2 | Open one of those 3 orders | Status is still `pending` (flagging doesn't change status). |
| 3 | Look in the order detail sheet for `flagged_at` | The detail body shows the flagged timestamp on a flagged order. |

### 4.5 Failure handling

| # | Action | Expected |
|---|---|---|
| 1 | Pick a mix: 4 pending + 1 cancelled order | Bar reads "5 selected". |
| 2 | Click Approve → Confirm | Toast lands on "4 ok · 1 failed" and turns red on the failed counter. |
| 3 | A "Why some failed" link appears under the counts | Click it — popover opens **upward** with three bullet causes. Popover stays open until you click outside or click the link again. |
| 4 | Wait 8 s | The toast does **not** auto-dismiss (you've interacted with it). It stays until you click X. |
| 5 | Click the X | Toast closes. |

### 4.6 Validation paths

| # | Action | Expected |
|---|---|---|
| 1 | Try selecting > 10,000 orders (impractical with 25/page; you can simulate by setting a smaller cap in dev or by direct API call) | Action buttons disable, bar shows "Over the 10,000 cap — narrow the selection." |
| 2 | Click an action button with selection > cap | Confirm dialog refuses to submit; inline message: `Bulk batches are capped at 10,000 …`. |
| 3 | In the reason textarea, paste 5,000 characters | Native `maxLength` clamps to 4,096; counter shows "4,096 / 4,096". |
| 4 | Submit two bulk actions in quick succession (different selections) | Two toasts stack vertically bottom-right. Each tracks its own job. |

### 4.7 Real-time event arrival

| # | Action | Expected |
|---|---|---|
| 1 | Open browser DevTools → Network tab → filter "EventStream" | A single `events` request with `text/event-stream` is open while any toast is active. |
| 2 | Submit a bulk action | Within 1–2 s of completion, a `data: {"type":"bulk_completed",...}` frame arrives. |
| 3 | Toast moves to terminal state immediately on the SSE frame | Without waiting for the next 1-second poll tick. |
| 4 | Dismiss the toast | The EventSource connection closes (network tab shows the request finishing). |

---

## 5. `/stats` — analytics dashboard

> **Important:** if you ran §4 first, the dashboard counts will be skewed by your bulk actions. Re-import (`dotnet run --project src/OrderOps.Importer`) for clean numbers, OR accept the drift.

| # | Action | Expected |
|---|---|---|
| 1 | Navigate to `/stats` | Page title becomes "Analytics · OrderOps". |
| 2 | KPI tiles render: Total orders (50,000), Total revenue (≈$2.3B), Avg order value (≈$46K) | Skeletons during load, then formatted currency. |
| 3 | Monthly trend line chart | 24 points from 2023-01 to 2024-12. Hover any point — tooltip shows month + count + revenue. |
| 4 | Status distribution donut | 6 slices in distinct colors. |
| 5 | Warehouse horizontal bars | 6 bars (5 warehouses + "unassigned"). |
| 6 | Top suppliers list | Exactly 10 rows, descending by revenue. |
| 7 | Click any top-supplier row | Navigates to `/suppliers/:id` for that supplier. |

---

## 6. `/suppliers` — list

| # | Action | Expected |
|---|---|---|
| 1 | Click "Suppliers" in the nav | 25 rows of suppliers (id, name, country, email, rating, active). |
| 2 | Pagination footer | "Page 1 of 20" (500 / 25). Next/Previous work. |
| 3 | Click any row | Navigates to that supplier's detail page. |
| 4 | Suppliers with `null` rating | Rating column shows an em-dash, not "null" or empty. |
| 5 | "active=false" suppliers | Badge styled differently from active suppliers. |

---

## 7. `/suppliers/:id` — detail & performance

| # | Action | Expected |
|---|---|---|
| 1 | From `/suppliers`, click any row | Page title becomes "<Supplier name> · OrderOps". |
| 2 | Header card | Name, supplier id (mono), country, email, active state, rating, total orders, total revenue. |
| 3 | Performance card | 5-stat grid: avg delivery days, rejection rate (%), avg order value, price consistency (%), total orders. |
| 4 | Monthly trend mini-chart | 24 entries on the same x-axis as `/stats`, just for this supplier. Suppliers with sparse data still render a continuous line (zero-filled months). |
| 5 | "Recent orders" table | Top 10 most-recently-created. Click a row → order detail sheet opens. |
| 6 | In the sheet, click the supplier id link | Stays on the same page (already viewing it). |
| 7 | Manually navigate to `/suppliers/sup_does_not_exist` | "Supplier not found" card. |

---

## 8. Cross-cutting: loading, error, empty states

| Surface | Loading | Error | Empty |
|---|---|---|---|
| `/orders` table | Skeleton rows in the table body (8 placeholder rows) | Red banner above the table | Inbox icon + "No orders match these filters." |
| Order detail sheet | Three skeleton cards | Red banner inside sheet body | (Sheet only opens for known orders.) |
| `/stats` KPIs and charts | Skeleton tiles + charts | Red error banner + "Failed to load dashboard" | (Always populated for valid data.) |
| `/suppliers` table | Skeleton rows | Red banner | "No suppliers" (only on filter that matches nothing — currently no filter UI on this page; skip.) |
| `/suppliers/:id` | Skeletons | "Supplier not found" for 404; red banner for 5xx | (404 case handled.) |
| Bulk job toast | Spinner + "Processing bulk job" | "Lost contact with the job." inline | (Auto-dismisses on completion.) |

To force the error state on any page: stop the API (`Ctrl+C` on `dotnet run`) and trigger a refetch (refresh, change filters, etc.).

---

## 9. Security & resilience

| # | Action | Expected |
|---|---|---|
| 1 | Open any order whose `notes` contain a seeded XSS payload (search `?status=cancelled` if hard to find — many seeded ones; or look for `<script>` in raw API: `curl -s 'http://localhost:3000/api/orders/ord_00277'`) | The Notes section renders the literal characters `<script>alert(1)</script>` as text. No alert fires, no DOM injection. |
| 2 | Open DevTools → Console | No errors during normal navigation. SSE connections are clean. |
| 3 | DevTools → Application → Local Storage | One `theme` entry. No PII, tokens, or sensitive data. |
| 4 | DevTools → Network → response headers on any `/api/...` request | `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer`, `Cross-Origin-Resource-Policy: same-origin`. |
| 5 | Try a CORS-violating request from another origin (open `http://example.com` and run `fetch('http://localhost:3000/api/orders')` in its console) | Fails CORS preflight (`Access-Control-Allow-Origin` only allows `http://localhost:5173`). |

---

## 10. Backend-only paths (no UI surface)

These have no FE rendering yet — exercised via the automated suite or curl.

```bash
# Anomalies — README §1.4
curl -s 'http://localhost:3000/api/orders/anomalies' | head -c 500

# Products with recursive category descent — README §1.1
curl -s 'http://localhost:3000/api/products?category=cat_001&limit=5'

# Real-time order_updated event — open an SSE stream while you PATCH something in the UI
curl -N 'http://localhost:3000/api/events'
# (in another terminal: PATCH any pending order via the UI; you should see an `order_updated` frame.)

# Filtered SSE subscription
curl -N 'http://localhost:3000/api/events?supplier_id=sup_042'
# (only events whose order belongs to sup_042 arrive on this stream.)

# Bulk action via API directly
curl -X POST 'http://localhost:3000/api/orders/bulk-action' \
  -H 'Content-Type: application/json' \
  -d '{"orderIds":["ord_00100","ord_00200"],"action":"flag","reason":"manual"}'
```

---

## 11. Tear-down

When done:

- Stop the FE dev server (`Ctrl+C` on the `vite` process).
- Stop the API (`Ctrl+C` on the `dotnet` process).
- Postgres + Redis can stay running (they're containers; harmless idle).
- Re-import once more if you plan to rerun the automated suite next.

---

## What this plan does NOT cover

- Concurrency stress beyond the two-tab simultaneous-PATCH check (§3 step 7) — automated suite has 10 such tests.
- Performance budgets (p95 timings) — automated suite measures these.
- Anomaly rule correctness — automated suite covers all 7 rules.
- Schema integrity / referential constraints — `dotnet run --project src/OrderOps.Importer` would have failed if any FK was bad.
- Importer idempotency — re-running it back-to-back during this plan implicitly tests this.
