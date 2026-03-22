# Scope Detection

Infer scope from the folder containing the majority of the changes.

| Folder | Scope |
|---|---|
| `.github/workflows` | `github` |
| `src/Core` | `core` |
| `src/Abstractions` | `abstractions` |
| `src/SourceGenerators` | `source-generators` |
| `src/OpenTelemetry` | `opentelemetry` |
| `tests` | `tests` |
| `test` | `testing` |
| `docs` | `docs` |
| `build` | `build` |
| dependency or package updates | `deps` |

If multiple folders are involved, prioritize the **primary concern of the change**.

If no mapping clearly applies, omit the scope.
