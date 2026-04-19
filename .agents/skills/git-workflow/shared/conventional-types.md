# Conventional Types

Valid `<type>` values:

| Type       | Description                                                       | SemVer impact |
|------------|-------------------------------------------------------------------|---------------|
| `feat`     | Introduces a new feature                                          | MINOR         |
| `fix`      | Patches a bug                                                     | PATCH         |
| `build`    | Changes to the build system or external dependencies              | —             |
| `chore`    | Maintenance tasks not modifying src or test files                 | —             |
| `ci`       | Changes to CI/CD configuration or scripts                         | —             |
| `docs`     | Documentation changes only                                        | —             |
| `perf`     | A code change that improves performance                           | —             |
| `refactor` | A code change that neither fixes a bug nor adds a feature         | —             |
| `style`    | Changes that do not affect meaning (whitespace, formatting, etc.) | —             |
| `test`     | Adding or updating tests                                          | —             |

## Breaking changes

A breaking change correlates with MAJOR in SemVer. Mark it in one of two ways:

**Append `!` after the type/scope:**

```
feat(api)!: remove deprecated endpoint
```

**Or include a `BREAKING CHANGE:` footer:**

```
feat(api): remove deprecated endpoint

BREAKING CHANGE: The /v1/users endpoint has been removed. Use /v2/users instead.
```

Both forms may be combined.
