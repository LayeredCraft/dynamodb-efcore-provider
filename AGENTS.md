# AGENTS.md

This file guides agentic coding tools working in this repository.

## Project Snapshot
- EF Core provider for AWS DynamoDB; LINQ -> PartiQL translation.
- .NET SDK: 10.0.101 (see `global.json`).
- Nullable reference types enabled; implicit usings enabled.
- Central package management via `Directory.Packages.props`.
- Warnings as errors (treat code quality issues seriously).

## Key Docs
- `CLAUDE.md` for architecture, query pipeline, and provider constraints.
- `.claude/do_not_commit/FEATURES.md` for feature support matrix.
- `.claude/do_not_commit/stories/` for implementation stories and acceptance criteria.
- `README.md` for usage and package metadata.

## Repo Layout
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/` provider implementation.
- `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.Tests/` unit tests.
- `tests/LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests/` integration tests.
- `examples/Example.Simple/` example app using DynamoDB Local.

## Build / Test / Run

### Build
```bash
dotnet build
```

### Tests (Microsoft.Testing.Platform)
The repo uses Microsoft.Testing.Platform (not VSTest). Use `dotnet test` with filters.

Set these for the project under test:
```bash
TEST_PROJECT="tests/<YourTestProject>/<YourTestProject>.csproj"
TFM="net10.0"
```

List tests:
```bash
DOTNET_NOLOGO=1 dotnet test \
  --project "$TEST_PROJECT" \
  -f "$TFM" -v q \
  --list-tests --no-progress --no-ansi
```

Run a single test method:
```bash
DOTNET_NOLOGO=1 dotnet test \
  --project "$TEST_PROJECT" \
  -f "$TFM" -v q \
  --filter-method "MyNamespace.MyTestClass.MyTestMethod" \
  --minimum-expected-tests 1 \
  --no-progress --no-ansi
```

Run a test class or namespace:
```bash
DOTNET_NOLOGO=1 dotnet test \
  --project "$TEST_PROJECT" \
  -f "$TFM" -v q \
  --filter-class "MyNamespace.MyTestClass" \
  --minimum-expected-tests 1 \
  --no-progress --no-ansi

DOTNET_NOLOGO=1 dotnet test \
  --project "$TEST_PROJECT" \
  -f "$TFM" -v q \
  --filter-namespace "MyNamespace.Tests" \
  --minimum-expected-tests 1 \
  --no-progress --no-ansi
```

### Example (DynamoDB Local)
```bash
cd examples/Example.Simple
docker-compose up -d
dotnet run
docker-compose down
```

## Formatting and Style

### EditorConfig Basics
- Indentation: 4 spaces for C# (`*.cs`); 2 spaces elsewhere.
- Line endings: LF; final newline required; trim trailing whitespace.
- Max line length: 100 (C#), 120 for XML files.

### C# Conventions (observed in repo)
- File-scoped namespaces are standard.
- Use `var` when the type is obvious on the right-hand side.
- Prefer target-typed `new()` and collection expressions (`[]`) when clear.
- Use expression-bodied members when small and readable.
- Fields: `_camelCase`; private readonly where possible.
- Types/methods/properties: `PascalCase`.
- Locals/parameters: `camelCase`.

### Usings
- Keep `using` directives at the top of the file.
- Order with `System.*` first, then external packages, then project namespaces.
- Keep groups alphabetized within each block.

### Nullability and Guards
- Nullable references are enabled; avoid nulls where possible.
- Use guard methods/throws early for invalid state (see `NotNull()` usage).

### Async and Library Code
- Prefer async APIs; sync query execution is unsupported by design.
- Use `ConfigureAwait(false)` in library code when awaiting tasks.
- Avoid blocking calls (`.Result`, `.Wait()`).

### Exceptions and Diagnostics
- Use `NotSupportedException` for PartiQL limitations or unsupported LINQ.
- Use `InvalidOperationException` for provider invariants and invalid state.
- Include actionable, descriptive exception messages.

### Public API Documentation
- Maintain XML docs on public types/methods when behavior is non-trivial.
- Favor concise summaries and parameter docs over verbose prose.

## Provider-Specific Guidance
- Do not skip `ParameterInliner` before `DynamoQuerySqlGenerator`.
- `SELECT *` is not supported; projections must be explicit.
- Query execution is async-only; sync enumeration throws by design.
- Operator precedence is enforced in SQL generation; preserve it.

## Dependencies and Packages
- Add/upgrade package versions in `Directory.Packages.props`.
- Keep `PrivateAssets` and `IncludeAssets` consistent with existing entries.

## Tests and Assertions
- Unit tests use xUnit v3; assertions often via AwesomeAssertions.
- NSubstitute is the mocking framework used in tests.
- Prefer meaningful test names and minimal fixture setup.

## Agent Rules (Cursor/Copilot)
- No `.cursor/rules/`, `.cursorrules`, or `.github/copilot-instructions.md` files found.

## Common Pitfalls
- Avoid changing files under `.claude/do_not_commit/` unless explicitly requested.
- Keep line length limits in mind; do not introduce long single-line strings.
- Do not add sync query APIs; follow async-only design constraints.

## References
- `CLAUDE.md` for architecture, pipeline stages, and extension points.
- `FEATURES.md` for supported LINQ/PartiQL behavior and limitations.
