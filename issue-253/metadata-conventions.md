# Issue #253 metadata/model-building/conventions/validation context

Scope here: only metadata, model-building APIs, conventions, model validation. Runtime/write/query files noted only where current metadata is consumed or issue requires “no raw annotation runtime inference”.

## Source requirements

- GitHub issue #253 open: “Implement ADR-003 EF key semantics for DynamoDB keys”. Body requires both `HasPartitionKey`/`HasSortKey` and EF `HasKey` as first-class key entry points, one finalized key model, no runtime ad-hoc inference, and shadow primary-key support when EF can model/map it.
- Local ADR: `decisions/ADR-003-ef-key-semantics-for-dynamodb-keys.md`. Same Option C direction, but ADR says shadow/runtime-only properties cannot be table keys. Issue supersedes ADR on shadow support: issue explicitly says shadow primary-key properties are allowed when valid DynamoDB scalar attributes; only runtime-only provider metadata must be rejected.

## Current implementation map

### Service/convention registration

- `src/EntityFrameworkCore.DynamoDb/Extensions/DynamoServiceCollectionExtensions.cs:32-34` registers:
  - `IProviderConventionSetBuilder -> DynamoConventionSetBuilder`
  - `IModelValidator -> DynamoModelValidator`
  - `IModelRuntimeInitializer -> DynamoModelRuntimeInitializer`
- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoConventionSetBuilder.cs:27-31` replaces EF `ValueGenerationConvention`, `KeyDiscoveryConvention`, `DiscriminatorConvention`.
- Same file `:42-51` model-finalizing order:
  - `DynamoAttributeNamingConventionApplier`
  - `DynamoKeyAnnotationConvention`
  - then annotation/property-added convention hooks for `DynamoKeyInPrimaryKeyConvention`
- Important: no model-finalizing convention currently resolves a complete final key model after all key inputs are known.

### Public model-building APIs

- `src/EntityFrameworkCore.DynamoDb/Extensions/DynamoEntityTypeBuilderExtensions.cs:46-50`
  - `HasPartitionKey(string)` only validates non-empty string and sets `Dynamo:PartitionKeyPropertyName` annotation.
  - Does **not** create/configure property. Shadow support currently requires separate `b.Property<T>("PK")` before/after.
- Same file `:64-68`
  - `HasSortKey(string)` same behavior for `Dynamo:SortKeyPropertyName`.
- Generic string overloads at `:233-245` delegate to non-generic overloads.
- Lambda overloads at `:260-287` call `entityTypeBuilder.Property(keyExpression)` before setting annotation, so CLR property exists in model.
- Current XML docs still say root entities should not configure `HasKey(...)` directly (`:37-41`, `:57-61`, `:251-276`). Must change with issue.

### Raw annotation storage/accessors

- `src/EntityFrameworkCore.DynamoDb/Metadata/Internal/DynamoAnnotationNames.cs:18-21`
  - `Dynamo:PartitionKeyPropertyName`, `Dynamo:SortKeyPropertyName` store raw role names.
- `src/EntityFrameworkCore.DynamoDb/Extensions/DynamoEntityTypeExtensions.cs:30-44`
  - mutable setters store/remove those annotations.
- Same file `:95-140`
  - read-only `GetPartitionKeyPropertyName/GetSortKeyPropertyName` read only annotations; `GetPartitionKeyProperty/GetSortKeyProperty` resolve by annotation name.
- Same file `:263-305`
  - convention setters/getters preserve configuration source for raw annotations.
- Gap: these accessors cannot distinguish raw provider annotations, convention hints, EF-key-inferred roles, or final resolved roles. Runtime and validation widely use them.

### Key discovery convention

- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoKeyDiscoveryConvention.cs`
  - Inherits EF `KeyDiscoveryConvention`.
  - Current docs explicitly say keys are never inferred from `[Key]` (`:28-35`). Must change.
  - `ShouldDiscoverKeyProperties` applies only to non-owned roots (`:41-44`).
  - `ProcessKeyProperties` ignores EF’s discovered candidates and replaces them with Dynamo name candidates (`:54-74`):
    - partition: first of `PK`/`PartitionKey`, else `Id`
    - sort: first `SK`/`SortKey`
  - `IsDiscoverableKeyProperty` excludes shadow and runtime-only (`:77-79`). This blocks convention-discovered shadow key support.
  - Name helpers at `:85-112` implement `PK`/`PartitionKey`, fallback `Id`, `SK`/`SortKey`.
- Consequence: convention-created EF PK uses Dynamo-specific names now. Explicit `HasKey` is left as explicit EF key but rejected later.

### Key annotation convention

- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoKeyAnnotationConvention.cs`
  - Model-finalizing convention writes `Dynamo:PartitionKeyPropertyName`/`Dynamo:SortKeyPropertyName` from conventional property names (`:35-52`).
  - Skips only explicit/data-annotation provider annotations (`:62-67`, `:95-98`).
  - It says `HasKey(...)` is not used to infer DynamoDB keys (`:18-20`). Must change.
  - Ambiguity throws when multiple conventional candidates exist (`:125-134`).
- High-risk interaction for issue #253:
  - If explicit `HasKey(e => e.CustomId)` exists and entity also has conventional `Id`, this convention will write a **convention-source** partition annotation for `Id` before any resolver. New resolver must not treat that convention-source annotation as user conflict against explicit EF key. Either skip convention annotation when explicit/data-annotation EF primary key exists, or let resolver override convention-source provider hints with explicit EF key roles.

### Provider annotation -> EF primary key synthesis

- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoKeyInPrimaryKeyConvention.cs`
  - Hooks `IEntityTypeAnnotationChangedConvention`, `IPropertyAddedConvention` only (`:30-32`).
  - On key annotations changed, calls `ProcessPrimaryKey` (`:39-50`).
  - On property added, re-runs synthesis for key property or explicit provider annotation (`:54-88`).
  - `ProcessPrimaryKey` skips derived/owned (`:96-100`).
  - Preserves explicit/data-annotation EF primary keys by returning when PK config source is not convention (`:102-108`).
  - Requires partition annotation; resolves annotation names to properties (`:110-128`).
  - Builds desired EF PK `[pk]` or `[pk, sk]` (`:122-132`).
  - Replaces existing convention key only (`:135-143`).
- Current behavior matches provider-first story only. For issue #253, this class likely becomes part of a final resolver or should be replaced by one that handles both directions.

### Model validator

- `src/EntityFrameworkCore.DynamoDb/Infrastructure/Internal/DynamoModelValidator.cs:16-35` validation flow:
  - pre-base: relationships, explicit root PK rejection, root has partition annotation, sort has partition annotation, configured key properties exist
  - base EF validation
  - post-base: key names/types/nullability, shared table schema, secondary indexes, discriminator, etc.
- `ValidateRootEntityDoesNotUseExplicitPrimaryKeyConfiguration` rejects every explicit/data-annotation root EF PK (`:161-180`). This is main blocker for `HasKey` support.
- `ValidateRootEntityHasPartitionKey` requires raw partition annotation and says provider does not infer from `HasKey`/`[Key]` (`:230-255`). Main blocker for `HasKey`-only support.
- `ValidateSortKeyHasResolvablePartitionKey` rejects sort annotation without partition annotation (`:258-275`). Needs adjustment: `HasSortKey` plus explicit two-part `HasKey` may be resolvable through EF PK.
- `ValidateConfiguredKeyPropertiesExist` resolves raw annotation names and rejects missing properties (`:278-302`). Still useful for explicit provider annotations, but missing-key behavior may differ if `HasPartitionKey(string)` should create shadow properties (decision needed).
- `ValidateConfiguredKeyPropertyIsSupported` rejects runtime-only **and shadow** keys (`:305-323`). Must keep runtime-only rejection but remove/change shadow rejection per issue.
- `ValidateKeyPropertyNames` validates raw annotations exactly match EF primary key shape (`:326-369`). With `HasKey` support this should validate final resolved roles vs EF PK and produce mismatch-specific errors, not blanket explicit-key rejection.
- `ValidateKeyPropertyTypes` and `ValidateKeyPropertyNullability` use `GetPartitionKeyProperty/GetSortKeyProperty` (`:372-420`), so they currently validate only annotation-resolved roles. Must validate inferred EF-key roles too.
- Type/nullability helpers allow string, byte[], numeric provider types and non-nullable provider type (`:387-402`, `:423-441`, `:968-984`). Reuse for inferred keys.
- Shared-table consistency uses `GetPartitionKeyProperty/GetSortKeyProperty` and compares attribute names/type categories (`:615-670`). Must consume resolved roles, not raw annotations.
- Root entity scope is `FindOwnership() == null && BaseType == null` (`:961-966`). Derived types use root key shape.

### Runtime metadata consumption relevant to “no raw annotation runtime inference”

- `src/EntityFrameworkCore.DynamoDb/Infrastructure/Internal/DynamoModelRuntimeInitializer.cs:22-34` builds runtime model after validation.
- Same file `:88-105` builds base-table `DynamoIndexDescriptor` by calling `sourceEntityType.GetPartitionKeyProperty()` and `GetSortKeyProperty()` — currently raw annotation accessors.
- `src/EntityFrameworkCore.DynamoDb/Metadata/Internal/DynamoRuntimeTableModel.cs:5-23` already has a runtime table/index descriptor shape holding partition/sort properties, but no separate resolved-key descriptor/provenance.
- Other runtime paths still call raw accessors directly:
  - `Storage/DynamoPartiqlStatementFactory.cs:103-120`, `:178-194`
  - `Storage/DynamoTransactionTargetIdentityFactory.cs:14-24`
  - `Query/Internal/DynamoSqlTranslatingExpressionVisitor.cs:1000-1007` for base table partition key check
- For metadata implementation, create final resolved roles before these paths. Later runtime work should consume `DynamoRuntimeTableModel`/resolved metadata only.

### EF Core Cosmos prior art (local repo)

- `/Users/jonasha/Repos/CSharp/efcore/src/EFCore.Cosmos/Metadata/Conventions/CosmosKeyDiscoveryConvention.cs`
  - Extends `KeyDiscoveryConvention`, appends partition key properties to discovered key, reacts to partition-key annotation changes.
- `/Users/jonasha/Repos/CSharp/efcore/src/EFCore.Cosmos/Metadata/Conventions/CosmosPartitionKeyInPrimaryKeyConvention.cs`
  - Many convention hooks (`IKeyAdded`, `IKeyRemoved`, `IEntityTypePrimaryKeyChanged`, annotation changes, base type changes, etc.) and only rewrites convention-created PK (`ConfigurationSource.Convention.Overrides(...)`). Useful pattern for robust convention timing.

## Current tested behavior

- Conventional names configure EF PK + annotations:
  - `DynamoKeyDiscoveryConventionTests.cs:57-69`: property `PK` -> EF PK `[PK]`, partition annotation `PK`.
  - `:134-146`: property `Id` fallback -> EF PK `[Id]`, partition annotation `Id`.
- Shadow conventional `PK` is ignored:
  - `DynamoKeyDiscoveryConventionTests.cs:212-224`: shadow `PK` + CLR `Id` -> fallback `Id` wins.
- Ambiguous conventional names throw:
  - `DynamoKeyDiscoveryConventionTests.cs:828-842`: both `PK` and `PartitionKey` require `HasPartitionKey`.
  - `:868-882`: both `SK` and `SortKey` require `HasSortKey`.
- Provider APIs synthesize EF PK without explicit `HasKey`:
  - `DynamoKeyInPrimaryKeyConventionTests.cs:54-66`: `HasPartitionKey(CustomPk)` -> EF PK `[CustomPk]`.
  - `:97-111`: `HasPartitionKey(CustomPk)` + `HasSortKey(CustomSk)` -> EF PK `[CustomPk, CustomSk]`.
- Explicit EF `HasKey` is rejected even when matching provider annotations:
  - `DynamoKeyInPrimaryKeyConventionTests.cs:251-257`, `:264-271` reject matching `HasKey(PkProp, SkProp)` + provider annotations.
  - `DynamoEntityKeyMappingConventionTests.cs:64-85` reject `HasKey(TenantId, OrderId)` alone.
  - `:132-155` reject three-part `HasKey(A,B,C)` with same generic explicit-key message.
- Nonexistent provider key property throws:
  - `TableKeySchemaValidationTests.cs:81-99`, contexts at `:528-557`.
- Current shadow provider keys are explicitly rejected:
  - tests `TableKeySchemaValidationTests.cs:137-156`, contexts `:642-676`.
  - `DynamoKeyInPrimaryKeyConventionTests.cs:118-146` also expects shadow provider keys to throw.
- Sort key without partition currently throws even if future EF key could supply partition:
  - `TableKeySchemaValidationTests.cs:126-134`, context `:621-635`.

## Gaps vs issue #253 story

1. **`HasKey` is not first-class.** Explicit/data-annotation root EF primary keys are rejected before inference (`DynamoModelValidator.cs:161-180`).
2. **No EF-PK-to-Dynamo key inference.** If only `HasKey` exists, raw provider annotations remain absent or convention-filled; validation requires annotation (`:230-255`).
3. **No unified resolved key descriptor.** Runtime/validation access raw `Dynamo:PartitionKeyPropertyName`/`SortKeyPropertyName` annotations through `GetPartitionKeyProperty*`. No provenance, no final resolved shape, no way to know if annotation is user config vs convention hint vs inferred final role.
4. **Convention annotations can conflict with explicit EF keys unless source-aware.** Example: `HasKey(CustomId)` on entity with `Id` property. Current finalizing convention can write `Id` as convention partition annotation. Resolver must let explicit/data-annotation EF PK win over convention-source hints.
5. **Shadow keys blocked in two places.** Discovery excludes shadow properties (`DynamoKeyDiscoveryConvention.cs:77-79`), and validator rejects shadow provider keys (`DynamoModelValidator.cs:319-323`). Issue wants valid shadow key support.
6. **Conflict validation too coarse.** Matching `HasKey` + `HasPartitionKey`/`HasSortKey` currently rejected by explicit-key rule. Need precise mismatch errors for wrong first property, wrong second property, missing/extra sort key, >2 EF PK properties.
7. **Finalization timing split.** Current provider-key -> EF-PK synthesis happens on annotation/property-added events, not as one final resolution after all inputs. Issue asks resolve final table key model during finalization and replace convention-created keys only after computing complete shape.
8. **Sort-key-only handling must consider EF PK.** Current validator requires partition annotation whenever sort annotation exists. Under issue, `HasSortKey(second)` + explicit two-part `HasKey(first, second)` could be valid if policy allows inferred partition from EF PK.
9. **Docs/XML/tests assert old semantics.** Many tests expect explicit `HasKey` rejection and shadow-key rejection.

## Likely implementation shape for metadata/conventions

Recommended: introduce one source-aware resolver/finalizing convention, e.g. `DynamoResolvedKeyConvention` plus internal descriptor, rather than layering more ad-hoc checks.

Possible descriptor fields:

- `IConventionProperty PartitionKeyProperty`
- `IConventionProperty? SortKeyProperty`
- `DynamoKeyConfigurationSource PartitionSource/SortSource` or raw `ConfigurationSource` plus origin enum (`ProviderAnnotation`, `EfPrimaryKey`, `ConventionName`)
- final ordered key properties

Resolution algorithm outline:

1. For each root non-owned entity type, read:
   - current EF primary key and `IConventionKey.GetConfigurationSource()`
   - provider partition/sort annotations and their `ConfigurationSource`
   - conventional name candidates (`PK`/`PartitionKey`, fallback `Id`, `SK`/`SortKey`) as convention hints only
2. Classify EF PK:
   - none
   - convention-created
   - explicit/data-annotation (`HasKey`, `[Key]`, `[PrimaryKey]`)
3. Classify provider annotations:
   - explicit/data-annotation provider config (`HasPartitionKey`, `HasSortKey`)
   - convention-source annotation/hint
   - none
4. Resolve precedence:
   - Explicit/data-annotation provider key roles + no explicit EF PK: synthesize/replace EF PK if no PK or only convention-created PK.
   - Explicit/data-annotation EF PK + no explicit provider roles: infer provider roles from EF PK order; ignore/override convention-source provider hints.
   - Both explicit provider roles and explicit EF PK: validate exact agreement; do not rewrite explicit EF PK.
   - No explicit inputs: use conventional name hints as today; synthesize convention EF PK and final roles.
5. Validate key shape:
   - final EF PK one or two properties
   - > 2 throws DynamoDB key-shape error
   - sort-key role without partition invalid unless explicit EF PK supplies partition and policy allows it
6. Store final resolved roles:
   - Prefer dedicated internal runtime/model annotation/descriptor to satisfy “no raw annotation runtime inference”.
   - Existing `Dynamo:PartitionKeyPropertyName`/`SortKeyPropertyName` may still be set for public extension compatibility, but runtime should treat them as finalized output, not raw input.
7. Synthesize EF PK only after complete shape computed, and only if current PK is null/convention-created. Never remove/reorder explicit/data-annotation PK.

Potential existing class changes:

- `DynamoKeyAnnotationConvention`: either keep only conventional candidate detection/ambiguity, or fold into new resolver. Must not let convention-source annotations override explicit `HasKey`.
- `DynamoKeyInPrimaryKeyConvention`: likely replace with finalizing resolver or reduce to early convenience only. If kept, ensure final resolver is source of truth.
- `DynamoEntityTypeExtensions`: add accessors for resolved key model or make existing getters consult resolved descriptor first. Avoid runtime re-inference from `FindPrimaryKey()`.
- `DynamoModelValidator`: remove blanket explicit-key rejection; validate resolved key model and conflicts.
- `DynamoConventionSetBuilder`: register resolver after attribute naming and conventional annotation/candidate phase; ensure validation sees final roles.

## Open decisions / risks

- **Partial provider annotations + explicit EF PK.** Need decide if `HasKey(pk, sk)` + `HasPartitionKey(pk)` but no `HasSortKey(sk)` is valid (EF infers sort) or invalid (provider style describes PK-only). Issue says “both styles ... exact agreement”, but ADR validation bullets can be read as provider annotations are hints. Recommend decide before coding tests.
- **`HasSortKey` without `HasPartitionKey` + explicit two-part `HasKey`.** Could be valid if first EF key supplies partition and sort annotation matches second. Current validator rejects. Need policy.
- **`HasPartitionKey(string)` creating shadow properties.** Current string overload only sets annotation. EF `HasKey("PK")` may create/configure shadow key via EF APIs; provider string API currently does not. Decide whether provider API should mimic `Property<string>(...)` creation or require preconfigured property for shadow keys. Issue says preserve EF semantics; likely `HasKey` shadow path must work, provider API shadow path can require property exists unless intentionally changed.
- **Convention-source provider annotations in public metadata.** If resolver overwrites annotations with inferred roles, external callers may see `GetPartitionKeyPropertyName()` work. If descriptor only, existing API may return null for `HasKey`-only models unless accessors are changed.
- **Runtime-only shadow property.** `DynamoResponseShadowPropertyConvention` adds `__executeStatementResponse` runtime-only shadow property. Keep hard rejection if it appears in EF PK/provider roles.
- **Validation before EF base validation.** Some provider checks run before base `ModelValidator`. Shadow/unmapped/type mapping behavior may require base validation first or targeted preflight for better messages.

## Tests to add/update (metadata-only)

Placement per `tests/AGENTS.md`: model-building/conventions/annotations tests in `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/` and `Metadata/Conventions/`. Use `[Fact(Timeout = TestConfiguration.DefaultTimeout)]`.

### New/updated convention tests

- `HasKey_SingleProperty_InferredAsPartitionKey`: `b.HasKey(e => e.Id)` only. Expect model valid, EF PK `[Id]`, resolved partition `Id`, no sort.
- `HasKey_TwoProperties_InferredAsPartitionAndSort`: `b.HasKey(e => new { e.TenantId, e.OrderId })`. Expect partition `TenantId`, sort `OrderId`.
- `HasKey_ThreeProperties_ThrowsDynamoShapeError`: expect targeted “supports only one- or two-part keys” message, not old “do not use HasKey”.
- `HasKey_WithConventionalIdButDifferentExplicitKey_UsesExplicitKey`: entity has `Id` and `CustomId`, `HasKey(CustomId)` only. Ensures convention-source `Id` hint does not win.
- `HasPartitionKey_WithoutHasKey_StillSynthesizesEfPrimaryKey`: preserve existing behavior.
- `HasPartitionKeyAndSortKey_WithoutHasKey_StillSynthesizesCompositeEfPrimaryKey`: preserve existing behavior.
- `HasKey_AndProviderKeys_Matching_IsValid`: explicit composite EF key plus matching provider annotations.
- `HasKey_AndProviderKeys_ReversedPartition_ThrowsMismatch`: `HasKey(TenantId, OrderId)` + `HasPartitionKey(OrderId)`.
- `HasKey_OnePart_AndHasSortKey_ThrowsShapeMismatch`: explicit one-part EF key plus sort annotation.
- `HasKey_TwoPart_AndHasSortKeySecond_NoPartitionAnnotation`: add once policy decided.
- `ExplicitProviderKeys_BeatConventionNames_WhenNoExplicitHasKey`: preserve existing explicit provider override tests.
- `AmbiguousConventionalNames_StillThrow_WhenNoExplicitProviderOrEfKey`: preserve ambiguity behavior.
- `AmbiguousConventionalNames_DoNotThrow_WhenExplicitHasKeyResolvesKey`: if explicit EF PK should override convention ambiguity.

### Shadow key tests

- `HasKey_ShadowStringProperty_IsValidPartitionKey`: `b.Property<string>("PK"); b.HasKey("PK");` Expect valid resolved partition.
- `HasKey_ShadowCompositeProperties_AreValidPartitionAndSortKeys`: `PK`/`SK` shadows with string/numeric/binary supported types.
- `HasPartitionKey_ShadowProperty_IsValid_WhenPropertyConfigured`: update old rejecting tests if provider API supports this.
- `HasPartitionKey_ShadowProperty_WithoutConfiguredProperty`: decide expected behavior: missing property error vs create shadow.
- `ShadowKey_InvalidType_Throws`: e.g. `b.Property<bool>("PK"); b.HasKey("PK")`.
- `RuntimeOnlyProperty_InPrimaryKey_Throws`: configure EF PK on `__executeStatementResponse` or create runtime-only property via test hook if accessible.

### Validation tests

- Inferred `HasKey` keys reuse existing type validation: bool key throws; numeric/string/byte[] pass; converter provider type checked; nullable provider type rejects.
- Inferred `HasKey` keys reuse nullability validation.
- Alternate keys ignored: `HasAlternateKey(e => e.Alternate)` with real primary/provider key; alternate must not affect resolved table key.
- Unique indexes ignored: `HasIndex(e => e.Alternate).IsUnique()` does not affect resolved table key.
- Shared-table with `HasKey`-only on different CLR property names but matching `HasAttributeName("PK"/"SK")` is valid.
- Shared-table with inferred keys but mismatched attribute names/type categories throws existing shared-table messages.
- Derived types cannot introduce conflicting table key; root key shape used.
- Complex/embedded properties named `PK`/`SK` still ignored.
- Data annotations: `[Key]` single-property and `[PrimaryKey(nameof(A), nameof(B))]` map like explicit EF keys; conflicts with provider annotations validate.

## Target validation commands

- Fast metadata/convention suite:
  - `dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj --filter "FullyQualifiedName~Metadata"`
- Narrow during development:
  - `dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj --filter "FullyQualifiedName~DynamoKey"`
  - `dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj --filter "FullyQualifiedName~TableKeySchemaValidationTests"`

## Bottom line

Current provider is annotation-first. It actively rejects `HasKey`, rejects shadow table keys, and runtime/validation read raw key annotations. Issue #253 needs source-aware final key resolution during finalization: provider APIs remain sufficient, EF `HasKey` infers Dynamo roles, both explicit styles validate agreement, convention hints stay lower precedence, shadow EF keys become valid when mapped/scalar, and runtime consumes finalized resolved key metadata only.
