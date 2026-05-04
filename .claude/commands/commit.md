---
description: Commit ONLY the work described in the argument. No Claude co-author trailer.
argument-hint: <short description of the affected work>
---

The human wants to commit only the work described as: **$ARGUMENTS**

This is a scoped commit, not a "git add -A". Be conservative.

## Step 1 — Pre-flight

- If `.git` does not exist in the project root, **stop**. Ask the human whether to `git init` and configure user.name / user.email first. Do not init silently.
- If `$ARGUMENTS` is empty, **stop** and ask the human to describe what slice this commit covers.

## Step 2 — Survey

Run in parallel:
- `git status`
- `git diff` (unstaged) and `git diff --staged` (already-staged)
- `git log --oneline -5` to mirror existing commit style

## Step 3 — Select files

Map every changed/untracked file to one of:
- **In-scope** — clearly part of `$ARGUMENTS`.
- **Out-of-scope** — unrelated changes (leftover scaffolding, unrelated edits, build artifacts).
- **Ambiguous** — could go either way.

If there are any **ambiguous** files, **stop and ask** which bucket they belong in. Do not guess.

Never stage:
- `.env` or any secret file
- `bin/`, `obj/`, `node_modules/`, `dist/`, `*.user` (these should be gitignored, but double-check)
- Files that contain credentials or tokens

## Step 4 — Stage and commit

- Stage in-scope files individually by path. **Do not** use `git add -A`, `git add .`, or wildcards.
- Compose a commit message:
  - **Subject:** imperative, ≤70 chars, no trailing period. Conventional-commit prefix only if existing commits in this repo already use them; otherwise plain prose.
  - **Body** (optional): why, not what — the diff already shows what.
  - **Do NOT** include a `Co-Authored-By: Claude …` trailer.
  - **Do NOT** include a "🤖 Generated with Claude Code" trailer.
  - **Do NOT** skip hooks (`--no-verify`).
- Use a heredoc when piping multiline messages to `git commit -m` to preserve formatting.
- Do **not** push.

## Step 5 — Report

After the commit succeeds:

```
Commit:        <short hash> <subject>
Included:      <list of files staged>
Untouched:     <list of files left uncommitted>
Next:          <ask the human whether to commit untouched files in another scoped commit, or leave them>
```

If the commit fails (pre-commit hook, etc.), do not amend or retry blindly. Surface the failure, fix the underlying issue with the human, then create a **new** commit.
