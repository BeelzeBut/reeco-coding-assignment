# Anomaly Strategy

`GET /api/orders/anomalies` runs a single SQL pass over `orders` joined to `suppliers` and `products`, flagging seven independent rules and projecting matched ones into a `text[]` per row. Severity is computed in C# from the matched-types array. Response shape is exactly `{ data: [{ order_id, anomaly_types, severity }] }` per the assignment spec — nothing else.

## Rules

| Rule | Predicate | Notes |
|---|---|---|
| `price_mismatch` | `abs(total_price − quantity × unit_price) > 0.01` | The 0.01 tolerance covers float rounding. |
| `negative_quantity` | `quantity < 0` | Returns are seeded as negative-qty rows. |
| `timestamp_anomaly` | `updated_at < created_at` | Impossible time travel. |
| `inactive_supplier` | `suppliers.active = false` | Active=false suppliers should not have new orders. |
| `price_spike` | `unit_price > products.price × 1.5` | Catalog price baseline; 50% above is a real outlier. |
| `after_hours` | `extract(hour from created_at) < 8 OR ≥ 18` (UTC) | `created_at` is `timestamptz`; UTC is the only honest no-tz-assumption window. |
| `risky_supplier` | `suppliers.rating ≤ 1.5` | NULL ratings are excluded by SQL three-valued logic and treated as not-risky (14 unrated suppliers). |

Three rules (`price_spike`, `after_hours`, `risky_supplier`) are bonus per the spec; the other four are required.

## Severity rubric

Computed deterministically from the matched-types array:

- **high** — any of `negative_quantity`, `price_mismatch`, `timestamp_anomaly`; **OR** `risky_supplier` matched; **OR** ≥3 types matched on a single order.
- **medium** — any of `inactive_supplier`, `price_spike`; **OR** exactly 2 types matched.
- **low** — single match of `after_hours`.

Rationale: financial-integrity bugs (negative quantity, broken `total = qty × unit`, impossible timestamps) directly affect reported revenue and audit accuracy → **high**. Governance and pricing-control concerns (using an inactive supplier, paying 1.5× catalog) are operational risks → **medium**. After-hours alone is an informational signal → **low**. Risky-supplier orders are escalated because the supplier's track record is itself the alarm. Multi-rule matches escalate one tier on the assumption that correlated anomalies on a single order are stronger evidence of underlying data corruption.

### Worked examples

- `["price_mismatch", "negative_quantity"]` → 2 types, but `price_mismatch` is in the high-single set → **high**.
- `["after_hours"]` → single type, `after_hours` is the only "low" rule → **low**.
- `["after_hours", "inactive_supplier"]` → exactly 2 types → **medium**.
- `["price_spike", "after_hours", "risky_supplier"]` → 3 types AND `risky_supplier` matched → **high**.

## Patterns observed in the seed (50,000 orders, 500 suppliers)

| Rule | Match count |
|---|---:|
| `price_mismatch` | 1,489 |
| `negative_quantity` | 507 |
| `timestamp_anomaly` | 208 |
| `inactive_supplier` | 2,247 |
| `price_spike` | 1,486 |
| `after_hours` | 29,158 |
| `risky_supplier` | 5,974 |

Total flagged orders: **33,648** (≈67% of the dataset). Severity split: 7,495 high · 3,042 medium · 23,111 low.

The dominant signal is `after_hours` (29,158 / 33,648 = 87%). Alone, this is informational. The high-severity bucket is concentrated in financial-integrity bugs: 1,489 price mismatches and 507 negative quantities are the bulk of the 7,495 high-severity orders. Note that many `price_spike` orders also flag `price_mismatch` because the seeded spike was applied to `unit_price` without recomputing `total_price` — that's expected and is correctly captured in two ways.

## What I'd improve with more time

- **Supplier-local time for `after_hours`.** Currently UTC. A supplier in Singapore placing a 14:00-local order is flagged as 06:00 UTC = after-hours. Joining `suppliers.country` to a static IANA-zone map would meaningfully reduce false positives.
- **Supplier-rate-based `risky_supplier`.** A supplier with high rejection_rate or high anomalous-order ratio is a stronger signal than a low rating alone. The aggregations slice already computes per-supplier `rejection_rate`; a second pass over the same query could surface `>0.2` rejection rate or `>0.5` anomalous-order ratio as the trigger. Keeping it as a static rating threshold for now keeps the SQL one-pass and easy to defend.
- **Severity tiering by magnitude.** `price_mismatch` of $0.05 is currently treated identically to a $50,000 mismatch. A relative threshold (`mismatch / total > 5%`) plus an absolute floor would capture more of the audit signal.
- **Caching.** The endpoint returns ~2.6 MB and scans 50k rows on every call. The performance slice will likely add Redis caching with invalidation on order writes (architecture §8.7).
- **Explainability per row.** Returning the offending values alongside the rule (`{ rule: "price_spike", actual: 49.95, baseline: 9.99 }`) would be useful for the FE drill-down. Out of scope for the current contract.
