# Provider Development

## Read the affected path

| Change                                   | Provider code                                                         | Tests                                                      | User docs                                            |
| ---------------------------------------- | --------------------------------------------------------------------- | ---------------------------------------------------------- | ---------------------------------------------------- |
| LINQ translation                         | `Query/Internal/DynamoQueryableMethodTranslatingExpressionVisitor.cs` | `tests/EntityFrameworkCore.DynamoDb.Tests/Query/`          | `docs/querying/`, `docs/limitations.md`              |
| Query compilation/materialization        | `Query/Internal/DynamoShapedQueryCompilingExpressionVisitor.cs`       | unit and integration query tests                           | `docs/querying/projection.md` when behavior changes  |
| Generated PartiQL                        | `Query/Internal/DynamoQuerySqlGenerator.cs`                           | integration `AssertSql` baselines                          | `docs/querying/`                                     |
| AWS execution/paging                     | `Storage/DynamoClientWrapper.cs`                                      | integration tests                                          | `docs/querying/pagination.md`, `docs/diagnostics.md` |
| CLR/DynamoDB conversion                  | `Storage/DynamoTypeMappingSource.cs`                                  | `tests/EntityFrameworkCore.DynamoDb.Tests/Storage/`        | modeling or limitations docs                         |
| Model metadata and configuration         | `Metadata/`, `Infrastructure/`                                        | `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/`       | `docs/configuration/`, `docs/modeling/`              |
| Writes and concurrency                   | `Storage/`, `ChangeTracking/`                                         | `SaveChangesTable/` integration tests                      | `docs/saving/`                                       |
| Precompiled query/AOT                    | `Design/Internal/`, `Infrastructure/DynamoGeneratedQueryRuntime.cs`   | AOT tests                                                  | `docs/querying/precompiled-queries.md`               |
| Change tracking                          | `ChangeTracking/`                                                     | `tests/EntityFrameworkCore.DynamoDb.Tests/ChangeTracking/` | `docs/saving/`, `docs/limitations.md`                |
| Provider diagnostics/interception        | `Diagnostics/`                                                        | `tests/EntityFrameworkCore.DynamoDb.Tests/Diagnostics/`    | `docs/diagnostics.md`                                |
| Public extensions and options            | `Extensions/`, `Infrastructure/`                                      | `tests/EntityFrameworkCore.DynamoDb.Tests/Extensions/`     | `docs/configuration/`, `docs/querying/`              |
| Design-time services                     | `Design/`                                                             | AOT or design-time tests                                   | `docs/querying/precompiled-queries.md`               |
| Shared provider helpers                  | `Utilities/`                                                          | closest unit test folder                                   | user docs only when behavior changes                 |
| Health checks                            | EF Core health-check integration                                      | `EntityFrameworkCore.DynamoDb.HealthChecks.Tests/`         | `docs/configuration/lifecycle.md`                    |
| EF-version, package, CI, release support | project files, `Taskfile.yml`, `.github/workflows/`                   | both EF-version suites                                     | `docs/multi-version-ef-strategy.md`                  |

For a query behavior change, trace translation, query shape, PartiQL generation, materialization,
and AWS execution before editing. Cover translation and execution when both can change.

## Test selection

- Fast internal translation, metadata, serializer, or type-mapping behavior: unit tests.
- Generated PartiQL, DynamoDB results, wire shape, paging, or `SaveChanges`: DynamoDB Local
  integration tests. Assert result data and the complete PartiQL statement.
- Inherited EF Core provider coverage: specification tests. Read that test directory's `AGENTS.md`.
- Generated interceptors or NativeAOT: AOT tests. Native publish/run is EF10-only; do not add an
  EF11 native smoke leg.

Use `[Fact(Timeout = TestConfiguration.DefaultTimeout)]` and the suite seed data for expected
results. Prefer `AsNoTracking()` except where tracking is the behavior under test.

## Before finishing a PR

1. Update user docs for changed behavior, configuration, diagnostics, limitations, or setup.
2. Update this skill when its guidance, file map, or sharp edges change.
3. Complete both maintenance checks in the PR template, with a reason when either is not applicable.
4. Run `task test:ef10` and `task test:ef11` for code changes. Run `task docs:build` for docs
   changes.
