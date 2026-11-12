# EF11 GA — EF Core 11 GA migration and specification-test remediation (PR B) — implementation plan

Status: plan, awaiting maintainer approval. Implementation is a separate step.

Beads: epic `dynamodb-efcore-provider-ar2` · this PR `dynamodb-efcore-provider-ar2.2` · children
`dynamodb-efcore-provider-ar2.2.1` (FindDynamoTest seed) and `dynamodb-efcore-provider-ar2.2.2`
(remaining spec-test triage) · blocks `ar2.3` (PR C docs/skills) and `ar2.4` (publish v11.0.0).

> **For agentic workers:** execute the tasks in §7 in order. Each task ends with a commit. Do not
> redesign the approach; where this plan offers a decision, the recommended option is the default
> unless the maintainer recorded a different choice in §13.

**Goal:** the `net11.0` line builds, restores and tests against EF Core 11.0.0 GA (no preview/RC
packages), the specification suite is green on both EF lines, and `docs/spec-test-coverage.md`
reflects the EF Core 11 GA spec surface.

**Architecture:** central package versions switch per `$(TargetFramework)`; the specification-test
project selects xUnit v2 (net10) or xUnit v3 4.0.1 (net11) per target with narrowly scoped `#if`
and net11-only attribute aliases. Spec-test failures are fixed at their real cause (fixture seed,
fixture table isolation, provider gap) or skipped only for durable DynamoDB constraints.

**Tech stack:** .NET 11 SDK `11.0.100` (GA), EF Core 11.0.0 / 10.0.14+, xUnit v2 2.9.3 (net10 spec),
xUnit v3 4.0.1 (net11 spec), Microsoft.Testing.Platform, DynamoDB Local via Testcontainers.

## Global constraints

- Never commit to `main`; branch first. Do not push, open the PR, or touch beads remote until the
  maintainer approves the plan (see §11 for exact bead commands).
- Do not modify: `.github/release-drafter*.yml`, `.github/workflows/publish-*.yaml`,
  `.github/workflows/release-drafter.yaml`, GitHub tags/releases, published NuGet packages, bead
  `ar2.5`.
- EF10 (`net10.0`) package selection, test framework (xUnit v2 via `YTest.MTP.XUnit2`) and spec
  totals stay exactly as on `main` (2331 total / 1368 passed / 963 skipped, 0 failed).
- Only the specification-test project changes xUnit; every other test project keeps xUnit v3
  `3.2.2` / `xunit.v3.mtp-v2 3.2.2` / `xunit.v3.aot.mtp-v2 4.0.1` exactly as today.
- Preserve the existing `NET10_0` / `NET11_0` source-compatibility blocks; they reflect real EF API
  differences.
- Do not remove NativeAOT workarounds or the NativeAOT warning baseline.
- No new skip may hide a provider gap; every new skip uses a constant from `SkipReason.cs` and is
  justified in §8's triage table (copied into the PR description).
- README, installation docs, `skills/` content, and release notes belong to PR C (`ar2.3`), not here.
- Manage package versions via `Directory.Packages.props` edits only where the `dotnet` CLI cannot
  express a TFM-conditional central version (it cannot); package *references* in the spec csproj
  are edited by hand for the same reason (TFM-conditional `ItemGroup`).
- Beads: `bd --sandbox` for every write. Never run `bd dolt push`, `bd sync`, `bd migrate`, `bd init`.

______________________________________________________________________

## 1. Confirmed current state (verified 2026-11-12 against `main` @ `8d54d89`)

| Item | State on `main` |
|---|---|
| `EFCoreVersion` (net11) | `[11.0.0-rc.1.26425.128, 12.0.0-a)` |
| `Microsoft.EntityFrameworkCore.Tasks` (net11) | `[11.0.0-rc.1.26425.128, 12.0.0-a)` |
| `Microsoft.EntityFrameworkCore.Specification.Tests` (net11) | pinned `11.0.0-preview.5.26302.115` (exemption: preview.6–rc.1 depend on unpublished `Microsoft.DotNet.XUnitV3Extensions`) |
| Microsoft.Extensions net11 group (6 packages incl. HealthChecks.EntityFrameworkCore) | `[11.0.0-rc.1.26425.128, 12.0.0-a)` |
| EF10 group | `EFCoreVersion [10.0.14, 11.0.0)`, Tasks `10.0.12`, HealthChecks `10.0.12`, Extensions `[10.0.14, 11.0.0)` — unchanged by this PR |
| Spec csproj | xUnit v2 for both TFMs (`xunit.runner.visualstudio`, `YTest.MTP.XUnit2`) |
| Local SDK | `11.0.100` (GA) installed; CI uses `dotnetVersion: 11.0.x` |
| `global.json` | no SDK pin; MTP runner — leave unchanged (epic decision: no SDK pin) |

GA package facts (from the local NuGet cache, nuspecs read directly):

- `Microsoft.EntityFrameworkCore.Specification.Tests 11.0.0` depends on `xunit.v3.assert 4.0.1` and
  `xunit.v3.extensibility.core 4.0.1`, EF `11.0.0`, and **no longer** references
  `Microsoft.DotNet.XUnitV3Extensions` → the preview.5 exemption can be removed.
- `Specification.Tests 10.0.14` still depends on `xunit.core 2.9.3` / `xunit.runner.visualstudio 3.1.3`.
- `xunit.v3.mtp-v2 4.0.1` pins `xunit.v3.core.mtp-v2 [4.0.1]` / `xunit.v3.assert [4.0.1]` (exact
  match to EF's floor). `4.0.2` exists; do not use it in this PR (§12).
- `Microsoft.EntityFrameworkCore.Tasks 11.0.0` and
  `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore 11.0.0` exist.

## 2. Scratch prototype (reusable as a reference, not as a patch source)

Location (previous session scratchpad, this machine only):
`/private/tmp/claude-501/-Users-ncipollina-source-repos-layered-craft-dynamodb-efcore-provider/de93af65-fc87-48f9-9f82-b6ecd284044a/scratchpad/ga`

- Standalone git repo; base tree is byte-identical to `main` `src/` and `tests/` (verified), plus a
  "scratch" commit containing only the `Directory.Packages.props` GA bump, plus 15 uncommitted
  spec-test files (the xUnit migration + seed fix).
- Logs alongside it: `spec-base-net10.0.log`, `spec-base-net11.0.log` (main),
  `spec-ga-net10.0.log` (2331/1368/963, identical to main), `spec-ga2-net11.0.log` (2440 total,
  479 failed before the seed fix → 107 after).
- `/private/tmp` is volatile. The implementer must **re-apply the changes from this plan** (all
  hunks are reproduced in §6/§7), not copy files from the scratch tree. The prototype may be used to
  diff against if it still exists.

## 3. Root-cause findings for the 107 remaining net11 failures

Clustered from `spec-ga2-net11.0.log` (FindDynamoTest excluded):

| Class | Failures | Cluster |
|---|---:|---|
| `NorthwindWhereQueryDynamoTest` | 51 | **Table collision** (see 3.1) |
| `NorthwindAsNoTrackingQueryDynamoTest` | 3 | **Table collision** (see 3.1) |
| `NorthwindSelectQueryDynamoTest` | 19 | 16 new GA theory rows `Multiple_members_of_correlated_single_result_subquery_lift_to_single_join(bool, string)`, 2 new `SelectMany_over_inline_array_projecting_range_variable_and_outer`, 1 guard |
| `ComplexTypeQueryDynamoTest` | 19 | 18 new GA tests (former ad-hoc complex-type scenarios now in the base) + 1 guard |
| `NorthwindQueryFiltersQueryDynamoTest` | 7 | 6 new `GroupBy_aggregate_through_filtered_navigation*` rows + 1 guard |
| `PrimitiveCollectionsQueryDynamoTest` | 4 | new `Inline_collection_SelectMany_with_unreferenced_collection_value`, `Min_on_MemoryExtensions`, `Max_on_MemoryExtensions` + 1 guard |
| `ComplexPropertiesProjectionDynamoTest` | 3 | new `Select_optional_associate_and_ints` (2 rows: ints list materialized as `null`) + 1 guard (also lists passing new `Select_required_associate_duplicated`, `Select_required_associate_and_optional_associate`) |
| `StringTranslationsDynamoTest` | 1 | guard: upstream renamed `Equals` → `Instance_Equals` |

### 3.1 Table-name collision between spec fixtures (test-infrastructure bug, 54 failures)

Evidence:

- `ComplexTypeQueryDynamoFixture` maps its `Customer` to DynamoDB table **`Customers`** with
  partition key `Id` (`tests/.../Query/ComplexTypeQueryDynamoTest.cs:551`).
  `NorthwindQueryDynamoFixture` maps Northwind `Customer` to the same table **`Customers`** with
  partition key `CustomerID` (`Query/NorthwindQueryDynamoFixture.cs:64`). DynamoDB Local table names
  are global for the shared container; `DynamoTestStore` does not namespace them, and fixture
  `CleanAsync` runs `EnsureDeleted` + `EnsureCreated` on every mapped table.
- The Northwind store is shared and seeded once per process (EF `TestStore` shared-store index), so
  any later fixture that recreates `Customers` silently replaces Northwind data for every Northwind
  class that runs afterwards.
- Execution order on `main` (xUnit v2): `ComplexTypeQuery` (log line 2174) runs **before** every
  Northwind class (≥2669). Order on GA (xUnit v3): `NorthwindSelect` (2615) → `ComplexTypeQuery`
  (3814) → `NorthwindWhere` (8176) → `NorthwindAsNoTracking` (9412). Exactly the two classes that
  run after `ComplexTypeQuery` fail.
- Failure signatures match: 11 × `Required property 'Customer.customerID' was not present in the
  DynamoDB item` (ComplexType items have `id`, not `customerID`) and 43 × `Expected: N, Actual: 0`
  (Northwind customers gone).

So these are not GA regressions, not provider gaps, and must **not** be skipped. The fix is to
give `ComplexTypeQueryDynamoFixture.Customer` its own table (§7 Task 4).

Other duplicated table names (latent ordering hazards, currently not failing):

| Table(s) | Fixtures | Assessment |
|---|---|---|
| `Customers`, `Orders`, `Products`, `Employees` | `NorthwindBulkUpdatesDynamoFixture`, `NorthwindQueryDynamoFixture` | Same entity shape and same `NorthwindData` seed; BulkUpdates ran first in both orders. Verify in Task 4 Step 6; fix only if a failure is observed (§13 D3). |
| `RootEntities`, `ValueRootEntities` | three `ComplexProperties*DynamoTest` fixtures | Same upstream associations model and seed. Verify only. |
| `Branes`, `Singularities` | `QueryExpressionInterceptionDynamoTest`, `SaveChangesInterceptionDynamoTest` | Same upstream interception model. Verify only. |

### 3.2 Preliminary classification of the other 53 (to be confirmed with evidence in Task 6)

| Method(s) | Observed | Preliminary class | Candidate action |
|---|---|---|---|
| `Multiple_members_of_correlated_single_result_subquery_lift_to_single_join` (16 rows) | `could not be translated` (correlated subquery over `Orders`) | Architectural (join / correlated subquery) | `SkipReason.JoinsNotSupported` or `SubqueryPushdownNotSupported` — pick the one matching the upstream body |
| `SelectMany_over_inline_array_projecting_range_variable_and_outer` (2), `Inline_collection_SelectMany_with_unreferenced_collection_value` (1) | `could not be translated` | Architectural (SelectMany = cross apply) | Reuse the reason already used for existing SelectMany/inline-collection overrides in the same class |
| `Min_on_MemoryExtensions`, `Max_on_MemoryExtensions` | `Method ... could not be translated` | Likely same as existing `Inline_collection_Min/Max_*` skips (`PrimitiveCollectionQueryShapeNotSupported`) | Confirm upstream body is an inline-collection Min/Max over a column (not a pure parameter evaluation); if it is parameter-only and EF can funcletize it, treat as provider gap |
| `GroupBy_aggregate_through_filtered_navigation{,_with_total,_ignore_query_filters}` (6 rows) | `could not be translated` | Architectural (GroupBy + navigation) | `GroupByNotSupported` (or `NavigationPropertiesNotSupported` if the navigation is the first blocker) |
| `ComplexTypeQuery` discriminator tests (7): `Optional_complex_type_with_discriminator`, `Nullable_complex_type_with_discriminator_*`, `Update_entity_with_nullable_complex_type_and_discriminator_does_not_throw` | `No backing field could be found for property ...` | **Unclear — may be a provider gap** (complex-type discriminator/shadow property handling) | Scout required; do not skip without evidence |
| `ComplexTypeQuery` index/alternate-key tests (5): `Can_query_by_complex_type_property_with_index`, `Can_update/delete_entity_with_index_on_complex_type_property`, `Can_query_by_alternate_key_on_complex_type_property`, `Can_save_batch_swapping_alternate_key_values_on_complex_type_property` | `The partition key property ... does not exist` | Likely architectural (alternate keys / unique indexes; ad-hoc model without a DynamoDB key mapping) | Matches the existing rationale for `AdHocComplexTypeQueryTestBase` (Skip) in the coverage doc; confirm and choose/introduce a centralized reason |
| `ComplexTypeQuery` scan-`Single` tests (4): `Nested_nullable_complex_type_with_discriminator_null_to_non_null_roundtrip`, `Non_optional_complex_type_with_all_nullable_properties`, `Nullable_complex_type_with_discriminator_null_to_non_null_roundtrip`, `Complex_type_equals_parameter_with_nested_types_with_property_of_same_name` | `Single/SingleOrDefault on a scan-like path ... ` | **Test/fixture configuration** candidate (scan opt-in), else provider guard by design | Check whether these ad-hoc contexts receive fixture options; if the provider's scan-`Single` guard is intentional, adding the scan opt-in to the ad-hoc context options is a fixture fix, not a query rewrite |
| `Projecting_complex_property_does_not_auto_include_owned_types` | owned entity type not supported | Architectural | `OwnedEntityTypesNotSupported` |
| `Non_optional_complex_type_with_all_nullable_properties_via_left_join` | FK relationship not supported | Architectural | `ForeignKeysNotSupported` / `JoinsNotSupported` |
| `Select_optional_associate_and_ints` (2 rows) | ints list is `null` | **Likely provider gap** (primitive collection materialization alongside optional complex projection) | Scout required; fix in projection/materialization if confirmed |
| Guards (6 classes) | missing overrides | Test infra | Add overrides (Task 5) |

## 4. Files to modify

| File | Change | Task |
|---|---|---|
| `Directory.Packages.props` | net11 GA versions; drop Specification.Tests exemption | 1 |
| `tests/EntityFrameworkCore.DynamoDb.SpecificationTests/EntityFrameworkCore.DynamoDb.SpecificationTests.csproj` | per-TFM xUnit packages; net11 `NoWarn` | 2 |
| `tests/.../SpecificationTests/TestUtilities/XunitV3Compatibility.cs` (new) | net11 `ConditionalFact/Theory` global aliases | 2 |
| `tests/.../SpecificationTests/AssemblyFixtures.cs` | `#if !NET11_0_OR_GREATER` around `CollectionBehavior` | 2 |
| `tests/.../SpecificationTests/xunit.runner.json` | `"parallelizeTestCollections": false` | 2 |
| `tests/.../SpecificationTests/TestUtilities/DynamoSpecificationContainerFixture.cs` | `IAsyncLifetime` `ValueTask` under `#if` | 2 |
| `tests/.../SpecificationTests/SeedingDynamoTest.cs` | `IAsyncLifetime` `ValueTask` under `#if` | 2 |
| `tests/.../SpecificationTests/EntityFrameworkServiceCollectionExtensionsDynamoTest.cs`, `LoggingDynamoTest.cs`, `ValueConvertersEndToEndDynamoTest.cs` | guard reflection uses `FactAttribute`/`TheoryAttribute` | 2 |
| `tests/.../SpecificationTests/CustomConvertersDynamoTest.cs` | `Optional_owned_with_converter_reading_non_nullable_column` returns `Task` on net11 | 2 |
| `tests/.../SpecificationTests/Query/PrimitiveCollectionsQueryDynamoTest.cs` | `*_different_type_mapping` → `*_different_property` on net11 (2 methods) | 2 |
| `tests/.../SpecificationTests/Query/Translations/StringTranslationsDynamoTest.cs` | `Equals` override net10-only | 2 (+ `Instance_Equals` in 5) |
| `tests/.../SpecificationTests/Query/FunkyDataQueryDynamoTest.cs` | `c.FirstName!` (GA nullable annotation) | 2 |
| `tests/.../SpecificationTests/Query/Associations/ComplexProperties/ComplexPropertiesProjectionDynamoTest.cs` | `[ConditionalTheory]` on `Select_associate_and_target_to_index_based_binding_via_closure`; new overrides (Task 5) | 2, 5 |
| `tests/.../SpecificationTests/FindDynamoTest.cs` | seed `NestedOwnedCollection = []` (ar2.2.1) | 3 |
| `tests/.../SpecificationTests/Query/ComplexTypeQueryDynamoTest.cs` | own table name; new GA overrides; baselines | 4, 5, 6 |
| `tests/.../SpecificationTests/Query/NorthwindSelectQueryDynamoTest.cs`, `NorthwindQueryFiltersQueryDynamoTest.cs` | new GA overrides (net11-only `#if` where the base method does not exist on EF10) | 5, 6 |
| `tests/.../SpecificationTests/SkipReason.cs` | only if a new centralized reason is needed | 6 |
| `src/EntityFrameworkCore.DynamoDb/**` | only for provider gaps confirmed in Task 6 (+ unit/integration test per repo rules) | 6 |
| `tests/.../SpecificationTests/AGENTS.md` | describe per-TFM xUnit, alias file, `xunit.runner.json` parallelization | 7 |
| `docs/spec-test-coverage.md` | GA EF11 surface, counts, rationale | 7 |
| `docs/plans/ef11-ga-migration.md` | this plan (commit with the PR) | 1 |

Not modified: other test csproj files, `Directory.Build.props`, `Taskfile.yml`, CI workflows,
`global.json`, NativeAOT scripts/baselines, release-drafter configs, README/docs outside the two above.

## 5. Dependency changes (`Directory.Packages.props`)

Exact edits (net10 lines untouched):

```diff
     <PackageVersion Include="Microsoft.EntityFrameworkCore.Tasks"
-                    Version="[11.0.0-rc.1.26425.128, 12.0.0-a)"
+                    Version="11.0.0"
                     Condition="'$(TargetFramework)' == 'net11.0'" />
```

```diff
     <EFCoreVersion Condition="'$(TargetFramework)' == 'net11.0'">
-      [11.0.0-rc.1.26425.128, 12.0.0-a)
+      [11.0.0, 12.0.0)
     </EFCoreVersion>
```

Replace the whole Specification.Tests block (comment + two conditional entries) with:

```xml
    <PackageVersion Include="Microsoft.EntityFrameworkCore.Specification.Tests"
                    Version="$(EFCoreVersion)" />
```

Net11 Microsoft.Extensions group:

```xml
  <ItemGroup Label="Microsoft Extensions net11" Condition="'$(TargetFramework)' == 'net11.0'">
    <PackageVersion Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore"
                    Version="11.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Caching.Abstractions" Version="[11.0.0, 12.0.0)" />
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions"
                    Version="[11.0.0, 12.0.0)" />
    <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="[11.0.0, 12.0.0)" />
    <PackageVersion Include="Microsoft.Extensions.Options" Version="[11.0.0, 12.0.0)" />
    <PackageVersion Include="Microsoft.Extensions.Primitives" Version="[11.0.0, 12.0.0)" />
  </ItemGroup>
```

Rationale: ranges mirror the EF10 shape (`[10.0.14, 11.0.0)`) so consumers get the lowest compatible
GA floor; `Tasks` and `HealthChecks.EntityFrameworkCore` are exact pins mirroring the EF10 pattern
(`10.0.12`) — `Tasks` must match the resolved EF runtime for query precompilation. `[11.0.0, 12.0.0)`
resolves to `11.0.0` (NuGet lowest-applicable).

Shared entries (`xunit.v3`, `xunit.v3.mtp-v2 3.2.2`, `xunit.runner.visualstudio`, `YTest.MTP.XUnit2`,
etc.) are **not** changed; the spec project overrides `xunit.v3.mtp-v2` locally (§6).

## 6. xUnit compatibility design (spec-test project only)

| Concern | net10.0 (EF10 Spec.Tests → xUnit v2) | net11.0 (EF11 GA Spec.Tests → xUnit v3 4.0.1) |
|---|---|---|
| Packages | `xunit.runner.visualstudio` + `YTest.MTP.XUnit2` (unchanged) | `xunit.v3.mtp-v2` `VersionOverride="4.0.1"` |
| `[ConditionalFact]`/`[ConditionalTheory]` | EF's own attributes (v2) | `global using` aliases to `Xunit.FactAttribute`/`TheoryAttribute`. A subclass shim does **not** work: xUnit v3 rejects a method carrying two different Fact-derived attributes (base `[Fact]` + override `[ConditionalFact]`). |
| Override guards that reflect attributes | `typeof(FactAttribute)` / `typeof(TheoryAttribute)` — valid on both: EF10's `ConditionalFact` derives from `FactAttribute` | same |
| `IAsyncLifetime` | `Task InitializeAsync()` / `Task DisposeAsync()` | `ValueTask` (v3) under `#if NET11_0_OR_GREATER` |
| Parallelization | `[assembly: CollectionBehavior(DisableTestParallelization = true)]` + json | json `"parallelizeTestCollections": false` only (attribute excluded on net11) |
| Analyzer noise | none today | `NoWarn xUnit1003;xUnit1024` (overrides inherit `[MemberData]`; some non-virtual base tests are hidden) — **net11-only** PropertyGroup |
| Runner filter flags | `--filter` / `--treenode-filter` | `--filter-class`, `--filter-method`, `--filter-not-class` (wildcards) |

`DynamoTestHelpers.AssertAllTestMethodsOverridden` (the per-class guard) already works on both; only
the three hand-rolled guard tests listed in §4 needed the attribute switch.

csproj target shape:

```xml
  <PropertyGroup Condition="'$(TargetFramework)' == 'net11.0'">
    <!-- EF provider pattern: overrides inherit [MemberData] and hide some non-virtual base tests. -->
    <NoWarn>$(NoWarn);xUnit1003;xUnit1024</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="Testcontainers.DynamoDb" />
  </ItemGroup>

  <!-- EF10 Specification.Tests is built on xUnit v2. -->
  <ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
    <PackageReference Include="xunit.runner.visualstudio">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
    <PackageReference Include="YTest.MTP.XUnit2" />
  </ItemGroup>

  <!-- EF11 Specification.Tests is built on xUnit v3 4.x; isolated here so other test projects keep 3.2.2. -->
  <ItemGroup Condition="'$(TargetFramework)' == 'net11.0'">
    <PackageReference Include="xunit.v3.mtp-v2" VersionOverride="4.0.1" />
  </ItemGroup>
```

`TestUtilities/XunitV3Compatibility.cs` (new, entire file):

```csharp
#if NET11_0_OR_GREATER
// EF Core 11 Specification.Tests (xUnit v3) marks base tests with plain [Fact]/[Theory]. xUnit v3 rejects a
// test whose override carries a *different* Fact-derived attribute ("multiple [Fact]-derived attributes"),
// so on EF11 the shared [ConditionalFact]/[ConditionalTheory] spellings must resolve to the exact same types.
global using ConditionalFactAttribute = Xunit.FactAttribute;
global using ConditionalTheoryAttribute = Xunit.TheoryAttribute;
#endif
```

## 7. Implementation sequence

Branch: `feat/ef11-ga-migration` from up-to-date `main`. Claim beads first (§11).
Docker must be running (DynamoDB Local). Spec-project shorthand used below:

```bash
SPEC=tests/EntityFrameworkCore.DynamoDb.SpecificationTests/EntityFrameworkCore.DynamoDb.SpecificationTests.csproj
```

### Task 1 — GA dependencies (bead `ar2.2`)

- [ ] Commit this plan file unchanged as the first commit (`docs(plans): add EF11 GA migration plan`).
- [ ] Apply §5 exactly.
- [ ] `dotnet restore EntityFrameworkCore.DynamoDb.slnx` — expected: succeeds; the net11 spec project
      fails **only** on xUnit-v3 compile/restore issues addressed in Task 2 (acceptable at this step:
      do not build the spec project yet). Check: `grep -E "NU1605|NU1608|NU1102|NU1103"` on restore
      output → no matches.
- [ ] Confirm resolution: `dotnet list src/EntityFrameworkCore.DynamoDb/EntityFrameworkCore.DynamoDb.csproj package --framework net11.0`
      → `Microsoft.EntityFrameworkCore 11.0.0`, Extensions `11.0.0`; same command with
      `--framework net10.0` → identical to `main` (`git stash` + rerun to compare if unsure).
- [ ] `grep -rn "rc\.1\.26425\|preview\.5\.26302" Directory.Packages.props` → no output.
- [ ] Commit: `build(deps): move EF11 line to EF Core 11.0.0 GA`.

### Task 2 — Spec-test xUnit per-target migration (bead `ar2.2`)

Apply all of the following (hunks are exactly the validated prototype):

- [ ] csproj per §6.
- [ ] Add `TestUtilities/XunitV3Compatibility.cs` per §6.
- [ ] `AssemblyFixtures.cs`:
  ```csharp
  #if !NET11_0_OR_GREATER
  [assembly: CollectionBehavior(DisableTestParallelization = true)]
  #endif
  ```
- [ ] `xunit.runner.json`:
  ```json
  {
    "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
    "parallelizeTestCollections": false
  }
  ```
- [ ] `DynamoSpecificationContainerFixture.cs` — `InitializeAsync`/`DisposeAsync`:
  ```csharp
  #if NET11_0_OR_GREATER
      public async ValueTask InitializeAsync() =>
  #else
      public async Task InitializeAsync() =>
  #endif
          await GetClientAsync().ConfigureAwait(false);
  ```
  and the same shape for `DisposeAsync` → `await DisposeContainerAsync().ConfigureAwait(false);`
  (keep the existing XML `<summary>` lines above each).
- [ ] `SeedingDynamoTest.cs`:
  ```csharp
  #if NET11_0_OR_GREATER
      public ValueTask InitializeAsync() => ValueTask.CompletedTask;
  #else
      public Task InitializeAsync() => Task.CompletedTask;
  #endif

  #if NET11_0_OR_GREATER
      public async ValueTask DisposeAsync() =>
  #else
      public async Task DisposeAsync() =>
  #endif
          await _testStore.DisposeAsync().ConfigureAwait(false);
  ```
- [ ] In `EntityFrameworkServiceCollectionExtensionsDynamoTest.cs`, `LoggingDynamoTest.cs`,
      `ValueConvertersEndToEndDynamoTest.cs`: replace
      `typeof(ConditionalFactAttribute)` → `typeof(FactAttribute)` and
      `typeof(ConditionalTheoryAttribute)` → `typeof(TheoryAttribute)` in the guard `Where` clause.
- [ ] `CustomConvertersDynamoTest.cs`:
  ```csharp
      [ConditionalFact(Skip = SkipReason.OwnedEntityTypesNotSupported)]
  #if NET11_0_OR_GREATER
      public override Task Optional_owned_with_converter_reading_non_nullable_column()
  #else
      public override void Optional_owned_with_converter_reading_non_nullable_column()
  #endif
          => base.Optional_owned_with_converter_reading_non_nullable_column();
  ```
- [ ] `PrimitiveCollectionsQueryDynamoTest.cs`: wrap the two `*_over_2_operations_same_parameter_different_type_mapping`
      overrides so net11 overrides `*_different_property` instead (same skip attribute, same body
      calling the renamed base). Methods:
      `Parameter_collection_Count_with_huge_number_of_values_over_2_operations_same_parameter_different_{type_mapping|property}` and
      `Parameter_collection_of_ints_Contains_int_with_huge_number_of_values_over_2_operations_same_parameter_different_{type_mapping|property}`.
- [ ] `StringTranslationsDynamoTest.cs`: wrap the existing `Equals` override in `#if !NET11_0_OR_GREATER … #endif`
      (`Instance_Equals` is added in Task 5).
- [ ] `FunkyDataQueryDynamoTest.cs`: in the provider-authored query lambdas, `c.FirstName.` →
      `c.FirstName!.` (GA annotated `FunkyCustomer.FirstName` as `string?`; compile warning-as-error otherwise). No semantic change.
- [ ] `ComplexPropertiesProjectionDynamoTest.cs`: inside the existing `#if !NET10_0` block, add
      `[ConditionalTheory]` above `[MemberData(nameof(TrackingData))]` on
      `Select_associate_and_target_to_index_based_binding_via_closure` (xUnit v3 requires the
      attribute on the override when `MemberData` is present).
- [ ] Build both: `task build:all` → 0 errors, no new warnings compared with `main` on net10.
- [ ] Discovery check:
      `dotnet test --project $SPEC --framework net11.0 --no-build --list-tests | tail -3` and the
      same for `net10.0` → both list tests (net10 count unchanged from `main`).
- [ ] Run net10 spec: `dotnet test --project $SPEC --framework net10.0 --no-build` → exactly
      2331 / 1368 passed / 963 skipped / 0 failed.
- [ ] Run net11 spec once, save output (`> artifacts/spec-net11-task2.log`, `artifacts/` is
      gitignored). Expected ≈2440 total, ≈479 failed (372 Find + 107).
- [ ] Commit: `test(spec): run EF11 GA specification tests on xUnit v3 per target`.

### Task 3 — FindDynamoTest seed (bead `ar2.2.1`)

- [ ] In `FindDynamoTest.cs` seed (the `OwnedCollection = [new Owned1 { Prop = 71 }, new Owned1 { Prop = 72 }]` line):
  ```csharp
                      OwnedCollection =
                      [
                          new Owned1 { Prop = 71, NestedOwnedCollection = [] },
                          new Owned1 { Prop = 72, NestedOwnedCollection = [] }
                      ]
  ```
- [ ] `dotnet test --project $SPEC --framework net11.0 --no-build --filter-class "*FindDynamoTest*"` (after build) → 0 failed.
- [ ] net10: `dotnet test --project $SPEC --framework net10.0 --no-build --filter "FullyQualifiedName~FindDynamoTest"` → pass/skip counts identical to Task 2's net10 run for Find. (The net10 runner is
      `YTest.MTP.XUnit2`, which takes `--filter`/`--treenode-filter`, not `--filter-class`; if the
      filter syntax is rejected, run the full net10 spec suite instead.)
- [ ] Commit: `test(spec): seed required nested complex collection in FindDynamoTest` (body: `Bead: dynamodb-efcore-provider-ar2.2.1`).

### Task 4 — Fixture table isolation (bead `ar2.2.2`)

- [ ] Prove the collision before fixing (evidence for the PR):
      `dotnet test --project $SPEC --framework net11.0 --no-build --filter-class "*NorthwindWhereQueryDynamoTest*" --filter-class "*NorthwindAsNoTrackingQueryDynamoTest*"`
      → expected 0 failed (the 54 failures disappear without `ComplexTypeQuery` in the run). If they
      do **not** disappear, stop and report; §3.1 is wrong.
- [ ] In `ComplexTypeQueryDynamoTest.cs` fixture `OnModelCreating`: `entity.ToDynamoTable("Customers")`
      → `entity.ToDynamoTable("ComplexTypeCustomers")`. Leave `ValuedCustomers` (no collision).
- [ ] Update every `AssertSql` baseline in that file that reads `FROM "Customers"` →
      `FROM "ComplexTypeCustomers"` (7 occurrences on `main`; re-count with
      `grep -c 'FROM "Customers"'`). Do not change any other baseline text.
- [ ] Build; run `--filter-class "*ComplexTypeQueryDynamoTest*"` on both TFMs → no new failures
      versus Task 2 (net10: identical; net11: only the GA-new tests from §3.2).
- [ ] Run the full net11 spec suite → `NorthwindWhere` and `NorthwindAsNoTracking` 0 failed.
- [ ] Latent-collision check (§3.1 second table): scan the full-run log for failures in any class
      using `Orders/Products/Employees/RootEntities/ValueRootEntities/Branes/Singularities`. Expected:
      none. If any appear, apply the same rename pattern to the *non-Northwind/non-upstream-default*
      fixture and record it in the PR.
- [ ] Commit: `test(spec): isolate ComplexTypeQuery customers table from Northwind fixtures`.

### Task 5 — Guard overrides for GA-added base methods (bead `ar2.2.2`)

For each class below, add one override per method listed by its `Check_all_tests_overridden`
failure (re-read the guard output from the current run; the list in §3 is from the prototype).
First pass rule (spec-tests skill): call `base` (wrapped in `NoSyncTest` for `bool async`
query theories), no classification skips yet. Methods that do not exist in EF10 Spec.Tests
go inside `#if !NET10_0 … #endif` (the existing convention in these files); verify by building
net10.

- `NorthwindSelectQueryDynamoTest`: `SelectMany_over_inline_array_projecting_range_variable_and_outer(bool async)`,
  `Multiple_members_of_correlated_single_result_subquery_lift_to_single_join(bool async, string method)`
  (copy the upstream `[MemberData]`/`[InlineData]` attribute shape so theory rows match).
- `NorthwindQueryFiltersQueryDynamoTest`: `GroupBy_aggregate_through_filtered_navigation`,
  `GroupBy_aggregate_through_filtered_navigation_with_total`,
  `GroupBy_aggregate_through_filtered_navigation_ignore_query_filters` (all `bool async`).
- `PrimitiveCollectionsQueryDynamoTest`: `Inline_collection_SelectMany_with_unreferenced_collection_value`,
  `Min_on_MemoryExtensions`, `Max_on_MemoryExtensions`.
- `ComplexTypeQueryDynamoTest`: the 17+ methods from its guard (§3 list).
- `ComplexPropertiesProjectionDynamoTest`: `Select_required_associate_duplicated`,
  `Select_required_associate_and_optional_associate`, `Select_optional_associate_and_ints`
  (`QueryTrackingBehavior`, `[MemberData(nameof(TrackingData))]` + `[ConditionalTheory]`).
- `StringTranslationsDynamoTest`: net11-only `Instance_Equals`, porting the baseline from the
  removed `Equals` override:
  ```csharp
  #if NET11_0_OR_GREATER
      public override async Task Instance_Equals()
      {
          await base.Instance_Equals();

          AssertSql(
              """
              SELECT "id", "$type", "bool", "byte", "byteArray", "dateOnly", "dateTime", "dateTimeOffset", "decimal", "double", "enum", "flagsEnum", "float", "guid", "int", "long", "short", "string", "timeOnly", "timeSpan"
              FROM "BasicTypesEntity"
              WHERE "string" = 'Seattle'
              """);
      }
  #endif
  ```
  If the captured PartiQL differs, use the captured text after confirming the query shape is the
  same `WHERE "string" = 'Seattle'` predicate.

- [ ] All six `Check_all_tests_overridden` guards pass on net11; net10 guards still pass.
- [ ] Commit: `test(spec): override EF Core 11 GA specification test additions`.

### Task 6 — Evidence-based triage of the remaining failures (bead `ar2.2.2`)

Run the full net11 suite and collect remaining failures. Expected starting set: §3.2 rows
(~49 test runs). Process, per the `dynamodb-efcore-specification-tests` skill:

1. Get the upstream bodies from EF Core **v11.0.0** source (not memory):
   `git clone --depth 1 --branch v11.0.0 https://github.com/dotnet/efcore "$TMPDIR/efcore-v11"`
   (read `test/EFCore.Specification.Tests/...`). Compare with Cosmos
   (`test/EFCore.Cosmos.FunctionalTests/...`) when a classification is unclear.
2. Group into clusters (table in §3.2). One scout (fresh-context subagent) per *unclear* cluster:
   ComplexType discriminator (7), ComplexType scan-`Single` (4), `Select_optional_associate_and_ints`
   (2), `Min/Max_on_MemoryExtensions` (2). Clear architectural clusters may be classified directly
   with a cited upstream line.
3. Each cluster ends as exactly one of:
   - **Provider fix** — implement in the provider layer named by the scout; add a unit or integration
     test per `tests/AGENTS.md`; spec override calls `base`. Any provider change is reported to the
     maintainer before commit if it touches public API or changes existing behaviour (§13 D4).
   - **Fixture/test fix** — e.g. scan opt-in for ad-hoc contexts via fixture options
     (`AllowUnsafeFilteredQueries()` / `ConfigureWarnings` precedent in
     `ComplexTypesTrackingDynamoTest.cs:853` and the ComplexType fixture), never by rewriting the
     upstream query.
   - **Architectural skip** — `[ConditionalFact/Theory(Skip = SkipReason.X)]`, body still delegates
     to `base`. Prefer existing constants: `JoinsNotSupported`, `SubqueryPushdownNotSupported`,
     `GroupByNotSupported`, `NavigationPropertiesNotSupported`, `OwnedEntityTypesNotSupported`,
     `ForeignKeysNotSupported`, `PrimitiveCollectionQueryShapeNotSupported`. Add a new constant to
     `SkipReason.cs` only for a genuinely new durable constraint (likely candidate: alternate
     keys/unique indexes on complex-type properties, if no existing constant fits).
   - **Sync path** — `NoSyncTest` (not a skip).
4. Forbidden: blanket/class-level skips, skips citing a provider gap without a tracked bead and
   maintainer approval, rewriting upstream bodies, client-side evaluation substitutions.
5. Produce the triage table (method · upstream intent/line · error · evidence · classification ·
   action) in `artifacts/ef11-spec-triage.md` (not committed) and paste it into the PR description.
6. If a confirmed provider gap is too large for this PR, stop and ask the maintainer (§13 D4); if
   deferral is approved, create **one** bead
   `bd --sandbox create "<gap>" -t bug -p 2 --parent dynamodb-efcore-provider-ar2` and cite its ID
   in a new centralized `SkipReason` constant.

- [ ] net11 spec suite: 0 failed. net10 spec suite: identical to `main`.
- [ ] Commit(s): `test(spec): classify EF Core 11 GA specification tests` and, separately,
      `fix(query): ...` for each provider fix (with its own tests).

### Task 7 — Coverage and test-infrastructure docs (bead `ar2.2.2`)

- [ ] `docs/spec-test-coverage.md`:
  - Method counts for every class whose EF11 surface changed: `ComplexTypeQueryTestBase` (+18 GA
    methods), `NorthwindSelectQueryTestBase` (+2), `NorthwindQueryFiltersQueryTestBase` (+3),
    `PrimitiveCollectionsQueryTestBase` (+3, renamed 2), `ComplexPropertiesProjectionTestBase` (+3),
    `StringTranslationsTestBase` (`Equals` → `Instance_Equals` on EF11). Recount from the guard
    output / upstream source, do not estimate.
  - `AdHocComplexTypeQueryTestBase` row (Skip): note that on EF11 GA several of its scenarios moved
    into `ComplexTypeQueryTestBase` and are now classified there; keep its EF10 rationale.
  - Notes column: user-facing reasons for every new skip cluster (e.g. "correlated single-result
    subqueries lifted to joins are skipped because DynamoDB PartiQL has no joins").
  - Coverage Summary table totals updated to match.
  - Add one sentence near the top: EF10 spec tests run on xUnit v2 and EF11 on xUnit v3 (from
    EF Core 11's own test package); counts are per EF line where they differ.
  - No internal file/test references in user-facing prose (repo docs rule).
- [ ] `tests/EntityFrameworkCore.DynamoDb.SpecificationTests/AGENTS.md`: update the
      `AssemblyFixtures.cs` row ("net10 only; net11 uses `xunit.runner.json`"), add a row for
      `TestUtilities/XunitV3Compatibility.cs`, and a short "EF11 / xUnit v3" note covering the
      aliases, `ValueTask` `IAsyncLifetime`, `[ConditionalTheory]` required alongside `[MemberData]`,
      guard attributes, per-TFM filter flags, and the **unique table name per fixture** rule (§3.1).
- [ ] `task docs:build` → succeeds.
- [ ] Commit: `docs(spec): refresh specification coverage for EF Core 11 GA`.

### Task 8 — Full validation, PR

- [ ] Run §9 in full. Record actual numbers.
- [ ] `git diff main --stat` contains only files from §4 (+ provider files from Task 6 if any).
- [ ] Push branch, open PR with `.github/pull_request_template.md`:
  - Title: `feat(ef11): target EF Core 11.0.0 GA` (autolabeler → `type: feat`; §13 D1).
  - Label: `ef11-only` (dependency change is EF11-line only; test-only edits shared with net10 do
    not change EF10 shipped behaviour).
  - Body: summary, §3.1 root cause, triage table, validation numbers (EF10 before/after, EF11
    totals), beads `ar2.2`, `ar2.2.1`, `ar2.2.2`.
  - Docs checkbox: updated (`docs/spec-test-coverage.md`). Provider-skill checkbox: not applicable —
    no consumer-facing behaviour change; GA install/skill updates are PR C (`ar2.3`).
- [ ] Stop. Do not merge, re-run release drafter, or edit releases.

## 8. Specification-test triage — efficiency rules

- Always triage from a **full-suite** run (ordering matters, §3.1), then re-verify single clusters
  with `--filter-class`/`--filter-method`.
- Save each full run to `artifacts/spec-net11-<step>.log`; extract failures with
  `grep -E "^failed " artifacts/spec-net11-<step>.log`.
- Cluster by exception text with class name stripped; classify clusters, not individual rows.
- Theory rows count separately in totals; the PR table lists methods with row counts.

## 9. Validation commands and expected outcomes

| # | Command | Expected |
|---|---|---|
| 1 | `dotnet restore EntityFrameworkCore.DynamoDb.slnx` | success; no `NU1605`/`NU1608`/`NU1102`/`NU1103` |
| 2 | `task build:all` | 0 errors on net10 and net11 |
| 3 | `task test:ef10` | all projects pass; spec 2331 / 1368 / 963 / 0 failed (identical to `main`); unit 942 passed |
| 4 | `task test:ef11` | all projects pass, 0 failed; unit 942; integration 474 (1 skipped); spec ≈2440 total, 0 failed |
| 5 | `task test:aot-generation FRAMEWORK=net10.0` and `FRAMEWORK=net11.0` | 38 passed each |
| 6 | `task test:aot-publish FRAMEWORK=net10.0` and `FRAMEWORK=net11.0` | publish succeeds, `verify-nativeaot-warnings.sh` passes against the existing baseline, 20 runtime tests pass |
| 7 | `scripts/write-target-framework-override.sh --clear` then `git status` | no `TargetFrameworkOverride.props` left behind |
| 8 | `task docs:build` | success |
| 9 | `grep -rn "rc\.1\.26425\|preview\.5\.26302" Directory.Packages.props src tests --include='*.props' --include='*.csproj'` | no output |
| 10 | Count `Skip = ` in spec project vs `main` (`git diff main -- tests/EntityFrameworkCore.DynamoDb.SpecificationTests | grep -c '^+.*Skip = '`) | every added skip appears in the triage table |

Use the .NET test MCP server when available; the CLI commands above are the fallback. Unit and
integration counts are from the GA scratch validation; a different count must be explained, not
silently accepted.

## 10. EF10 regression safeguards

- `Directory.Packages.props` net10 entries are not edited (diff review: only net11/`$(EFCoreVersion)`
  -driven lines change; `Specification.Tests` keeps resolving `$(EFCoreVersion)` = `[10.0.14, 11.0.0)`
  on net10, same as today).
- All xUnit/test-shape changes are behind `net11.0` conditions or `#if NET11_0_OR_GREATER`, except:
  guard `typeof(FactAttribute)` (equivalent on v2 because `ConditionalFactAttribute : FactAttribute`),
  `xunit.runner.json` `parallelizeTestCollections: false` (redundant with the v2 attribute), the
  Find seed (valid on EF10 — verified), and the ComplexType table rename (test-only).
- Gate: net10 spec totals identical to `main` after **every** task (2331/1368/963).
- `task test:ef10` full run and NativeAOT net10 publish in §9.

## 11. Beads tracking

```bash
bd --sandbox update dynamodb-efcore-provider-ar2.2 --claim
bd --sandbox update dynamodb-efcore-provider-ar2.2.1 --claim     # Task 3
bd --sandbox close  dynamodb-efcore-provider-ar2.2.1 --reason "Seed fixed; Find spec 0 failures net10/net11"   # after Task 3 commit
bd --sandbox update dynamodb-efcore-provider-ar2.2.2 --claim     # Tasks 4-7
bd --sandbox comments add dynamodb-efcore-provider-ar2.2.2 "Root cause for 54 failures: Customers table collision ComplexTypeQuery vs Northwind (xUnit v3 order). Fixed by table rename."
```

- `ar2.2.2` and `ar2.2` stay **open** until the PR is merged; post-merge verification closes them
  (workflow step 6).
- Create a new bead only for a deferred provider gap approved under §13 D4.
- Never: `bd dolt push`, `bd sync`, `bd migrate`, `bd init`.

## 12. Risks and mitigations

| Risk | Mitigation |
|---|---|
| A provider gap is misclassified as an architectural skip | §3.2 marks the unclear clusters; scouts with upstream v11.0.0 source; triage table in PR reviewed independently |
| xUnit v3 ordering exposes further shared-state bugs | Full-suite runs only for classification; Task 4 latent-collision check; unique-table rule documented in spec AGENTS.md |
| Class order changes again when tests are added, masking/unmasking collisions | Root-cause fix (unique table) rather than pinning order |
| Dependabot bumps `xunit.v3.mtp-v2` override or EF ranges | `VersionOverride` lives in the csproj; a later bump to 4.0.2 is compatible (same major) and reviewed normally |
| `[11.0.0, 12.0.0)` admits `12.0.0-preview.*` | NuGet resolves the lowest applicable version (`11.0.0`); same shape as EF10's range |
| CI SDK `11.0.x` differs from local `11.0.100` | Both GA; no SDK pin (epic decision) |
| NativeAOT net11 warning baseline shifts with GA | Prototype validated; if `verify-nativeaot-warnings.sh` fails, stop and report the diff — do not edit the baseline without maintainer approval |
| Spec total for net11 differs from ≈2440 | Explain via added/renamed GA methods in the PR; not an error by itself |
| `/private/tmp` prototype is gone | Plan reproduces every hunk; prototype is optional |

**Rollback:** revert the PR merge commit. All changes are confined to central package versions,
the spec-test project, docs, and (only if Task 6 required) targeted provider fixes with their own
tests. No published artifacts, tags, or workflows change, so reverting restores the RC-based EF11
line exactly. Partial rollback is possible by commit (Tasks are independent commits).

## 13. Decisions requiring maintainer approval

| ID | Decision | Recommendation |
|---|---|---|
| D1 | PR title type (`feat` vs `chore`) — affects only the v11.0.0 notes category | `feat(ef11): target EF Core 11.0.0 GA` |
| D2 | Pin `Tasks`/`HealthChecks.EntityFrameworkCore` at `11.0.0` (bead text) vs range (prototype) | Pin `11.0.0` (mirrors EF10; Tasks must match EF runtime) |
| D3 | Fix only the proven `Customers` collision vs rename all duplicated spec tables | Only proven collision; others verified, documented |
| D4 | Provider fixes found in Task 6: include in PR B, or defer behind a tracked bead + skip | Include if small and covered by tests; otherwise ask before deferring |
| D5 | Update the internal `dynamodb-efcore-specification-tests` skill (xUnit v3 notes, nonexistent `task test:spec` command, unique-table rule) here or in PR C | PR C (keeps PR B scope minimal); this PR updates spec `AGENTS.md` only |

## 14. Acceptance criteria

1. `Directory.Packages.props` contains no preview/RC EF11 or Microsoft.Extensions versions; the
   Specification.Tests exemption is gone; EF10 entries byte-identical to `main`.
2. Restore has no `NU1605`/`NU1608`.
3. Spec project uses xUnit v2 on net10 and xUnit v3 4.0.1 on net11; all other test projects unchanged.
4. `task test:ef10` and `task test:ef11` pass with 0 failures; EF10 spec totals identical to `main`.
5. FindDynamoTest passes on both TFMs via the seed fix only (`ar2.2.1`).
6. The 54 Northwind failures are fixed by fixture table isolation, not skips.
7. Every other remaining failure has an evidence-backed classification in the PR triage table;
   every new skip uses a `SkipReason` constant and a durable DynamoDB constraint; no class-level or
   blanket skips; all `Check_all_tests_overridden` guards pass.
8. AOT interceptor generation (38) and NativeAOT publish/run (20) pass on net10 and net11 with the
   existing warning baseline.
9. `docs/spec-test-coverage.md` reflects the EF11 GA surface; `task docs:build` passes.
10. PR labelled `ef11-only`; no release-drafter, workflow, tag, release or package changes.
