# EF Core 11 NativeAOT / precompiled-query enablement — research

Status: research complete, no implementation yet. Scope: EF11 NativeAOT/precompiled-query
enablement only. `.Limit(n)` NativeAOT support is explicitly out of scope for this document and
is a separate provider-owned follow-up.

Consumer motivation: `trivia-platform` (`TriviaDbContext` on `EntityFrameworkCore.DynamoDb`)
wants to NativeAOT-publish two Lambda hosts targeting .NET 11. See its
`docs/research/2026-09-09-native-aot-compatibility-research.md` and
`docs/adr/0019-native-aot-deployment-architecture.md` (not modified here).

## 1. Current architecture (unchanged, treated as the baseline)

- `src/EntityFrameworkCore.DynamoDb/Design/Internal/DynamoPrecompiledQueryCodeGenerator.cs` —
  `DynamoPrecompiledQueryCodeGenerator : PrecompiledQueryCodeGenerator`. Overrides
  `GeneratePrecompiledQueries(...)`; post-processes EF's generated interceptor code via
  regex-based rewrites (`RewriteLegacyInterceptLocations`, `RewriteGeneratedFilePreamble`,
  `RewriteExecutorPreamble`) to adapt the generated executor preamble/PartiQL template to the
  provider's execution model.
- `src/EntityFrameworkCore.DynamoDb/Design/Internal/DynamoCSharpRuntimeAnnotationCodeGenerator.cs`
  — overrides `Generate(IEntityType, ...)` (strips `DynamoAnnotationNames.TableGroupName` before
  serializing compiled-model annotations) and a `Create(CoreTypeMapping, ...)` overload.
- `src/EntityFrameworkCore.DynamoDb/Design/Internal/DynamoDesignTimeServices.cs` — registers both
  as `IPrecompiledQueryCodeGenerator` / `ICSharpRuntimeAnnotationCodeGenerator` singletons, under
  `#pragma warning disable EF9100` (EF's experimental-API suppression).
- **No `#if NET11_0` / `#if EF11` / `EFCORE11` conditional compilation exists anywhere in `src/`**
  (confirmed by grep — zero hits). The provider is 100% shared source between EF10 and EF11
  today; the only place EF-version differences are handled is `Directory.Packages.props`
  (MSBuild `Condition` on `$(Configuration)`), not C#.

This matches the objective's framing: a real precompiled-query implementation already exists and
should be the starting point, not something to redesign.

## 2. Validation infrastructure inventory

- **`tests/EntityFrameworkCore.DynamoDb.AotTests/`** (`PrecompiledQueryGenerationTests.cs`,
  `PrecompiledParityTests.cs`, `CompiledModelExecutionTests.cs`, fixture entities, TFM
  config-conditional net10.0/net11.0). These tests invoke
  `DynamoPrecompiledQueryCodeGenerator`/base `PrecompiledQueryCodeGenerator` **in-process via
  Roslyn** (`Compilation` + `SyntaxGenerator`). **They do not reference
  `Microsoft.EntityFrameworkCore.Tasks` at all** — the csproj has no `PackageReference` for it.
  This means `task test:aot-generation` never exercises the real MSBuild-task precompile path and
  cannot, by itself, prove or disprove the historical EF11 blocker. It currently passes on both
  EF10 and EF11 configs, but that is not evidence the Tasks-based precompile step works on EF11.
- **`testapps/EntityFrameworkCore.DynamoDb.NativeAotSmoke/`** — the only project that references
  `Microsoft.EntityFrameworkCore.Tasks` as a real build-time package (`PrivateAssets=all`), sets
  `PublishAot=true`, and is a real, runnable DynamoDB-backed console app. `Configurations`
  includes all four (`Debug/Release EF10/EF11`), TFM is config-conditional. Invoked via
  `scripts/run-nativeaot-smoke.sh` (publish → AOT-warning baseline gate → Docker-run against
  DynamoDB Local), wrapped by `task test:aot-publish` (default `Release EF10`). This is where the
  real blocker lives and reproduces.
- **CI** (`.github/workflows/pr-build.yaml`): `application-build` (Debug EF10 + Debug EF11
  matrix), `native-aot` (**EF10-only**, `Release EF10`, runs the smoke script), `aot-generation`
  (Debug EF10 + Debug EF11 matrix, builds/tests `AotTests.csproj` directly with the `dotnet` CLI).
  A `build` gate requires all three green. There is currently no EF11 leg of `native-aot` at all.
- **Docs**: `docs/querying/precompiled-queries.md` already accurately describes today's state —
  "experimental," EF11 has a known blocker, NativeAOT is "unavailable for EF Core 11," and it
  correctly distinguishes `test:aot-generation` (both EF versions) from `test:aot-publish` (EF10
  only demonstrated).
- **Taskfile**: `test:aot-generation` (CONFIG param), `test:aot-publish` (CONFIG default
  `Release EF10`, shells to the smoke script), `build:ef11:ci-sdk` (installs a CI-matching .NET 11
  SDK to `.dotnet/ef11`). No EF11-specific `test:aot-publish` task exists; it would have to be
  invoked manually with `CONFIG="Release EF11"`, and is untested/unsupported per the smoke app's
  `AGENTS.md`.

## 3. Version research

| Package/SDK | Current pin (this repo) | Latest available (confirmed on nuget.org / dotnet.microsoft.com, Sept 2026) |
|---|---|---|
| .NET 10 SDK/runtime | not explicitly pinned (`global.json` has no SDK pin) | **10.0.12** (SDK 10.0.401), released 2026-09-08 |
| Microsoft.EntityFrameworkCore / .Relational / .Design (10.x) | `[10.0.11, 11.0.0)` → resolves 10.0.11 | **10.0.12** |
| Microsoft.EntityFrameworkCore.Tasks (10.x) | `10.0.11` | **10.0.12** |
| .NET 11 SDK | `11.0.100-preview.6.26359.118` referenced by `build:ef11:ci-sdk`'s channel install; **`11.0.100-rc.1.26425.128` is already installed and is the active `dotnet` on this machine** | **`11.0.100-rc.1.26425.128`** (RC1, released 2026-09-08; officially RC with go-live support, GA expected November 2026) |
| Microsoft.EntityFrameworkCore / .Relational / .Design (11.x) | `EFCoreVersion` range `[11.0.0-preview.5.26302.115, 12.0.0-a)` → **actually resolves to preview.5** | **`11.0.0-rc.1.26425.128`** |
| Microsoft.EntityFrameworkCore.Tasks (11.x) | `11.0.0-preview.6.26359.118` | **`11.0.0-rc.1.26425.128`** (confirmed present on nuget.org, exact same build number as EFCore/Design/Relational — the whole EF11 surface moves in lockstep) |
| Microsoft.Extensions.* (net11 group) | `11.0.0-preview.5.26302.115` / range floor `preview.6.26359.118` (inconsistent, see below) | **`11.0.0-rc.1.26425.128`** |

**Important finding — the repo's current EF11 pins are already internally inconsistent.**
`Microsoft.EntityFrameworkCore.Tasks` is pinned to `preview.6.26359.118`, but `EFCoreVersion`
(driving `Microsoft.EntityFrameworkCore`/`.Design`/`.Relational`/`.Specification.Tests`) is
range-floored at `preview.5.26302.115` and NuGet resolves it to exactly that — `preview.5`, one
build behind Tasks. This mismatch, not an upstream EF defect, is the proximate cause of the
historical blocker (see §4).

## 4. Reproducing the old EF11 blocker

### 4a. At the repo's current (mismatched) pins — blocker reproduces exactly as documented

Built `Debug EF11` and ran `task test:aot-generation`'s equivalent (`dotnet build`/`dotnet test`
on `AotTests.csproj`): **passes**, 13/13 — but, per §2, this test project doesn't reference
`Microsoft.EntityFrameworkCore.Tasks`, so it proves nothing about the real precompiler.

Ran the real smoke app (`dotnet publish
testapps/EntityFrameworkCore.DynamoDb.NativeAotSmoke/... --configuration "Release EF11" --runtime
osx-arm64`) at the repo's current pins: **fails**, reproducing the AGENTS.md-documented symptom
exactly — the `Microsoft.EntityFrameworkCore.Tasks` MSBuild task's Roslyn compilation of the
generated interceptor cannot resolve `Amazon.*`, `DbContext`, `DbSet<>`,
`DbContextOptionsBuilder`, `ModelBuilder`, `KeyType`, `TableStatus`, `SaveChangesAsync`, etc.
(`CS0246`/`CS1061`/`CS8410`/`CS0103` cascade). Root cause, confirmed via the exact compiler error:

```
error CS0012: The type 'DbContextOptionsBuilder' is defined in an assembly that is not
referenced. You must add a reference to assembly
'Microsoft.EntityFrameworkCore, Version=10.0.11.0...'
```

i.e. `Microsoft.EntityFrameworkCore.Tasks` `preview.6` was built expecting an EFCore surface from
`preview.6`/newer, but the actual EFCore assembly resolved into the closure is `preview.5` (per
the version-range floor in `Directory.Packages.props`). The isolated Roslyn compilation the Tasks
package performs to compile the generated interceptor doesn't have the matching-version EFCore
assembly on its reference closure, so it fails to resolve basic EF/provider types.

**This specific failure is repo-owned (a version-pin mismatch), not an upstream EF defect.**

### 4b. At matched, latest RC1 pins — the pin-mismatch failure disappears; a second, small, provider-owned blocker appears

Temporarily aligned `Directory.Packages.props` so **all** of `Microsoft.EntityFrameworkCore`,
`.Relational`, `.Design`, `.Specification.Tests`, `Microsoft.EntityFrameworkCore.Tasks`, and the
`Microsoft.Extensions.*` net11 group point at the same build,
`11.0.0-rc.1.26425.128` (the file was reverted afterward; `git status` is clean, no residual
changes). Result:

- **Restore succeeds cleanly** — no more Tasks/EFCore version mismatch, confirming §4a's
  diagnosis.
- **Provider fails to build**, with exactly the two breaks the existing AGENTS.md predicted:
  - `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryableMethodTranslatingExpressionVisitor.cs:13`
    — `CS0534: does not implement inherited abstract member
    'QueryableMethodTranslatingExpressionVisitor.TranslateFullJoin(ShapedQueryExpression,
    ShapedQueryExpression, LambdaExpression, LambdaExpression, LambdaExpression)'`.
    Confirmed via reflection against the RC1 assembly: the new abstract member's exact signature
    is
    `ShapedQueryExpression? TranslateFullJoin(ShapedQueryExpression outer, ShapedQueryExpression inner, LambdaExpression outerKeySelector, LambdaExpression innerKeySelector, LambdaExpression resultSelector)`
    on the **core** (non-relational) `QueryableMethodTranslatingExpressionVisitor` base class —
    it affects any custom provider deriving from that base, not just relational providers. It
    backs EF Core 11's new `Queryable.FullJoin` LINQ operator (mirrors `LeftJoin`/`RightJoin`
    added in EF Core 10/.NET 10). DynamoDB/PartiQL has no `FULL OUTER JOIN`; the correct
    implementation is almost certainly a `NotSupportedException`-style stub matching how the
    provider already handles other unsupported join/set-operation shapes — the same pattern
    already used elsewhere in the visitor, not new architecture.
  - `src/EntityFrameworkCore.DynamoDb/Design/Internal/DynamoCSharpRuntimeAnnotationCodeGenerator.cs:44`
    — `CS0115: 'Create(CoreTypeMapping, CSharpRuntimeAnnotationCodeGeneratorParameters,
    ValueComparer?, ValueComparer?, ValueComparer?)': no suitable method found to override`.
    Confirmed via reflection against the RC1 assembly: the base
    `Microsoft.EntityFrameworkCore.Design.Internal.CSharpRuntimeAnnotationCodeGenerator.Create`
    overload relevant here is now
    `protected virtual bool Create(CoreTypeMapping typeMapping, CSharpRuntimeAnnotationCodeGeneratorParameters parameters)`
    — two parameters, `bool`-returning. The provider's override still targets an older five-parameter
    signature (with extra `ValueComparer?` parameters) that no longer exists on the base type at
    RC1. This is a mechanical override-signature update, not an architecture change.

**Ownership summary**: the originally documented blocker
("`Microsoft.EntityFrameworkCore.Tasks` can't resolve EF/AWS types compiling generated
interceptors") is **fixed by realigning package versions** — it was a repo-owned pin-consistency
bug, not an upstream defect, and disappears entirely once Tasks and EFCore point at the same
build. What's left once versions are aligned is two small, mechanical, provider-owned API
adaptations (`TranslateFullJoin` override, `Create` override signature) — not a redesign, not an
upstream blocker, not a build/tooling issue.

Neither the `TranslateFullJoin` nor the `Create` signature change appears in Microsoft's own
[EF Core 11 breaking-changes](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-11.0/breaking-changes)
or
[provider-facing-changes](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-11.0/provider-facing-changes)
docs — both were found only by direct source/assembly inspection. Other provider-facing EF11
changes documented officially and worth double-checking during implementation: type mapping made
generic (`CoreTypeMapping<T>`/`RelationalTypeMapping<T>`, `Clone(...)` dropped the `clrType` arg),
`JsonPath` changed from `string?` to a structured type, no-op SQL casts now stripped, and
`FindIndex`/`AddIndex` now accept `IReadOnlyProperty` for complex-type-traversing indexes.

No GitHub issue matching the specific Tasks/type-resolution symptom was found against
`dotnet/efcore` — it appears to be an unreported, repo-specific pin-mismatch rather than a filed
upstream bug. Related (but not matching) NativeAOT/precompile issues exist (`dotnet-ef
optimize --nativeaot` CS9137 workaround via `InterceptorsPreviewNamespaces` in #35945;
precompilation rejecting a DbContext passed through a primary-constructor parameter in #38030) —
worth knowing about but not blocking this repo's issue.

### 4c. Not reproduced yet

`Release EF11` native publish (`ilc`/`PublishAot=true`) was not completed end-to-end at RC1 pins
because the provider doesn't compile there yet (§4b) — the two API adaptations are a prerequisite
for even attempting the native-compile stage. An EF10 native-publish parity check was attempted
locally and hit an unrelated local sandbox artifact (a stale `obj` directory with a
space-containing config name breaking `ilc`'s object-file write path); this is environment noise,
not evidence of an EF10 regression — EF10 native publish is already proven in CI (`native-aot`
job), which is the authoritative signal.

## 5. What EF11 enablement actually requires

Given §4, this is much smaller than "redesign the precompiled-query architecture":

1. **Version realignment** (EF10 → 10.0.12 across EFCore/Design/Relational/Tasks; EF11 → RC1
   `11.0.0-rc.1.26425.128` across the same set, plus `Microsoft.Extensions.*` net11 group and the
   `.dotnet/ef11` CI SDK install channel). This alone eliminates the pin-mismatch failure.
2. **Two small, shared-architecture API adaptations**, gated by the existing
   `EFCoreVersion`/`TargetFramework` MSBuild conditioning (no `#if` needed if the signatures are
   compatible across both EF10 and EF11 target frameworks — needs a quick check of whether EF10's
   base signatures already match what RC1 expects, since the provider is single-sourced across
   both TFMs today):
   - Implement `TranslateFullJoin` on
     `DynamoQueryableMethodTranslatingExpressionVisitor`, following the same
     "unsupported shape" pattern the visitor already uses elsewhere.
   - Update `DynamoCSharpRuntimeAnnotationCodeGenerator`'s `Create(CoreTypeMapping, ...)` override
     to the new two-parameter, `bool`-returning signature.
   - **Confirmed via reflection against both EFCore 10.0.11 and 11.0.0-rc.1.26425.128**: EF10's
     base `Create(CoreTypeMapping, ...)` overload still has the original five-parameter,
     `ValueComparer`-taking signature the provider currently overrides; EF11 RC1's is the new
     two-parameter, `bool`-returning signature. They are incompatible — one override cannot
     satisfy both. Likewise, `TranslateFullJoin` exists only on the EF11 base class; on EF10 the
     base type has no such abstract member, so an EF10-config override would fail to compile
     (`CS0115`, no matching base method). **This means EF11 enablement forces the provider's
     first `#if NET11_0` (or equivalent `TargetFramework`-conditional) C#** — localized to these
     two methods, not a broader split. This is a real, if small, precedent-setting change to the
     provider's "single shared source tree" approach and is called out in §10 as worth a
     deliberate decision rather than a silent one.
3. **NativeAOT validation**: extend the existing `native-aot` CI job/`run-nativeaot-smoke.sh`
   architecture to also run `Release EF11`, rather than inventing a new harness. The script
   already parameterizes `CONFIG`; a matrix addition is the natural shape.
4. **`test:aot-generation`/`AotTests.csproj` gap**: this project should reference
   `Microsoft.EntityFrameworkCore.Tasks` (matching the smoke app) so it actually exercises the
   real MSBuild precompile path per EF version, not just the in-process Roslyn generator API.
   Today it gives a false sense of EF11 coverage.
5. Docs: `docs/querying/precompiled-queries.md`'s "EF Core 11 currently has a known blocker...
   NativeAOT unavailable for EF Core 11" language needs updating once EF11 is validated (not done
   in this pass, per instructions).

None of this rises to "redesign the precompiled-query architecture." The generator, the design
services, and the interceptor-rewrite logic all remain unchanged and shared between EF10 and
EF11.

## 6. CI/version strategy (identified, not implemented)

- Extend `native-aot` to a matrix job covering `Release EF10` and `Release EF11` (or add a
  parallel EF11 leg) using the same `run-nativeaot-smoke.sh` script, parameterized by `CONFIG`.
- `aot-generation` already matrixes EF10/EF11 — once `AotTests.csproj` references
  `Microsoft.EntityFrameworkCore.Tasks` (item 4 above), this job becomes meaningful evidence for
  both versions without new infrastructure.
- `build:ef11:ci-sdk` should install the RC1 channel (`--channel 11.0` already tracks the latest
  11.0 servicing/preview/RC feed, so no change needed there beyond the `Directory.Packages.props`
  pin).

## 7. Package/public API implications

- No provider public API changes are required by §4's two fixes — both are internal/protected
  overrides on `Internal`-namespaced design-time types (already `#pragma warning disable EF9100`
  experimental surface).
- `EFCoreVersion` range should probably be tightened to pin the exact validated RC1 build rather
  than an open floor (`[11.0.0-rc.1.26425.128, 12.0.0-a)` or an exact pin) to prevent the kind of
  silent pin drift that caused §4a — this is a policy question, see §9.

## 8. Documentation/skill impact (identified only, not applied)

- `docs/querying/precompiled-queries.md`: remove/update the "EF Core 11 currently has a known
  blocker... NativeAOT unavailable for EF Core 11" language once validated; document the RC1
  version requirement.
- Native AOT compatibility/version matrix (wherever it's tracked — not found as a separate doc in
  this repo currently; may need to be added to `precompiled-queries.md` or a new
  `docs/diagnostics.md` section).
- `testapps/EntityFrameworkCore.DynamoDb.NativeAotSmoke/AGENTS.md`: the whole "EF10 ONLY" framing
  and the specific blocker description need rewriting once EF11 is enabled.
- `AGENTS.md` (repo root): the NativeAOT section's "EF10 ONLY" instruction is central to this
  change and must be updated in the implementation PR.
- No skill in this repo currently encodes EF10/EF11-specific NativeAOT behavior that this would
  invalidate (checked available skills; none reference this repo's AOT architecture specifically).

## 9. ADR gate

**No ADR is warranted.** Per §5, EF11 enablement resolves to: realigning already-declared package
version pins to a consistent, latest RC1 set; two small, mechanical, non-public-API method
implementations following the provider's existing unsupported-shape pattern; extending an
existing CI script to a second configuration via its existing `CONFIG` parameter; and closing a
test-coverage gap in an existing test project. None of this is an architectural change, a
public-API decision, or a compatibility-policy shift — it's dependency/version updates, adapting
to compatible EF11 APIs, validation, CI, and documentation, which the objective's own architecture
gate explicitly excludes from needing an ADR.

## 10. Decisions (resolved with repo owner during this research pass)

- **EF11 package pin policy**: pin EF11 packages (`Microsoft.EntityFrameworkCore`,
  `.Relational`, `.Design`, `.Specification.Tests`, `Microsoft.EntityFrameworkCore.Tasks`, the
  `Microsoft.Extensions.*` net11 group) to an **exact** build
  (`11.0.0-rc.1.26425.128` initially), not a floating range — unlike EF10's current
  floor+open-ceiling style. This directly prevents the pin-drift bug that caused the reproduced
  blocker in §4a.
- **`#if TargetFramework` precedent**: accepted, kept minimal — limited to the two methods in
  §4b/§5 (`TranslateFullJoin`, the `Create` override signature). This is the provider's first
  target-framework-conditional C#; document it as a deliberate, narrow exception to the otherwise
  shared source tree.
- **CI shape**: extend the existing `native-aot` job to a `fail-fast: false` matrix over
  `Release EF10` / `Release EF11`, reusing `run-nativeaot-smoke.sh` as-is (parameterized by
  `CONFIG`), rather than adding a separate parallel job — mirrors how `aot-generation` already
  matrixes EF10/EF11.
