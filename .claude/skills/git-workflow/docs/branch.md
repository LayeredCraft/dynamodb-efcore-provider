# Branch Workflow

## Shared references

Load before executing:

- [Scope Detection](../shared/scope-detection.md)
- [Conventional Types](../shared/conventional-types.md)
- [Safety Rules](../shared/safety-rules.md)

---

## Goal

Create a properly named branch for the current work based on inferred change intent, then switch to it.

## Branch naming format

```
<type>/<scope>-<short-description>
```

If no scope applies:

```
<type>/<short-description>
```

Rules:

- lowercase only
- hyphen-separated
- concise and descriptive
- remove punctuation

Examples:

- `feat/core-add-pr-automation`
- `fix/github-handle-detached-head`
- `docs/update-readme`
- `ci/github-improve-release-workflow`

## Workflow

1. Inspect repository status and changed files
2. Infer change type (see [Conventional Types](../shared/conventional-types.md))
3. Infer optional scope (see [Scope Detection](../shared/scope-detection.md))
4. Generate branch name
5. Create the branch
6. Switch to the branch

## Branch-specific safety

If a branch with the same name already exists, append a short numeric suffix (e.g. `-2`) rather than overwriting it.

See also [Safety Rules](../shared/safety-rules.md) for general constraints.

## Output

Report:

- branch name created
- branch switched to
