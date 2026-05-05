# Coding Principles

These rules are binding. They apply to every change made by the AI assistant. They take precedence over default model behavior. When two rules conflict, prefer the stricter one and surface the conflict to the human owner.

---

## 1. Clean and scalable code

- Naming carries meaning. Identifiers must read like intent, not abbreviation soup.
- Pure functions where possible. Side effects are isolated and named.
- No dead code, no unused parameters, no leftover scaffolding.
- Be deliberate about allocations on hot paths. Aggregations that touch 50k rows cannot afford casual `IEnumerable` materializations.
- Choose data structures for the access pattern, not for what's familiar.

## 2. Small files, small functions

- A function does one thing. If a function name needs "And", split it.
- Soft ceiling: ~30 lines per function, ~250 lines per file. Crossing it is allowed only with a clear reason.
- Controllers are thin. They parse input, call a service, format output. They do not contain business logic.
- Repositories own SQL. Services own business rules. Controllers own HTTP. No layer-skipping.
- Group by feature when possible (e.g. `Features/Orders/`, `Features/Suppliers/`) rather than by technical role only.

## 3. Comments

- Default: write no comments. The code names things; the names explain.
- Only justified comments:
  - A non-obvious WHY (a hidden constraint, a perf trick, a workaround for a bug or an edge case in the dataset).
  - A regulatory/spec quote when the code looks wrong without it (e.g. "test expects exactly 10 entries").
- When you do comment, keep it to one line where possible. No banner comments, no boilerplate XML doc on every property, no narration of what the next line does.

## 4. SOLID, always

- **S** — Single responsibility. A class has one reason to change. The `OrderService` does not also send emails.
- **O** — Open/closed. New behavior is added by extension, not by editing battle-tested code paths.
- **L** — Liskov. A subtype must be a drop-in for its base. No "throw NotImplementedException".
- **I** — Interface segregation. Many small role-interfaces, not one god interface. Consumers depend only on what they use.
- **D** — Dependency inversion. Services depend on abstractions registered in DI, not on concrete `new`-ed dependencies. Static `DateTime.UtcNow` and `Guid.NewGuid` are exceptions only when not on a tested business path.

No corner cuts. If a "quick fix" violates SOLID, surface it and ask before merging.

## 5. End-of-session checks

After each implementation increment, before handing back to the human, perform and report:

1. **Typecheck / build** — `dotnet build` (and `tsc --noEmit` for the frontend) must pass with zero errors and zero warnings introduced by the change.
2. **Code review (self)** — re-read the diff with these principles in hand. Flag anything that violates them.
3. **Implementation confidence** — one of `low | medium | high`, with one sentence justifying it.
4. **Risk assessment** — top 3 risks introduced by this change (correctness, performance, security, regression). If none, say so explicitly.
5. **Edge-case audit** — list edge cases considered, edge cases handled, edge cases knowingly deferred. The dataset is full of traps (negative quantities, null warehouses, circular categories, XSS in notes, timestamp anomalies, duplicate names, mismatched prices, inactive suppliers with orders) — every change touching that surface must be checked against the relevant traps.
6. **Human-check summary** — after every feature implementation or bug fix, provide a short bulleted list of the most important modifications that the human reviewer should manually eyeball before approving. Examples: "new SQL in `OrderRepository.GetById` — verify the JOIN cardinality"; "changed default `limit` from 50 to 20 in `OrdersController` — confirm intentional"; "added new index migration — verify it doesn't conflict with existing index names". Keep it tight: 2–5 bullets, each ≤1 line. The goal is to direct human attention to the parts of the diff that are easy to miss in a quick scan and would be costly to get wrong.

Format the report consistently so it's easy to skim. Example:

> **Build:** clean. **Confidence:** medium. **Risks:** (1) … (2) … (3) … **Edges considered:** …; **handled:** …; **deferred:** … **Docs:** updated `ai-architecture.md` §6.1 and §9. **Human-check:** (1) new index on `orders.created_at` — verify name doesn't collide; (2) tightened error response for 404 — confirm shape matches `tests/basic-crud.test.ts:42`.

## 6. MVC for the backend

- ASP.NET Core controllers (not minimal APIs) for the request surface.
- Models = DTOs for the wire + domain entities for the database. Do not leak entities into HTTP responses; map explicitly.
- Views in this context = the JSON response shapes. Their structure is fixed by the README and tests — never deviate.
- Controllers stay thin. Services own business logic. Repositories own data access.

## 7. Small increments, human gate

This is the strongest rule. Do not bypass it.

- Work in the smallest meaningful slice. Prefer "implement GET /api/orders/:id" over "implement all CRUD".
- After each slice: stop, summarize, run end-of-session checks (§5), wait for the human to review and commit.
- **Never invent design decisions.** If a piece of information is missing — schema choice, library choice, response field, severity rubric, edge-case behavior — **ask**. Do not guess and do not "make a reasonable assumption" silently.
- It is better to ask one extra question than to ship code that has to be ripped out.
- If you discover during implementation that a previously-agreed approach won't work, stop and surface it; do not pivot unilaterally.

## 8. Documentation must stay current

The project's docs are part of the contract — out-of-date docs are worse than no docs because they actively mislead the next session.

After **every** conversation that produces a meaningful change (new code, new decision, new dependency, scope change, scrap-and-rewrite, anomaly rubric tweak, new edge case discovered, etc.), before handing back to the human:

- Update **`ai-architecture.md`**:
  - Promote any **PENDING** decision that became a real decision (move it from §10 Open Decisions into the relevant section, with the chosen approach).
  - Append to **§9 Done So Far** as new milestones land.
  - Add new constraints, new endpoints, new edge cases, new packages, new files of note.
  - Remove or correct any statement that is no longer accurate.
- Update **`coding-principles.md`** when a new working agreement, ban, or check is introduced. If the human says "from now on, X" — that's a principles update, not a one-off note.
- Create or update other docs only when warranted. **All project docs live under `docs/`** — never write a new `.md` outside that folder (exception: `README.md` at the root, which is the assignment spec and is read-only).
  - `docs/ARCHITECTURE.md` (the deliverable for human reviewers) — written/maintained near submission.
  - `docs/ANOMALY_STRATEGY.md` (deliverable) — updated each time the anomaly rules or severity rubric change.
  - Any new cross-cutting doc needs a one-line entry in `docs/ai-architecture.md` so it's discoverable.
- Never let docs and reality drift. If you cannot update the doc in the same increment, raise it as a follow-up task and surface it to the human.

The end-of-session report (§5) must include a **Docs:** line stating which docs were updated, or `Docs: none needed (no semantic change)` with one-sentence justification.

---

## How to apply during a session

1. Read `ai-architecture.md` and this file at the start of every non-trivial session.
2. Before writing code: confirm scope of the increment with the human.
3. While writing code: check rules §1–§4 continuously, not just at the end.
4. Before handing back: run the §5 checklist (now including the **Docs:** line) and update docs per §8.
5. After human review and commit: ask what the next slice is. Do not start a new slice on your own.
