# PR Workflow

## Shared references

Load before executing:

- [Scope Detection](../shared/scope-detection.md)
- [File Inclusion Policy](../shared/file-inclusion-policy.md)
- [Safety Rules](../shared/safety-rules.md)
- [Conventional Types](../shared/conventional-types.md)

______________________________________________________________________

## Goal

Prepare the current work for review and create a pull request that includes:

- a correctly named branch
- a conventional commit message
- a PR title following the required format
- a reviewable PR body that explains what changed, why, validation, and risk

Template location: `../templates/pull-request-template.md`

______________________________________________________________________

## PR title format

```
<type>[optional scope]: <description>
```

For breaking changes, append `!` after the type/scope: `feat(api)!: remove deprecated endpoint`

Example: `feat(core): add automated PR workflow`

______________________________________________________________________

## Branch rules

Create a new branch if:

- the current branch is `main`
- the repository is in detached `HEAD`

If already on a feature branch, use the current branch.

Branch naming follows `<type>/<scope>-<short-description>` (or `<type>/<short-description>` when no scope applies). See [Branch Workflow](branch.md) for full naming rules.

______________________________________________________________________

## Execution flow

### 1 — Inspect repository

Determine: current branch, whether HEAD is detached, git status, modified files, diff summary, and commit history against the base branch.

### 2 — Infer metadata

Determine: PR type, optional scope, short description, PR title, branch name.

### 3 — Prepare branch

If on `main` or detached `HEAD`, create a new branch and switch to it. Otherwise stay on the current branch.

### 4 — Commit work

Stage all user-modified files per [File Inclusion Policy](../shared/file-inclusion-policy.md). Exclude only obvious junk. Create commit. Skip if nothing to commit.

### 5 — Push branch

Push to origin. Set upstream if necessary.

### 6 — Generate PR body

Load `../templates/pull-request-template.md` and adapt it to the actual change.

Treat the template as a default outline, not a rigid contract. Prioritize reviewer scanability and signal quality over filling every heading.

Required information:

- what changed
- why it changed
- how it was validated

Default outline (adapt as needed):

- Summary - 2-4 sentences covering what changed and why
- Changes - grouped in the way that makes the diff easiest to review (for example by concern, subsystem, workflow, or user impact)
- Validation - concrete tests, manual verification, and confidence signals
- Breaking Changes - include only when applicable
- Related Issues - include only when applicable; do not invent issue numbers
- Release Notes - include only for user-visible or package-relevant changes
- Notes for Reviewers - include when review guidance, risks, tradeoffs, follow-up context, or requested feedback focus would help; for UI changes, include screenshots/video links when useful

Review mode:

- open as draft when implementation is incomplete, checks are pending, or early feedback is requested
- when draft, state what is incomplete and what feedback is being requested

Rules:

- omit empty sections entirely (do not include `N/A`, `None`, or `No related issues`)
- prefer fewer, high-signal sections over boilerplate
- use backticks for identifiers, commands, files, and code terms
- keep the Summary concise and focused on intent, not file-by-file trivia

### 7 — Create PR

Create the pull request using the generated title and body, as draft or ready-for-review based on the review mode rules above.

______________________________________________________________________

## Output

Report:

- branch name and whether it was created
- commit message and whether a commit was created
- PR title
- PR body
- any files excluded and why
- any assumptions or blockers
