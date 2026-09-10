# EF Core 11 NativeAOT / precompiled-query enablement — implementation plan

Status: plan, not yet implemented. Follows
[`ef11-native-aot-precompiled-queries-research.md`](ef11-native-aot-precompiled-queries-research.md).
Scope: EF11 NativeAOT/precompiled-query enablement only. `.Limit(n)` NativeAOT support is a
separate, out-of-scope follow-up — not touched here. Sized for one PR.

**Revision note (this pass):** implementation began, and a clean (non-raced) whole-solution build
against the exact-pinned EF11 RC1 packages surfaced a materially larger EF11 compile-break surface
than the research pass's RC1 experiment had found. That earlier experiment was run while a
background research fork was concurrently, unexpectedly re-editing `Directory.Packages.props` in
the same working tree (see the prior implementation report) — its "only two errors" result should
be treated as unreliable/incomplete, not as evidence the surface was actually that small. This
revision documents the corrected, complete break inventory found by direct, sequential
investigation (Bash builds + reflection against both EF10 10.0.11 and EF11 RC1 assemblies, no
concurrent tooling). Two of the originally planned fixes are already implemented and confirmed
compiling under `Debug EF10`; nothing has been reverted. See §"Corrected EF11 compile-break
inventory" below for the full picture; the original §3/§4 sections are left in place, updated in
place with what was learned.

Decisions carried in from research (settled, not reopened):

1. Pin EF11 packages to the exact validated build `11.0.0-rc.1.26425.128` (no floating range),
   **with one confirmed exception**: `Microsoft.EntityFrameworkCore.Specification.Tests` stays at
   `11.0.0-preview.5.26302.115` for EF11 configs. Every EF11 build of that package from
   `preview.6.26359.118` through the current `rc.1.26425.128` depends on
   `Microsoft.DotNet.XUnitV3Extensions` (an EF-internal CI package, confirmed absent from
   nuget.org — 404), which breaks `dotnet restore` for the whole solution under
   `Debug/Release EF11`. `preview.5.26302.115` is the newest EF11 build of this one package whose
   nuspec doesn't reference it (confirmed via direct nuspec inspection of all four EF11
   preview/RC builds). This is an upstream EF11 packaging bug, not provider-owned; re-pin to
   `$(EFCoreVersion)` once a newer EF11 build fixes it upstream. Applied and verified — see
   §"Corrected EF11 compile-break inventory".
2. A narrow `#if`-guarded conditional is acceptable for confirmed EF10/EF11 API incompatibilities,
   used only where the two versions' APIs are genuinely incompatible (not mechanically for every
   EF11-related fix). Per the repo's existing strategy doc (`docs/multi-version-ef-strategy.md`
   §4.1/§4.2), the established pattern is the SDK's **built-in TFM symbols**
   (`#if NET10_0` / `#if NET11_0`), not custom `DefineConstants` — the DynamoDB provider already
   has a 1:1 config→TFM mapping, so no `EF10`/`EF11` constants are defined or needed. **The actual
   footprint is larger than the two methods originally scoped** — see §"Final `#if NET11_0`
   footprint" below for the corrected, complete list.
3. Extend the existing `native-aot` CI job to a `fail-fast: false` matrix over
   `Release EF10` / `Release EF11`, reusing `scripts/run-nativeaot-smoke.sh` as-is.
4. No ADR — reassessed against the larger break surface; conclusion unchanged. See
   §"ADR gate — reassessed" below.

## New finding this pass: `Microsoft.EntityFrameworkCore.Tasks` in `AotTests.csproj` — do not add it

Traced `Microsoft.EntityFrameworkCore.Tasks.targets` directly
(`~/.nuget/packages/microsoft.entityframeworkcore.tasks/<ver>/buildTransitive/Microsoft.EntityFrameworkCore.Tasks.targets`).
The precompile step only runs at `Build` time if `$(EFPrecompileQueriesStage)` (or
`$(EFScaffoldModelStage)`) is explicitly set to `build`; otherwise it only runs at `Publish` time,
gated by `$(PublishAot)=='true'` or `$(_EFPublishAot)=='true'`. Neither `AotTests.csproj` nor the
smoke app sets the build-stage property — the smoke app relies entirely on `PublishAot=true` at
publish time.

Consequence: simply adding a bare `<PackageReference Include="Microsoft.EntityFrameworkCore.Tasks" />`
to `AotTests.csproj` (which only does `dotnet build`/`dotnet test`, never `dotnet publish`) would
be a **no-op** — the precompile target would never fire, so it wouldn't catch anything, including
the exact pin-mismatch bug this research reproduced. Making it catch that would require also
setting `EFPrecompileQueriesStage=build` (or `EFScaffoldModelStage=build`), which is a materially
bigger change (new MSBuild wiring, not just a package reference) than "add a package reference."

Given the CI plan already adds a `Release EF11` leg to `native-aot` (item 3 above), which runs the
real `Microsoft.EntityFrameworkCore.Tasks` publish-time pipeline for both EF10 and EF11, the
acceptance criteria in the original objective (precompiled-query generation succeeds, interceptors
compile, no unexpected fallback, `PublishAot=true` succeeds, native executable runs) are already
fully covered for both EF versions by the smoke matrix. Adding Tasks to `AotTests.csproj` would
not add distinct coverage without the extra stage-property wiring, and that wiring is out of scope
for a minimal PR whose job is enablement, not new validation architecture.

**Decision: do not add `Microsoft.EntityFrameworkCore.Tasks` to `AotTests.csproj` in this PR.**
Keep the existing, intentional division: `AotTests` exercises the provider's generator API
in-process; `NativeAotSmoke` exercises the real Tasks/publish/native/runtime pipeline. Note the
build-stage-property option in `docs/querying/precompiled-queries.md` as a possible future
fast-feedback layer (catches pin mismatches without needing the AOT toolchain/Docker), explicitly
deferred, not part of this PR.

## Files to change

### 1. `Directory.Packages.props`

- EF10 line: bump `Microsoft.EntityFrameworkCore`/`.Relational`/`.Design`/`.Specification.Tests`
  range floor and `Microsoft.EntityFrameworkCore.Tasks` from `10.0.11` → `10.0.12`.
  `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` (net10 group) and the
  `Microsoft.Extensions.*` net10 range floors → `10.0.12`.
- EF11 line: replace the `EFCoreVersion` range
  (`[11.0.0-preview.5.26302.115, 12.0.0-a)`) with an **exact pin**:
  `EFCoreVersion` = `11.0.0-rc.1.26425.128` (drop the range syntax entirely — a single version,
  not `[x, y)`).
  `Microsoft.EntityFrameworkCore.Tasks` (EF11 condition) → exact `11.0.0-rc.1.26425.128`.
  `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore` (net11 group) and the five
  `Microsoft.Extensions.*` net11 entries → exact `11.0.0-rc.1.26425.128` (drop their `[x, y)`
  ranges too, for consistency with the exact-pin decision).

### 2. `global.json`

No SDK pin currently exists (checked — file only has `test.runner`). No change needed; leave as
is. (The strategy doc's §4.2 Step 4 describing a `rollForward: latestMajor` SDK pin does not apply
— this repo's `global.json` has no `sdk` block to begin with.)

### 3. `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryableMethodTranslatingExpressionVisitor.cs` — **implemented, confirmed**

Read every existing `Translate*` override in the file first. The provider's exact, consistent
convention for every unsupported LINQ shape (`TranslateLeftJoin`, `TranslateRightJoin`,
`TranslateJoin`, `TranslateGroupJoin`, set operations, aggregates, etc.) is a one-line
`=> UnsupportedOperator(<name>, DynamoStrings.<Reason>)` call — never `null` returned directly,
never a thrown exception. `TranslateFullJoin` matches this exactly, guarded by `#if NET11_0` (the
member doesn't exist on the EF10 base class, so an unconditional override fails to compile under
`Debug/Release EF10` — confirmed by reflection: EF10's `QueryableMethodTranslatingExpressionVisitor`
has no such member at all). Implemented as:

```csharp
#if NET11_0
protected override ShapedQueryExpression? TranslateFullJoin(
    ShapedQueryExpression outer,
    ShapedQueryExpression inner,
    LambdaExpression outerKeySelector,
    LambdaExpression innerKeySelector,
    LambdaExpression resultSelector)
    => UnsupportedOperator("FullJoin", DynamoStrings.JoinsNotSupported);
#endif
```

("FullJoin" as a literal string, not `nameof(Queryable.FullJoin)`, matching the existing
`"LeftJoin"`/`"RightJoin"` literals right above it — those extension methods aren't referenced via
`nameof` in this file either, so this stays consistent with the file's own established style
rather than introducing a new pattern.) Implemented and confirmed compiling under `Debug EF10`
(`dotnet build` clean, 0 errors, since the whole block is compiled out under `NET10_0`).

### 4. `src/EntityFrameworkCore.DynamoDb/Design/Internal/DynamoCSharpRuntimeAnnotationCodeGenerator.cs` — **implemented, confirmed**

**Comparer-semantics investigation, resolved:** read the pre-existing EF10 override body in full.
The provider's own logic (the `DynamoTypeMapping`-specific collection-priming branch) never reads
`valueComparer`, `keyValueComparer`, or `providerValueComparer` — they're pure passthrough
arguments forwarded untouched to `base.Create(typeMapping, parameters, valueComparer,
keyValueComparer, providerValueComparer)`. So EF11 dropping them from its overload signature drops
no actual provider behavior; the fix is genuinely mechanical, not just signature-shaped. Extracted
the shared DynamoDB-specific logic into one private `TryCreateDynamoMapping` helper called from
both `#if` branches, so the only version-specific code is the outer method signature and which
`base.Create(...)` overload it forwards to:

```csharp
#if NET11_0
public override bool Create(
    CoreTypeMapping typeMapping,
    CSharpRuntimeAnnotationCodeGeneratorParameters parameters)
{
    if (TryCreateDynamoMapping(typeMapping, parameters, out var created))
        return created;

    return base.Create(typeMapping, parameters);
}
#else
public override bool Create(
    CoreTypeMapping typeMapping,
    CSharpRuntimeAnnotationCodeGeneratorParameters parameters,
    ValueComparer? valueComparer = null,
    ValueComparer? keyValueComparer = null,
    ValueComparer? providerValueComparer = null)
{
    if (TryCreateDynamoMapping(typeMapping, parameters, out var created))
        return created;

    return base.Create(typeMapping, parameters, valueComparer, keyValueComparer, providerValueComparer);
}
#endif
```

Implemented and confirmed compiling under `Debug EF10` (`dotnet build` clean, 0 errors). EF11
compile confirmation is pending the fixes in the next section (the solution doesn't build under
`Debug EF11` yet for unrelated reasons below), but this specific file introduces no new error in
the EF11 build output.

### 5. `tests/EntityFrameworkCore.DynamoDb.AotTests/`

No new package reference (see decision above). Confirm existing generation/parity tests
(`PrecompiledQueryGenerationTests.cs`, `PrecompiledParityTests.cs`, `CompiledModelExecutionTests.cs`)
still pass unchanged under `Debug EF11` once the full break inventory (§A/§B/§C) compiles — they already
run in CI for both configs, so no structural test changes are expected here, only confirmation.

### 6. `.github/workflows/pr-build.yaml`

Add a matrix entry to the `native-aot` job:

```yaml
native-aot:
  name: native AOT smoke (${{ matrix.configuration }})
  runs-on: ubuntu-latest
  strategy:
    fail-fast: false
    matrix:
      include:
        - configuration: "Release EF10"
          dotnetVersion: "10.0.x"
        - configuration: "Release EF11"
          dotnetVersion: "11.0.x"
```

(Adds `fail-fast: false`, matching `aot-generation`'s existing style, so an EF11 smoke failure
doesn't hide an EF10 result or vice versa.) No other job needs a matrix change — `application-build`
and `aot-generation` already cover both configs.

### 7. `testapps/EntityFrameworkCore.DynamoDb.NativeAotSmoke/AGENTS.md`

Rewrite — it currently documents the EF10-only restriction and the old blocker as unconditional
fact. Replace with: both EF10 and EF11 are CI-validated; EF11 requires exact-pinned
`11.0.0-rc.1.26425.128` packages (link to the research/plan docs for why exact pinning matters,
including the one documented Specification.Tests exemption); the `#if NET11_0` locations (§"Final
`#if NET11_0` footprint" in the plan doc) to be aware of when EF Core ships further breaking
changes.

### 8. `AGENTS.md` (repo root)

Update the `## NativeAOT Smoke Test — EF10 ONLY` section: remove the "EF10 ONLY" framing and the
"Do NOT add an EF11 smoke leg to CI" instruction (now stale — the opposite has happened), update
the blocker description to point at the research doc instead of asserting it's currently broken.

### 9. `docs/querying/precompiled-queries.md`

- Remove the "EF Core 11 currently has a known blocker... Treat NativeAOT as unavailable for EF
  Core 11" language from the experimental-support warning; state both EF10 and EF11 are
  CI-validated, still experimental.
- Update the package-version example if it should show the EF11 pin style too (currently only
  shows a 10.0.11 example — consider adding a short EF11-pin note given the exact-pin decision, so
  consumers don't default to a floating range for EF11 packages).
- Update "Verification" section: replace "EF Core 11 interceptor generation is tested, but its
  NativeAOT publish-and-run path remains blocked as described above" with a description of the
  now-passing `task test:aot-publish CONFIG="Release EF11"` path, and mention the CI matrix.
- Leave the `.Limit(n)` restriction bullet untouched — explicitly out of scope; do not imply it's
  resolved.

### 10. `docs/multi-version-ef-strategy.md`

Optional/light-touch: §4.4 "The Killer Workflow When EF11 Ships" was written prospectively; once
this PR lands, it's no longer hypothetical. Consider adding a short note/back-reference to the two
research/plan docs as the worked example, but this is not required for the PR to be complete — skip
if it adds noise without a clear reader benefit; use judgment during implementation rather than
treating this as mandatory.

## Corrected EF11 compile-break inventory

Found by a clean, sequential (non-raced) `dotnet build EntityFrameworkCore.DynamoDb.slnx
--configuration "Debug EF11"` after applying the version bumps (including the
Specification.Tests exemption above, without which the build doesn't even restore) and the two
§3/§4 fixes. 12 errors, in 5 files, reducing to 3 distinct root causes:

### A. `IReadOnlyIndex.Properties` changed element type

Confirmed via reflection: `IReadOnlyIndex.Properties` is
`IReadOnlyList<IReadOnlyProperty>` on EF10 (10.0.11) and
`IReadOnlyList<IReadOnlyPropertyBase>` on EF11 RC1. This is the "indexes may now traverse
complex-type properties" change the research doc's §4b already named from Microsoft's own
provider-facing-changes doc, but its compile-time ripple wasn't traced there. `IReadOnlyProperty`
derives from `IReadOnlyPropertyBase`, so this is a widening, not unrelated types — but every
existing call site assumed the narrower `IReadOnlyProperty` and calls `.GetAttributeName()`
(a `DynamoPropertyExtensions` extension method that only accepts `IReadOnlyProperty`) or passes the
value into a parameter typed `IReadOnlyProperty`.

**Provider semantic question, now resolved:** does `EntityFrameworkCore.DynamoDb` support a
complex-type property as a secondary-index key? No — confirmed by reading
`ValidateSecondaryIndexKeyPropertyType` (`DynamoModelValidator.cs:816`), which already requires
every index key to resolve to a DynamoDB-storable scalar provider type (string/number/binary) and
throws `InvalidOperationException` otherwise. A complex-type property can never satisfy that; it
was simply never reachable before because `index.Properties` couldn't contain one. **Decision
(per your direction): reject a non-`IReadOnlyProperty` index member as an unsupported provider
model shape, failing clearly with `InvalidOperationException`, not silently filtering or casting
unsafely.**

**Where the check belongs:** primarily in `DynamoModelValidator.cs`, since it's the fail-fast gate
this provider already uses for every other unsupported index shape (wrong key type, duplicate
partition/sort keys, missing partition/sort key, etc. — all in the same file, same pattern).
Concretely:

- `ValidateGlobalSecondaryIndex` (`:714`) — `index.Properties[0]` (partition key) and
  `index.Properties[1]` (optional sort key) are read as `IReadOnlyPropertyBase` on EF11; guard
  each before use.
- `ValidateLocalSecondaryIndex` (`:757`) — `index.Properties[0]` (alternate sort key), same guard.
- `ValidateSecondaryIndexKeyPropertyType` (`:816`) already takes `IReadOnlyProperty` as its
  parameter type — no change needed there; it becomes the single place scalar-ness is assumed,
  once callers guarantee it.

Proposed shared helper (one `#if`-guarded definition, called identically from both EF versions —
this is the "shared code where compatible, narrow `#if` only where it differs" shape you asked
for):

```csharp
#if NET11_0
private static IReadOnlyProperty AsScalarIndexProperty(
    string declaringEntityDisplayName,
    IReadOnlyIndex index,
    IReadOnlyPropertyBase indexProperty,
    string keyRole)
{
    if (indexProperty is IReadOnlyProperty scalarProperty)
        return scalarProperty;

    throw new InvalidOperationException(
        $"Entity type '{declaringEntityDisplayName}' configures secondary index "
        + $"'{GetSecondaryIndexDisplayName(index)}' with complex-type member "
        + $"'{indexProperty.Name}' as DynamoDB {keyRole}, but DynamoDB index keys must "
        + "reference scalar (non-complex-type) properties.");
}
#else
private static IReadOnlyProperty AsScalarIndexProperty(
    string declaringEntityDisplayName,
    IReadOnlyIndex index,
    IReadOnlyProperty indexProperty,
    string keyRole)
    => indexProperty; // EF10: index.Properties is already IReadOnlyList<IReadOnlyProperty>
#endif
```

Every existing call site (`globalPartitionKeyProperty`, `globalSortKeyProperty`,
`localSortKeyProperty`) wraps its `index.Properties[n]` read with this helper; the rest of each
method's logic (duplicate-key checks, attribute-name comparisons, the call into
`ValidateSecondaryIndexKeyPropertyType`) is completely unchanged and unconditional, since it's all
expressed in terms of `IReadOnlyProperty` either way.

**Downstream guards — resolved:** `DynamoModelValidator` runs as EF Core's `IModelValidator`
during normal in-process model building/finalization, which always precedes
`IModelRuntimeInitializer` (`DynamoModelRuntimeInitializer`) in EF's own pipeline — both are
registered as the standard EF service replacements in `DynamoServiceCollectionExtensions.cs:34-35`,
not a custom pipeline. So for a normally-built (non-precompiled) model, validation running first is
an EF Core platform guarantee, not something the provider has to re-enforce.

**But this PR is specifically about precompiled/compiled models**, and that's exactly the case
where this guarantee doesn't hold within a single process: a compiled model is generated once
(validation runs then, during that generation build) and the *runtime* process loads the
already-built compiled model directly — `DynamoModelRuntimeInitializer` and
`DynamoSaveChangesPlanner` (used at query/save time against that loaded model) never re-run
`DynamoModelValidator` in the runtime process at all. If the compiled model was correctly generated
from a validated source model, the invariant still holds transitively. But per your instruction to
keep the behavior "provider-level and explicit rather than allowing an `InvalidCastException` or
silently changing the configured index" — and because this is precisely the compiled-model path
this PR is enabling — **keep the same guarded cast (not a bare unchecked cast) at the two
downstream sites too**, so a still-`IReadOnlyPropertyBase`-typed value reaching them (e.g. from a
compiled model generated by a future EF/provider version with a bug, or any other path that
bypasses the in-process validator) fails with the same clear message instead of an
`InvalidCastException` or a silently wrong descriptor:

- `DynamoModelRuntimeInitializer.cs:188-189, 208` (`BuildGlobalSecondaryIndexDescriptor`,
  `BuildLocalSecondaryIndexDescriptor`) — reuse the same `AsScalarIndexProperty` helper (move it to
  a shared internal location both files can call, e.g. a small internal static class, or keep one
  copy in `DynamoModelValidator` and reference it — decide based on which keeps visibility/access
  modifiers simplest; both are internal to the provider assembly already).
- `DynamoSaveChangesPlanner.cs:53-58` (`foreach (var property in index.Properties)` /
  `property.GetAttributeName()`) — same guard; this one is in the save-changes hot path, so the
  guard should stay a cheap type-pattern check (as above), not a LINQ/allocation-heavy validation
  pass.

This is not redundant validation-for-its-own-sake — it's the same fail-fast philosophy the
provider already applies everywhere else in these three files, applied consistently to a metadata
shape that's newly reachable on EF11.

### B. `IMemberClassifier.IsCandidatePrimitiveProperty` signature change

Confirmed via reflection — genuinely incompatible arity, not a shared-callable overload:

- EF10: `IsCandidatePrimitiveProperty(MemberInfo, IConventionModel, bool, out CoreTypeMapping?)`
  — 4 parameters.
- EF11 RC1: `IsCandidatePrimitiveProperty(MemberInfo, IConventionModel, bool, out CoreTypeMapping?,
  out Type? elementType, out bool ...)` — 6 parameters, two new required `out` parameters.

`#if NET11_0` is required here (contrary to the plan's earlier hope of sharing this call) — `out`
parameters can't be defaulted away like the `Create` overload's optional value parameters were.
The fix is still purely mechanical: in
`DynamoComplexPropertyDiscoveryConvention.cs:57`, add `out _` twice on the EF11 branch; the
existing 4-argument call stays unconditional on EF10.

### C. `IUpdateAdapter.CreateEntry(...)` obsolete-as-error

Confirmed via reflection this is **not** a version-independent fix, contrary to the hope in your
message — the non-obsolete overload does not exist on EF10 at all:

- EF10: exactly one `CreateEntry(IDictionary<string,object?>, IEntityType)` overload, **not**
  obsolete.
- EF11 RC1: the same string-keyed overload now exists but is marked `[Obsolete]` (which this repo's
  `TreatWarningsAsErrors=true` turns into a build failure), and a second overload,
  `CreateEntry(IReadOnlyDictionary<IProperty,object?>, IEntityType)`, was added as the
  replacement — but it's EF11-only.

So this needs `#if NET11_0` too. At the call site (`DynamoDatabaseCreator.cs:359-362`,
`InsertDataAsync`'s seed-data loop), `targetSeed` comes from `entityType.GetSeedData()`, which
returns property-**name**-keyed dictionaries (`IDictionary<string,object?>`) — that shape is part
of EF's model-level seed data storage and is unaffected by this change on either EF version. The
EF11 branch needs to translate that into the new `IProperty`-keyed shape before calling
`CreateEntry`, using `runtimeEntityType.FindProperty(name)` for each key (already available at the
call site as `runtimeEntityType`):

```csharp
#if NET11_0
var propertyKeyedSeed = new Dictionary<IProperty, object?>(targetSeed.Count);
foreach (var (propertyName, value) in targetSeed)
    propertyKeyedSeed[runtimeEntityType.FindProperty(propertyName)!] = value;
var entry = updateAdapter.CreateEntry(propertyKeyedSeed, runtimeEntityType);
#else
var entry = updateAdapter.CreateEntry(targetSeed, runtimeEntityType);
#endif
```

This preserves identical semantics (same property values, same entity type, same `EntityState.Added`
assignment right after) — `FindProperty` on the seed-data property names is safe because seed data
is validated against the model's own properties elsewhere in EF's pipeline; it isn't introducing a
new failure mode. No change to shadow-property, conversion, store-generated, or temporary-value
handling — `CreateEntry` populates an `IUpdateEntry` from the given values either way, and neither
overload does conversion/generation logic itself (that happens later, during `SaveChanges`, exactly
as before).

## Final `#if NET11_0` footprint

Corrected from the original plan's two methods to five locations across five files. All are either
(a) a call to a different, genuinely-incompatible-arity base/framework API, or (b) a small shared
private helper with two `#if`-guarded bodies called identically from both versions — never a
broader abstraction, never touching public API:

1. `DynamoQueryableMethodTranslatingExpressionVisitor.cs` — `TranslateFullJoin` override (§3,
   done).
2. `DynamoCSharpRuntimeAnnotationCodeGenerator.cs` — `Create` override, shared body via
   `TryCreateDynamoMapping` (§4, done).
3. `DynamoModelValidator.cs` — `AsScalarIndexProperty` helper (§A), used at 3 call sites within the
   same file.
4. `DynamoModelRuntimeInitializer.cs` — reuses the same helper at 2 call sites (§A).
5. `DynamoSaveChangesPlanner.cs` — reuses the same helper at 1 call site (§A).
6. `DynamoComplexPropertyDiscoveryConvention.cs` — `IsCandidatePrimitiveProperty` call, extra
   `out _, out _` on EF11 (§B).
7. `DynamoDatabaseCreator.cs` — `CreateEntry` call, EF11 builds an `IProperty`-keyed dictionary
   first (§C).

(Items 3–5 share one helper definition, so the "two `#if`-guarded bodies" count stays at 6 distinct
`#if NET11_0` blocks across 7 files, not 7 independent implementations of the same logic.)

## ADR gate — reassessed

Still **no ADR**. The larger surface doesn't change the reasoning: every fix is (a) a version-pin
correction (Specification.Tests exemption — an upstream packaging bug, temporary, reversible), (b)
adapting to a genuinely-incompatible EF11 API signature with no provider design choice involved
(`IsCandidatePrimitiveProperty`, `CreateEntry`), or (c) applying the provider's own
**pre-existing, already-established** fail-fast validation philosophy (reject an unsupported model
shape with a clear `InvalidOperationException`, exactly like every other secondary-index
validation rule already in `DynamoModelValidator.cs`) to a metadata shape EF11 newly makes
reachable. Rejecting complex-type index keys is not a new compatibility policy — it's the same
"DynamoDB index keys must be scalar" rule the file already enforces via
`ValidateSecondaryIndexKeyPropertyType`, now reachable one step earlier in the type hierarchy. No
public API changes, no new provider abstraction, no design trade-off requiring sign-off beyond
what's already been decided above.

| Package | From | To |
|---|---|---|
| `Microsoft.EntityFrameworkCore` / `.Relational` / `.Design` / `.Specification.Tests` (EF10) | `[10.0.11, 11.0.0)` | `[10.0.12, 11.0.0)` |
| `Microsoft.EntityFrameworkCore.Tasks` (EF10) | `10.0.11` | `10.0.12` |
| `Microsoft.Extensions.*` (net10 group) | `10.0.11` / `[10.0.11, 11.0.0)` | `10.0.12` / `[10.0.12, 11.0.0)` |
| `EFCoreVersion` (EF11, drives EFCore/.Relational/.Design) | range `[11.0.0-preview.5.26302.115, 12.0.0-a)` | exact `11.0.0-rc.1.26425.128` |
| `Microsoft.EntityFrameworkCore.Specification.Tests` (EF11) | range (via `$(EFCoreVersion)`) | exact `11.0.0-preview.5.26302.115` — **deliberately exempted**, see decision 1 above |
| `Microsoft.EntityFrameworkCore.Tasks` (EF11) | `11.0.0-preview.6.26359.118` | exact `11.0.0-rc.1.26425.128` |
| `Microsoft.Extensions.*` (net11 group, 6 entries) | mixed `preview.5`/`preview.6` ranges | exact `11.0.0-rc.1.26425.128` each |

No public NuGet dependency range is broadened — EF11 moves from a loose range to a tighter, exact
pin (narrower, not wider) everywhere except the one confirmed, documented, temporary exception;
EF10 stays the same range shape, just a higher floor.

## Conditional-compilation approach

`#if NET11_0` / `#else` (SDK built-in TFM symbol), matching `docs/multi-version-ef-strategy.md`
§4.1's established convention exactly — no new `DefineConstants`. **Corrected footprint: 6 distinct
`#if NET11_0` blocks across 7 files** (see "Final `#if NET11_0` footprint" above), not the
originally planned two methods — the research pass's RC1 build experiment undercounted the break
surface (see revision note at top). Every block is either a call to a genuinely
incompatible-arity API or a small shared private helper, never a broader abstraction. This is the
provider's first use of this pattern in practice; the strategy doc anticipated it but no prior PR
has needed it until now.

## Validation commands

EF10 (must remain green, unchanged behavior):

```bash
dotnet restore EntityFrameworkCore.DynamoDb.slnx -p:Configuration="Debug EF10"
dotnet build EntityFrameworkCore.DynamoDb.slnx --configuration "Debug EF10" --no-restore
dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj --configuration "Debug EF10" --no-build
task test:aot-generation CONFIG="Debug EF10"
task test:aot-publish CONFIG="Release EF10"
```

EF11 (new, must pass after this PR):

```bash
dotnet restore EntityFrameworkCore.DynamoDb.slnx -p:Configuration="Debug EF11"
dotnet build EntityFrameworkCore.DynamoDb.slnx --configuration "Debug EF11" --no-restore
dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj --configuration "Debug EF11" --no-build
task test:aot-generation CONFIG="Debug EF11"
task test:aot-publish CONFIG="Release EF11"
```

`task test:aot-publish CONFIG="Release EF11"` is the acceptance-critical command — it exercises
`PublishAot=true`, the real `Microsoft.EntityFrameworkCore.Tasks` pipeline, the AOT warning
baseline gate, and (via Docker) actual execution against DynamoDB Local, matching every item in
the objective's Native AOT validation checklist.

Full suites per repo convention before declaring done: `task test:ef10` and `task test:ef11`.

## Acceptance criteria

- `Debug EF10`/`Release EF10` build, test, and `task test:aot-publish CONFIG="Release EF10"` are
  unaffected (no regression from the version bump).
- `Debug EF11`/`Release EF11` build cleanly with no `CS0534`/`CS0115`/`CS7036`/`CS1503`/`CS1929`/
  `CS0618` errors (the full corrected break inventory above, not just the original two).
- `dotnet restore EntityFrameworkCore.DynamoDb.slnx -p:Configuration="Debug EF11"` succeeds,
  including `SpecificationTests` (validates the Specification.Tests pin exemption).
- `task test:aot-generation CONFIG="Debug EF11"` passes (already does; must keep passing).
- `task test:aot-publish CONFIG="Release EF11"` passes end-to-end: publish succeeds, AOT warning
  IDs stay within (or an updated) reviewed baseline, and the smoke app runs its parameterized,
  materializing, and `SaveChanges` operations against DynamoDB Local successfully.
- CI's `native-aot` job is green for both `Release EF10` and `Release EF11` matrix legs.
- `Directory.Packages.props` has no *accidental* EF11 version drift or mismatch: all applicable
  EF11 runtime/design/tooling packages (`Microsoft.EntityFrameworkCore`, `.Relational`, `.Design`,
  `.Tasks`, the `Microsoft.Extensions.*` net11 group) are aligned to the exact pin
  `11.0.0-rc.1.26425.128`. `Microsoft.EntityFrameworkCore.Specification.Tests` is the single
  documented, temporary exception at `11.0.0-preview.5.26302.115` (upstream packaging defect,
  test-only, not part of the consumer-facing NativeAOT requirement) — the class of *undocumented*
  bug that caused the original blocker (pins silently diverging with no record of why) cannot
  recur.
- `.Limit(n)` precompiled-query restriction is untouched and still documented as unsupported —
  this PR does not change that behavior.
- Updated docs (`AGENTS.md`, smoke app `AGENTS.md`, `docs/querying/precompiled-queries.md`) no
  longer claim EF11 NativeAOT is blocked or unsupported.

## Newly discovered risks (this revision)

- **Resolved**: the `Create` override's dropped `ValueComparer` parameters (§4) were confirmed
  unused by the provider's own logic — no behavior loss, no remaining risk.
- **Open, low severity**: item C's `CreateEntry` fix assumes `runtimeEntityType.FindProperty(name)`
  never returns `null` for a seed-data key. This should hold (seed data is defined against the
  same model), but hasn't been exercised against this provider's actual seed-data test coverage
  yet — verify during implementation by running the existing seed-data tests (if any) under
  `Debug EF11`, and add a defensive `?? throw` with a clear message if `FindProperty` could
  plausibly return `null` for a legitimately-configured shadow/converted property.
- **Open, low severity**: item A's shared `AsScalarIndexProperty` helper needs a decision on where
  it physically lives (duplicated `#if`-guarded private static in each of the three files vs. one
  internal shared location) — a pure code-organization call with no behavioral difference, left to
  implementation-time judgment per your "don't create an abstraction just to avoid a few explicit
  checks" guidance; default to the smallest diff (duplicate the ~10-line helper if that avoids
  introducing a new shared internal type) unless three near-identical copies would themselves be
  confusing.
- Not yet attempted this revision: `task test:aot-generation CONFIG="Debug EF11"` and
  `task test:aot-publish CONFIG="Release EF11"` — blocked until the §A/§B/§C fixes are implemented
  and `Debug EF11` builds clean. This is the next implementation step, not yet done.

## Revised finding: EF11 compiled-model codegen drops `ClrType` for every property — provider-wide, not just `Create`

**Retracting part of the earlier `Create(...)` conclusion.** The provider's own comparer-passthrough
logic in `Create` genuinely is mechanical and unaffected (confirmed unchanged, still correct). But
that conclusion did not cover — and turned out to be incomplete about — how **EF Core's own base
`CSharpRuntimeAnnotationCodeGenerator`** uses the `Create` override's return path to decide what
compiled-model source to emit for a `CoreTypeMapping`. That part is not mechanical on EF11.

**Finding, confirmed by diffing actual generated compiled-model source between EF10 and EF11** (same
model, same `CompiledModelExecutionTests` scenario): EF Core's own public convenience method
`CoreTypeMapping.Clone(...)` — reflected over by EF's base codegen to decide which named arguments
to emit when reconstructing a mapping in generated code — **no longer accepts a `clrType`
parameter on EF11 RC1** (confirmed via reflection: EF11's `Clone` signature is
`Clone(ref TypeMappingInfo?, ValueConverter, ValueComparer comparer, ValueComparer keyComparer,
ValueComparer providerValueComparer, CoreTypeMapping elementTypeMapping, JsonValueReaderWriter)` —
no `clrType`; EF10's included `clrType` as a named optional parameter).

This provider's `DynamoTypeMappingSource` pattern is: every property's `DynamoTypeMapping` is
represented, in generated compiled-model source, as `DynamoTypeMapping.Default.Clone(...,
clrType: typeof(X))` — cloning a single shared `object`-typed singleton and overriding its
`ClrType` per property via that named argument. On EF11, since `clrType` no longer exists on
`Clone(...)`, EF's codegen silently omits it — **every property's compiled-model mapping is
generated with `ClrType` left as `object`, not just collection/dictionary properties.** Confirmed
by inspecting the generated source for the plain `string pk` (partition key) property, not only
`Dictionary<string, decimal> Charges` — both lose their `clrType:` argument identically. This is
provider-wide across every scalar and collection property that goes through compiled-model
generation, not a `Create`-specific or collection-specific issue.

**Why this wasn't caught earlier**: it's a runtime failure inside a dynamically-compiled and
dynamically-loaded assembly's static initializer (`CompiledModelExecutionTests` uses Roslyn to
compile and load the generated compiled-model source in-process), only reachable once `Debug EF11`
actually builds and this specific test executes — which never happened before this session, since
every earlier attempt failed to compile first (the original Tasks/EFCore pin mismatch, then the
`TranslateFullJoin`/`Create` signature breaks, then the §A/§B/§C breaks). This is the first time
the provider has ever executed a real EF11 compiled-model round-trip.

### Correction to an earlier statement in this section

An earlier pass of this investigation stated that EF11's new generic mapping types
(`CoreTypeMapping<T>`) appeared "unused, forward-looking infrastructure not yet wired into the
compiled-model generator." **That was wrong and is retracted.** A follow-up investigation, fetching
EF Core's actual source from `dotnet/efcore` at both the `v10.0.12` and `v11.0.0-rc.1.26425.128`
tags via `gh api`, found that EF11 introduced and actively uses generic mapping types as part of
the same NativeAOT work that removed `clrType` from `Clone(...)`:

- The originating change is **[dotnet/efcore#38440 — "Make type mapping generic so that it can
  create value comparers for NativeAOT"](https://github.com/dotnet/efcore/pull/38440)**, merged
  2026-06-22, fixing **[dotnet/efcore#36817](https://github.com/dotnet/efcore/issues/36817)** (a
  real user's NativeAOT compiled-model bug report about `ValueComparer.CreateDefault(Type, bool)`
  using `MakeGenericMethod` reflection, not NativeAOT-safe).
- `CoreTypeMapping<T>`'s entire purpose is narrower than "carries ClrType": its only override is
  `CreateDefaultComparer`, which uses the generic, reflection-free `ValueComparer.CreateDefault<T>(bool)`
  overload instead of the reflection-based `CreateDefault(Type, bool)`. It does **not**
  automatically set `ClrType = typeof(T)` — that still comes from `CoreTypeMappingParameters`,
  unchanged from EF10.
- EF's own built-in relational mappings (`StringTypeMapping`, `IntTypeMapping`, etc.) still derive
  from the plain, non-generic `CoreTypeMapping` — they never needed this trick, because each already
  has its own dedicated concrete class with a naturally-correctly-typed `Default`.
- **The directly relevant, first-party reference implementation is EF's InMemory provider**
  (`src/EFCore.InMemory/Storage/Internal/`), which ships exactly this problem's solution:
  `InMemoryTypeMapping` (existing non-generic base, unchanged) plus a new
  `InMemoryTypeMapping<T> : InMemoryTypeMapping` (**not** `: CoreTypeMapping<T>` — single
  inheritance from the provider's own existing base, no diamond problem), giving every closed
  instantiation (`InMemoryTypeMapping<string>`, `InMemoryTypeMapping<decimal>`, ...) its own
  genuinely-correctly-typed static `Default`, because static members on closed generic types are
  per-instantiation in .NET. Fetched in full:

  ```csharp
  public class InMemoryTypeMapping<T> : InMemoryTypeMapping
  {
      public static new InMemoryTypeMapping<T> Default { get; } = new();

      public InMemoryTypeMapping(ValueComparer? comparer = null, ValueComparer? keyComparer = null,
          JsonValueReaderWriter? jsonValueReaderWriter = null)
          : base(typeof(T), comparer, keyComparer, jsonValueReaderWriter) { }

      private InMemoryTypeMapping(CoreTypeMappingParameters parameters) : base(parameters) { }

      protected override ValueComparer CreateDefaultComparer(bool favorStructuralComparisons)
          => ClrType == typeof(T)
              ? ValueComparer.CreateDefault<T>(favorStructuralComparisons)
              : base.CreateDefaultComparer(favorStructuralComparisons);

      public override CoreTypeMapping WithComposedConverter(ValueConverter? converter, ValueComparer? comparer = null,
          ValueComparer? keyComparer = null, CoreTypeMapping? elementMapping = null,
          JsonValueReaderWriter? jsonValueReaderWriter = null)
          => new InMemoryTypeMapping<T>(Parameters.WithComposedConverter(converter, comparer, keyComparer, elementMapping, jsonValueReaderWriter));

      protected override CoreTypeMapping Clone(CoreTypeMappingParameters parameters)
          => new InMemoryTypeMapping<T>(parameters);
  }
  ```

- `InMemoryTypeMappingSource` resolves an actual runtime `Type` to the correct closed generic
  mapping via a `switch` of direct generic calls for common types, falling back to reflection for
  arbitrary types — **explicitly declared AOT-safe by EF's own source comment**:

  ```csharp
  [UnconditionalSuppressMessage("AOT", "IL3050:...",
      Justification = "The type mapping source is not used at runtime by NativeAOT applications, which use a compiled model instead.")]
  private static InMemoryTypeMapping CreateMappingWithReflection(Type clrType, ...)
  {
      var genericType = typeof(InMemoryTypeMapping<>).MakeGenericType(clrType);
      return ... (InMemoryTypeMapping)Activator.CreateInstance(genericType, ...)!;
  }
  ```

  This is the first-party confirmation that `TypeMappingSource`/`FindMapping` reflection is
  genuinely safe under NativeAOT: a compiled model bypasses `FindMapping` entirely at runtime
  (already independently confirmed earlier in this investigation — generated compiled-model source
  never references `DynamoTypeMappingSource` at all), so `FindMapping`'s own reflection only ever
  runs in ordinary non-AOT apps or during compiled-model *generation* (also always a normal,
  non-AOT-published `dotnet` process).
- Checked via reflection: `ValueComparer.CreateDefault<T>(bool)` (the generic overload) **already
  exists on EF10 10.0.11**, and `CoreTypeMapping\`1` does **not** exist on EF10 — confirming a
  `DynamoTypeMapping<T> : DynamoTypeMapping` (mirroring `InMemoryTypeMapping<T>`, not inheriting
  EF's `CoreTypeMapping<T>`) compiles and behaves identically, unconditionally, on both EF10 and
  EF11 — **no `#if NET11_0` needed for this piece at all**, pending confirmation by actual
  compilation during implementation.

### Root-cause classification (superseding the "needs more investigation" note above)

**Category A — an EF11-intended provider-contract migration, with a small, directly-precedented,
largely mechanical adaptation.** Not category B (no bespoke redesign required — EF's own InMemory
provider ships the exact template) and not category C (no upstream defect — this is a confirmed,
deliberate EF11 refactor with a working, documented-in-source migration path, just not
communicated as provider migration guidance anywhere outside EF's own source).

### Approved architecture

Mirror `InMemoryTypeMapping` / `InMemoryTypeMapping<T>` / `InMemoryTypeMappingSource` for DynamoDB:

- **`DynamoTypeMapping` (existing, unchanged)** stays the shared, non-generic home for every
  DynamoDB-specific behavior: `ReaderWriter`, `CreateReadExpression`, `CreateAttributeValue`,
  `GenerateConstant`, `CreatePartiQlLiteralExpression`, collection-codec priming support, etc. None
  of this is duplicated or moved.
- **New `DynamoTypeMapping<T> : DynamoTypeMapping`** — a thin, mechanical addition mirroring
  `InMemoryTypeMapping<T>` exactly: typed static `Default`, a constructor supplying `typeof(T)` to
  the existing `Type clrType` base constructor, the AOT-safe `CreateDefaultComparer` override, and
  `Clone`/`WithComposedConverter` overrides that preserve `DynamoTypeMapping<T>` (return the
  generic type, not the plain base) so cloned/converted mappings keep their correct closed generic
  type through the whole chain.
- **`DynamoTypeMappingSource`'s ~5 construction call sites** (`new DynamoTypeMapping(clrType)` /
  `new DynamoTypeMapping(clrType, comparer).WithComposedConverter(...)`) switch to resolving the
  correct closed `DynamoTypeMapping<T>` — direct generic calls for common known CLR types, a
  `MakeGenericType`+`Activator.CreateInstance` fallback for arbitrary (including value-converted)
  CLR types, carrying the same `[UnconditionalSuppressMessage]` justification EF's own InMemory
  provider uses. This reflection is confined to the mapping-source/generation-time path only — it
  never appears in generated compiled-model source or any NativeAOT runtime code path.
- **Why this matters for DynamoDB specifically**: the provider supports arbitrary user model CLR
  types via `ValueConverter`, so a finite "one dedicated class per CLR type" strategy (which is
  sufficient for EF's own built-in relational mappings, covering a small fixed catalog of database
  column types) isn't viable here. The closed-generic-type approach preserves arbitrary-CLR-type
  support — `DynamoTypeMapping<TAny>` works for literally any `T`, generated on demand — without
  any NativeAOT-unsafe runtime reflection in the path that actually executes inside an AOT-published
  binary. `ClrType` remains the *model* type in converted scenarios (`Converter?.ModelClrType ??
  Parameters.ClrType`, unchanged EF behavior); the converter still carries model↔provider
  translation exactly as today.
- **Collections need no new mechanism.** EF's base `Create` codegen already recurses into
  `Create(typeMapping.ElementTypeMapping, parameters)` for element mappings; once both the outer
  and element mappings resolve to correctly-typed closed `DynamoTypeMapping<T>` instances, this
  unmodified recursion reconstructs them correctly. `PrimeListMapping`/`PrimeDictionaryMapping`
  (this provider's own reflection-free collection-codec priming, layered on top via the existing
  `Create` override) is unaffected and unchanged.
- **`DynamoComplexTypeMapping : DynamoTypeMapping(clrType)`** (used for whole complex-type values
  in query parameters/constants) also constructs via a runtime `Type` — flagged as likely needing
  the same generic treatment for full consistency; scope to be confirmed during implementation
  rather than assumed up front.

### ADR decision

**No ADR.** This is internal provider infrastructure (`DynamoTypeMapping`/`DynamoTypeMappingSource`
are not part of any documented public extension contract beyond ordinary type-mapping
customization that remains unaffected); no existing public API is removed or changed; the approach
directly follows EF Core's own first-party InMemory provider pattern rather than inventing a new
one; it's required to adapt to EF11's NativeAOT type-mapping contract, not a discretionary design
choice; and it introduces no new independent provider abstraction or consumer-facing policy.
Adding `DynamoTypeMapping<T>` is a new type, but that alone doesn't clear the bar for an ADR here.

### Upstream issue decision

**No EF issue will be filed.** The `Clone(clrType:)` removal is part of EF11's intentional,
confirmed NativeAOT type-mapping redesign (PR #38440), and EF's own InMemory provider demonstrates
the complete, working migration path — there is no defect to report. A documentation suggestion to
the EF team (that third-party non-relational providers migrating to NativeAOT compiled models need
this InMemory-mirroring pattern, since it isn't called out anywhere as provider guidance) could be
considered separately, later, outside this PR.

## Implementation status (this revision)

**Generic `DynamoTypeMapping<T>` implementation is complete**, matching the approved architecture
above, with one deviation found only by actually running the EF10 NativeAOT smoke test (not by
compilation): EF10 has no `CreateDefaultComparer` override point on `CoreTypeMapping` at all
(confirmed absent via reflection), so `DynamoTypeMapping<T>`'s EF10 constructor branch eagerly
computes comparers via the generic, reflection-free `ValueComparer.CreateDefault<T>(bool)`
overload (present on EF10 too) instead of leaving them to lazily fall back to the reflection-based
`CreateDefault(Type, bool)` — which is exactly what a NativeAOT-published EF10 binary hit and
crashed on before this fix. Confirmed fixed: `Release EF10` NativeAOT smoke now passes completely,
end to end (publish, warning baseline, all 8 runtime scenarios including `SaveChanges`).

Two more EF11-specific issues were found and fixed while running the **full** EF10/EF11
solution test suites (not just the AOT-focused ones) — both unrelated to type mapping:

- `CoreEventId.ShadowPropertyNameNotValidIdentifierWarning` (new to EF11, confirmed absent on
  EF10) flags this provider's deliberate `$type` discriminator shadow property under EF's
  spec-test harness (which treats warnings as errors). Fixed via the provider's existing default
  `ConfigureWarnings` mechanism in `DynamoDbContextOptionsExtensions.cs` (same pattern as the
  pre-existing `DynamoEventId.ScanLikeQueryDetected` default), `#if NET11_0`-guarded. `$type`
  itself was not renamed — that's a wire-format-breaking change and out of the question.
  Reduced 1345 → 26 spec-test failures.
- The seed-data `CreateEntry` fix's defensive `throw` (added when it was believed to be purely
  mechanical) was too strict: EF's own shared spec-test fixtures seed a complex/owned-shaped key
  this provider's `FindProperty` doesn't resolve, which the pre-EF11 string-keyed overload
  tolerated silently. Changed from throw to skip-unmatched-keys, restoring that same tolerance.
  Reduced 26 → 0 spec-test failures.

Current validated state:
- `Debug/Release EF10` — full solution build, full test suite (3588 total, 0 failed), NativeAOT
  smoke: **all green**.
- `Debug EF11` — full solution build, full test suite (3653 total, 0 failed), `task
  test:aot-generation CONFIG="Debug EF11"`: **all green**.
- `Release EF11` NativeAOT publish (`task test:aot-publish CONFIG="Release EF11"`): **blocked**.

### Correcting an earlier conclusion: RC1 package alignment did not fix the original Tasks blocker

An earlier pass of this investigation (during the initial research phase, before implementation)
concluded the original documented blocker — `Microsoft.EntityFrameworkCore.Tasks`'s isolated
`OptimizeDbContext` compilation failing to resolve `Amazon.*`/`DbContext`/etc. when compiling
generated interceptors — was a repo-owned version-pin mismatch, fixed once EF11 packages were
aligned to an identical exact pin. **That conclusion was incomplete and is retracted.** Retesting
the smoke app's actual `dotnet publish` at the now fully-aligned, exact
`11.0.0-rc.1.26425.128` pins reproduces the exact same failure, unchanged, with all type-mapping
and diagnostic fixes above already in place: the isolated `OptimizeDbContext` compilation still
cannot resolve `Amazon.*`, `DbContext`, `DbSet<>`, or any smoke-app-specific type, even though the
normal build of the same project (immediately prior, in the same `dotnet publish` invocation)
succeeds cleanly. The original research pass's "only 2 errors, TranslateFullJoin/Create" result
for a bumped-to-RC1 build evidently never actually reached this specific failure mode (that
investigation ran during a session where a background fork was independently, concurrently racing
edits to `Directory.Packages.props` — see the research doc's revision note — and most likely never
completed a real `dotnet publish` of the smoke app). This publish-time failure is genuinely
distinct from, and unaffected by, every fix implemented in this pass.

This is now the sole remaining blocker to `Release EF11` NativeAOT support. CI matrix changes and
documentation claiming EF11 NativeAOT support remain intentionally deferred until it's resolved —
implementing or documenting either now would misrepresent the actual state.

### Root cause, confirmed: upstream `Microsoft.EntityFrameworkCore.Design` defect, present in both EF10 and EF11

Traced via MSBuild diagnostic log (`-flp:v=diag`) of the failing `Release EF11` publish. The
`OptimizeDbContext` MSBuild task correctly runs against the smoke app project (not the provider —
that earlier hypothesis is ruled out) and correctly shells out to `dotnet exec ... ef.dll
dbcontext optimize --precompile-queries --nativeaot ...` against the already-built
`EntityFrameworkCore.DynamoDb.NativeAotSmoke.dll`. The stack trace of the actual failure is:

```
System.InvalidOperationException: Compilation failed with errors: ... CS0246: 'Amazon' ... CS0246: 'DbContext' ...
   at Microsoft.EntityFrameworkCore.Design.Internal.DbContextOperations.PrecompileQueries(...)
   at Microsoft.EntityFrameworkCore.Design.Internal.DbContextOperations.Optimize(...)
   ...
```

Fetched `DbContextOperations.cs` from `dotnet/efcore` at both `v10.0.12` and
`v11.0.0-rc.1.26425.128`. The `PrecompileQueries` method is **byte-for-byte identical** between
the two versions (same TODO comment, same call), containing:

```csharp
outputDir = Path.GetFullPath(Path.Combine(_projectDir, outputDir ?? "Generated"));

// TODO: pass through properties
MSBuildWorkspace workspace = null!;
Project project;
try
{
    workspace = MSBuildWorkspace.Create(new Dictionary<string, string> { ["_EFGenerationStage"] = "build" });
    workspace.LoadMetadataForReferencedProjects = true;
    ...
    project = workspace.OpenProjectAsync(_project).GetAwaiter().GetResult();
}
...
var compilation = project.GetCompilationAsync().GetAwaiter().GetResult()!;
```

**The `// TODO: pass through properties` comment is EF Core's own developers acknowledging the
gap**: `MSBuildWorkspace.Create(...)` is given only `_EFGenerationStage=build` as a global
property — never `$(Configuration)`, `$(RuntimeIdentifier)`, or any other property the *actual*
build invocation used. When `workspace.OpenProjectAsync` re-opens and re-evaluates the `.csproj`
for this query-precompilation-specific Roslyn `Compilation`, MSBuild evaluates it with whatever
its own default `Configuration` resolves to — not the real `Release EF11` in progress.

This repo's entire EF10/EF11 dual-support architecture is built on **custom, non-default
build-configuration names** (`Debug EF10`/`Release EF10`/`Debug EF11`/`Release EF11` — see
`docs/multi-version-ef-strategy.md`), with every EF-version-specific `PackageReference`/
`TargetFramework` gated on `Condition="'$(Configuration)' == '...'"`, and `Directory.Packages.props`
falling back to the **EF10** version range whenever `$(Configuration)` doesn't match either EF11
condition (`<EFCoreVersion Condition="'$(EFCoreVersion)' == ''">[10.0.12, 11.0.0)</EFCoreVersion>`).
This means:

- For `Release EF10` (EF10 smoke — passes): `MSBuildWorkspace`'s implicit default-Configuration
  re-evaluation happens to fall through to the *same* EF10-flavored package/TFM resolution the
  real build used — the mismatch exists but is invisible because both land in the same place.
- For `Release EF11` (EF11 smoke — fails): the implicit default re-evaluation falls through to
  that **same EF10 fallback**, but the actual assembly being analyzed was built for
  **net11.0/EF11 RC1**. The re-opened project's intermediate/`obj` output path, target framework,
  and package versions no longer match what was actually built at all — resulting in a
  from-scratch, effectively unbuilt project graph, which is consistent with *literally nothing*
  resolving (not `Amazon`, not `DbContext`, not any framework type).

Attempted to build an external minimal repro (two-project library+app solution, custom
configuration names, `Microsoft.EntityFrameworkCore.InMemory`, `PublishAot=true`,
`--precompile-queries`) to demonstrate this independent of the provider. It did not reach the
same failure — it got past the `MSBuildWorkspace` reference-resolution step (no `CS0246` errors)
and instead hit an unrelated `LinqToCSharpSyntaxTranslator` crash translating a trivial `Where`
query, which appears to be a separate, generic EF11 RC1 query-precompilation limitation unrelated
to this issue. Given the time already invested and that the mechanism is independently confirmed
by direct source inspection (not speculative), further repro iteration was not pursued. The
provider repo itself remains a fully reliable, deterministic repro (reproduced twice, identically).

**Classification: confirmed upstream `Microsoft.EntityFrameworkCore.Design`/`.Tasks` limitation**
(category B), present unchanged in both EF10 and EF11, that only *manifests* for EF11 in this repo
because of how this repo's specific default-fallback design (EF10 as the implicit default) happens
to mask it for EF10 and expose it for EF11. Not repo-owned, not fixable by any MSBuild property
this repo could set (the gap is that `PrecompileQueries` never reads *any* externally-supplied
property for its `MSBuildWorkspace.Create(...)` call — there's no supported hook to feed it
`Configuration` from the outer build). No repository-owned fix was found or applied.

**Outcome**: `Release EF11` NativeAOT publish cannot be made to pass without either (a) an upstream
EF Core fix (passing `$(Configuration)`/other properties through to the `PrecompileQueries`
`MSBuildWorkspace` instantiation), or (b) this repo restructuring its EF10/EF11 differentiation
away from non-default `$(Configuration)` values entirely (a much larger, disruptive change,
explicitly not attempted here — it would contradict the established, documented multi-version
strategy). CI matrix and documentation changes remain deferred. This PR's `.Limit(n)` exclusion
stays as originally scoped; this new finding does not change that.

### Minimal upstream repro — confirmed, both EF versions

A follow-up pass produced a clean, minimal, deterministic, single-project repro (built outside
this repo, in a temporary scratch location — not retained here) that isolates the defect from any
DynamoDB-provider or query-translation specifics: one `.csproj` with an *unconditional default*
`TargetFramework` (matching this repo's exact pattern of a top-level default overridden by a
`Configuration`-conditional `PropertyGroup`), custom `Debug Custom`/`Release Custom` configuration
names, and `Configuration`-conditional `PackageReference`s split by the resulting
`$(TargetFramework)`. A trivial `DbContext` with zero LINQ queries (to avoid an unrelated,
separately-discovered EF11 RC1 `LinqToCSharpSyntaxTranslator` limitation with query translation)
is enough — `PrecompileQueries` unconditionally builds and validates a `Compilation` before ever
searching for queries.

Result: `dotnet build --configuration "Release Custom"` succeeds correctly (net11.0,
`11.0.0-rc.1.26425.128`, confirmed via build output paths). `dotnet publish` of the same then fails
identically to the provider repo's failure — `Microsoft.EntityFrameworkCore` itself unresolvable,
cascading into every dependent type. Flipping which target framework is the "wrong" unconditional
default (net11.0 default / net10.0 conditional, EF10 packages) reproduces the **identical** failure
using `microsoft.entityframeworkcore.tasks/10.0.12` instead — confirming the defect is genuinely
version-independent, exactly as the identical source code between the two versions predicted.

This is now considered a fully confirmed, upstream-quality, minimal, reproducible defect in
`Microsoft.EntityFrameworkCore.Design`/`.Tasks`, independent of this provider.

**Filed: [dotnet/efcore#38951](https://github.com/dotnet/efcore/issues/38951) — "dotnet ef
dbcontext optimize --precompile-queries does not pass `$(Configuration)` to MSBuildWorkspace,
breaking non-default build configurations."** Affects `Microsoft.EntityFrameworkCore.Design`/
`.Tasks` `10.0.12` and `11.0.0-rc.1.26425.128` identically (confirmed via source). Possibly related
to the pre-existing, unresolved
[dotnet/efcore#36055](https://github.com/dotnet/efcore/issues/36055) (same
`PrecompileQueries`/`MSBuildWorkspace.OpenProjectAsync` code path, different proximate symptom —
noted in the filed issue, not treated as a duplicate).

`Release EF11` NativeAOT publish remains blocked on this repo's side pending upstream resolution
of #38951. No provider-side workaround is planned — none exists that doesn't either bypass EF's
own safety mechanisms or require abandoning this repo's established, documented multi-version
build-configuration architecture (`docs/multi-version-ef-strategy.md`).

### Second upstream issue filed: `Specification.Tests` public restore

Separately, **[dotnet/efcore#38952](https://github.com/dotnet/efcore/issues/38952) —
"Microsoft.EntityFrameworkCore.Specification.Tests 11.0 RC1 cannot be restored from NuGet.org
because of non-public Microsoft.DotNet.XUnitV3Extensions dependency"** was filed for the
`Microsoft.EntityFrameworkCore.Specification.Tests` restore gap already documented above (§
"Version research" / the `Directory.Packages.props` exemption). Verified directly against
NuGet.org during this pass:

- `Microsoft.EntityFrameworkCore.Specification.Tests` **`11.0.0-preview.5.26302.115`** is the last
  version that restores cleanly from public NuGet.org (confirmed via a clean `dotnet restore`).
- The broken dependency (`Microsoft.DotNet.XUnitV3Extensions`, requested at
  `11.0.0-beta.26403.1` in the RC1 nuspec, confirmed absent from NuGet.org entirely — 404 on the
  package index) first appears in **`11.0.0-preview.6.26359.118`** and remains present, unchanged,
  through `11.0.0-preview.7.26381.103` and `11.0.0-rc.1.26425.128`.
- Exact restore failure reproduced in a clean scratch project: `error NU1101: Unable to find
  package Microsoft.DotNet.XUnitV3Extensions. No packages exist with this id in source(s):
  nuget.org`.

This repo's existing workaround (`Directory.Packages.props` pinning
`Microsoft.EntityFrameworkCore.Specification.Tests` to `11.0.0-preview.5.26302.115` while every
other EF11 package uses the exact `11.0.0-rc.1.26425.128` pin) remains in place and is unaffected
by this issue's resolution timeline. No provider-side workaround beyond that existing pin is
planned — there is no way to restore a newer `Specification.Tests` build from public sources until
this is fixed upstream.

### Final status of this PR

- **Provider compatibility implementation: complete.** All EF11 compile/runtime/metadata
  adaptations (§A/§B/§C above, the generic `DynamoTypeMapping<T>` architecture, the two diagnostic
  fixes) are implemented, tested, and validated.
- **EF10 NativeAOT: fully validated end-to-end** (build, full test suite, `test:aot-generation`,
  `test:aot-publish` — all green, no regressions from this PR's changes).
- **EF11 build/tests/AOT-generation: fully validated** (build, full test suite, and
  `test:aot-generation` all green).
- **EF11 NativeAOT *publish*: blocked**, exclusively by the confirmed upstream
  `Microsoft.EntityFrameworkCore.Design`/`.Tasks` defect above — not by anything in this repo or
  this provider.
- **No provider-side workaround will be added** for the upstream defect — none exists that doesn't
  either bypass EF's own safety mechanisms or require abandoning this repo's established
  multi-version build-configuration architecture.
- CI matrix changes and documentation claiming EF11 NativeAOT support remain deferred until the
  upstream issue is resolved.
- This PR is expected to remain open pending upstream resolution, or to be scoped down (EF11
  build/test/AOT-generation support merged now, NativeAOT publish support following once EF Core
  fixes the defect) — a decision for the repo owner, not made here.

## Unresolved decisions requiring input

None remaining. All decisions — including the final architecture direction above — were resolved
via direction received during this investigation.
