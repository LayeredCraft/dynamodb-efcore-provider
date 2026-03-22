# Commit Workflow

## Shared references

Load before executing:

- [Scope Detection](../shared/scope-detection.md)
- [File Inclusion Policy](../shared/file-inclusion-policy.md)
- [Safety Rules](../shared/safety-rules.md)
- [Conventional Types](../shared/conventional-types.md)

---

## Goal

Create a commit representing the user's current working changes using a conventional commit format.

## Commit format

```
<type>[optional scope]: <description>

[optional body]

[optional footer(s)]
```

The description must immediately follow the colon and space. Scope is wrapped in parentheses when present: `feat(parser): add CSV support`.

For breaking changes, append `!` after the type/scope and/or include a `BREAKING CHANGE:` footer. See [Conventional Types](../shared/conventional-types.md) for details.

## Workflow

1. Inspect repository status
2. Identify all modified files
3. Stage all user-modified files (see [File Inclusion Policy](../shared/file-inclusion-policy.md))
4. Exclude only obvious junk artifacts
5. Infer `<type>` and `<scope>` (see [Conventional Types](../shared/conventional-types.md) and [Scope Detection](../shared/scope-detection.md))
6. Generate and create the commit

## Output

Report:

- commit message used
- files committed
- any files excluded and why
