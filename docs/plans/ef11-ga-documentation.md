# EF11 GA — GA documentation and dual-version release preparation (PR C) — implementation plan

Status: plan, awaiting maintainer approval. Implementation is a separate step.

Beads: epic `dynamodb-efcore-provider-ar2` · this PR `dynamodb-efcore-provider-ar2.3` · release gate
`dynamodb-efcore-provider-ar2.4` (publish **v10.2.0 and v11.0.0**) · post-GA cleanup
`dynamodb-efcore-provider-ar2.5` (PR D) · not-a-blocker follow-up `dynamodb-efcore-provider-ar2.6`
(complex-type discriminator null semantics).

> **For agentic workers:** execute the tasks in §7 in order. Each task ends with a commit. This is
> a documentation-and-skill PR: do not change provider code, tests, dependencies, `zensical.toml`,
> Taskfile, or `.github/`. Where this plan gives replacement text, use it verbatim unless it
> contradicts the repository (then stop and report). Recommended options are the defaults unless
> the maintainer recorded a different choice in §13.

## Global constraints

- Two stable lines ship from the same `main`: **10.x** (`net10.0`, EF Core 10) and **11.x**
    (`net11.0`, EF Core 11). Never imply one package works across EF Core majors.
- Each published package is single-target: `publish-release.yaml` writes a single-TFM override from
    the tag major, so `10.*` packs only `lib/net10.0` and `11.*` packs only `lib/net11.0`.
- Verified dependency ranges (`Directory.Packages.props` on `main` @ `e84829a`):
    - `net10.0`: `Microsoft.EntityFrameworkCore` `[10.0.14, 11.0.0)`.
    - `net11.0`: `Microsoft.EntityFrameworkCore` `[11.0.0, 12.0.0)`.
- `README.md` is packed into **both** NuGet packages (`Directory.Build.props` `PackageReadmeFile`),
    so it must be correct for a reader on either line.
- User docs (`docs/` pages in the nav, `README.md`) must not reference internal code, tests, beads,
    Taskfile tasks, or repo scripts (`AGENTS.md` docs rule).
- Do not claim universal NativeAOT compatibility. Validation is linux-x64 only.
- Do not remove historical content (`docs/alpha-exit-priorities.md`, `docs/multi-version-ef-strategy.md`,
    `docs/internal/**`, `docs/plans/**`, GitHub releases/tags).
- Do not edit `.agents/skills/` or `.claude/skills/` (symlinks); edit `skills/<name>/` only.
- Do not implement `ar2.6` (complex-type discriminator persistence / null semantics).

## 1. Confirmed current state (verified 2026-11-13 against `main` @ `e84829a`)

| Item                                              | Finding                                                                                                                                                                                                                                                                                                                                                                                                                     |
| ------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Local HEAD vs `main`                              | `feat/ef11-ga-migration` @ `ecedb99` has no content diff with `origin/main` `e84829a` (PR #361 squash). Branch PR C from `origin/main`.                                                                                                                                                                                                                                                                                     |
| NuGet stable versions                             | `10.0.0`, `10.1.0`. No stable 11.x yet.                                                                                                                                                                                                                                                                                                                                                                                     |
| NuGet previews                                    | `10.2.0-preview.109–145`, `11.0.0-preview.72–145`, `11.0.1-preview.92–144` (the last set came from the pre-PR-A drafter that resolved `v11.0.1`).                                                                                                                                                                                                                                                                           |
| GitHub releases                                   | Drafts `v10.2.0 (EF Core 10)` and `v11.0.0 (EF Core 11)`; Latest = `v10.1.0`; prerelease `v11.0.0-preview`.                                                                                                                                                                                                                                                                                                                 |
| Release mechanism                                 | `publish-release.yaml`: tag major → TFM, build, full test, linux-x64 NativeAOT publish+run, pack, push. Unchanged by this PR.                                                                                                                                                                                                                                                                                               |
| Docs build                                        | `uv run zensical build --clean` → `No issues found` on current `main`.                                                                                                                                                                                                                                                                                                                                                      |
| Upstream EF issues (checked with `gh issue view`) | `#37750` CLOSED (complex-collection value factories in compiled models; fix ships in EF Core 12), `#38625` CLOSED (misleading required-complex-collection error; fix ships in EF Core 12), `#38951` OPEN (`$(Configuration)` not passed to MSBuildWorkspace), `#38955` OPEN (multi-targeted projects: empty `$(TargetFramework)` during generation), `#38962` OPEN (no extensibility point for provider-defined terminals). |
| Beads                                             | `ar2.1` CLOSED. **`ar2.2` and `ar2.2.2` are still IN_PROGRESS** although PR #361 merged; `ar2.2.1` CLOSED. `ar2.3`, `ar2.4`, `ar2.5`, `ar2.6` OPEN. `ar2.4` is blocked by `ar2.2` until it is closed (see §11).                                                                                                                                                                                                             |

## 2. Documentation inventory

| Surface                            | Path                                                                                                                                      | Role                        | PR C action                                                |
| ---------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------- | --------------------------- | ---------------------------------------------------------- |
| README (GitHub + both NuGet pages) | `README.md`                                                                                                                               | Entry point                 | Edit (§3, §4)                                              |
| Docs home                          | `docs/index.md`                                                                                                                           | Requirements + install      | Edit                                                       |
| Getting started                    | `docs/getting-started.md`                                                                                                                 | Prerequisites + install     | Edit (canonical version-selection section)                 |
| Precompiled queries / NativeAOT    | `docs/querying/precompiled-queries.md`                                                                                                    | AOT setup + restrictions    | Edit                                                       |
| Limitations                        | `docs/limitations.md`                                                                                                                     | AOT + modeling limits       | Edit (targeted)                                            |
| Projection                         | `docs/querying/projection.md`                                                                                                             | Projection support          | Edit one paragraph                                         |
| Complex types                      | `docs/modeling/complex-types.md`                                                                                                          | Complex modeling            | Add two short notes                                        |
| Health checks                      | `docs/configuration/lifecycle.md`                                                                                                         | Health-check package        | Add version note                                           |
| Contributing                       | `CONTRIBUTING.md`                                                                                                                         | Contributor prerequisites   | Fix SDK line                                               |
| Public skill                       | `skills/dynamodb-efcore-provider/` (`SKILL.md`, `references/setup.md`, `references/aot.md`, `references/modeling.md`, `evals/evals.json`) | Downloadable consumer skill | Edit                                                       |
| Internal spec-test skill           | `skills/dynamodb-efcore-specification-tests/SKILL.md`                                                                                     | Engineering guidance        | Edit                                                       |
| Internal AOT skill                 | `skills/dynamodb-efcore-aot/evals/evals.json`                                                                                             | Engineering eval            | One-line fix                                               |
| Historical / internal pages        | `docs/alpha-exit-priorities.md`, `docs/multi-version-ef-strategy.md`, `docs/internal/**`, `docs/plans/**`, `docs/spec-test-*.md`          | Not in nav                  | No content change except one banner line (§3.10, optional) |
| Site config                        | `zensical.toml`                                                                                                                           | Nav/theme                   | No change (§8)                                             |

Accurate pages not listed above (configuration, saving, querying other than projection/precompiled,
diagnostics, single-table design) need no change.

## 3. Incorrect or stale statements and exact corrections

Line numbers are from `main` @ `e84829a`; re-locate by the quoted text if they drift.

### 3.1 `README.md`

- **L9** `> **Note:** This provider is in preview. Review the [current limitations](...) before adopting it.`
    → stale (both lines are stable). Replace the note with the **Installation** section in §4.2
    inserted after the intro paragraph (L11–12), and this line:

    ```markdown
    Review the [limitations](https://dynamodb-ef-core.layeredcraft.dev/limitations/) before adopting
    the provider; DynamoDB's access-pattern model means some EF Core features and LINQ shapes are not
    supported.
    ```

- **L95** `- Table mapping, key mapping, owned types, and secondary-index metadata are supported.`
    → wrong: owned entity types are not supported (`docs/limitations.md` "Owned Entity Types Are Not
    Supported"). Replace with:

    ```markdown
    - Table mapping, key mapping, complex properties and complex collections, and secondary-index
      metadata are supported. Owned entity types are not supported; use complex types instead.
    ```

- Keep L88–90 (precompiled/NativeAOT experimental) unchanged.

### 3.2 `docs/index.md`

- **L17–23** Requirements table (`.NET 10.0+`, `EF Core 10.0+`) → wrong: `10.0+` implies 10.x works on
    .NET 11/EF Core 11 and vice versa. Replace the `## Requirements` section with the §4.1 table under
    heading `## Supported versions`.
- **L25–29** `## Install` single unversioned command → replace with the §4.2 command block plus one
    sentence: `See [Choose a package version](getting-started.md#choose-a-package-version) for central package management and multi-targeted projects.`

### 3.3 `docs/getting-started.md`

- **L14** `- **.NET 10** and **EF Core 10**` → replace with
    `- **.NET 10 with EF Core 10**, or **.NET 11 with EF Core 11** — see [Choose a package version](#choose-a-package-version)`.
- **L24–28** `## Installation` unversioned command → replace the section body with §4.2–§4.4 under
    `## Installation` with a first subsection `### Choose a package version` (this anchor is linked
    from `index.md`, README, and the skill).

### 3.4 `docs/querying/precompiled-queries.md`

- **L12–18** warning admonition: keep "Experimental support" and the production caveat; replace the
    CI sentence with the §5.1 text.
- **L25–41** project-setup XML shows only EF Core 10 `10.0.12`. Replace with the two labeled blocks
    in §5.2.
- **L49–53** `For EF Core 11, pin to the exact build ... (for example 11.0.0-rc.1.26425.128) ... EF Core 11 is still prerelease ...` → stale prerelease guidance. Replace with §5.3.
- **L55–59** publish command shows only `net10.0`. Replace with §5.4.
- Add after L65 the single-target paragraph in §5.5 (currently only the skill has it).
- **L186–188** duplicated link: `See [Limitations](../limitations.md) for both. See [Limitations](../limitations.md) before using NativeAOT with collection-valued entity members.` →
    `See [Limitations](../limitations.md) before using NativeAOT with collection-valued entity members.`
- **L183** `(dotnet/efcore#37750)` → `(dotnet/efcore#37750, fixed upstream for EF Core 12)`.
- **L196–211** `## Verification` uses repo-internal `task test:aot-*` commands and provider-developer
    instructions ("update the provider rewrite and its compatibility tests together"). Replace the
    section with §5.6.

### 3.5 `docs/limitations.md`

- **L22** `CI publishes and runs the NativeAOT runtime test project for both EF Core 10 and EF Core 11.`
    → replace with §5.1 validation sentence plus the macOS arm64 paragraph (§5.7).
- **L90** `(tracked upstream: [dotnet/efcore#37750](...))` → `(tracked upstream: [dotnet/efcore#37750](...), fixed for EF Core 12; it still applies to EF Core 10 and 11)`.
- Add the "Upstream EF Core issues" list (§5.8) immediately before L122 (`See [Precompiled Queries and NativeAOT]...`).
- **L565–569** last sentence `This is an incomplete provider implementation, not a DynamoDB platform limitation, and is tracked by bead \`dynamodb-efcore-provider-ar2.6\`.`→ internal bead ID in user docs. Replace with:`This is an incomplete provider implementation, not a DynamoDB platform
    limitation. Use a CLR-mapped discriminator, or model optionality on the complex property itself,
    when a null complex value must round-trip.`(Before writing the second sentence, confirm`docs/modeling/complex-types.md`"Null and Missing Attribute Behavior" still says an optional complex property stored as`NULL`materializes`null\`;
    if not, drop the second sentence.)
- **L632–637** `### Owned Types in \`Select\` Project the Full Container`/ "nested owned property path" / "full owned container attribute" → terminology is wrong (owned types are unsupported). Rename to`### Nested Complex Paths in \`Select\` Project the Full Container\` and replace "owned" with
    "complex" in the body (two occurrences). No behavior change.

### 3.6 `docs/querying/projection.md`

- **L77** `... DTO or anonymous projections that include primitive collection properties are also not currently supported. Project an entity, or materialize entities first and select the collection client-side.` → contradicted by integration coverage: anonymous projections of entity primitive
    collections (`new { item.Pk, item.Tags, item.LabelSet }`) and of a primitive collection inside an
    optional complex property (`new { x.Pk, x.OptionalComplex!.Ints }`, PR B) both pass. Replace L77
    with:

    ```markdown
    Anonymous-type and DTO projections can include primitive collection properties, including a
    primitive collection nested in a complex property; the query projects the top-level attribute and
    the collection is shaped client-side. When an optional complex property is `null`, a nested
    collection projected through it is `null`. Directly shaping a primitive collection as the
    top-level query result, such as `Select(e => e.Tags)` or `Select(e => e.Tags.ToList())`, is not
    currently supported; project it inside an anonymous type or DTO instead.
    ```

    Before committing, run the two integration tests named in §9 step 4 to confirm both shapes on
    both TFMs. If either fails, keep the original sentence and report.

### 3.7 `docs/modeling/complex-types.md`

Append to `### Null and Missing Attribute Behavior` (after the "Strict materialization" warning):

```markdown
!!! note "Required complex collections"

    A complex collection that EF Core treats as required — for example a non-nullable
    `List<Address>` in a project with nullable reference types enabled — must be non-null when you
    call `SaveChangesAsync`. Initialize it to an empty collection. EF Core 10 and 11 report a null
    required complex collection with a misleading message that names a property and "belongs to the
    type"; the message is corrected in EF Core 12
    ([dotnet/efcore#38625](https://github.com/dotnet/efcore/issues/38625)).

!!! note "Complex-type discriminators"

    A discriminator configured on a complex type is persisted only when it is mapped to a CLR
    property. See [Complex Type Discriminators](../limitations.md#complex-type-discriminators).
```

### 3.8 `docs/configuration/lifecycle.md`

- **L71–75** unversioned `dotnet add package Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`
    — on a .NET 10 project this resolves the newest (11.x) package. Keep the sentence above it and
    replace the block with:

    ```shell
    # .NET 10 / EF Core 10
    dotnet add package Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore --version 10.0.14

    # .NET 11 / EF Core 11
    dotnet add package Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore --version 11.0.0
    ```

    Then add: `Use the patch version that matches your other EF Core packages.` (Verify both versions
    exist on NuGet with the flat-container query in §9 step 6.)

### 3.9 `CONTRIBUTING.md`

- **L7** `- .NET SDK 10.0.x. \`global.json\` pins \`10.0.103\` with latest-minor roll-forward.` → stale (`global.json`only selects the test runner; CI builds both targets with the 11.0.x SDK). Replace with:`- .NET SDK 11.0.x, which builds both the \`net10.0\` and \`net11.0\` targets. \`global.json\`
    selects the Microsoft.Testing.Platform runner and does not pin an SDK.\`

### 3.10 `docs/multi-version-ef-strategy.md` (optional, recommended)

Historical page, not in nav, but published at `/multi-version-ef-strategy/`. **L607–612** claims
NuGet "selects the correct version based on their project's `<TargetFramework>`" for an unversioned
install — that is false for single-target packages and is exactly the misunderstanding §4 prevents.
Do not edit the body. Append one sentence to the existing "Superseded" banner (L3–13):
`Consumer install guidance in §5 is also superseded: NuGet does not choose a major version by target framework; see Getting Started → Choose a package version.`

### 3.11 Searched and intentionally unchanged

- `docs/alpha-exit-priorities.md` ("alpha"), `docs/internal/**`, `docs/plans/**`,
    `docs/spec-test-coverage.md` — historical/internal.
- `docs/querying/filtering.md:99,156` ("alpha" is sample data).
- `docs/diagnostics.md:62-63` OpenTelemetry packages are not EF-major-specific.
- EF Core 10-only NativeAOT caveats (`limitations.md` L47–53, L108–113; `precompiled-queries.md`
    L183–193) remain correct for the 10.x line — keep them.

## 4. Version compatibility and installation (canonical text)

### 4.1 Compatibility table

Used verbatim in `README.md`, `docs/index.md`, `docs/getting-started.md`, and
`skills/dynamodb-efcore-provider/references/setup.md`:

```markdown
| Provider package | Target framework | EF Core | .NET |
| --- | --- | --- | --- |
| `EntityFrameworkCore.DynamoDb` 10.x | `net10.0` | 10.0.x | .NET 10 |
| `EntityFrameworkCore.DynamoDb` 11.x | `net11.0` | 11.0.x | .NET 11 |

Each package targets one framework and one EF Core major version. Match the provider's major
version to your EF Core major version: 11.x packages cannot be installed in a `net10.0` project,
and 10.x packages do not support EF Core 11. The exact EF Core range is listed on each package's
NuGet page — for example, 10.2.0 requires EF Core 10.0.14 or later 10.0.x, and 11.0.0 requires
EF Core 11.0.0 or later 11.0.x.
```

### 4.2 Install commands

```bash
# .NET 10 / EF Core 10
dotnet add package EntityFrameworkCore.DynamoDb --version 10.2.0

# .NET 11 / EF Core 11
dotnet add package EntityFrameworkCore.DynamoDb --version 11.0.0
```

Followed by:

```markdown
Use the latest release of the line that matches your EF Core version. Always pass `--version` on
.NET 10: without it, NuGet selects the newest stable package, which is an 11.x package that cannot
be installed in a `net10.0` project. Packages with a `-preview` suffix are CI builds from `main`;
do not use them in production.
```

**Decision (recommended):** explicit versions everywhere; unversioned install is documented only as
the thing to avoid on .NET 10. Floating `10.*` is not recommended in docs because central package
management rejects floating versions by default. Exact versions in docs need a refresh per release
(see §13 Q3).

### 4.3 Central package management (getting-started only)

```xml
<!-- Directory.Packages.props -->
<PackageVersion Include="EntityFrameworkCore.DynamoDb" Version="10.2.0" />
```

`Use \`11.0.0\` for .NET 11 projects.\`

### 4.4 Multi-targeted applications and upgrading (getting-started only)

```xml
<ItemGroup Condition="'$(TargetFramework)' == 'net10.0'">
  <PackageReference Include="EntityFrameworkCore.DynamoDb" Version="10.2.0" />
</ItemGroup>
<ItemGroup Condition="'$(TargetFramework)' == 'net11.0'">
  <PackageReference Include="EntityFrameworkCore.DynamoDb" Version="11.0.0" />
</ItemGroup>
```

```markdown
To move from 10.x to 11.x, retarget the project to `net11.0`, update every
`Microsoft.EntityFrameworkCore.*` package (including `Tasks` and `Design` if you use precompiled
queries) to 11.0.x, and then update the provider to 11.x.
```

## 5. NativeAOT guidance changes

### 5.1 Validation statement (precompiled-queries admonition and limitations L22)

```markdown
The provider validates query-interceptor generation for EF Core 10 and EF Core 11, and publishes and
runs a NativeAOT test application for both on `linux-x64` before every release. Other runtime
identifiers are not validated by the provider.
```

### 5.2 Project setup XML (replace the single block)

Introduce with: `Add the EF Core Tasks and Design packages at the same version as the \`Microsoft.EntityFrameworkCore\` version your application resolves, enable NativeAOT, and allow EF
Core's generated interceptor namespace:`then keep the`<PropertyGroup>\` as is, then:

```xml
<!-- EF Core 10 (provider 10.x) -->
<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore.Tasks" Version="10.0.14" PrivateAssets="all" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.14" PrivateAssets="all" />
</ItemGroup>
```

```xml
<!-- EF Core 11 (provider 11.x) -->
<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore.Tasks" Version="11.0.0" PrivateAssets="all" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="11.0.0" PrivateAssets="all" />
</ItemGroup>
```

(`10.0.14` = the 10.x provider's EF Core floor; see §13 Q2 about the repo's own `10.0.12` Tasks pin.)

### 5.3 Version alignment (replaces L49–53)

```markdown
Pin exact versions for Tasks and Design rather than floating ranges, and keep them on the same
version as your other EF Core packages: 10.0.x with provider 10.x, 11.0.x with provider 11.x. A
Tasks build that does not match the provider's expected EF Core output fails the build with
`generated an incompatible precompiled-query executor template`.
```

### 5.4 Publish commands

```bash
dotnet publish --configuration Release --framework net10.0 --runtime linux-x64
dotnet publish --configuration Release --framework net11.0 --runtime linux-x64
```

### 5.5 Single-target requirement (new, after the generation-stage paragraph)

```markdown
Publish NativeAOT from a project that targets a single framework and uses the standard `Debug` or
`Release` configuration. EF Core's generation step currently loses the target framework in
multi-targeted projects ([dotnet/efcore#38955](https://github.com/dotnet/efcore/issues/38955)) and
the build configuration for non-default configurations
([dotnet/efcore#38951](https://github.com/dotnet/efcore/issues/38951)).
```

### 5.6 Replacement `## Verification` section → rename `## Validating your application`

```markdown
Publish your application for the runtime identifier you deploy and run its real query shapes
before relying on NativeAOT. A query with no generated interceptor publishes successfully and fails
only when it first executes, with `Query wasn't precompiled and dynamic code isn't supported with
NativeAOT`.

If generation reports an incompatible executor template, align the Tasks, Design, and EF Core
package versions with your provider package. If they already match, report an issue with the EF
Core and provider versions.
```

### 5.7 macOS arm64 note (limitations, NativeAOT section, after §5.1 sentence)

```markdown
On macOS arm64, publishing an EF Core 11 NativeAOT application has been observed to fail inside
EF Core 11.0.0's query-precompilation build step, before provider code runs. Linux publishing is
not affected. Until this is resolved, publish EF Core 11 NativeAOT applications on Linux — for
example in a Linux container or CI runner. EF Core 10 publishing on macOS arm64 is not affected.
```

Gate: include §5.7 only if the maintainer confirms Q1 (§13). If not confirmed, omit it and keep
§5.1's "Other runtime identifiers are not validated" sentence, which is accurate either way.

### 5.8 Upstream EF Core issues list (limitations, end of NativeAOT section)

```markdown
### Upstream EF Core issues affecting NativeAOT

| Issue | Effect | Status |
| --- | --- | --- |
| [dotnet/efcore#38962](https://github.com/dotnet/efcore/issues/38962) | `ToPageAsync` is not precompiled | Open |
| [dotnet/efcore#38955](https://github.com/dotnet/efcore/issues/38955) | Multi-targeted projects cannot generate | Open |
| [dotnet/efcore#38951](https://github.com/dotnet/efcore/issues/38951) | Non-default build configurations cannot generate | Open |
| [dotnet/efcore#37750](https://github.com/dotnet/efcore/issues/37750) | Tracked queries over complex collections fail; use `AsNoTracking()` | Fixed in EF Core 12 |
```

Re-check each issue's state with `gh issue view <n> -R dotnet/efcore --json state` before
committing; update the Status column if it changed.

Out of scope: any NativeAOT behavior change, new AOT tests, warning-baseline changes.

## 6. Public agent skill changes (`skills/dynamodb-efcore-provider/`)

Preserve frontmatter (`name`, `description`; no `metadata.internal`), the "Choose a subject" table,
progressive-reference layout, and evals schema (`skill_name`, `evals[] {id, prompt, expected_output, files}`).

1. `SKILL.md` — add to "Rules that avoid unsafe advice" (first bullet):

    ```markdown
    - Match the provider's major version to EF Core's: 10.x for `net10.0`/EF Core 10, 11.x for
      `net11.0`/EF Core 11. Always give an explicit `--version`; never suggest an 11.x package or a
      `-preview` package for a .NET 10 application. See `references/setup.md`.
    ```

2. `references/setup.md` — insert after the first paragraph a `## Choose the package version`
    section containing the §4.1 table, the §4.2 commands and paragraph, and the §4.4 upgrade
    sentence. Keep the rest unchanged.

3. `references/aot.md`

    - L18–19 XML: replace with the two §5.2 blocks (compact one-line form is fine).
    - L24–25 publish: `dotnet publish -c Release -f net10.0 -r linux-x64` (or `-f net11.0` for EF Core 11).
    - L32–34: replace "On EF Core 11 pin the exact prerelease build." with "Pin exact versions;
        10.0.x for provider 10.x, 11.0.x for provider 11.x."
    - L40–41 single-target bullet: append `(dotnet/efcore#38955, #38951)`.
    - After the L1–8 intro add: `Provider validation runs NativeAOT publish-and-run on linux-x64 for EF Core 10 and 11; other runtime identifiers are not validated.` Add the §5.7 macOS sentence
        only if Q1 is confirmed.
    - The `#37750` bullet (~L160): append `; fixed upstream for EF Core 12`.

4. `references/modeling.md` — under "Shapes that work well" add:

    ```markdown
    - Required complex collections must be non-null before `SaveChangesAsync`; initialize them to an
      empty collection. EF Core 10/11 report a null one with a misleading "belongs to the type"
      message (dotnet/efcore#38625).
    - A complex-type discriminator is persisted only when mapped to a CLR property
      (`HasDiscriminator(v => v.Kind)`); a shadow discriminator is not stored, and EF Core's
      complex-type discriminator null semantics are not implemented.
    ```

5. `evals/evals.json` — append:

    ```json
    {
      "id": 5,
      "prompt": "Our API targets net10.0 with EF Core 10. Which EntityFrameworkCore.DynamoDb package should we install, and what command should we run? We also want precompiled queries.",
      "expected_output": "Recommends the 10.x line with an explicit --version, explains that 11.x requires net11.0/EF Core 11 and that an unversioned install selects 11.x, and aligns Microsoft.EntityFrameworkCore.Tasks and Design to the same 10.0.x EF Core version.",
      "files": []
    }
    ```

Spec-test infrastructure is **not** consumer-relevant; add nothing about it to the public skill.

## 7. Internal engineering guidance changes

### 7.1 `skills/dynamodb-efcore-specification-tests/SKILL.md` (internal, `metadata.internal: true`)

- Replace the `## Commands` section (L410–432; `task test:spec` and `task test:spec:all` do not exist
    in `Taskfile.yml`) with:

    ````markdown
    ## Commands

    Use the .NET test MCP server when available. CLI fallback (build once per framework, then filter):

    ```bash
    SPEC=tests/EntityFrameworkCore.DynamoDb.SpecificationTests/EntityFrameworkCore.DynamoDb.SpecificationTests.csproj
    dotnet build $SPEC --framework net10.0
    dotnet build $SPEC --framework net11.0

    # net11.0 — xUnit v3: wildcard class/method filters
    dotnet test --project $SPEC --framework net11.0 --no-build --filter-class "*FindDynamoTest*"
    dotnet test --project $SPEC --framework net11.0 --no-build --filter-class "*ComplianceDynamoTest*"

    # net10.0 — xUnit v2 through YTest.MTP.XUnit2: VSTest-style filter
    dotnet test --project $SPEC --framework net10.0 --no-build --filter "FullyQualifiedName~FindDynamoTest"
    dotnet test --project $SPEC --framework net10.0 --no-build --filter "FullyQualifiedName~ComplianceDynamoTest"
    ```

    Full suites (whole solution, includes the spec project): `task test:ef10` and `task test:ef11`.
    Always validate spec changes on both frameworks; overrides and skips can differ by target.

    When debugging query baselines, inspect assertion failures and captured `PartiQL baseline
    mismatch` text. Check `dotnet test --project $SPEC --framework <tfm> --help` for the live-output
    options each runner supports.
    ````

    The implementer must run each of the four `dotnet test` lines once (any pass/skip result is fine;
    an "unknown option" error is not) and correct the syntax before committing.

- Add a new section before `## Implementation workflow`:

    ```markdown
    ## Target frameworks and xUnit versions

    The spec project runs xUnit v2 on `net10.0` (EF Core 10 Specification.Tests) and xUnit v3 4.x on
    `net11.0` (EF Core 11 Specification.Tests). Read
    `tests/EntityFrameworkCore.DynamoDb.SpecificationTests/AGENTS.md` before editing. Key rules:

    - `[ConditionalFact]`/`[ConditionalTheory]` are global aliases to `Fact`/`Theory` on `net11.0`; do
      not add a `FactAttribute` subclass (xUnit v3 rejects multiple fact attributes).
    - Override guards compare against `typeof(FactAttribute)`; keep them TFM-neutral.
    - Isolate EF Core 11 API drift and GA-only base tests with `#if NET11_0`; EF10 counts must not
      change when only EF11 is targeted.
    - xUnit v3 reports a skipped `[Theory]` once rather than once per data row, so net11 totals are
      not comparable row-for-row with net10.
    - Every fixture must use a unique physical table name; fixtures sharing a name with different key
      schemas collide.
    ```

    Verify each bullet against the spec project `AGENTS.md` and source before committing; drop any
    bullet that is not true on `main`.

### 7.2 `skills/dynamodb-efcore-aot/evals/evals.json`

- The eval whose prompt is `Verify this branch really supports NativeAOT for EF10 and EF11 before merge.` has `expected_output` "... publishes and launches the EF10 native smoke app ..." → replace
    "publishes and launches the EF10 native smoke app" with "publishes and runs the NativeAOT runtime
    tests for net10.0 and net11.0".

### 7.3 Out of scope (noted, not changed)

- `skills/dynamodb-efcore-docs-update/references/doc-pages.md` lists stale page paths
    (`docs/operators.md`, `docs/projections.md`, `docs/configuration.md`, `docs/architecture.md`).
    Not GA-related; leave for a separate cleanup.
- Beads memory `nativeaot-smoke-app-and-ci-native-aot-leg` says NativeAOT is EF10-only — stale and
    injected into every session. Maintainer action (§11), not a file change.

## 8. Documentation navigation

- **No new pages and no `zensical.toml` change.** Version selection lives in
    `getting-started.md#choose-a-package-version` (canonical), summarized on `index.md`, and linked
    from README and the skill. This keeps the nav unchanged and the anchor stable.
- `pymdownx.tabbed` is not enabled; use labeled code blocks (comments), not content tabs.
- README links must be absolute `https://dynamodb-ef-core.layeredcraft.dev/...` URLs because the
    README renders on NuGet; use `https://dynamodb-ef-core.layeredcraft.dev/getting-started/#choose-a-package-version`.
- Known, out of scope: Zensical publishes every Markdown file under `docs/`, so `docs/plans/**`,
    `docs/internal/**`, and the historical pages are reachable by URL (`site/plans/...`,
    `site/internal/...`) though not in nav. See §13 Q5.

## 9. Validation commands

Run from the repo root on the PR C branch.

1. Docs build: `task docs:build` → `No issues found`, exit 0.

2. Stale-text sweep (each must print nothing):

    ```bash
    grep -rnE "still prerelease|exact prerelease|11\.0\.0-rc|is in preview|owned types, and" \
      README.md docs/index.md docs/getting-started.md docs/limitations.md docs/querying \
      docs/modeling docs/configuration docs/saving skills/dynamodb-efcore-provider
    grep -rnE "dynamodb-efcore-provider-ar2|\bbead\b|task test:" \
      README.md docs/index.md docs/getting-started.md docs/limitations.md docs/querying \
      docs/modeling docs/configuration docs/saving docs/diagnostics.md docs/dynamodb-concepts.md
    grep -rn "test:spec" skills/
    ```

3. Presence checks (each must print at least one line):

    ```bash
    grep -n "choose-a-package-version\|Choose a package version" README.md docs/index.md docs/getting-started.md
    grep -n -- "--version 10.2.0" README.md docs/index.md docs/getting-started.md skills/dynamodb-efcore-provider/references/setup.md
    grep -n -- "--version 11.0.0" README.md docs/index.md docs/getting-started.md skills/dynamodb-efcore-provider/references/setup.md
    grep -n "net11.0 --runtime linux-x64" docs/querying/precompiled-queries.md
    ```

4. Projection claim (§3.6), both TFMs, against DynamoDB Local via Testcontainers:

    ```bash
    INT=tests/EntityFrameworkCore.DynamoDb.IntegrationTests/EntityFrameworkCore.DynamoDb.IntegrationTests.csproj
    dotnet build $INT --framework net10.0 && dotnet build $INT --framework net11.0
    for f in net10.0 net11.0; do
      dotnet test --project $INT --framework $f --no-build \
        --filter-method "*Select_AnonymousProjection_WithCollectionProperties*" \
        --filter-method "*Select_primitive_collection_inside_*optional_complex*"
    done
    ```

    Expected: 3 passed per framework.

5. JSON validity: `python3 -m json.tool skills/dynamodb-efcore-provider/evals/evals.json >/dev/null && python3 -m json.tool skills/dynamodb-efcore-aot/evals/evals.json >/dev/null`.

6. Package versions cited in docs exist:

    ```bash
    for p in microsoft.entityframeworkcore.tasks microsoft.entityframeworkcore.design \
             microsoft.extensions.diagnostics.healthchecks.entityframeworkcore; do
      curl -s https://api.nuget.org/v3-flatcontainer/$p/index.json \
        | python3 -c "import json,sys;v=json.load(sys.stdin)['versions'];print('$p',all(x in v for x in ['10.0.14','11.0.0']))"
    done
    ```

    Expected: `True` for all three.

7. Upstream issue states: `for i in 37750 38625 38951 38955 38962; do gh issue view $i -R dotnet/efcore --json state -q .state; done`.

8. Markdown formatting: `format <each changed .md file>` (CONTRIBUTING "Formatting"); re-run step 1.

9. Code suites: not required (no code changes). If the PR template or CI demands them, `task test:ef10` and `task test:ef11` must stay green; report results either way.

10. Spot-check rendered pages: open `site/getting-started/index.html`, `site/index.html`,
    `site/querying/precompiled-queries/index.html`, `site/limitations/index.html` and confirm the
    tables and admonitions render and the `#choose-a-package-version` anchor resolves.

## 10. Acceptance criteria

- No user-facing page or README calls the provider preview or EF Core 11 prerelease.
- README, `docs/index.md`, `docs/getting-started.md`, and the public skill carry the same §4.1 table
    and explicit-version install commands for both lines; no page recommends an unversioned install
    for .NET 10.
- NativeAOT docs: EF Core 11 setup example present; GA version-alignment guidance replaces the
    prerelease pin; linux-x64 is stated as the validated platform; upstream issues table present with
    verified states; macOS arm64 note present only if Q1 confirmed; no universal-compatibility claim;
    no Taskfile/repo commands in user docs.
- `limitations.md` has no bead ID; owned-type wording in the projection section is corrected.
- `projection.md` matches the integration tests in §9 step 4.
- Public skill: rules, setup, aot, modeling, and eval 5 updated; frontmatter and structure unchanged.
- Internal spec-test skill: no `task test:spec`; per-TFM commands verified to run; xUnit v2/v3
    section present and accurate.
- §9 steps 1–8 pass. PR template documentation and provider-skill boxes both checked.
- Diff touches only the files in §2 marked Edit/Add/fix (plus the optional §3.10 banner line).

## 11. Release-tracking updates

Applied during planning (`bd --sandbox`):

- `ar2.4` retitled **"Publish v10.2.0 and v11.0.0 from the Release Drafter drafts"**; description and
    acceptance criteria now cover both versions, the order (v10.2.0 → v11.0.0 → verify both → PR D),
    per-package NuGet checks, and post-publish install smoke checks. Status unchanged (OPEN).
- `ar2.3` design field points to this plan.

Maintainer actions needed (not done here):

- Close `ar2.2.2` and `ar2.2` if post-merge verification of PR #361 is complete; while `ar2.2` is
    open, `ar2.4` stays blocked.
- Refresh the stale beads memory: `bd --sandbox remember --key nativeaot-smoke-app-and-ci-native-aot-leg "NativeAOT runtime tests run in CI for net10.0 and net11.0 on linux-x64 (pr-build native-aot matrix, publish-release gate). EF11 publish on macOS arm64 has failed locally inside EF Core Tasks 11.0.0 query precompilation; Linux is the arbiter."`
- Epic `ar2` title/acceptance still say v11.0.0 only; optional retitle to "Release v10.2.0 and
    v11.0.0 (EF Core 11 GA)".

PR C commit trailers: `Bead: dynamodb-efcore-provider-ar2.3`. Label: `docs` (not `ef10-only` /
`ef11-only` — content affects both release notes). Close `ar2.3` after merge and post-merge docs
deploy check.

## 12. Scope exclusions

- Provider code, tests, `Directory.Packages.props`, `*.csproj`, Taskfile, `zensical.toml`,
    `.github/**`, release drafts, tags, NuGet packages.
- Aligning the repo's own `Microsoft.EntityFrameworkCore.Tasks` net10 pin (`10.0.12`) with the
    EF Core floor (`10.0.14`) — see Q2.
- `ar2.6` complex-type discriminator persistence/null semantics.
- NativeAOT behavior work, macOS arm64 root-cause investigation, filing upstream issues.
- Unlisting/deprecating `11.0.1-preview.*` packages (Q4).
- Hiding `docs/plans/**` / `docs/internal/**` from the published site (Q5).
- `doc-pages.md` path cleanup (§7.3).
- PR D (`include-pre-releases` removal).

## 13. Decisions requiring maintainer approval

| #   | Question                                                                                                                                                                                                                                                                                                | Recommendation                                                                                                                                                                                                                                  |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Q1  | Is the macOS arm64 EF Core Tasks 11.0.0 query-precompilation failure (`MSBuildWorkspace.OpenProjectAsync`, RpcClient null) reproducible enough to document for users? Only one local observation is recorded; earlier research recorded a different, non-fatal macOS `dsymutil` issue on the same path. | Document §5.7 only if you can reproduce it once more; otherwise ship §5.1's neutral "other RIDs are not validated" sentence. Filing an upstream issue is a separate task.                                                                       |
| Q2  | Docs recommend Tasks/Design `10.0.14` (provider EF floor) while the repo itself builds with Tasks `10.0.12` against EF `10.0.14`.                                                                                                                                                                       | Keep `10.0.14` in docs (EF's own guidance is to match). Optionally open a follow-up to align the repo pin after release.                                                                                                                        |
| Q3  | Exact versions (`10.2.0`, `11.0.0`) in docs go stale on each release.                                                                                                                                                                                                                                   | Accept; add "refresh install examples" to the release checklist. Alternative: phrase as `--version 10.*` (works with PackageReference, not with CPM).                                                                                           |
| Q4  | NuGet `11.0.1-preview.92–144` sorts **above** `11.0.0`; anyone opting into prereleases (`--prerelease`, `11.*-*`) gets an rc-era build instead of GA until a newer 11.0.x preview ships.                                                                                                                | Release-readiness concern, not a PR C blocker. Docs already steer users to stable explicit versions. Consider unlisting `11.0.1-preview.*` after 11.0.0 publishes (reverses the "keep previews listed" decision only for that misnumbered set). |
| Q5  | `docs/plans/**` and `docs/internal/**` are published as orphan pages.                                                                                                                                                                                                                                   | Leave for a separate decision (Zensical exclusion or moving plans out of `docs/`).                                                                                                                                                              |
| Q6  | Should README carry the install table (it appears on both NuGet pages)?                                                                                                                                                                                                                                 | Yes — it is the most likely place a .NET 10 user picks the wrong major.                                                                                                                                                                         |
