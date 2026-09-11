# EF Core 11 NativeAOT / precompiled-query enablement — implementation plan

Status: plan, ready for implementation. This is the execution guide; investigation history,
alternatives considered, and upstream issue analysis live in
[`ef10-ef11-build-configuration-strategy-research.md`](ef10-ef11-build-configuration-strategy-research.md)
and
[`ef11-native-aot-precompiled-queries-research.md`](ef11-native-aot-precompiled-queries-research.md).

## 1. Objective / scope

Enable EF Core 11 NativeAOT and precompiled-query support end-to-end (compiled model, generated
query interceptors, real NativeAOT publish, real execution against DynamoDB Local), for both EF10
and EF11, without waiting on upstream fixes. `.Limit(n)` NativeAOT support is a separate,
out-of-scope follow-up — not touched here.

EF Core's NativeAOT/precompiled-query tooling currently loses externally supplied build context
(a custom `$(Configuration)` value, or `$(TargetFramework)` in a multi-targeted project) during
internal project reevaluation — see the research document for the two upstream issues
([dotnet/efcore#38951](https://github.com/dotnet/efcore/issues/38951),
[dotnet/efcore#38955](https://github.com/dotnet/efcore/issues/38955)) this repo hit and how they
were isolated. The validated repository-owned workaround is a physical, generated single-TFM
`TargetFrameworkOverride.props` file: because it's read from the project's own import graph rather
than passed as an external property, it survives every internal reopen/re-invocation EF triggers,
regardless of what properties that reopen does or doesn't carry.

## 2. Final architecture

```text
Configuration:         Debug / Release (standard — no "EF10"/"EF11" suffix)
Target frameworks:     net10.0 / net11.0 (checked in, standard <TargetFrameworks>)
Normal source / PR CI: ordinary multi-targeted build/test — no override needed
Version-specific work
(release, preview,
NativeAOT publish):    generated physical TargetFrameworkOverride.props forces single-TFM
                        evaluation for that operation only, then is deleted
v10 release:           net10.0 -> EntityFrameworkCore.DynamoDb 10.x
v11 release:           net11.0 -> EntityFrameworkCore.DynamoDb 11.x
```

`#if NET10_0`/`#if NET11_0` (the SDK's built-in TFM symbols) remain the established
conditional-compilation convention (`docs/multi-version-ef-strategy.md` §4.1) — unaffected by this
migration, since those symbols are already TFM-derived, not `Configuration`-derived.

## 3. Package/version changes

| Package | From | To |
|---|---|---|
| `Microsoft.EntityFrameworkCore` / `.Relational` / `.Design` / `.Specification.Tests` (EF10) | `[10.0.11, 11.0.0)` | `[10.0.12, 11.0.0)` |
| `Microsoft.EntityFrameworkCore.Tasks` (EF10) | `10.0.11` | `10.0.12` |
| `Microsoft.Extensions.*` (net10 group) | `10.0.11` / `[10.0.11, 11.0.0)` | `10.0.12` / `[10.0.12, 11.0.0)` |
| `EFCoreVersion` (EF11, drives EFCore/.Relational/.Design) | range `[11.0.0-preview.5.26302.115, 12.0.0-a)` | exact `11.0.0-rc.1.26425.128` |
| `Microsoft.EntityFrameworkCore.Specification.Tests` (EF11) | range (via `$(EFCoreVersion)`) | exact `11.0.0-preview.5.26302.115` — **deliberate, temporary exception** (see below) |
| `Microsoft.EntityFrameworkCore.Tasks` (EF11) | `11.0.0-preview.6.26359.118` | exact `11.0.0-rc.1.26425.128` |
| `Microsoft.Extensions.*` (net11 group, 6 entries) | mixed `preview.5`/`preview.6` ranges | exact `11.0.0-rc.1.26425.128` each |

**Specification.Tests exception**: every EF11 build of this package from `preview.6.26359.118`
through `rc.1.26425.128` depends on `Microsoft.DotNet.XUnitV3Extensions` (an EF-internal CI
package, not published to nuget.org — 404), which breaks restore for the whole solution.
`preview.5.26302.115` is the newest EF11 build whose nuspec doesn't reference it. This is an
upstream EF11 packaging bug, not provider-owned; re-pin to `$(EFCoreVersion)` once a newer EF11
build fixes it upstream. Applied under `$(TargetFramework) == 'net11.0'` (was
`$(Configuration)`-conditioned; see §5).

No public NuGet dependency range is broadened — EF11 moves from a loose range to a tighter, exact
pin everywhere except this one documented exception; EF10 stays the same range shape, just a
higher floor. `global.json` has no `sdk` block and needs no change.

## 4. Provider compatibility changes

Five `#if NET11_0` locations across seven files (mechanical adaptations to genuinely incompatible
EF11 API changes — no public API changes, no new provider abstraction):

1. **`DynamoQueryableMethodTranslatingExpressionVisitor.cs`** — `TranslateFullJoin` override,
   matching the file's existing `UnsupportedOperator(...)` convention (the member doesn't exist on
   the EF10 base class):
   ```csharp
   #if NET11_0
   protected override ShapedQueryExpression? TranslateFullJoin(
       ShapedQueryExpression outer, ShapedQueryExpression inner,
       LambdaExpression outerKeySelector, LambdaExpression innerKeySelector,
       LambdaExpression resultSelector)
       => UnsupportedOperator("FullJoin", DynamoStrings.JoinsNotSupported);
   #endif
   ```

2. **`DynamoCSharpRuntimeAnnotationCodeGenerator.cs`** — `Create` override. EF11 drops the
   `ValueComparer` parameters from the signature; confirmed the provider's own logic never reads
   them (pure passthrough), so the fix is mechanical: extract the shared body into
   `TryCreateDynamoMapping`, called from both `#if` branches, with only the outer signature and
   `base.Create(...)` overload differing per version.

3. **`DynamoModelValidator.cs` / `DynamoModelRuntimeInitializer.cs` / `DynamoSaveChangesPlanner.cs`**
   — `IReadOnlyIndex.Properties` widens from `IReadOnlyList<IReadOnlyProperty>` (EF10) to
   `IReadOnlyList<IReadOnlyPropertyBase>` (EF11), since indexes may now traverse complex-type
   properties. This provider already rejects complex-type index keys
   (`ValidateSecondaryIndexKeyPropertyType`), so a complex-type index member is an unsupported
   provider model shape — fail clearly, don't cast unsafely. Shared helper, one `#if`-guarded
   definition:
   ```csharp
   #if NET11_0
   private static IReadOnlyProperty AsScalarIndexProperty(
       string declaringEntityDisplayName, IReadOnlyIndex index,
       IReadOnlyPropertyBase indexProperty, string keyRole)
   {
       if (indexProperty is IReadOnlyProperty scalarProperty) return scalarProperty;
       throw new InvalidOperationException(
           $"Entity type '{declaringEntityDisplayName}' configures secondary index "
           + $"'{GetSecondaryIndexDisplayName(index)}' with complex-type member "
           + $"'{indexProperty.Name}' as DynamoDB {keyRole}, but DynamoDB index keys must "
           + "reference scalar (non-complex-type) properties.");
   }
   #else
   private static IReadOnlyProperty AsScalarIndexProperty(
       string declaringEntityDisplayName, IReadOnlyIndex index,
       IReadOnlyProperty indexProperty, string keyRole)
       => indexProperty;
   #endif
   ```
   Used at 3 call sites in `DynamoModelValidator.cs` (`ValidateGlobalSecondaryIndex`,
   `ValidateLocalSecondaryIndex`), and reused defensively at the equivalent 2 call sites in
   `DynamoModelRuntimeInitializer.cs` and 1 in `DynamoSaveChangesPlanner.cs` — both bypass the
   in-process `DynamoModelValidator` when loading/using a precompiled model, so the same guard
   belongs there too, cheap type-pattern check only (no allocation-heavy validation in the
   save-changes hot path). Physical location (one shared internal helper vs. duplicated per file)
   is an implementation-time call — default to the smallest diff.

4. **`DynamoComplexPropertyDiscoveryConvention.cs`** — `IMemberClassifier.IsCandidatePrimitiveProperty`
   gains two required `out` parameters on EF11 (genuinely incompatible arity, can't default away
   like optional value parameters): add `out _, out _` on the EF11 branch; EF10's 4-argument call
   stays unconditional.

5. **`DynamoDatabaseCreator.cs`** — `IUpdateAdapter.CreateEntry(IDictionary<string,object?>, ...)`
   is `[Obsolete]` on EF11 (fatal under `TreatWarningsAsErrors=true`); the replacement
   `CreateEntry(IReadOnlyDictionary<IProperty,object?>, ...)` overload is EF11-only. In
   `InsertDataAsync`'s seed-data loop:
   ```csharp
   #if NET11_0
   var propertyKeyedSeed = new Dictionary<IProperty, object?>(targetSeed.Count);
   foreach (var (propertyName, value) in targetSeed)
       if (runtimeEntityType.FindProperty(propertyName) is { } property)
           propertyKeyedSeed[property] = value;
   var entry = updateAdapter.CreateEntry(propertyKeyedSeed, runtimeEntityType);
   #else
   var entry = updateAdapter.CreateEntry(targetSeed, runtimeEntityType);
   #endif
   ```
   Skip (not throw) unmatched keys — some of EF's own shared spec-test seed fixtures carry
   complex/owned-shaped entries this provider's model doesn't expose as a scalar `IProperty`; the
   pre-EF11 string-keyed overload tolerated these silently, so this preserves that behavior.

**`DynamoTypeMapping<T>` — generic type-mapping architecture** (no `#if` needed once implemented;
compiles identically on both EF10 and EF11): EF11's `CoreTypeMapping.Clone(...)` drops its
`clrType` parameter (part of EF's NativeAOT type-mapping redesign,
[dotnet/efcore#38440](https://github.com/dotnet/efcore/pull/38440)), so this provider's previous
pattern — cloning a single shared `DynamoTypeMapping.Default` singleton and overriding `ClrType`
per property via that named argument — silently generates every compiled-model property mapping
with `ClrType` left as `object`. EF's own InMemory provider ships the reference solution; mirror it
exactly:

- `DynamoTypeMapping` (existing, unchanged) stays the shared, non-generic home for all
  DynamoDB-specific behavior (`ReaderWriter`, `CreateReadExpression`, `CreateAttributeValue`,
  collection-codec priming, etc.).
- New `DynamoTypeMapping<T> : DynamoTypeMapping`, mirroring `InMemoryTypeMapping<T>`:
  ```csharp
  public class DynamoTypeMapping<T> : DynamoTypeMapping
  {
      public static new DynamoTypeMapping<T> Default { get; } = new();

      public DynamoTypeMapping(ValueComparer? comparer = null, ValueComparer? keyComparer = null)
          : base(typeof(T), comparer, keyComparer) { }

      private DynamoTypeMapping(CoreTypeMappingParameters parameters) : base(parameters) { }

      protected override ValueComparer CreateDefaultComparer(bool favorStructuralComparisons)
          => ClrType == typeof(T)
              ? ValueComparer.CreateDefault<T>(favorStructuralComparisons)
              : base.CreateDefaultComparer(favorStructuralComparisons);

      public override CoreTypeMapping WithComposedConverter(...) =>
          new DynamoTypeMapping<T>(Parameters.WithComposedConverter(...));

      protected override CoreTypeMapping Clone(CoreTypeMappingParameters parameters) =>
          new DynamoTypeMapping<T>(parameters);
  }
  ```
- `DynamoTypeMappingSource`'s ~5 construction call sites switch to resolving the correct closed
  `DynamoTypeMapping<T>` — direct generic calls for common CLR types, a
  `MakeGenericType`+`Activator.CreateInstance` fallback for arbitrary (including value-converted)
  CLR types, with the same `[UnconditionalSuppressMessage]` justification EF's InMemory provider
  uses (this reflection only ever runs at mapping-source/generation time, never inside generated
  compiled-model source or any NativeAOT runtime path).
- Collections need no new mechanism — EF's base `Create` codegen already recurses into element
  mappings; once outer and element mappings both resolve to correctly-typed closed
  `DynamoTypeMapping<T>` instances, the existing recursion reconstructs them correctly.
- `DynamoComplexTypeMapping` (whole complex-type values in query parameters/constants) also
  constructs via a runtime `Type` — likely needs the same generic treatment; confirm scope during
  implementation.

No ADR for either the `#if` footprint or `DynamoTypeMapping<T>` — internal provider infrastructure,
no public API change, directly follows EF's own first-party precedent. No upstream issue for the
`Clone(clrType:)` removal — confirmed intentional EF11 redesign with a working, EF-documented
migration path, not a defect.

**Decision: do not add `Microsoft.EntityFrameworkCore.Tasks` to `AotTests.csproj`.** It only builds
and tests (never publishes), so the package would be a no-op without also wiring
`EFScaffoldModelStage=build`/`EFPrecompileQueriesStage=build` — out of scope. The `native-aot` CI
matrix (§6) already exercises the real Tasks/publish/native/runtime pipeline for both EF10 and
EF11, fully covering the precompiled-query/NativeAOT acceptance surface. Keep the existing split:
`AotTests` exercises the provider's generator API in-process; `NativeAotSmoke` exercises the real
publish/native/runtime pipeline.

## 5. Build-system migration

- All 8 `.csproj` files (`src/EntityFrameworkCore.DynamoDb`, `tests/*` ×5, `examples/Example.Simple`,
  `testapps/EntityFrameworkCore.DynamoDb.NativeAotSmoke`): replace `<Configurations>Debug
  EF10;Release EF10;Debug EF11;Release EF11</Configurations>` plus per-configuration `<TargetFramework>`
  `PropertyGroup`s with `<TargetFrameworks>net10.0;net11.0</TargetFrameworks>`.
- `Directory.Packages.props`: all `$(Configuration)`-conditioned `PackageVersion`/`EFCoreVersion`
  entries switch to `$(TargetFramework)`-conditioned (`== 'net11.0'` / `!= 'net11.0'`).
- `.slnx`: solution `<Configurations>` collapse to standard `Debug`/`Release`; per-project
  `<Configurations>` mapping blocks removed (no longer needed — names match directly).
- Repo-root `Directory.Build.props`: add
  ```xml
  <Import Project="$(MSBuildThisFileDirectory)TargetFrameworkOverride.props"
          Condition="Exists('$(MSBuildThisFileDirectory)TargetFrameworkOverride.props')" />
  ```
  as the first child of `<Project>`. `examples/Directory.Build.props` needs its own copy (it
  doesn't chain-import the repo root today).
- New `scripts/write-target-framework-override.sh`:
  - `scripts/write-target-framework-override.sh <tfm>` — writes `TargetFrameworkOverride.props`
    (repo root) with `<TargetFramework><tfm></TargetFramework><TargetFrameworks></TargetFrameworks>`.
  - `scripts/write-target-framework-override.sh --clear` — removes it.
  - Intentionally dumb: no EF-version, package, or release knowledge.
- `.gitignore`: add `/TargetFrameworkOverride.props`.
- `Taskfile.yml`: `CONFIG=` vars become `FRAMEWORK=` vars (`net10.0`/`net11.0`, default
  `net10.0`); `task test:aot-publish` generates the override, runs the smoke script, clears the
  override (`trap ... EXIT` inside the underlying script so a failed run can't leave it behind).
- `scripts/run-nativeaot-smoke.sh`: accept a `FRAMEWORK` argument, pass `--framework` to the
  underlying `dotnet publish` (restore/publish already scope correctly once the override is
  present; explicit `--framework` keeps the script usable standalone too).

## 6. CI/CD changes

- **`pr-build.yaml`**: `application-build` needs no override — ordinary multi-targeted build/test
  covers both TFMs in one pass; keep delegating to `devops-templates`' `pr-build.yaml` unchanged,
  `buildConfiguration: Debug`, no per-EF matrix. `native-aot` becomes a `fail-fast: false` matrix
  over `net10.0`/`net11.0` (EF11 becomes exercisable here, not just EF10): each leg writes the
  override, runs `scripts/run-nativeaot-smoke.sh`, clears the override in an `if: always()` step.
  `aot-generation` needs no override (confirmed: it never sets
  `EFScaffoldModelStage`/`EFPrecompileQueriesStage`, so it never triggers EF Tasks' publish-time
  generation path) — just add a `net10.0`/`net11.0` `--framework` matrix.
- **`publish-release.yaml`** and **`publish-preview.yaml`**: become fully local — no more `uses:
  LayeredCraft/devops-templates/.github/workflows/publish-*.yml`. This provider's EF-major
  selection, override generation, single-TFM pack, and NativeAOT validation are repo-specific
  release behavior, not shared-template material. The generic
  `LayeredCraft/devops-templates/.github/actions/nuget-push` **composite action** stays for the
  OIDC push step in both (required for NuGet Trusted Publishing's `job_workflow_ref` check;
  version-agnostic, not provider-specific).
  - Release: `resolve` job maps tag major (`v10.*`/`v11.*`) → `net10.0`/`net11.0` (replaces the old
    `configuration` derivation with the same shape).
  - Preview: matrix over `{targetFramework, dotnetVersion, drafterConfig, artifact_name}` replaces
    the old `{configuration, ...}` matrix; release-drafter dry-run preview-version resolution is
    unchanged.
  - Both: `write-target-framework-override.sh <tfm>` → restore/build/test → **real NativeAOT smoke
    validation** (`scripts/run-nativeaot-smoke.sh`) → pack → upload artifact → clear override
    (`if: always()`). A failing NativeAOT leg must block packing and publishing — no package is
    published without it passing, for both preview and release.
- **Release Drafter is unaffected.** `release-drafter-ef10.yml`/`-ef11.yml` are keyed purely on
  `filter-by-range` (semver range against tag history) and `config-name`; `release-drafter.yaml`'s
  matrix keys on those same config file names. No dependency on `$(Configuration)`/
  `$(TargetFramework)` anywhere — confirmed, no changes needed.
- **`devops-templates` branch `feat/reusable-workflow-target-framework-support`** (additive
  `targetFramework` input prototype) is not needed by this repository under this design; left
  unmerged, untouched.
- Packaging isolation: `dotnet pack` with the override present needs no `-p:TargetFrameworks=`
  override at all — verified to produce a single-TFM, single-dependency-group `.nupkg` with no
  cross-contamination for both EF lines.

## 7. Documentation updates

- `testapps/EntityFrameworkCore.DynamoDb.NativeAotSmoke/AGENTS.md`: replace the EF10-only
  restriction with: both EF10 and EF11 are CI-validated via the `net10.0`/`net11.0` `native-aot`
  matrix; EF11 requires the exact `11.0.0-rc.1.26425.128` pin.
- Root `AGENTS.md`: remove the `## NativeAOT Smoke Test — EF10 ONLY` framing and the "Do NOT add an
  EF11 smoke leg to CI" instruction; document `TargetFrameworkOverride.props` and
  `scripts/write-target-framework-override.sh` as the EF-line-selection mechanism.
- `docs/querying/precompiled-queries.md`: remove the "EF Core 11 currently has a known blocker"
  language; state both EF10 and EF11 are CI-validated (still experimental); update the
  "Verification" section to describe the passing EF11 NativeAOT smoke path. Leave the `.Limit(n)`
  restriction bullet untouched.
- `docs/multi-version-ef-strategy.md`: optional light-touch back-reference to this plan and the
  research doc as the worked example — not required for completeness, use judgment.

## 8. Validation

Ordinary multi-targeted restore/build/test — no override, both TFMs in one pass:

```bash
dotnet restore EntityFrameworkCore.DynamoDb.slnx
dotnet build EntityFrameworkCore.DynamoDb.slnx --configuration Release --framework net10.0 --no-restore
dotnet build EntityFrameworkCore.DynamoDb.slnx --configuration Release --framework net11.0 --no-restore
task test:ef10
task test:ef11
```

AOT-generation, both TFMs, no override needed:

```bash
task test:aot-generation FRAMEWORK=net10.0
task test:aot-generation FRAMEWORK=net11.0
```

NativeAOT smoke, both TFMs, via the physical override:

```bash
scripts/write-target-framework-override.sh net10.0
scripts/run-nativeaot-smoke.sh Release "" net10.0
scripts/write-target-framework-override.sh --clear

scripts/write-target-framework-override.sh net11.0
scripts/run-nativeaot-smoke.sh Release "" net11.0
scripts/write-target-framework-override.sh --clear
```

Confirm no residue and normal multi-targeting is intact:

```bash
git status --short   # TargetFrameworkOverride.props must not appear (gitignored) or exist on disk
dotnet build EntityFrameworkCore.DynamoDb.slnx -getProperty:TargetFramework -getProperty:TargetFrameworks --configuration Release
# expect: TargetFramework="" and TargetFrameworks="net10.0;net11.0"
```

Packaging isolation, both lines:

```bash
scripts/write-target-framework-override.sh net10.0
dotnet pack src/EntityFrameworkCore.DynamoDb/EntityFrameworkCore.DynamoDb.csproj --configuration Release -p:Version=10.x.x -o artifacts
scripts/write-target-framework-override.sh --clear

scripts/write-target-framework-override.sh net11.0
dotnet pack src/EntityFrameworkCore.DynamoDb/EntityFrameworkCore.DynamoDb.csproj --configuration Release -p:Version=11.x.x -o artifacts
scripts/write-target-framework-override.sh --clear
```
Inspect both `.nupkg`s: each must contain only its own `lib/<tfm>/*` and only its own TFM's EF
dependency group.

## 9. Acceptance criteria

- Standard `<TargetFrameworks>net10.0;net11.0</TargetFrameworks>` is checked in; no `Debug
  EF10`/`Release EF10`/`Debug EF11`/`Release EF11` MSBuild configuration names exist anywhere in
  the repo.
- With no `TargetFrameworkOverride.props` present, `dotnet restore`/`build`/`test` on the solution
  build and test both `net10.0` and `net11.0` using ordinary multi-targeting — no override, no
  externally-supplied MSBuild property, needed for normal development or PR CI.
- `net10.0`/`net11.0` build cleanly with no `CS0534`/`CS0115`/`CS7036`/`CS1503`/`CS1929`/`CS0618`
  errors.
- `dotnet restore` succeeds for the full solution including `SpecificationTests` (validates the
  Specification.Tests pin exemption), with no override present.
- AOT-generation passes for both `net10.0` and `net11.0` (no override needed).
- NativeAOT smoke passes end-to-end for both `net10.0` and `net11.0` using
  `TargetFrameworkOverride.props`: publish succeeds, AOT warning IDs stay within the reviewed
  baseline, and the smoke app runs its parameterized, materializing, and `SaveChanges` operations
  against DynamoDB Local successfully.
- `TargetFrameworkOverride.props` is absent from the working tree after every successful or failed
  local or CI operation that generates it (verified via `git status --short`/`ls`, not assumed).
- No workflow or script relies on an externally supplied MSBuild property (`-p:Configuration=`,
  `-p:TargetFramework=`, `-p:EfCoreVersion=`, or similar) to select the EF line during NativeAOT
  generation or query precompilation — selection is always via the physical
  `TargetFrameworkOverride.props` file.
- CI's `native-aot` job is a real `net10.0`/`net11.0` `fail-fast: false` matrix, both legs green.
- `v10.*` release tags produce only the `net10.0`/EF10 package line; `v11.*` tags produce only the
  `net11.0`/EF11 package line (verified by `.nupkg` inspection, not assumed from the workflow logic
  alone).
- Preview publishing produces independently isolated EF10 and EF11 packages (same isolation
  verification as release).
- Real NativeAOT smoke validation (not a compile-only check) gates NuGet publication in both
  `publish-preview.yaml` and `publish-release.yaml` — a failing smoke leg blocks packing and
  publishing.
- `publish-preview.yaml` and `publish-release.yaml` no longer call
  `LayeredCraft/devops-templates/.github/workflows/publish-preview.yml`/`publish-release.yml`; the
  generic `LayeredCraft/devops-templates/.github/actions/nuget-push` composite action remains in
  use for the OIDC push step in both.
- Release Drafter (`release-drafter-ef10.yml`/`-ef11.yml`, `release-drafter.yaml`) is unchanged —
  confirmed no dependency on `$(Configuration)`/`$(TargetFramework)`.
- The `devops-templates` branch `feat/reusable-workflow-target-framework-support` is not required
  by this repository (left unmerged, untouched).
- `Directory.Packages.props` has no accidental EF11 version drift: all applicable EF11
  runtime/design/tooling packages are aligned to the exact pin `11.0.0-rc.1.26425.128`, except the
  one documented `Specification.Tests` exception.
- `.Limit(n)` precompiled-query restriction is untouched and still documented as unsupported.
- Updated docs (`AGENTS.md`, smoke app `AGENTS.md`, `docs/querying/precompiled-queries.md`) no
  longer claim EF11 NativeAOT is blocked or unsupported, and no longer reference the retired
  `Debug/Release EFn` configuration scheme.
