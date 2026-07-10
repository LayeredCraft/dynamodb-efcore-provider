# Codebase Cleanup / Release Readiness Analysis

Branch: `chore/codebase-cleanup-release-prep`

Scope: entire repo, read-only analysis plus local validation. Installed cleanup skills are repo-local. This is a historical audit summary; findings fixed by this PR are marked for traceability.

## Validation run

| Check                                                       | Result                                                                                                                                                                                                                                         |
| ----------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `task build:ef10`                                           | Passed                                                                                                                                                                                                                                         |
| `task build:ef11`                                           | Passed                                                                                                                                                                                                                                         |
| `task test:ef10`                                            | Passed: 3437 total, 2569 succeeded, 868 skipped                                                                                                                                                                                                |
| `task test:ef11`                                            | Passed: 3500 total, 2590 succeeded, 910 skipped                                                                                                                                                                                                |
| `task docs:build`                                           | Passed                                                                                                                                                                                                                                         |
| `task pack:ef10 && task pack:ef11`                          | Initially failed release intent: both produced/overwrote `EntityFrameworkCore.DynamoDb.0.0.2-alpha.nupkg`; fixed: clean pack now emits `EntityFrameworkCore.DynamoDb.10.0.0-alpha.nupkg` and `EntityFrameworkCore.DynamoDb.11.0.0-alpha.nupkg` |
| `dotnet format ... --verify-no-changes`                     | Initially failed: whitespace/charset fixes needed; fixed in this PR and kept clean after repo hook formatting                                                                                                                                  |
| `format hook git pre-commit --log-level warn`               | Passed; commit hook reported unmatched workflow YAML files but made no changes, and `dotnet format` remained clean                                                                                                                             |
| `dotnet list ... package --vulnerable --include-transitive` | No vulnerable packages found                                                                                                                                                                                                                   |
| `dotnet list ... package --outdated --include-transitive`   | Outdated patch/transitive packages found; top-level `AWSSDK.DynamoDBv2` 4.0.101 -> 4.0.101.1                                                                                                                                                   |
| `git clean -ndX`                                            | Many ignored local artifacts present; preview only, no deletion                                                                                                                                                                                |

## Original critical / release findings

1. **Package versioning broken for EF10/EF11 release packages**

   **Status:** Fixed in this PR. `Directory.Build.props` no longer pins concrete `Version`; provider `VersionPrefix` remains `10.0.0` for EF10 and `11.0.0` for EF11, with the existing prerelease suffix applied.

   - Original evidence: `Directory.Build.props:3-4` set concrete `<Version>0.0.2-alpha</Version>`; provider csproj only set `VersionPrefix` per TFM at `src/EntityFrameworkCore.DynamoDb/EntityFrameworkCore.DynamoDb.csproj:23-29`; `Taskfile.yml:147-155` did not pass version.
   - Observed before fix: `task pack:ef10` and `task pack:ef11` both produced `.nupkg/EntityFrameworkCore.DynamoDb.0.0.2-alpha.nupkg`; EF11 overwrote EF10.
   - Fix applied: remove global `<Version>` so SDK package version follows EF-line `VersionPrefix`, while global `VersionSuffix` preserves prerelease package semantics.
   - Final local pack outputs after cleaning: `.nupkg/EntityFrameworkCore.DynamoDb.10.0.0-alpha.nupkg` and `.nupkg/EntityFrameworkCore.DynamoDb.11.0.0-alpha.nupkg`.

2. **`string.Compare` / `CompareTo` against `1` or `-1` is semantically wrong**

   **Status:** Fixed in this PR. Translation now supports compare-to-zero shapes only; non-zero comparands are rejected.

   - Original evidence: `DynamoSqlTranslatingExpressionVisitor.cs:1489-1551` mapped `== 1` to `>` and `== -1` to `<`.
   - Bug: .NET compare contract is negative/zero/positive, not exact `-1`/`1`; provider could silently return wrong rows.
   - Fix applied: only translate comparisons against `0`; reject exact non-zero comparands.

3. **Non-skipped spec rows pass without executing sync branch**

   - Evidence: `ComplexTypesTrackingDynamoTest.cs:14-15` says sync rows no-op; many overrides return `Task.CompletedTask` for `async == false`, e.g. lines `28-29` onward.
   - Risk: green spec rows without base behavior, skip, or asserted provider failure.
   - Fix: convert sync branches to explicit no-sync assertion/skip strategy and add compliance guard for non-skipped no-op overrides.

4. **Release drafter EF-specific configs unused**

   **Status:** Fixed in this PR. Release drafter workflow now uses EF-specific configs.

   - Original evidence: `.github/release-drafter-ef10.yml` and `-ef11.yml` defined `v10.*`/`v11.*`, but `.github/workflows/release-drafter.yaml:15-20` called reusable workflow without matrix/config input; generic `.github/release-drafter.yml` remained active.
   - Risk: releases/tags may not select intended EF line.
   - Fix applied: matrix/config usage for EF-specific drafter jobs.

## High priority

05. **Build/publish workflows have broad permissions/secrets to external reusable workflows**

    **Status:** Fixed in this PR. PR and publish workflows use narrower permissions, no inherited secrets, and pinned reusable workflow/action references.

    - Original PR evidence: `.github/workflows/pr-build.yaml:7` used `permissions: write-all`; line `22` referenced external workflow tag `@v10.1`; line `30` used `secrets: inherit`.
    - Additional publish evidence: `.github/workflows/publish-preview.yaml` and `.github/workflows/publish-release.yaml` used workflow-level write permissions, external `@v10.1` refs, and `secrets: inherit`.
    - Fix applied: least-privilege caller permissions, remove inherited secrets where templates do not declare custom required secrets, and pin reusable workflow/action references to `76a2269c95c0f17eaac80d3020c783ff10be4371`.

06. **Docs workflow watches wrong config and over-grants permissions**

    **Status:** Fixed in this PR. Docs workflow watches/builds `zensical.toml` and scopes Pages permissions to deploy.

    - Original evidence: `.github/workflows/docs.yaml` watched `zensical.yml`, but repo uses `zensical.toml`; build command omitted `-f zensical.toml`; workflow-level `pages: write`/`id-token: write` applied to PR build.
    - Fix applied: watch `zensical.toml`, build with `-f zensical.toml`, scope deploy permissions to deploy job only.

07. **NuGet metadata weak while package analysis disabled**

    **Status:** Fixed in this PR. Package metadata is now present and package analysis is no longer disabled by `Directory.Build.props`.

    - Original evidence: `Directory.Build.props:20` set `NoPackageAnalysis=true`; generated nuspec showed `<description>Package Description</description>` and no tags.
    - Fix applied: add `Description`, `PackageTags`, and release notes metadata; remove the package-analysis opt-out.

08. **`string.Contains(char)` / `StartsWith(char)` accepted but likely fail generation**

    **Status:** Fixed in this PR. Char overloads are rejected during translation with coverage.

    - Original evidence: char overloads routed to string functions in `DynamoSqlTranslatingExpressionVisitor.cs:97-109`, `867-882`, `1611-1630`; no `char` mapping in type mapping source.
    - Fix applied: reject char overloads cleanly before AWS execution.

09. **Projection alias dedupe ignores case**

    **Status:** Fixed in this PR. Alias de-duplication now uses ordinal case-sensitive comparison.

    - Original evidence: `SelectExpression.cs:377-387` deduped aliases with `StringComparison.OrdinalIgnoreCase`.
    - Risk: DynamoDB attributes are case-sensitive; `Name` and `name` can collapse.
    - Fix applied: use `StringComparison.Ordinal`.

10. **Example model typo likely breaks seeded `Description` materialization**

    **Status:** Fixed in this PR. Example model now uses the correct `Description` spelling.

    - Original evidence: `examples/Example.Simple/Program.cs` property/output used `Desciption`; seed JSON used `Description`.
    - Fix applied: rename property/output to `Description`.

## Medium priority

11. **Provider-owned AWS client not disposed**

    **Status:** Fixed in this PR. Wrapper tracks ownership and preserves user-supplied clients.

    - Original evidence: `DynamoClientWrapper.cs:45-52` lazily created `AmazonDynamoDBClient`; wrapper is scoped (`DynamoServiceCollectionExtensions.cs:43-47`) but did not implement `IDisposable`.
    - Fix applied: track ownership and dispose only internally-created client.

12. **Integration fixture lifecycle can ignore cancellation / hang**

    **Status:** Fixed in this PR. Fixture lifecycle now uses bounded waits.

    - Original evidence: `DynamoTestFixtureBase.cs:93-117` used sync `gate.Wait()` and `CancellationToken.None`; wait loops at `141-179` ran forever until table state changed.
    - Fix applied: add deadlines/bounded waits for table lifecycle operations.

13. **DynamoDB Local image mismatch**

    **Status:** Fixed in this PR. Fixtures now use the shared pinned image name from `tests/DynamoDbLocalImage.cs`.

    - Original evidence: integration used `amazon/dynamodb-local:latest`; spec fixture pinned `amazon/dynamodb-local:2.6.1`.
    - Fix applied: centralize/pin image tag (`amazon/dynamodb-local:3.3.0`).

14. **Read PartiQL lacks 8KB preflight validation**

    **Status:** Fixed in this PR. Read execution validates statement byte length before AWS calls.

    - Original evidence: writes validated in `DynamoSaveChangesPlanner.cs:13,128-152`; read SQL generation returned string without equivalent check.
    - Fix applied: shared statement byte-size validator for query and write paths.

15. **`ExecutePartiQl` defers parameter-list snapshot until enumeration**

    **Status:** Fixed in this PR. Parameter list is snapshotted at enumerable creation.

    - Original evidence: `DynamoClientWrapper.cs:62` cloned request without parameters; per-enumerator clone happened later.
    - Fix applied: clone parameter list at enumerable creation too.

16. **Reversed inclusive range does not normalize to `BETWEEN`**

    **Status:** Fixed in this PR. Reversed inclusive bounds now normalize to `BETWEEN`.

    - Original evidence: normalizer expected property on left for both bounds in `DynamoQueryableMethodTranslatingExpressionVisitor.cs:1138-1178`.
    - Fix applied: normalize property-right comparisons too (`low <= prop && prop <= high`).

17. **Public docs stale / broken README path**

    **Status:** Partially fixed in this PR. README now points at `docs/querying/operators.md`; detailed EF-line packaging guidance lives in `docs/multi-version-ef-strategy.md`. Getting-started install docs remain intentionally generic in this PR.

    - Original evidence: `README.md:91-92` said `docs/operators.md`; actual file is `docs/querying/operators.md`. Docs still emphasized EF10 without clear EF11 package-major guidance.
    - Fix applied: update README path and multi-version strategy docs; do not claim user-facing install docs were expanded.

18. **Primitive collection projection skipped without executable negative guard**

    **Status:** Fixed in this PR. Primitive collection projection now executes through integration coverage and docs list projection as supported.

    - Original evidence: `PrimitiveCollectionsTable/SelectTests.cs:23-45` skipped; docs listed feature as future.
    - Fix applied: enable `Select_AnonymousProjection_WithCollectionProperties` with SQL assertion and update query operator docs.

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

    **Status:** Fixed in this PR.

    - Original evidence: `dotnet format --verify-no-changes` failed across `DynamoSaveChangesPlanner.cs`, `DynamoWriteExecutor.cs`, `Check.cs`, many tests, and `examples/Example.Simple/Program.cs` charset.
    - Fix applied: run repo hook formatter and `dotnet format`; keep `format hook git pre-commit --log-level warn` passing and `dotnet format --verify-no-changes` clean. Hook reports workflow YAML as unmatched formatter inputs but does not alter them.

27. **Ignored local cruft present**

    - Evidence: `git clean -ndX` lists `.DS_Store`, `.air/`, `.cache/`, `.dotnet/`, `.env`, `.nupkg/`, `.venv/`, `site/`, `TestResults/`, `bin/`, `obj/`, etc.
    - Fix: clean ignored artifacts after confirming local files safe; never delete `.env` without explicit approval.

## Recommended cleanup sequence

1. Fix release packaging/versioning + metadata; verify `pack:ef10` and `pack:ef11` produce distinct correct packages (`10.0.0-alpha` and `11.0.0-alpha`).
2. Fix correctness bugs: string compare, char overloads, projection case sensitivity, reversed BETWEEN.
3. Fix CI/release/docs workflows: release drafter, docs config watch, PR permissions.
4. Fix test integrity: ComplexTypesTracking no-op sync rows, image pinning, lifecycle cancellation.
5. Run format-only cleanup separately.
6. Remove dead code/placeholders and polish XML comments.
7. Add/expand coverage matrices and negative guards.
8. Final validation: repo hook formatter, `dotnet format --verify-no-changes`, `git diff --check`, `task test:ef10`, `task test:ef11`, `task docs:build`, clean `task pack:ef10`/`task pack:ef11`, inspect nupkgs.
