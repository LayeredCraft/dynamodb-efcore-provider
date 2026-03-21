# PR Workflow

## Shared references

Load before executing:

- [Scope Detection](../shared/scope-detection.md)
- [File Inclusion Policy](../shared/file-inclusion-policy.md)
- [Safety Rules](../shared/safety-rules.md)
- [Conventional Types](../shared/conventional-types.md)

---

## Goal

Prepare the current work for review and create a pull request that includes:

- a correctly named branch
- a conventional commit message
- a PR title following the required format
- a PR body generated from the local template

Template location: `../templates/pull-request-template.md`

---

## PR title format

```
<type>[optional scope]: <description>
```

For breaking changes, append `!` after the type/scope: `feat(api)!: remove deprecated endpoint`

Example: `feat(core): add automated PR workflow`

---

## Branch rules

Create a new branch if:

- the current branch is `main`
- the repository is in detached `HEAD`

If already on a feature branch, use the current branch.

Branch naming follows `<type>/<scope>-<short-description>` (or `<type>/<short-description>` when no scope applies). See [Branch Workflow](branch.md) for full naming rules.

---

## Execution flow

### 1 — Inspect repository

Determine: current branch, whether HEAD is detached, git status, modified files, diff summary.

### 2 — Infer metadata

Determine: PR type, optional scope, short description, PR title, branch name.

### 3 — Prepare branch

If on `main` or detached `HEAD`, create a new branch and switch to it. Otherwise stay on the current branch.

### 4 — Commit work

Stage all user-modified files per [File Inclusion Policy](../shared/file-inclusion-policy.md). Exclude only obvious junk. Create commit. Skip if nothing to commit.

### 5 — Push branch

Push to origin. Set upstream if necessary.

### 6 — Generate PR body

Load `../templates/pull-request-template.md` and populate:

- Summary — what this PR does and why
- Changes — key implementation details
- Validation — build/test status, breaking changes
- Related Issues — do not invent issue numbers
- Release Notes — user-visible changes only
- Notes for Reviewers — risky areas, known limitations, follow-up work

Do not mark checklist items complete unless confirmed.

### 7 — Create PR

Create the pull request using the generated title and body.

---

## Output

Report:

- branch name and whether it was created
- commit message and whether a commit was created
- PR title
- PR body
- any files excluded and why
- any assumptions or blockers
