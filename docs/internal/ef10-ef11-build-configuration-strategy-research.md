# EF10/EF11 build-configuration strategy — research

Status: research/design investigation only. No implementation. No ADR. This does not modify or
supersede `docs/multi-version-ef-strategy.md`; it re-examines one specific consequence of that
strategy (custom build-configuration names) now that
[dotnet/efcore#38951](https://github.com/dotnet/efcore/issues/38951) is a known blocker to EF11
NativeAOT publish. See `docs/internal/ef11-native-aot-implementation-plan.md` for the full EF11
NativeAOT enablement work this investigation supports.

## 1. Current architecture

Four named MSBuild configurations — `Debug EF10`, `Release EF10`, `Debug EF11`, `Release EF11` —
declared via `<Configurations>` on every project, with `TargetFramework` and (in the provider
project) `VersionPrefix` derived from `$(Configuration)` via conditional `PropertyGroup`s. Central
Package Management (`Directory.Packages.props`) conditions package versions (`EFCoreVersion`,
`Microsoft.EntityFrameworkCore.Tasks`, `Microsoft.Extensions.*`) on the same `$(Configuration)`
value (the `Microsoft.Extensions.*` groups already condition on `$(TargetFramework)` instead —
see §6 below, this matters). `Taskfile.yml`, CI (`.github/workflows/pr-build.yaml`), and the
release/publish pipeline all key off these same four names.

## 2. Original requirements and reasons (reconstructed from source, not assumed)

Traced to PR [#262](https://github.com/LayeredCraft/dynamodb-efcore-provider/pull/262)
("feat(ci): add EF10/EF11 multi-version build configuration support") and the strategy document it
introduced, `docs/multi-version-ef-strategy.md`. That document is itself explicit, worked-example
research (analyzing MongoDB's EF Core provider, which supports EF8/EF9/EF10 from one branch using
the same `Debug EFn`/`Release EFn` pattern with custom `#if EFn` `DefineConstants`) mapped onto
this repo's needs. Confirmed requirements, in the document's own words and this repo's actual
project files:

1. **Separate NuGet package version lines per EF major version, published as separate releases**
   (`docs/multi-version-ef-strategy.md` Part 5): "The EF10 configuration builds
   `EntityFrameworkCore.DynamoDb 10.x.x`; the EF11 configuration builds
   `EntityFrameworkCore.DynamoDb 11.x.x`... The major version in the package version IS the EF
   Core compatibility signal. This is the same convention used by EF Core itself, Npgsql, Pomelo,
   and MongoDB's own provider." This is the load-bearing requirement: NuGet packages have exactly
   one version per `.nupkg`; a single multi-targeted build with one `Version`/`VersionPrefix`
   cannot express "10.x.x for the net10.0 audience, 11.x.x for the net11.0 audience" — the
   provider's own `VersionPrefix` is set per-`$(TargetFramework)` specifically to make this work
   (`src/EntityFrameworkCore.DynamoDb/EntityFrameworkCore.DynamoDb.csproj:24-29`).
2. **Avoiding branch-per-version** (`docs/multi-version-ef-strategy.md` Part 4.1's comparison
   table): the alternative the team explicitly rejected was *not* ordinary multi-targeting — it
   was maintaining separate long-lived branches per EF major version (the older, still-common
   convention among some EF providers), which the doc rejects for cherry-pick pain, N-times code
   review, and merge-conflict risk. Custom configurations were chosen specifically to keep
   **one branch, one PR, one review** for changes affecting both EF lines.
3. **A 1:1 Configuration↔TargetFramework↔EF-version mapping was assumed sufficient**
   (`docs/multi-version-ef-strategy.md` §4.1): "The DynamoDB provider always has a 1:1 mapping
   (EF10 = net10.0, EF11 = net11.0)... No custom `DefineConstants` needed" — the SDK's own
   `NET10_0`/`NET11_0` symbols were judged sufficient for `#if` guards, unlike MongoDB's EF8/EF9
   case where two EF versions shared one TFM. This is confirmed still true today: no combination
   other than net10.0+EF10 or net11.0+EF11 exists anywhere in the codebase.
4. **CI matrix and release-tag-driven publishing**: `pr-build.yaml`'s `application-build` job
   already matrices over the four configurations; `publish-release.yaml` derives
   `buildConfiguration: auto` from the release tag prefix (`v10.*` → `Release EF10`, `v11.*` →
   `Release EF11`). This is a *consumer* of the configuration scheme, not an independent
   requirement — it could adapt to whatever the underlying mechanism is, provided the mechanism
   still lets CI select "build only the EF10 artifact" / "build only the EF11 artifact"
   independently.

No evidence was found that ordinary multi-targeting was tried and rejected for a *technical*
reason (restore conflicts, IDE confusion, etc.) — the strategy doc's own comparison table frames
the decision entirely against **branch-per-version**, not against **ordinary
`<TargetFrameworks>`**. This matters: the two rejected/accepted alternatives in the original
decision were "custom configs" vs. "separate branches," not "custom configs" vs. "standard
multi-targeting." The latter comparison appears not to have been evaluated at the time — plausibly
because the package-version-per-major-line requirement (§1 above) was assumed to require a
config-per-line lockstep with the package version, without separately checking whether the *TFM*
axis specifically needed to ride on `$(Configuration)` too, as opposed to just `VersionPrefix`.

## 3. How #38951 interacts with this

`docs/internal/ef11-native-aot-implementation-plan.md` has the full root-cause writeup.
Summary: EF's query-precompilation step (`DbContextOperations.PrecompileQueries`) reopens the
`.csproj` via `MSBuildWorkspace.Create(...)` without passing `$(Configuration)` (or any other
build property) through — an acknowledged upstream gap (`// TODO: pass through properties`,
unchanged in EF Core 10.0.12 and 11.0.0-rc.1.26425.128). Because every EF-version-specific
`TargetFramework`/`PackageReference` in this repo is gated on a **custom** `$(Configuration)`
value MSBuildWorkspace has no way to know, the re-evaluated project silently falls back to
whatever `Directory.Packages.props`'s *unconditional default* resolves to (currently the EF10
branch), producing a completely different, never-actually-built project graph for an EF11
build — hence total type-resolution failure during precompilation.

## 4. Alternatives investigated

### A. Current architecture (status quo)

**Benefits**: proven, in production use, matches the strategy doc's own worked rationale, requires
no migration, keeps `#if NET10_0`/`#if NET11_0` guards exactly as documented, gives CI/release
tooling an explicit, human-readable configuration name per artifact.

**Liabilities, now that NativeAOT/precompiled queries are in scope**: blocked on #38951 for the
`test:aot-publish CONFIG="Release EF11"` path specifically (confirmed: `test:aot-generation`,
ordinary build/test, and everything *not* going through `MSBuildWorkspace`-based query
precompilation work fine today under both EF10 and EF11 — this is a narrow, but blocking, gap).

**Supported repo-side configuration that could work around it without private target overrides**:
none found. `PrecompileQueries` accepts no external configuration/property injection point;
`_EFGenerationStage` is the only global property EF's own code passes, and it's hardcoded inside
`DbContextOperations.cs`, not settable from the consuming project. No environment variable, no
`.editorconfig`-style file, no `global.json` setting was found that influences
`MSBuildWorkspace.OpenProjectAsync`'s implicit default `Configuration`. This is consistent with
the "supported workaround: none found" conclusion already recorded when #38951 was filed.

### B. Standard TFM multi-targeting

```xml
<TargetFrameworks>net10.0;net11.0</TargetFrameworks>
```
with plain `Debug`/`Release` and `Directory.Packages.props` conditions switched from
`$(Configuration)` to `$(TargetFramework)`.

> **Superseded finding (see §10): this alternative does NOT currently unblock EF11 NativeAOT.**
> The two validation passes below were real and their results stand as described, but neither one
> exercised a project shape with **TFM-conditioned `PackageReference`s** (`Condition="'$(TargetFramework)'
> == 'net11.0'"`) — which is exactly what this repo's `Directory.Packages.props` needs, since EF10
> and EF11 are different major package versions. A follow-up pass with a minimal repro that *does*
> use TFM-conditioned package references (matching this repo's actual CPM shape) reproduces a
> distinct upstream defect in `Microsoft.EntityFrameworkCore.Tasks` itself, filed as
> [dotnet/efcore#38955](https://github.com/dotnet/efcore/issues/38955). See §10 for the full
> writeup. The synthetic/real-repo results below are left as originally recorded for the audit
> trail, but should be read with that caveat.

**Experimentally validated — twice, both fully passing:**

1. **Synthetic repro** (isolated, InMemory provider, no DynamoDB): `dotnet publish -f net11.0 -c
   Release` on a multi-targeted project completed the **entire** NativeAOT publish pipeline —
   `Optimizing DbContext...` step succeeded with zero errors, ILC ran, a native executable was
   produced. Repeated for `-f net10.0`: identical success. Neither had the `CS0234`/cascading
   `CS0246` failure that identical-shaped custom-`Configuration` projects reproduce reliably.
2. **Real repository validation** (this branch, `feat/ef11-nativeaot-compatibility`, changes made
   and then reverted after observing results): temporarily converted
   `src/EntityFrameworkCore.DynamoDb/EntityFrameworkCore.DynamoDb.csproj` and
   `testapps/EntityFrameworkCore.DynamoDb.NativeAotSmoke/EntityFrameworkCore.DynamoDb.NativeAotSmoke.csproj`
   from `<Configurations>` to `<TargetFrameworks>net10.0;net11.0</TargetFrameworks>`, and switched
   the two `$(Configuration)`-conditioned `Directory.Packages.props` groups (`EFCoreVersion`,
   `Microsoft.EntityFrameworkCore.Tasks`) to `$(TargetFramework)` conditions (mirroring the
   `Microsoft.Extensions.*` groups, which were **already** `$(TargetFramework)`-conditioned —
   this repo had already partially adopted this exact pattern for those packages). Ran
   `dotnet publish testapps/.../EntityFrameworkCore.DynamoDb.NativeAotSmoke.csproj --configuration
   Release --framework net11.0 --runtime osx-arm64`:
   - Restore succeeded.
   - `OptimizeDbContext` ran and succeeded — confirmed by inspecting the actual generated output:
     `obj/Release/net11.0/Program.EFInterceptors.SmokeContext.g.cs` contains **real, compiled
     query interceptors** (`Query1_Where1`, `PrecompiledQueryContext<TSource>`, etc.), not a
     skipped/no-op step.
   - `obj/Release/net11.0/SmokeItemEntityType.g.cs` references `DynamoTypeMapping<...>` 13 times —
     confirming this session's EF11 `DynamoTypeMapping<T>` architecture (§ of the implementation
     plan) also works correctly under this scheme, unmodified.
   - `dotnet publish` completed fully: a real, executable, ~30MB native binary was produced at the
     requested output path, plus a `.dSYM` bundle.
   - Repeated for `--framework net10.0`: identical clean success, ~29MB native binary.
   - **Not yet run to completion**: actually executing the published binary against DynamoDB Local
     (the full `run-nativeaot-smoke.sh` script) — the publish/precompile success (the actual
     blocked step) was confirmed directly; end-to-end execution was not re-validated in this pass
     since it reuses machinery already proven working in the EF11 implementation work.

   **Migration-completeness finding**: converting only these two projects and leaving the other six
   (`Tests`, `HealthChecks.Tests`, `IntegrationTests`, `AotTests`, `SpecificationTests`,
   `Example.Simple`) on the old `$(Configuration)`-based scheme, then running a **solution-wide**
   `dotnet restore -p:Configuration="Debug EF11"`, produced `NU1109` central-package-version
   downgrade conflicts in the untouched projects. This is an expected consequence of a
   **half**-migrated, inconsistent state (the untouched projects' `Configuration`-based EF10/EF11
   selection was now reading a `Directory.Packages.props` whose conditions had partly switched
   axes) — not a flaw in the TFM-based approach itself. It does mean: **a real migration must
   convert every project in the solution together, in one coordinated change**, not incrementally
   project-by-project, or CI/local solution-wide operations will break mid-migration. All
   experimental changes were reverted; the working tree matches the last commit exactly.

**Does it solve #38951 today?** **Yes, confirmed experimentally**, because
`MSBuildWorkspace.OpenProjectAsync`'s re-evaluation of a project's declared `<TargetFrameworks>`
is core, well-tested Roslyn/MSBuild-workspace behavior (this is how every IDE analyzes any
multi-targeted project) — unlike an arbitrary custom `$(Configuration)` value, which
`MSBuildWorkspace` has no special handling for and simply drops per the `// TODO: pass through
properties` gap. The precompilation re-open naturally lands on a real, already-restored TFM
regardless of which one the outer build/publish was actually targeting, because *both* were built
from the one multi-targeted project's own declared `TargetFrameworks` — there is no "wrong
default" to fall back to.

**Does it satisfy the original requirements?**
- Package-version-per-EF-line (§2.1): **preserved**. `VersionPrefix` is already
  `$(TargetFramework)`-conditioned in the provider project today, independent of `Configuration`;
  no change needed there. `dotnet pack -p:TargetFrameworks=net10.0 -p:Version=10.x.x` can still
  produce one single-TFM-targeted, single-version `.nupkg` per release, exactly as today's
  `dotnet pack --configuration "Release EF10"` does. **Correction from an earlier draft of this
  document**: `dotnet pack --framework net10.0` is **not** a valid switch —
  verified experimentally (scratch multi-targeted `net10.0;net11.0` project, real `dotnet pack`
  invocation): it fails outright with `MSB1001: Unknown switch. Switch: --framework`. `dotnet
  build`/`dotnet publish`/`dotnet test` accept `--framework`; `dotnet pack` does not. The correct,
  verified mechanism is the MSBuild property override `-p:TargetFrameworks=net10.0`, confirmed by
  `unzip -l` and nuspec inspection to produce a package containing **only** `lib/net10.0/*.dll` and
  **only** the net10.0-conditioned `PackageReference` set as its nuspec dependency group (repeated
  symmetrically for net11.0/EF11-rc.1 — no cross-contamination either direction). Release/publish
  CI would key off `-p:TargetFrameworks=net10.0`/`net11.0` instead of `--configuration "Release
  EF10"/"Release EF11"` — a mechanical substitution, not a loss of capability, but **not** the
  `--framework` flag as an earlier draft of this section assumed.
- Avoiding branch-per-version (§2.2): **preserved** — still one branch, one PR, TFM-conditioned
  code exactly as `#if NET10_0`/`#if NET11_0` today (this doesn't change at all; those symbols are
  already TFM-derived, not Configuration-derived).
- 1:1 mapping (§2.3): **unaffected** — still true, still enforced structurally by
  `<TargetFrameworks>net10.0;net11.0</TargetFrameworks>` rather than by four parallel
  `Configuration` names collapsing to two TFMs.
- CI/Taskfile (§2.4): needs updating (see §8, migration complexity) but nothing about the
  *capability* is lost — matrixing over `--framework net10.0`/`net11.0` with `-c
  Debug`/`Release` is at least as expressive as matrixing over four configuration strings, and is
  the more standard shape for tooling (IDEs, `dotnet` CLI flags, MSBuild target framework
  inference) to understand natively.

**New consideration this alternative introduces**: ordinary multi-targeted `dotnet build`/`dotnet
test` (no `--framework` filter) builds **both** TFMs by default — CI/Taskfile commands that
currently build "just EF10" via `--configuration "Debug EF10"` would need an explicit
`--framework net10.0` filter to preserve that single-version-at-a-time behavior locally; this is a
one-line addition to each Taskfile target, not a structural problem.

### C. Dedicated EF version property (e.g. `EfCoreVersion=10`/`11` with standard `Debug`/`Release`)

**Would it solve #38951?** **No — ruled out without needing to implement it.** The mechanism of
the defect (`MSBuildWorkspace.Create(new Dictionary<string,string>{["_EFGenerationStage"]="build"})`
passing *no* other global properties) means **any** externally-set MSBuild global property —
`Configuration`, a custom `EfCoreVersion`, anything — is equally lost on re-evaluation. This
alternative only relocates which property carries the version signal; it does not change *how*
`MSBuildWorkspace` discovers it. Confirmed by inspecting the exact same source location already
used to diagnose #38951 — there is no special-casing for `Configuration` there; the loss is
total and property-name-agnostic. **Do not pursue this as an #38951 mitigation.** It remains a
theoretically cleaner separation of "build flavor" (Debug/Release) from "EF line" than the current
scheme, but it does not solve the blocking problem and was not validated further given that.

### D. Separate project files sharing source (`EntityFrameworkCore.DynamoDb.EF10.csproj` /
`...EF11.csproj`)

Not experimentally validated (ruled out on architectural grounds before reaching implementation,
consistent with "prefer boring standard behavior" and avoiding duplicated project metadata).
Each project would independently have a single, real `<TargetFramework>` and no custom
`Configuration` axis, which — by the same reasoning as Alternative B — would *also* avoid #38951
(MSBuildWorkspace has no special-case needed at all when there's only one, real, standard TFM per
project). So this **would** solve the blocker too, structurally for free. But:

- Requires either (a) `<Compile Include="../EntityFrameworkCore.DynamoDb/**/*.cs" />`-style
  glob-shared source across two independently-versioned `.csproj` files (duplicated
  `PackageReference`/`ItemGroup` metadata, two `AssemblyInfo`s, two sets of analyzer/warning
  configuration to keep in sync by hand), or (b) a shared `.props`/`.targets` include file to
  de-duplicate the metadata — which is most of the way to reinventing what
  `<TargetFrameworks>` already gives for free in one file.
- Doubles the number of project files needing maintenance for every future structural change
  (new analyzer, new `LangVersion`, new source file needing explicit inclusion if not using a
  wildcard glob).
- IDE experience is worse: most IDEs (Rider, VS) present multi-targeted single projects with a
  TFM switcher naturally; two separate `.csproj` files for "the same library" is a less standard,
  less discoverable shape, and doubles Solution Explorer/project-list clutter.
- No clear advantage over Alternative B was found that would justify this extra maintenance
  burden, given B already satisfies every requirement in §2 and independently solves #38951.

**Verdict**: technically viable, not recommended — Alternative B achieves the same #38951-avoidance
with substantially less structural duplication.

### E. Prior art from other multi-EF-version providers

`docs/multi-version-ef-strategy.md` (this repo's own prior research, Part 1) already documents
MongoDB's EF Core provider using the same custom-configuration-plus-`DefineConstants` pattern this
repo adapted. To the extent that pattern is followed by other providers needing to support
overlapping EF major versions from one branch, they would carry the **same latent exposure** to
#38951 the moment they add NativeAOT/precompiled-query support — this is not unique to this
provider or repo. The more traditional convention among mature third-party EF providers (Npgsql,
Pomelo, historically) has been **separate release branches per EF major version**, which
structurally avoids this entire class of problem (each branch's projects have a single, real,
unconditional TFM — falling under the same "no special-casing needed" reasoning as Alternatives B
and D) at the cost of exactly the cherry-pick/branch-maintenance pain this repo's strategy doc
already weighed and rejected. No further public research into other providers' exact build files
was performed for this pass (would require deeper external repository inspection than time
allowed); the structural reasoning above (single real TFM per project avoids the defect
category entirely, regardless of *which* specific pattern — separate branches, separate projects,
or multi-targeting — achieves it) is judged sufficient without it.

## 5. Summary: which alternatives avoid #38951 today

| Alternative | Avoids #38951 | Unblocks EF11 NativeAOT overall | Confirmed how |
|---|---|---|---|
| A. Current (custom `Configuration`) | No | No (#38951) | Reproduced twice, including real end-to-end with a minimal repro filed upstream |
| B. Standard `<TargetFrameworks>` multi-targeting | **Yes** | **No — blocked by a different upstream defect** (#38955, see §10) | #38951 avoided (experimentally validated); a *new* defect found and reproduced for the TFM-conditioned-`PackageReference` shape this repo actually needs |
| C. Dedicated `EfCoreVersion` property, standard `Configuration` | No | No (#38951) | Ruled out by source inspection — property-loss is general, not `Configuration`-specific |
| D. Separate project files per EF line | Yes (structurally) | **Yes** — each file is single-targeted, so neither #38951 nor #38955 applies | Not implemented; reasoning only |
| E. Separate branches (traditional provider convention) | Yes (structurally) | **Yes** — each branch is single-targeted | Not this repo's model; noted as prior art only |

**Note**: #38955 is specific to genuinely multi-targeted (`<TargetFrameworks>` plural) projects. D and
E don't multi-target at all (each variant is a single-`<TargetFramework>` project), so neither
defect applies to them — though both were already set aside in §4 for reintroducing the
branch/file-duplication pain the current architecture was built to avoid, independent of NativeAOT.

## 6. Migration complexity if B is adopted

- `Directory.Packages.props`: two remaining `$(Configuration)`-conditioned groups
  (`Microsoft.EntityFrameworkCore.Tasks`, `EFCoreVersion` and its dependents `Design`,
  `Microsoft.EntityFrameworkCore`, `.Relational`, `.Specification.Tests`) need to switch to
  `$(TargetFramework)`. The `Microsoft.Extensions.*` groups need **no change** — they're already
  `$(TargetFramework)`-conditioned, evidence this repo had already implicitly started down this
  path in one place.
- Every `.csproj` currently declaring `<Configurations>Debug EF10;Release EF10;Debug
  EF11;Release EF11</Configurations>` plus the four TFM-selecting `PropertyGroup`s needs to
  collapse to a single `<TargetFrameworks>net10.0;net11.0</TargetFrameworks>` plus removal of
  those `PropertyGroup`s. Confirmed by inspection: this pattern is repeated across **all** src and
  test projects (provider, `Tests`, `HealthChecks.Tests`, `IntegrationTests`, `AotTests`,
  `SpecificationTests`, the smoke app, `Example.Simple`) — roughly 8 project files.
- **Must be done as one coordinated change**, not incrementally — confirmed experimentally (§4.B)
  that a mixed state breaks solution-wide restore via CPM downgrade conflicts.
- `Taskfile.yml`: every `task build/test/pack` target keyed on `CONFIG="Debug EF10"` etc. needs to
  become `--framework net10.0 --configuration Debug` (or equivalent); `build:ef11:ci-sdk`'s SDK
  install/build logic is unaffected (still installs a specific SDK channel, just invokes it with
  different flags).
- `.github/workflows/pr-build.yaml`: the `native-aot`/`aot-generation` jobs run raw `dotnet`
  commands directly (not via the reusable workflow) and are trivially convertible to
  `--framework`-based invocation. The `application-build` job, and `publish-release.yaml` /
  `publish-preview.yaml` in full, delegate to reusable workflows hosted in the separate
  `LayeredCraft/devops-templates` repo — see §9 below; this is a materially larger piece of the
  migration than a same-repo YAML edit.
- Release-drafter configs (`release-drafter-ef10.yml`/`-ef11.yml`) are unaffected — they're keyed
  on release tag prefix, not build configuration name.
- `#if NET10_0`/`#if NET11_0` guards throughout the provider source: **zero changes needed** —
  these were already TFM-derived, not Configuration-derived, per the original strategy doc.
- `docs/multi-version-ef-strategy.md`: would need a substantial rewrite/addendum — its Quick Start
  table, all `--configuration "Debug EF11"` examples, and Part 4.1's comparison table's "why not
  multi-targeting" gap would need addressing directly (this document, if the decision proceeds,
  is the natural place to fold in that update — not done in this pass, per instructions).

## 7. Risks

- CI/Taskfile churn is mechanical but touches many files at once — real risk of a transient CI
  break during the migration PR if any target is missed; mitigated by doing it as a single
  coordinated PR with full CI validation before merge, exactly as the "must convert everything
  together" finding already implies.
- `dotnet build`/`dotnet test` with no `--framework` filter now builds/tests **both** TFMs by
  default where today's `dotnet build --configuration "Debug EF10"` builds exactly one — anyone
  running raw (non-Taskfile) commands without a `--framework` flag gets a different (broader, not
  narrower) default than today. Not unsafe, but a behavior change worth calling out in the
  migration PR description.
- Local/IDE muscle memory: contributors used to typing `Debug EF10` as a configuration name in
  Rider/VS's configuration dropdown would need to adjust to `Debug` + a separate TFM selector —
  minor, but real onboarding friction for existing contributors.
- This alternative was validated against the **precompile/publish** step specifically (the actual
  blocker) and against ordinary restore/build for the two converted projects; it was **not**
  re-validated against the full existing test suite, `Specification.Tests`, or the full CI matrix
  in its converted form, since that would require the full coordinated migration this research
  explicitly stops short of (per instructions: research only, no implementation).

## 8. Recommendation (revised again — see §11, current)

**§10 found Alternative B alone (plain `<TargetFrameworks>net10.0;net11.0</TargetFrameworks>`)
does not unblock EF11 NativeAOT** (hits #38955). **§11 found a refinement — standard
multi-targeting plus a physical, generated single-TFM MSBuild props override for AOT
publish/pack — that avoids *both* #38951 and #38955 and was validated real end-to-end for both
EF10 and EF11**, including actual DynamoDB Local query execution. This is now the recommended
direction; see §11 for the full mechanism, evidence, and remaining open questions before treating
it as a final decision (this is still research, not an implemented or committed architecture).

The parts of this research that didn't depend on NativeAOT working were already independently
verified and remain valid regardless of which exact variant is adopted:

- Full solution restore/build/test is clean on both `net10.0` and `net11.0` under standard
  multi-targeting — 1744 unit tests, 0 failures across both TFMs, including `AotTests` and
  `HealthChecks.Tests`.
- `dotnet pack -p:TargetFrameworks=<tfm>` (not `--framework`, which `dotnet pack` doesn't support)
  correctly produces an isolated, single-TFM, single-version package with no cross-TFM
  contamination — and with the §11 props-override mechanism, plain `dotnet pack` (no property
  override needed at all) does the same.
- CPM, the `.slnx`, `Taskfile.yml`, and `scripts/run-nativeaot-smoke.sh` all convert mechanically
  and cleanly.

The migration would still be a two-repo change (this repo plus an additive `devops-templates`
PR — see §9) with real CI/Taskfile/`.slnx` churn. That cost is now justified by §11's finding that
it actually unblocks EF11 NativeAOT, rather than merely being architecturally tidier.


## 9. Follow-up audit: CI/release automation and devops-templates coupling

A second research pass (still research-only; no implementation) audited every workflow file,
`Taskfile.yml`, invoked scripts, and this repo's dependency on the external
`LayeredCraft/devops-templates` reusable-workflow repo, to check for hidden reasons Alternative B
would not work in practice.

**Workflows reviewed** (all 8): `pr-build.yaml`, `publish-release.yaml`, `publish-preview.yaml`,
`required-build-check.yaml`, `release-drafter.yaml`, `docs.yaml`, `dependabot-auto-merge.yml`,
`pr-title-check.yaml`.

- `required-build-check.yaml`, `docs.yaml`, `dependabot-auto-merge.yml`, `pr-title-check.yaml`:
  no `Configuration`/TFM coupling — **no change needed** under B.
- `release-drafter.yaml`: matrices over `release-drafter-ef10.yml`/`-ef11.yml` config *files*,
  keyed on release-tag prefix, not build `Configuration` — **no change needed**.
- `pr-build.yaml`: `native-aot` and `aot-generation` jobs invoke `dotnet`/scripts directly with a
  `configuration` matrix value (`"Debug EF10"`/`"Debug EF11"`) — same-repo change, straightforward.
  The `application-build` job instead sets `buildConfiguration: ${{ matrix.configuration }}` and
  delegates to `LayeredCraft/devops-templates/.github/workflows/pr-build.yaml@<pinned-sha>`.
- `publish-release.yaml` and `publish-preview.yaml`: delegate **entirely** to devops-templates
  reusable workflows (`publish-release.yml`, `publish-preview.yml`), passing only
  `buildConfiguration`.

**devops-templates coupling** (inspected directly at
`/Users/ncipollina/source/repos/layered-craft/devops-templates`, current `main` @ `07d4436`, which
contains the SHA this repo pins): none of the three reusable workflows this repo consumes
(`pr-build.yaml`, `publish-release.yml`, `publish-preview.yml`) have any `--framework`/TFM-shaped
input today — every `dotnet restore`/`build`/`test`/`pack`/`publish` invocation in them is keyed
**only** on `--configuration "${{ inputs.buildConfiguration }}"` (confirmed by direct grep of all
three files). `publish-release.yml`'s `resolve` job additionally hardcodes the naming convention
itself:

```bash
if [ "${{ inputs.buildConfiguration }}" = "auto" ]; then
  MAJOR="${VERSION%%.*}"
  echo "configuration=Release EF${MAJOR}" >> "$GITHUB_OUTPUT"
```

i.e. it derives the literal string `"Release EF10"`/`"Release EF11"` from the release tag's major
version and assumes that string is a valid `$(Configuration)` value. This is load-bearing
production automation, not incidental.

**Blast radius**: `devops-templates` is shared, not private to this repo. It is consumed by at
least `sharp-mud`, `decoweaver`, `lambda-aspnetcore-hosting-extensions`,
`aws-secrets-manager-provider`, `compono`, and this repo, all pinned to workflow-file paths in that
repo (mix of SHA- and path-pinned references depending on repo). **Any devops-templates change
must be additive** (e.g. a new optional `targetFramework`/`packFrameworks`-style input, defaulting
to today's single-`buildConfiguration` behavior) so the other consumers — none of which use a
multi-TFM or non-default-`Configuration` scheme — are unaffected. This is a normal, low-risk kind
of reusable-workflow change (adding an optional input with a backward-compatible default), not a
breaking one, but it is still a **second, separate repository and PR**, reviewed and merged
independently of this repo's migration PR, and this repo's migration cannot fully land in CI until
that companion PR merges.

**`dotnet pack` verification** (experimental, per audit requirement — see the correction in §4.B
above): confirmed `dotnet pack --framework <tfm>` does not exist as a switch;
`-p:TargetFrameworks=<tfm>` is the correct, verified mechanism, and produces packages with fully
isolated per-TFM assets and dependencies. `publish-preview.yml`'s current `dotnet pack ...
--no-build -o artifacts /p:Version=...` step (no TFM filter) would need
`-p:TargetFrameworks=$targetFramework` added — one more concrete line-item for the
devops-templates PR.

**Central Package Management**: converting all `~8` project files simultaneously (provider, 5 test
projects, the smoke app, `Example.Simple`) to `<TargetFrameworks>net10.0;net11.0</TargetFrameworks>`
was re-checked against the full project inventory (`grep` over every `.csproj`) — confirmed all
share the identical `<Configurations>Debug EF10;Release EF10;Debug EF11;Release
EF11</Configurations>` + 4-way `PropertyGroup` pattern, so the same mechanical edit applies
uniformly; no project has a divergent shape that would need special-case handling.

**Taskfile/scripts**: `Taskfile.yml` confirmed to parameterize `CONFIG="Debug EF10"`-style strings
across `restore`, `build`, `build:ef10`, `build:ef11`, `build:ef11:ci-sdk`, `build:all`, `test`,
`test:ef10`, `test:ef11`, `test:unit`, `test:aot-generation`, `test:aot-publish`. One discrepancy
noted, **unrelated to this migration**: root `CLAUDE.md`/`AGENTS.md` documents `task pack:ef10`/
`task pack:ef11` as existing commands, but no `pack:*` task exists in the current `Taskfile.yml` —
this looks like pre-existing documentation drift, not something introduced by or blocking this
research; worth a separate, small fix regardless of the TFM-migration decision.
`scripts/run-nativeaot-smoke.sh` takes `CONFIG` as its first positional argument and passes it
straight through to `-p:Configuration=`/`--configuration`; under B it would take a `--framework`
argument instead — a small, mechanical script change.

**IDE/solution (`.slnx`)**: `EntityFrameworkCore.DynamoDb.slnx` declares the same 4 `BuildType`
names (`Debug EF10`, `Release EF10`, `Debug EF11`, `Release EF11`) at the solution level and maps
every project's `ProjectConfiguration` to them explicitly, once per project (provider,
`Example.Simple`, all 5 test projects — the smoke app is not currently in the `.slnx`). Under B,
this entire block collapses to the standard 2 solution `BuildType`s (`Debug`/`Release`) with
implicit project-configuration mapping (no explicit `<Configurations>` block needed per project,
since `<TargetFrameworks>` handles the EF-line axis instead) — a simplification, not added
complexity, for both the file and the Rider/VS configuration dropdown.

**Challenge to the recommendation**: the devops-templates coupling is a genuine, previously
under-scoped cost — the original recommendation understated migration scope as "a single
coordinated migration PR." It is not, however, a **meaningful problem** with the underlying
technical claim: the MSBuild/TFM mechanics proven in §4.B (restore, build, publish, NativeAOT
precompilation, and now `pack`) are unaffected by which repo houses the CI YAML that invokes them,
and the required devops-templates change is additive and low-risk, not a redesign of that repo.
~~**The recommendation stands: Alternative B**, revised only in scope — see §8.~~ **Superseded by
§10**: a later validation pass found Alternative B doesn't actually unblock EF11 NativeAOT (a
different upstream defect, #38955). The devops-templates scope finding above is still accurate and
still relevant if this decision is revisited later — it just isn't the reason to hold off anymore.

## 10. Later finding: Alternative B does not unblock EF11 NativeAOT (#38955)

A further validation pass — done specifically to test the full, real migration (all ~8 project
files, `Directory.Packages.props`, `.slnx`, `Taskfile.yml`,
`scripts/run-nativeaot-smoke.sh`) rather than the two-project partial conversion in §4.B — found
that EF11 NativeAOT publish still fails after the migration, with a completely different symptom
from #38951.

**What was actually done**: converted all 8 project files to
`<TargetFrameworks>net10.0;net11.0</TargetFrameworks>`, switched `Directory.Packages.props`'
remaining `$(Configuration)`-conditioned groups to `$(TargetFramework)`, collapsed the `.slnx` to
standard `Debug`/`Release`, and updated `Taskfile.yml`/`scripts/run-nativeaot-smoke.sh` to use
`--framework`. Full solution restore/build/test passed cleanly on both TFMs (1744 tests, 0
failures). `scripts/run-nativeaot-smoke.sh` for **EF10/net10.0 passed completely**, real
end-to-end, including DynamoDB Local execution of all 8 smoke queries. The same script for
**EF11/net11.0 failed** during the `Microsoft.EntityFrameworkCore.Tasks` package's own
`OptimizeDbContext`/"Optimizing DbContext..." step, with `CS0234`/`CS0246` errors indicating the
project had **no package references at all** during that step — not the #38951 symptom.

**Root cause, confirmed with a minimal, DynamoDB-independent repro** (a two-file console app,
`<TargetFrameworks>net10.0;net11.0</TargetFrameworks>`, TFM-conditioned `PackageReference`s,
`Microsoft.EntityFrameworkCore.Tasks` referenced only under `net11.0`) and a `-v:diag` MSBuild log:
`Microsoft.EntityFrameworkCore.Tasks.targets`' `_EFGenerateFiles` target does an internal
`<MSBuild Targets="Build" Properties="Configuration=...;Platform=...;PublishAot=false;...">`
re-invocation that never includes `$(TargetFramework)`. The diagnostic log shows the re-invoked
sub-build computing `_TargetFramework` as **both** `net10.0` and `net11.0` (the "no TFM selected,
decide all frameworks" cross-targeting-orchestrator shape) with no `$(TargetFramework)` ever bound
to a single value anywhere in that sub-build. Because this repo's TFM-conditioned
`PackageReference` `ItemGroup`s (`Condition="'$(TargetFramework)' == 'net11.0'"` — required because
EF10 and EF11 are different major package versions) are evaluated once at project load against an
empty `$(TargetFramework)`, they never activate, so the re-invoked sub-build compiles with zero
package references.

A follow-up diagnostic experiment — patching a local copy of the installed `.targets` file to add
`TargetFramework=$(TargetFramework)` to the internal property list — did **not** fix it: a second
diagnostic log showed `$(TargetFramework)` is already empty at the point that property list is
constructed (inside `_EFGenerateFilesBeforePublish`, hooked via
`AfterTargets="GetCopyToPublishDirectoryItems"`/`BeforeTargets="GeneratePublishDependencyFile"`),
not merely dropped later by the `<MSBuild Properties="...">` call. The defect is deeper than a
single missing property. No workaround was applied to this repo or proposed to `devops-templates`.

**Why the earlier §4.B validation didn't catch this**: neither the synthetic InMemory repro nor the
two-project real-repo conversion in §4.B used TFM-conditioned `PackageReference`s — both had the
same package set available regardless of `$(TargetFramework)`'s value, so the empty-`$(TargetFramework)`
sub-build accidentally still found its references and the defect never manifested. This repo's
actual requirement (different EF Core major versions per TFM, via CPM `$(TargetFramework)`
conditions) is exactly the shape that triggers it.

**Filed upstream**: [dotnet/efcore#38955](https://github.com/dotnet/efcore/issues/38955), with the
minimal repro, exact `-v:diag` evidence, the targets-file excerpts, and the negative result of the
diagnostic property-addition experiment. Explicitly distinguished from #38951 (a different
mechanism — `MSBuildWorkspace` losing `$(Configuration)` on project reopen — that only matters
after a project already has a resolved `$(TargetFramework)`; #38955 happens earlier and blocks
multi-targeted projects regardless of whether `$(Configuration)` is custom or standard).

**Net effect on the recommendation at the time**: EF11 NativeAOT was blocked either way — by
#38951 on the current architecture, by #38955 on Alternative B. See §11 for the refinement that
changes this.

## 11. Physical single-TFM props-override refinement — avoids both #38951 and #38955

A further pass tested a specific refinement of Alternative B: keep ordinary
`<TargetFrameworks>net10.0;net11.0</TargetFrameworks>` checked in for normal development, but for
AOT publish/pack, generate a small physical MSBuild `.props` file that forces the project to
evaluate as genuinely single-targeted — **without** passing `-p:TargetFramework=...` as an external
property (already known to be lost by both #38951's and #38955's internal reopen/re-invocation
mechanisms).

**Mechanism**: repo-root `Directory.Build.props` conditionally imports an untracked
`AotOverride.props` if it exists:
```xml
<Import Project="$(MSBuildThisFileDirectory)AotOverride.props"
        Condition="Exists('$(MSBuildThisFileDirectory)AotOverride.props')" />
```
Each `.csproj` declares its default multi-targeting conditionally, so the override (if already
applied earlier in evaluation, via `Directory.Build.props`) takes precedence:
```xml
<PropertyGroup Condition="'$(TargetFramework)' == '' and '$(TargetFrameworks)' == ''">
  <TargetFrameworks>net10.0;net11.0</TargetFrameworks>
</PropertyGroup>
```
`AotOverride.props` itself is a two-line generated file:
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net11.0</TargetFramework>
    <TargetFrameworks></TargetFrameworks>
  </PropertyGroup>
</Project>
```
(`net10.0` for the EF10 variant.) Because `Directory.Build.props` is auto-imported very early in
project evaluation — before the SDK decides single- vs. cross-targeting — this works **regardless
of what external properties any later reopen/re-invocation does or doesn't pass**, since the
override isn't an external property at all; it's baked into the project's own import graph and is
re-read on every fresh evaluation, including ones EF triggers internally with zero external
properties.

**Proof (minimal repro, no external `-p:TargetFramework`)**:
- No override file: `TargetFramework` empty, `TargetFrameworks` = `net10.0;net11.0` (ordinary
  multi-targeting, confirmed via `-getProperty`).
- `AotOverride.props` present (net11.0): `TargetFramework` = `net11.0`, `TargetFrameworks` = empty
  — confirmed the same way, with zero external properties passed.
- Same for net10.0.
- Removing the file restores ordinary multi-targeting immediately, with no persistent state.

**Proof against #38955's specific mechanism**: a `-v:diag` log of a real EF11 NativeAOT publish
with the override in place shows the exact same internal `<MSBuild Targets="Build"
Properties="Configuration=...;PublishAot=false;...">` re-invocation that #38955 is about (no
`TargetFramework` in its `Properties=`/`Global Properties:` — identical to the failing case) — but
this time the re-invoked sub-build resolves `net11.0` correctly throughout, with **no** `_TargetFramework=net10.0;net11.0` outer-cross-targeting item ever appearing. `Optimizing DbContext...`
succeeds with zero `CS0246`/`CS0234` errors, produces a real interceptor file
(`Program.EFInterceptors.BlogContext.g.cs`), and the resulting NativeAOT binary runs a real LINQ
query correctly.

**Proof against #38951's specific mechanism**: the same publish's "Query precompilation is an
experimental feature" log line confirms the query-precompilation step (the one that internally uses
`MSBuildWorkspace.Create()` with zero external properties, per §3) engaged and **succeeded** — no
`Amazon`/`Microsoft.EntityFrameworkCore` unresolved errors. This works for the identical reason as
#38955: the `MSBuildWorkspace` reopen doesn't need to receive `Configuration`/`TargetFramework` as
external properties, because whatever project state it reopens still re-imports
`Directory.Build.props` → `AotOverride.props`, so the correct TFM/package set is available
regardless of what properties that reopen carries (or fails to carry).

**Real provider validation** (temporarily applied to all ~8 project files, `Directory.Packages.props`
switched to `$(TargetFramework)` conditions, `.slnx` collapsed to `Debug`/`Release`,
`examples/Directory.Build.props` given its own override import since it doesn't chain-import the
repo-root file today — all reverted afterward, nothing committed):

- No override: full solution restore/build/test green on both `net10.0` and `net11.0` (871/873
  unit tests respectively, 0 failures) — ordinary multi-targeting fully intact.
- EF11 override: `scripts/run-nativeaot-smoke.sh` produced a real ~30MB native binary; a real
  `Microsoft.EntityFrameworkCore.Tasks.Tasks(105,5)` "Optimizing DbContext..." step succeeded with
  correct `DynamoTypeMapping<T>` compiled-model output and real generated query interceptors; the
  AOT warning baseline matched exactly (`IL2026 IL2055 IL2060 IL2067 IL2072 IL2075 IL2091 IL2104
  IL3050 IL3053`, no new IDs); running the binary against a real `amazon/dynamodb-local:3.3.0`
  container executed all 8 existing smoke scenarios successfully (generated async query,
  unconverted/converted enum projections, numeric parameter query, composite key predicate, null
  propagation, `SaveChanges` write/read-back).
- EF10 override: identical mechanism, full script run including DynamoDB Local, all 8 scenarios
  passed, exit 0.
- Packaging: `dotnet pack` with the EF10 override (no `-p:TargetFrameworks=` needed at all — the
  override already scopes it) produced a `.nupkg` with only `lib/net10.0/*` and only the net10.0 EF
  dependency group; the EF11 override produced only `lib/net11.0/*` and the net11.0 EF11-RC1
  dependency group. No cross-TFM contamination either direction.

**Known environment caveat, not an architecture defect**: on this development machine (macOS,
Xcode Command Line Tools only, no full Xcode), `net11.0`'s ILCompiler build reproducibly triggers a
non-fatal `dsymutil`/clang module-cache warning during native linking that MSBuild's publish
pipeline surfaces as a non-zero process exit code, even though the native binary is produced
correctly and runs correctly (verified by running the binary directly against DynamoDB Local,
bypassing the smoke script's strict exit-code gate, with full success). `net10.0`'s ILCompiler did
not reproduce this on the same machine in the same session. This is a local macOS
toolchain/Xcode-installation-version pairing issue orthogonal to the props-override mechanism —
worth checking in whatever CI runner is eventually used (likely a non-issue there, since CI images
typically have appropriate toolchains installed), but not investigated further here since it's not
architecture-related.

**Interaction with #38951/#38955 summary**: both are avoided for the same underlying reason —
neither defect is actually about "losing properties" being *fixable* from outside; it's about the
correct TFM/config not being *derivable* from outside in the first place for the reopened/
re-invoked project instance. The props-override sidesteps this by making the correct state a
property of the project's own file-system-anchored import graph rather than of any external
property flow, so it doesn't matter that EF's internal reopens carry zero (or wrong) properties.

**Not yet done at the time**: exact CI wiring, and a deliberate side-by-side judgment call against
keeping separate `.csproj` files per EF line (still not investigated — explicitly out of scope
throughout). §12 now settles the CI wiring question.

## 12. Final CI/CD design (research/planning pass — not yet implemented)

A follow-up planning pass (no code changes) designed the concrete CI/release shape for the
props-override architecture, and settled one open question from §9: **`devops-templates` is not
used for either publish workflow.** The NativeAOT-aware, EF-major-aware release logic (override
generation, single-TFM pack, real NativeAOT smoke validation before publish) is this repo's own
concern, not something to generalize into the shared reusable-workflow repo. This reverses §9's
"additive `targetFramework` input" direction for *publish* specifically — that prototype (pushed,
unmerged, on `devops-templates` branch `feat/reusable-workflow-target-framework-support`) is no
longer needed for this repo and can be left alone (not merged, not deleted).

**Override file**: renamed from the experimental `AotOverride.props` to **`TargetFrameworkOverride.props`**
— it now scopes restore/build/test/pack/publish for a release, not only AOT publish. Same
mechanism as validated in §11 (conditional import from `Directory.Build.props`, gitignored,
generated and deleted by CI/local scripts, never committed).

**Helper scripts** (dumb, single-purpose, no EF/NuGet/release knowledge):
- `scripts/write-target-framework-override.sh <tfm>` — writes the file for the given TFM.
- `scripts/write-target-framework-override.sh --clear` — removes it.

**PR CI** (`pr-build.yaml`): ordinary build/test needs no override at all — a plain multi-targeted
`dotnet build`/`dotnet test` already validates both `net10.0` and `net11.0` in one pass, so
`application-build` can keep delegating to `devops-templates` unchanged, with `buildConfiguration:
Debug` (no more per-EF matrix, no devops-templates edit needed). The `native-aot` job becomes a
2-leg `fail-fast: false` matrix (`net10.0`, `net11.0`) — EF11 is finally exercisable here, not just
EF10 — each leg: write override → `scripts/run-nativeaot-smoke.sh` → clear override
(`if: always()`). The `aot-generation` job needs no override either — it doesn't trigger EF Tasks'
publish-time generation path at all (confirmed: no `EFScaffoldModelStage`/`EFPrecompileQueriesStage`
are set anywhere in this repo, so `_EFGenerateFilesAfterBuild`/`_EFGenerateFilesBeforePublish` never
fire for it); it already works today with a plain `--framework` filter.

**`publish-release.yaml` and `publish-preview.yaml`**: brought fully local (no `uses:
LayeredCraft/devops-templates/.github/workflows/publish-*.yml`). Both keep the *composite*
`LayeredCraft/devops-templates/.github/actions/nuget-push` action for the actual NuGet OIDC
push step — that action is generic, version-agnostic, already runs inline in the caller's job
(required for NuGet Trusted Publishing's `job_workflow_ref` check), and isn't provider-specific, so
it stays. Release: tag major version (`v10.*`/`v11.*`) selects `net10.0`/`net11.0` (replacing the
old `Release EF10`/`Release EF11` → `configuration` derivation with a `target_framework`
derivation, same `resolve` job shape). Preview: matrix over `{targetFramework, dotnetVersion,
drafterConfig, artifact_name}` replaces the old `{configuration, ...}` matrix — release-drafter
dry-run version resolution is unchanged. Both flows insert real NativeAOT smoke validation
(`scripts/run-nativeaot-smoke.sh`) between build/test and pack, so a NativeAOT regression blocks
packing and publishing — the current devops-templates-delegated flow has no such gate today, this
is a new correctness improvement, not parity.

**Release Drafter is unaffected — confirmed, not assumed.** `release-drafter-ef10.yml`/
`-ef11.yml` are keyed purely on `filter-by-range` (a semver range against the tag history) and
`config-name`; `release-drafter.yaml`'s own matrix is keyed on those same config file names. Neither
references `$(Configuration)`/`$(TargetFramework)`/any MSBuild property anywhere. No changes needed
or made.

**Package isolation**: unchanged from §11's finding — `dotnet pack` with the override present needs
no `-p:TargetFrameworks=` override at all; the physical file already scopes it, verified to produce
single-TFM, single-dependency-group packages with no cross-contamination.

**Known environment note carried forward**: the dsymutil/clang module-cache flakiness observed for
`net11.0` in §11 was reproduced only on a local macOS (Xcode Command Line Tools only) machine; the
actual CI runners for this repo (`ubuntu-latest`) don't have that toolchain at all, so it's not
expected to recur there — not verified in this pass, flagged for confirmation once implemented.
