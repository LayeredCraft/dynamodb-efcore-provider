# Codebase Cleanup / Release Readiness Analysis

Branch: `chore/codebase-cleanup-release-prep`

Scope: entire repo, read-only analysis plus local validation. Installed cleanup skills are repo-local. Subagent audit artifacts live under `.pi-subagents/artifacts/outputs/6102b418-9fe7-4bc2-ac91-84d0504c536f/cleanup-audit/`.

## Validation run

| Check                                                       | Result                                                                                          |
| ----------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| `task build:ef10`                                           | Passed                                                                                          |
| `task build:ef11`                                           | Passed                                                                                          |
| `task test:ef10`                                            | Passed: 3437 total, 2569 succeeded, 868 skipped                                                 |
| `task test:ef11`                                            | Passed: 3500 total, 2590 succeeded, 910 skipped                                                 |
| `task docs:build`                                           | Passed                                                                                          |
| `task pack:ef10 && task pack:ef11`                          | Failed release intent: both produced/overwrote `EntityFrameworkCore.DynamoDb.0.0.2-alpha.nupkg` |
| `dotnet format ... --verify-no-changes`                     | Failed: whitespace/charset fixes needed                                                         |
| `dotnet list ... package --vulnerable --include-transitive` | No vulnerable packages found                                                                    |
| `dotnet list ... package --outdated --include-transitive`   | Outdated patch/transitive packages found; top-level `AWSSDK.DynamoDBv2` 4.0.101 -> 4.0.101.1    |
| `git clean -ndX`                                            | Many ignored local artifacts present; preview only, no deletion                                 |

## Critical / release blockers

1. **Package versioning broken for EF10/EF11 release packages**

   - Evidence: `Directory.Build.props:3-4` sets concrete `<Version>0.0.2-alpha</Version>`; provider csproj only sets `VersionPrefix` per TFM at `src/EntityFrameworkCore.DynamoDb/EntityFrameworkCore.DynamoDb.csproj:23-29`; `Taskfile.yml:147-155` does not pass version.
   - Observed: `task pack:ef10` and `task pack:ef11` both produced `.nupkg/EntityFrameworkCore.DynamoDb.0.0.2-alpha.nupkg`; EF11 overwrote EF10.
   - Fix: remove global `<Version>` or override `Version`/`PackageVersion` per EF line; make pack task require/pass `VERSION`.

2. **`string.Compare` / `CompareTo` against `1` or `-1` is semantically wrong**

   - Evidence: `DynamoSqlTranslatingExpressionVisitor.cs:1489-1551` maps `== 1` to `>` and `== -1` to `<`.
   - Bug: .NET compare contract is negative/zero/positive, not exact `-1`/`1`; provider can silently return wrong rows.
   - Fix: only translate comparisons against `0`, or reject exact non-zero comparands.

3. **Non-skipped spec rows pass without executing sync branch**

   - Evidence: `ComplexTypesTrackingDynamoTest.cs:14-15` says sync rows no-op; many overrides return `Task.CompletedTask` for `async == false`, e.g. lines `28-29` onward.
   - Risk: green spec rows without base behavior, skip, or asserted provider failure.
   - Fix: convert sync branches to explicit no-sync assertion/skip strategy and add compliance guard for non-skipped no-op overrides.

4. **Release drafter EF-specific configs unused**

   - Evidence: `.github/release-drafter-ef10.yml` and `-ef11.yml` define `v10.*`/`v11.*`, but `.github/workflows/release-drafter.yaml:15-20` calls reusable workflow without matrix/config input; generic `.github/release-drafter.yml` remains active.
   - Risk: releases/tags may not select intended EF line.
   - Fix: matrix two drafter jobs or explicitly pass EF-specific config.

## High priority

05. **PR build has broad permissions/secrets to external reusable workflow**

    - Evidence: `.github/workflows/pr-build.yaml:7` uses `permissions: write-all`; line `22` references external workflow tag `@v10.1`; line `30` uses `secrets: inherit`.
    - Fix: least-privilege permissions, remove inherited secrets if not required, pin to SHA/protected tags.

06. **Docs workflow watches wrong config and over-grants permissions**

    - Evidence: `.github/workflows/docs.yaml` watches `zensical.yml`, but repo uses `zensical.toml`; build command omits `-f zensical.toml`; workflow-level `pages: write`/`id-token: write` applies to PR build.
    - Fix: watch `zensical.toml`, build with `-f zensical.toml`, scope deploy permissions to deploy job only.

07. **NuGet metadata weak while package analysis disabled**

    - Evidence: `Directory.Build.props:20` sets `NoPackageAnalysis=true`; generated nuspec showed `<description>Package Description</description>` and no tags.
    - Fix: add `Description`, `PackageTags`, release notes strategy; re-enable package analysis or narrowly suppress known false positives.

08. **`string.Contains(char)` / `StartsWith(char)` accepted but likely fail generation**

    - Evidence: char overloads routed to string functions in `DynamoSqlTranslatingExpressionVisitor.cs:97-109`, `867-882`, `1611-1630`; no `char` mapping in type mapping source.
    - Fix: convert `char` to one-character string before SQL function, or reject cleanly.

09. **Projection alias dedupe ignores case**

    - Evidence: `SelectExpression.cs:377-387` dedupes aliases with `StringComparison.OrdinalIgnoreCase`.
    - Risk: DynamoDB attributes are case-sensitive; `Name` and `name` can collapse.
    - Fix: use `StringComparison.Ordinal`.

10. **Example model typo likely breaks seeded `Description` materialization**

    - Evidence: `examples/Example.Simple/Program.cs` property/output uses `Desciption`; seed JSON uses `Description`.
    - Fix: rename property or map attribute explicitly.

## Medium priority

11. **Provider-owned AWS client not disposed**

    - Evidence: `DynamoClientWrapper.cs:45-52` lazily creates `AmazonDynamoDBClient`; wrapper is scoped (`DynamoServiceCollectionExtensions.cs:43-47`) but does not implement `IDisposable`.
    - Fix: track ownership and dispose only internally-created client.

12. **Integration fixture lifecycle can ignore cancellation / hang**

    - Evidence: `DynamoTestFixtureBase.cs:93-117` uses sync `gate.Wait()` and `CancellationToken.None`; wait loops at `141-179` run forever until table state changes.
    - Fix: async initialization or `WaitAsync`, pass test cancellation, add deadlines.

13. **DynamoDB Local image mismatch**

    - Evidence: integration uses `amazon/dynamodb-local:latest`; spec fixture pins `amazon/dynamodb-local:2.6.1`.
    - Fix: centralize/pin image tag.

14. **Read PartiQL lacks 8KB preflight validation**

    - Evidence: writes validate in `DynamoSaveChangesPlanner.cs:13,128-152`; read SQL generation returns string without equivalent check.
    - Fix: shared statement byte-size validator for query and write paths.

15. **`ExecutePartiQl` defers parameter-list snapshot until enumeration**

    - Evidence: `DynamoClientWrapper.cs:62` clones request without parameters; per-enumerator clone happens later.
    - Fix: clone parameter list at enumerable creation too.

16. **Reversed inclusive range does not normalize to `BETWEEN`**

    - Evidence: normalizer expects property on left for both bounds in `DynamoQueryableMethodTranslatingExpressionVisitor.cs:1138-1178`.
    - Fix: normalize property-right comparisons too (`low <= prop && prop <= high`).

17. **Public docs stale / broken README path**

    - Evidence: `README.md:91-92` says `docs/operators.md`; actual file is `docs/querying/operators.md`. Docs still emphasize EF10 without clear EF11 package-major guidance.
    - Fix: update README and docs install/version pages.

18. **Primitive collection projection skipped without executable negative guard**

    - Evidence: `PrimitiveCollectionsTable/SelectTests.cs:23-45` skipped; docs list feature as future.
    - Fix: implement feature or add negative test asserting clear unsupported error.

19. **Scalar equality/type mapping coverage is representative, not exhaustive**

    - Evidence: spec audit docs note many equality rows skipped; integration coverage strong but selective.
    - Fix: add data-driven integration matrix for supported scalar families.

## Cleanup / dead code candidates

20. **Unused cached method infos**

    - Evidence: `EnumerableMethods.SelectWithOrdinal` and `ToArray` in `EnumerableMethods.cs:24-41`, no references found.
    - Fix: remove properties and static init assignments.

21. **Provider-local `ExpressionPrinter` path appears dead**

    - Evidence: no `new ExpressionPrinter` / external call refs found; SQL expression `Print(ExpressionPrinter)` overrides appear unused.
    - Fix: remove or migrate selected debug printing to EF Core `IPrintableExpression`.

22. **Empty skipped unit tests**

    - Evidence: `DynamoDatabaseCreatorTests.cs:26-29` and `42-45` are skipped `Task.CompletedTask` placeholders.
    - Fix: delete.

23. **Boilerplate XML comments**

    - Evidence: 111 matches for `Represents the ... type` / `Provides functionality for this member` in `src`.
    - Fix: remove from internal/test members; replace public API docs with meaningful summaries.

24. **NamingOverride/NamingConvention stale copied names/comments**

    - Evidence: subagent audit found NamingOverride infrastructure named `NamingConventions...`; NamingConvention XML says CamelCase while code uses KebabCase.
    - Fix: rename/update comments.

25. **Mock-only tests live in integration project**

    - Evidence: `IntegrationTests/Storage/DynamoClientWrapperTests.cs` and `SharedTable/ParameterlessQueryTests.cs` use fake clients, no live table.
    - Fix: move to unit tests or document reason.

26. **Formatting drift**

    - Evidence: `dotnet format --verify-no-changes` failed across `DynamoSaveChangesPlanner.cs`, `DynamoWriteExecutor.cs`, `Check.cs`, many tests, and `examples/Example.Simple/Program.cs` charset.
    - Fix: run `dotnet format` in dedicated formatting commit.

27. **Ignored local cruft present**

    - Evidence: `git clean -ndX` lists `.DS_Store`, `.air/`, `.cache/`, `.dotnet/`, `.env`, `.nupkg/`, `.venv/`, `site/`, `TestResults/`, `bin/`, `obj/`, etc.
    - Fix: clean ignored artifacts after confirming local files safe; never delete `.env` without explicit approval.

## Recommended cleanup sequence

1. Fix release packaging/versioning + metadata; verify `pack:ef10` and `pack:ef11` produce distinct correct packages.
2. Fix correctness bugs: string compare, char overloads, projection case sensitivity, reversed BETWEEN.
3. Fix CI/release/docs workflows: release drafter, docs config watch, PR permissions.
4. Fix test integrity: ComplexTypesTracking no-op sync rows, image pinning, lifecycle cancellation.
5. Run format-only cleanup separately.
6. Remove dead code/placeholders and polish XML comments.
7. Add/expand coverage matrices and negative guards.
8. Final validation: `task test:ef10`, `task test:ef11`, `task docs:build`, `task pack:ef10`, `task pack:ef11`, inspect nupkgs.
