---
description: Load full project context for a fresh conversation on candidate-package
---

You are starting (or resuming) a conversation on the **candidate-package** project — a procurement order-management dashboard built with .NET + Postgres + Redis on the backend and React + TypeScript on the frontend.

## Step 1 — Load the working context (in order)

Read these files. They are the primary context. Do not skim — load them fully.

1. `docs/ai-architecture.md` — the AI working-context document. PENDING markers signal decisions that still require human alignment per coding-principles §7. Do not invent values for them.
2. `docs/coding-principles.md` — the binding rules for every change. Re-read every session, not just once.
3. `README.md` — the assignment specification. The tests in `tests/` are the authoritative spec when the README and tests disagree.

## Step 2 — Check current state

Run these in parallel where possible:

- `docker compose ps` — confirm Postgres + Redis are healthy. If not, surface it before doing anything else.
- `git status` (and `git log --oneline -10` if a repo exists) — what is uncommitted, what was the last commit. If `.git` does not exist, note it; do not auto-init.
- List `src/OrderOps.Api/Controllers/` and `src/OrderOps.Api/` to see how far implementation has progressed.
- If `tests/node_modules` is missing, flag it.

## Step 3 — Report back

Post a brief checklist (no prose paragraphs):

```
Docs loaded:        docs/ai-architecture.md, docs/coding-principles.md, README.md
Infrastructure:     <postgres state> | <redis state>
Git:                <repo? | last commit | dirty files count>
Implementation:     <controllers present | endpoints implemented>
Open decisions:     <list of docs/ai-architecture.md §10 items still PENDING that block the next likely slice>
```

## Step 4 — Wait

Do not begin coding. Do not propose a slice until the human picks one. Per coding-principles §7, work proceeds in small human-gated increments.

If the human asks "what should we do next?", suggest the smallest unblocked slice from docs/ai-architecture.md §7 (test order: basic → filtering → aggregations → ...) and the open decisions that need to be resolved before that slice can start.
