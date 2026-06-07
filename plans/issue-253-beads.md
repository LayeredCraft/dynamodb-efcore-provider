# Issue 253 Bead Breakdown: EF Key Semantics + DynamoDB Table Keys

Use this as source text for `bd create` beads. Planning-only artifact; no actual bead DB changes made here.

Each bead must end with:

1. Run targeted tests listed in bead.
2. Self-review code:
   - inspect `git diff --stat` and `git diff`;
   - verify no unrelated changes;
   - verify error messages are actionable;
   - verify new code reuses existing helpers where possible;
   - verify runtime/query/write code does not re-infer key roles differently from final model.
3. Fix all self-review findings.
4. Re-run targeted tests.
5. Commit bead changes only:
   - `git status --short`
   - `git add <files>`
   - `git commit -m "<message from bead>"`

## Parent Epic

### Bead 253-EPIC — Implement ADR-003 EF key semantics for DynamoDB keys

**Type:** epic\
**Priority:** 1\
**Depends on:** none

**Goal:** Support EF Core key configuration (`HasKey`, `[Key]`, `[PrimaryKey]`, `Id`, `{Entity}Id`) and DynamoDB provider key APIs (`HasPartitionKey`, `HasSortKey`) as equivalent inputs to one finalized DynamoDB table-key model consumed by validation, runtime metadata, query planning, table creation, writes, and docs.

**Acceptance:**

- Provider APIs remain preferred and sufficient.
- EF-native key APIs work for one/two-part table keys.
- Combined EF/provider config validates exact agreement.
- Invalid 3+ key parts fail with DynamoDB shape error.
- Typed mapped shadow keys work; runtime-only keys fail.
- Runtime/query/write/table-definition code consume finalized roles, not separate ad-hoc inference.
- Full unit suite passes; targeted integration smoke passes if touched.

______________________________________________________________________

## Implementation Beads

### Bead 253-01 — Replace split key conventions with final resolver preserving current behavior

**Type:** task\
**Priority:** 1\
**Depends on:** 253-EPIC\
**Commit message:** `Implement finalized DynamoDB key resolver baseline`

**Scope:** Metadata/convention infrastructure only. Preserve current provider/convention behavior first; do not yet enable explicit `HasKey` acceptance except where needed to avoid regressions.

**Files:**

- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoConventionSetBuilder.cs`
- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoTableKeyResolutionConvention.cs` (new)
- `src/EntityFrameworkCore.DynamoDb/Metadata/Internal/DynamoTableKeyResolver.cs` (new)
- `src/EntityFrameworkCore.DynamoDb/Metadata/Internal/DynamoResolvedTableKey.cs` (new if useful)
- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoKeyDiscoveryConvention.cs`
- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoKeyAnnotationConvention.cs`
- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoKeyInPrimaryKeyConvention.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/Conventions/DynamoKeyDiscoveryConventionTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/Conventions/DynamoKeyInPrimaryKeyConventionTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/Conventions/DynamoEntityKeyMappingConventionTests.cs`

**Implementation notes:**

- Add resolver that collects:
  - EF primary key and `IConventionKey.GetConfigurationSource()`;
  - provider partition/sort annotations and annotation configuration sources;
  - conventional candidates: `PK`, `PartitionKey`, fallback `Id`, `SK`, `SortKey`.
- Add finalizing convention:
  - skip owned/derived entity types;
  - call resolver;
  - synthesize/replace EF PK only when current key is absent or convention-source;
  - write final `Dynamo:PartitionKeyPropertyName` and optional `Dynamo:SortKeyPropertyName` annotations.
- Change `DynamoConventionSetBuilder`:
  - stop replacing `KeyDiscoveryConvention`;
  - stop registering `DynamoKeyAnnotationConvention`;
  - stop registering `DynamoKeyInPrimaryKeyConvention` hooks;
  - register new finalizing convention after `DynamoAttributeNamingConventionApplier`.
- Keep current convention semantics:
  - `PK` / `PartitionKey` beat fallback `Id`;
  - `SK` / `SortKey` appended;
  - explicit provider APIs beat conventional names;
  - ambiguous `PK` + `PartitionKey` or `SK` + `SortKey` still throws when unresolved.
- Old convention classes may remain unused for now or be reduced to helper usage; avoid broad deletion unless tests confirm no references.

**Tests:**

- Existing convention tests for provider API synthesis and conventional `PK`/`SK` discovery pass.
- Existing ambiguity tests still pass.
- Existing explicit `HasKey` rejection tests may still pass in this bead if validator unchanged.

**Target test commands:**

```bash
dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj --filter "FullyQualifiedName~DynamoKeyDiscoveryConventionTests|FullyQualifiedName~DynamoKeyInPrimaryKeyConventionTests|FullyQualifiedName~DynamoEntityKeyMappingConventionTests"
```

**Acceptance:**

- Current supported key mapping behavior unchanged.
- Convention set has one final key resolution path.
- No event-driven provider-key-to-EF-PK rewrites remain registered.
- Self-review/fix/commit done.

______________________________________________________________________

### Bead 253-02 — Enable explicit EF `HasKey` inference and conflict validation

**Type:** task\
**Priority:** 1\
**Depends on:** 253-01\
**Commit message:** `Support EF HasKey for DynamoDB table keys`

**Scope:** Make `HasKey(...)` first-class for root DynamoDB entities and validate conflicts precisely.

**Files:**

- `src/EntityFrameworkCore.DynamoDb/Metadata/Internal/DynamoTableKeyResolver.cs`
- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoTableKeyResolutionConvention.cs`
- `src/EntityFrameworkCore.DynamoDb/Infrastructure/Internal/DynamoModelValidator.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/Conventions/DynamoEntityKeyMappingConventionTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/TableKeySchemaValidationTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/TableKeySchemaTests.cs`

**Implementation notes:**

- Remove validator blanket rejection of explicit/data-annotation EF PK.
- Resolver rules:
  - explicit/data EF PK with 1 property => final partition key;
  - explicit/data EF PK with 2 properties => final partition + sort key;
  - explicit/data EF PK with 3+ properties => DynamoDB shape error;
  - explicit provider partition/sort annotations must match EF PK positions when both configured;
  - missing provider role can be inferred from explicit EF PK;
  - convention-source provider hints must not override explicit EF PK.
- Error cases:
  - provider partition != EF PK first property;
  - provider sort != EF PK second property;
  - one-part EF PK + provider sort;
  - same property configured as partition and sort;
  - sort configured and no partition can be resolved.

**Tests to add/update:**

- `HasKey(e => e.Id)` only => partition `Id`, sort null.
- `HasKey(e => new { e.TenantId, e.OrderId })` only => partition `TenantId`, sort `OrderId`.
- `HasKey(A, B, C)` => targeted max-two-parts error.
- Entity with `Id` plus explicit `HasKey(CustomId)` => partition `CustomId`.
- Matching `HasKey` + `HasPartitionKey`/`HasSortKey` valid.
- Partial combined valid:
  - `HasKey(TenantId, OrderId)` + `HasPartitionKey(TenantId)`;
  - `HasKey(TenantId, OrderId)` + `HasSortKey(OrderId)`.
- Mismatches throw targeted errors.
- Ambiguous conventional candidates do not throw if explicit EF key resolves role.

**Target test commands:**

```bash
dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj --filter "FullyQualifiedName~DynamoEntityKeyMappingConventionTests|FullyQualifiedName~TableKeySchemaValidationTests|FullyQualifiedName~TableKeySchemaTests"
```

**Acceptance:**

- Explicit `HasKey` no longer rejected.
- One/two-part EF PKs infer final DynamoDB roles.
- Combined config validates exact agreement.
- Existing provider-only behavior still passes.
- Self-review/fix/commit done.

______________________________________________________________________

### Bead 253-03 — Restore and extend conventional key behavior on top of EF base discovery

**Type:** task\
**Priority:** 1\
**Depends on:** 253-02\
**Commit message:** `Finalize DynamoDB key convention precedence`

**Scope:** Ensure removing custom `KeyDiscoveryConvention` does not change intended DynamoDB naming conventions.

**Files:**

- `src/EntityFrameworkCore.DynamoDb/Metadata/Internal/DynamoTableKeyResolver.cs`
- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoTableKeyResolutionConvention.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/Conventions/DynamoKeyDiscoveryConventionTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/Conventions/DynamoEntityKeyMappingConventionTests.cs`

**Implementation notes:**

- Preserve current convention order when no explicit EF/provider config exists:
  - partition candidates: `PK` / `PartitionKey`, else fallback `Id`;
  - sort candidates: `SK` / `SortKey`;
  - synthesize EF PK `[partition]` or `[partition, sort]`.
- Ensure DynamoDB conventional names beat EF base `Id` discovery only when EF PK is convention-source.
- Keep ambiguity behavior source-aware:
  - unresolved multiple conventional candidates throw;
  - explicit provider role resolves ambiguity;
  - explicit EF PK resolves ambiguity.
- Verify EF `{EntityName}Id` discovery still works when no DynamoDB-specific candidates exist; infer partition from EF convention PK.

**Tests to add/update:**

- `PK` + `Id`, no explicit config => partition `PK`.
- `PartitionKey` + `Id`, no explicit config => partition `PartitionKey`.
- `PK` + `SK`, no explicit config => EF PK `[PK, SK]`.
- `{EntityName}Id` with no Dynamo-specific candidates => partition inferred from EF convention key.
- `PK` + `PartitionKey`, no explicit => ambiguity error.
- `PK` + `PartitionKey`, explicit `HasKey(Custom)` => no ambiguity if role resolved.

**Target test commands:**

```bash
dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj --filter "FullyQualifiedName~DynamoKeyDiscoveryConventionTests|FullyQualifiedName~DynamoEntityKeyMappingConventionTests"
```

**Acceptance:**

- DynamoDB conventional naming behavior preserved.
- EF-native discovery now coexists with DynamoDB convention precedence.
- Self-review/fix/commit done.

______________________________________________________________________

### Bead 253-04 — Support `[Key]` and `[PrimaryKey]` data annotations

**Type:** task\
**Priority:** 1\
**Depends on:** 253-02\
**Commit message:** `Support key data annotations for DynamoDB table keys`

**Scope:** Data-annotation key metadata should behave like explicit EF key metadata.

**Files:**

- `src/EntityFrameworkCore.DynamoDb/Metadata/Internal/DynamoTableKeyResolver.cs`
- `src/EntityFrameworkCore.DynamoDb/Infrastructure/Internal/DynamoModelValidator.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/Conventions/DynamoEntityKeyMappingConventionTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/TableKeySchemaValidationTests.cs`

**Implementation notes:**

- Treat `ConfigurationSource.DataAnnotation` EF PK as explicit constraint.
- Do not rewrite/reorder data-annotation PKs.
- Infer roles from data-annotation PK order.
- Validate conflicts against provider annotations exactly like fluent `HasKey`.

**Tests:**

- `[Key] public string Id { get; set; }` => final partition `Id`.
- `[PrimaryKey(nameof(TenantId), nameof(OrderId))]` => final partition/sort.
- `[Key]` + conflicting `HasPartitionKey` => mismatch error.
- `[PrimaryKey(A,B,C)]` => max-two-parts error.

**Target test commands:**

```bash
dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj --filter "FullyQualifiedName~DynamoEntityKeyMappingConventionTests|FullyQualifiedName~TableKeySchemaValidationTests"
```

**Acceptance:**

- Data annotation key paths match fluent `HasKey` semantics.
- Conflict messages are provider-specific and actionable.
- Self-review/fix/commit done.

______________________________________________________________________

### Bead 253-05 — Allow typed mapped shadow table keys and reject runtime-only keys

**Type:** task\
**Priority:** 1\
**Depends on:** 253-02\
**Commit message:** `Allow mapped shadow DynamoDB table keys`

**Scope:** Implement issue shadow policy across validation and serialization basics.

**Files:**

- `src/EntityFrameworkCore.DynamoDb/Infrastructure/Internal/DynamoModelValidator.cs`
- `src/EntityFrameworkCore.DynamoDb/Storage/DynamoEntityWritePlanFactory.cs`
- `src/EntityFrameworkCore.DynamoDb/Storage/DynamoEntityItemSerializerSource.cs` (only if serializer assumptions need fixes)
- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/TableKeySchemaValidationTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/Conventions/DynamoKeyInPrimaryKeyConventionTests.cs` or replacement resolver tests
- `tests/EntityFrameworkCore.DynamoDb.Tests/Storage/*` new/update shadow write tests

**Implementation notes:**

- Remove validator ban on `property.IsShadowProperty()` for table keys.
- Keep validator ban on `property.IsRuntimeOnly()`.
- Require property to exist: `HasPartitionKey("PK")` without `Property<T>("PK")` remains missing property error.
- Ensure type/nullability checks run for shadow keys.
- Update `DynamoEntityWritePlanFactory`:
  - stop excluding `(shadow && key)` properties;
  - continue excluding runtime-only properties;
  - ensure `SerializeProperty` has serializer for shadow key properties.
- Verify original-value serializers can read shadow key values for update/delete WHERE.

**Tests:**

- `Property<string>("PK"); HasKey("PK")` valid.
- `Property<string>("PK"); Property<string>("SK"); HasKey("PK", "SK")` valid.
- `Property<string>("PK"); HasPartitionKey("PK")` valid.
- `HasPartitionKey("PK")` without `Property<T>("PK")` throws missing property.
- `Property<bool>("PK"); HasKey("PK")` throws unsupported key type.
- nullable shadow key throws required/non-nullable error.
- runtime-only property configured as key throws targeted runtime-only error.
- insert with shadow key serializes `PK`/`SK` attributes.

**Target test commands:**

```bash
dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj --filter "FullyQualifiedName~TableKeySchemaValidationTests|FullyQualifiedName~DynamoKey|FullyQualifiedName~SaveChanges"
```

**Acceptance:**

- Typed mapped shadow table keys validate and serialize.
- Runtime-only keys rejected.
- Missing string-provider key property behavior unchanged.
- Self-review/fix/commit done.

______________________________________________________________________

### Bead 253-06 — Update validator flow for resolved roles and shared-table/index consistency

**Type:** task\
**Priority:** 1\
**Depends on:** 253-02, 253-05\
**Commit message:** `Validate resolved DynamoDB table key model`

**Scope:** Make all validation consume finalized key roles and preserve existing table/index/discriminator checks.

**Files:**

- `src/EntityFrameworkCore.DynamoDb/Infrastructure/Internal/DynamoModelValidator.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/TableKeySchemaValidationTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/SecondaryIndexMetadataTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/DiscriminatorValidationTests.cs`

**Implementation notes:**

- Pre-base checks should not require raw provider partition annotation; they should rely on resolver having synthesized EF PK/final annotations.
- Ensure base EF validator sees keyful roots after resolver finalization.
- Keep/reuse:
  - key property type category checks;
  - key nullability checks;
  - shared table physical attr name/type category consistency;
  - LSI requires resolved table PK+SK;
  - discriminator attr cannot collide with final PK/SK attr names.
- Add helper methods if needed to centralize “final partition property” / “final sort property.”

**Tests:**

- Shared table mixed style valid:
  - entity A uses provider APIs;
  - entity B uses `HasKey`;
  - physical attr names same.
- Shared table inferred attr mismatch throws existing partition/sort attr message.
- Shared table type category mismatch still throws.
- LSI with HasKey-only base table works when base table has PK+SK.
- LSI with HasKey-only PK-only table throws missing table sort key.
- Discriminator collision checks use HasKey-only final key attrs.
- Alternate keys and unique indexes ignored for table-key resolution.

**Target test commands:**

```bash
dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj --filter "FullyQualifiedName~TableKeySchemaValidationTests|FullyQualifiedName~SecondaryIndexMetadataTests|FullyQualifiedName~DiscriminatorValidationTests"
```

**Acceptance:**

- All validation rules operate on finalized key roles.
- Shared-table/index/discriminator behavior unchanged except new EF-key inputs valid.
- Self-review/fix/commit done.

______________________________________________________________________

### Bead 253-07 — Runtime table model helpers and HasKey table-definition support

**Type:** task\
**Priority:** 1\
**Depends on:** 253-06\
**Commit message:** `Build runtime table keys from finalized model roles`

**Scope:** Ensure runtime descriptors and table creation consume finalized key roles.

**Files:**

- `src/EntityFrameworkCore.DynamoDb/Infrastructure/Internal/DynamoModelRuntimeInitializer.cs`
- `src/EntityFrameworkCore.DynamoDb/Metadata/Internal/DynamoRuntimeTableModel.cs`
- `src/EntityFrameworkCore.DynamoDb/Extensions/DynamoModelExtensions.cs`
- `src/EntityFrameworkCore.DynamoDb/Storage/Internal/DynamoTableDefinitionBuilder.cs` (test-driven minimal code only)
- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/SecondaryIndexMetadataTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Storage/DynamoTableDefinitionBuilderTests.cs`

**Implementation notes:**

- Build base-table `DynamoIndexDescriptor` from finalized partition/sort roles.
- Add runtime lookup helper if useful:
  - input: query/write entity type;
  - output: base table descriptor or partition/sort properties for table group.
- Keep GSI/LSI descriptors derived from EF indexes as today.
- Keep table definition builder descriptor-driven; avoid direct model annotation reads.

**Tests:**

- Runtime table model HasKey-only one-part descriptor has partition property from EF PK.
- Runtime table model HasKey-only two-part descriptor has partition/sort from EF PK order.
- Create-table request HasKey-only one-part emits HASH only.
- Create-table request HasKey-only two-part emits HASH/RANGE.
- `HasAttributeName` on HasKey-only keys controls physical schema names.
- Mixed shared table config emits single consistent table schema.

**Target test commands:**

```bash
dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj --filter "FullyQualifiedName~SecondaryIndexMetadataTests|FullyQualifiedName~DynamoTableDefinitionBuilderTests"
```

**Acceptance:**

- Runtime model is canonical source for base table/index key descriptors.
- Table definition works for HasKey-only models.
- Self-review/fix/commit done.

______________________________________________________________________

### Bead 253-08 — Query planning uses finalized key descriptors for base table safety

**Type:** task\
**Priority:** 1\
**Depends on:** 253-07\
**Commit message:** `Use finalized table keys in query planning`

**Scope:** Query planning and SQL generation safety for HasKey-only models.

**Files:**

- `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoSqlTranslatingExpressionVisitor.cs`
- `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoProjectionBindingExpressionVisitor.cs`
- `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryTranslationPostprocessor.cs` (only if helper integration needed)
- `tests/EntityFrameworkCore.DynamoDb.Tests/Query/ScanQueryGuardTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Query/OrderByPartitionKeyValidationTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Query/LimitTranslationTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Query/ToQueryStringTests.cs`

**Implementation notes:**

- `DynamoConstraintExtractionVisitor` and `DynamoAutoIndexSelectionAnalyzer` should remain unchanged unless tests expose descriptor gaps.
- Update base-table `IsEffectivePartitionKey` to use runtime descriptors/final roles instead of raw `ResolveKeyMappedEntityType().GetPartitionKeyProperty()` inference.
- Keep explicit `.WithIndex()` path descriptor-driven.
- Update minimal projection fallback to use final base-table partition property when possible.
- Do not change safety semantics:
  - PK equality/IN required for keyed query;
  - unsafe OR touching key remains unsafe;
  - SK condition only valid for actual sort key;
  - order-by rules unchanged.

**Tests:**

- HasKey-only `Where(e => e.Id == id)` no scan warning/error under scan guard.
- HasKey-only composite `TenantId == x && OrderId == y` safe for `First`/`Single`.
- HasKey-only partition equality + sort range is keyed, not filter-only scan.
- HasKey-only sort-only predicate is scan-like/unsafe.
- HasKey-only unsafe OR touching PK/SK remains unsafe.
- HasKey-only `OrderBy(sort)` valid only with partition equality.
- HasKey-only `OrderBy(nonKey)` rejected.
- `IN` limit: 51 partition values throws max-50; 101 non-key values throws max-100.
- `EF.Property<T>` shadow key equality and sort condition translate as key conditions.

**Target test commands:**

```bash
dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj --filter "FullyQualifiedName~ScanQueryGuardTests|FullyQualifiedName~OrderByPartitionKeyValidationTests|FullyQualifiedName~LimitTranslationTests|FullyQualifiedName~ToQueryStringTests"
```

**Acceptance:**

- Query planner treats HasKey-only and provider-key models equivalently.
- No new ad-hoc key role inference added.
- Self-review/fix/commit done.

______________________________________________________________________

### Bead 253-09 — Find and point-read behavior for EF-key models

**Type:** task\
**Priority:** 2\
**Depends on:** 253-07, 253-08\
**Commit message:** `Support Find with EF key mapped DynamoDB keys`

**Scope:** `Find` / `FindAsync` for one/two-part EF-native keys and shadow keys.

**Files:**

- `src/EntityFrameworkCore.DynamoDb/Infrastructure/*` or query/find files if needed after inspection
- `src/EntityFrameworkCore.DynamoDb/Storage/DynamoPartiqlStatementFactory.cs` if find SQL uses shared builder
- `tests/EntityFrameworkCore.DynamoDb.Tests/Infrastructure/DynamoFindTests.cs`

**Implementation notes:**

- Inspect current `Find` flow before changing. Likely EF primary key already drives key count/type validation; ensure generated statement uses finalized table key attributes and base table, not GSI auto-selection.
- Preserve existing behavior:
  - `Limit = 1`;
  - base table source only;
  - wrong value count/type errors unchanged;
  - tracked entity returned without network call.
- Add shadow key `Find` only if EF supports setting/tracking shadow key values in test setup.

**Tests:**

- HasKey-only one-part `FindAsync([key])` uses base table and `Limit = 1`.
- HasKey-only two-part `FindAsync([pk, sk])` uses key order and `Limit = 1`.
- HasKey-only with `HasAttributeName` uses physical attr names in statement.
- Wrong key count/type errors unchanged.
- Existing provider-key find tests still pass.

**Target test commands:**

```bash
dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj --filter "FullyQualifiedName~DynamoFindTests"
```

**Acceptance:**

- `Find` behavior equivalent across provider-key and EF-key configuration.
- Self-review/fix/commit done.

______________________________________________________________________

### Bead 253-10 — SaveChanges update/delete/transaction identity use finalized table keys

**Type:** task\
**Priority:** 1\
**Depends on:** 253-07, 253-05\
**Commit message:** `Use finalized table keys for writes`

**Scope:** Write WHERE predicates, transaction target identity, and key mutation behavior.

**Files:**

- `src/EntityFrameworkCore.DynamoDb/Storage/DynamoPartiqlStatementFactory.cs`
- `src/EntityFrameworkCore.DynamoDb/Storage/DynamoTransactionTargetIdentityFactory.cs`
- `src/EntityFrameworkCore.DynamoDb/Storage/DynamoEntityWritePlanFactory.cs` if not completed in 253-05
- `src/EntityFrameworkCore.DynamoDb/Extensions/DynamoModelExtensions.cs` if runtime helper needed
- `tests/EntityFrameworkCore.DynamoDb.Tests/Storage/DynamoDerivedSaveChangesTests.cs`
- New/update save tests under `tests/EntityFrameworkCore.DynamoDb.Tests/Storage/`
- Transaction tests under `tests/EntityFrameworkCore.DynamoDb.Tests/Storage/` if existing fixture exists

**Implementation notes:**

- Update/delete should resolve partition/sort through finalized runtime table key descriptor.
- Preserve original-value serializers for WHERE key values.
- Key mutation guard can stay `property.IsPrimaryKey()` because resolver enforces EF PK == final table key; if that invariant ever changes, switch to final role check.
- Concurrency predicates should still exclude primary/final key properties.
- Transaction identity should serialize final partition and optional sort values, including binary/numeric key values.

**Tests:**

- HasKey-only one-part insert includes key attr.
- HasKey-only one-part update/delete WHERE uses key attr and original value.
- HasKey-only two-part update/delete WHERE uses first key as partition, second as sort.
- Matching combined config emits same SQL as provider-only config.
- Shadow key insert/update/delete works if shadow support enabled.
- Key mutation throws existing `NotSupportedException` for either key part.
- Transaction duplicate target identity uses final partition/sort serialized values.

**Target test commands:**

```bash
dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj --filter "FullyQualifiedName~SaveChanges|FullyQualifiedName~DynamoDerivedSaveChangesTests|FullyQualifiedName~Transaction"
```

**Acceptance:**

- Writes consume finalized key roles.
- Provider-key, HasKey-only, mixed config, and shadow key write paths pass tests.
- Self-review/fix/commit done.

______________________________________________________________________

### Bead 253-11 — Value generation and key type mapping regression coverage

**Type:** task\
**Priority:** 2\
**Depends on:** 253-02, 253-06\
**Commit message:** `Cover value generation for EF-mapped DynamoDB keys`

**Scope:** Ensure removing custom key discovery does not regress key value generation/type mapping behavior.

**Files:**

- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoValueGenerationConvention.cs`
- `src/EntityFrameworkCore.DynamoDb/Infrastructure/Internal/DynamoModelValidator.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Infrastructure/DynamoModelValidatorTypeMappingTests.cs`
- Metadata/convention tests as needed

**Implementation notes:**

- `DynamoValueGenerationConvention` currently keys off `FindPrimaryKey()` and should continue working if resolver finalizes EF PK before validation/runtime.
- Confirm string/numeric keys remain application-assigned.
- Confirm single Guid key retains EF client generation behavior.
- Confirm composite keys are application-assigned unless user explicitly configures generation.
- Confirm converter provider types still used for key type category validation.

**Tests:**

- HasKey-only string/numeric key does not get store/generated identity semantics.
- HasKey-only single Guid key keeps existing generation behavior.
- HasKey-only composite with Guid part remains application-assigned unless explicitly configured.
- HasKey-only converter-backed key validates using provider type.
- HasKey-only nullable provider converter still throws existing nullable provider error.

**Target test commands:**

```bash
dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj --filter "FullyQualifiedName~DynamoModelValidatorTypeMappingTests|FullyQualifiedName~ValueGeneration|FullyQualifiedName~DynamoKey"
```

**Acceptance:**

- Key value generation/type validation parity preserved for provider and EF key config.
- Self-review/fix/commit done.

______________________________________________________________________

### Bead 253-12 — Clean up obsolete key convention code and public XML docs

**Type:** task\
**Priority:** 2\
**Depends on:** 253-03, 253-04, 253-05, 253-06\
**Commit message:** `Clean up obsolete DynamoDB key convention code`

**Scope:** Remove dead convention code and update in-code documentation after behavior change works.

**Files:**

- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoKeyDiscoveryConvention.cs`
- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoKeyAnnotationConvention.cs`
- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoKeyInPrimaryKeyConvention.cs`
- `src/EntityFrameworkCore.DynamoDb/Extensions/DynamoEntityTypeBuilderExtensions.cs`
- `src/EntityFrameworkCore.DynamoDb/Extensions/DynamoEntityTypeExtensions.cs`
- `src/EntityFrameworkCore.DynamoDb/Metadata/Internal/DynamoAnnotationNames.cs`
- Test class names/files if old convention-specific names are misleading

**Implementation notes:**

- Delete obsolete classes if no references remain; otherwise mark internal helpers clearly.
- Rename tests from old convention names to resolver-oriented names if practical:
  - e.g. `DynamoTableKeyResolutionConventionTests`.
- Update XML docs:
  - provider APIs preferred/sufficient;
  - EF `HasKey` supported;
  - string overload property existence behavior;
  - shadow mapped key support.
- Run search for old phrases:
  - “do not use HasKey”;
  - “does not infer table keys from HasKey”;
  - “shadow key properties are not supported”.

**Tests:**

- Compile is main signal.
- Run all metadata tests.

**Target test commands:**

```bash
dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj --filter "FullyQualifiedName~Metadata"
```

**Acceptance:**

- No obsolete registered convention remains.
- In-code docs match new semantics.
- Metadata tests pass.
- Self-review/fix/commit done.

______________________________________________________________________

### Bead 253-13 — Documentation and ADR update

**Type:** task\
**Priority:** 2\
**Depends on:** 253-02, 253-05, 253-12\
**Commit message:** `Document EF key semantics for DynamoDB table keys`

**Scope:** Public docs/ADR only.

**Files:**

- `docs/configuration/table-key-mapping.md`
- `docs/modeling/entities-keys.md`
- `docs/limitations.md`
- `docs/diagnostics.md`
- `docs/saving/add-update-delete.md`
- `README.md` if feature summary should mention EF-native key support
- `decisions/ADR-003-ef-key-semantics-for-dynamodb-keys.md`

**Implementation notes:**

- `table-key-mapping.md`:
  - provider APIs remain preferred;
  - EF `HasKey`, `[Key]`, `[PrimaryKey]` supported;
  - one-part EF key => partition;
  - two-part EF key => partition/sort;
  - > 2 parts invalid;
  - combined config must match;
  - string provider overloads require existing property;
  - typed mapped shadow keys allowed, runtime-only rejected.
- `entities-keys.md`:
  - remove “Do not use HasKey” warning;
  - add EF-native examples;
  - update composite-key section.
- `limitations.md`:
  - remove root `HasKey` limitation;
  - keep DynamoDB max two table key parts.
- `diagnostics.md`:
  - replace old root `HasKey` row with mismatch/too-many-parts/missing-property/runtime-only rows.
- `saving/add-update-delete.md`:
  - key mutation wording refers to finalized table key / EF primary key.
- ADR:
  - mark accepted/implemented if maintainers want;
  - resolve shadow conflict: typed mapped shadow allowed, runtime-only rejected;
  - clarify partial provider role + explicit EF PK behavior.

**Tests/checks:**

```bash
grep -R "do not use HasKey\|HasKey.*rejected\|shadow key properties are not supported\|does not infer table keys from HasKey" docs README.md decisions || true
```

**Acceptance:**

- Docs no longer describe old unsupported `HasKey` behavior.
- ADR no longer conflicts with implemented shadow-key policy.
- Self-review/fix/commit done.

______________________________________________________________________

### Bead 253-14 — Targeted integration smoke for EF-key mapping

**Type:** task\
**Priority:** 3\
**Depends on:** 253-08, 253-09, 253-10, 253-13\
**Commit message:** `Add integration smoke for EF key mapped DynamoDB tables`

**Scope:** Integration tests only, if local integration infra supports creating/using test tables for this shape. If integration suite is expensive or environment-gated, keep focused and skip-safe per existing patterns.

**Files:**

- `tests/EntityFrameworkCore.DynamoDb.IntegrationTests/SimpleTable/*`
- `tests/EntityFrameworkCore.DynamoDb.IntegrationTests/PkSkTable/*`
- `tests/EntityFrameworkCore.DynamoDb.IntegrationTests/SaveChangesTable/*`
- Or new `tests/EntityFrameworkCore.DynamoDb.IntegrationTests/EfKeyTable/*` if cleaner.

**Implementation notes:**

- Add minimal models configured with `HasKey` only:
  - one-part key table;
  - two-part key table.
- Exercise real query/write paths:
  - insert item;
  - query by partition;
  - query by partition + sort;
  - update non-key attr;
  - delete.
- Use existing shared infra/table lifecycle patterns.
- Keep tests gated consistently with existing integration tests.

**Target test command:**

```bash
dotnet test tests/EntityFrameworkCore.DynamoDb.IntegrationTests/EntityFrameworkCore.DynamoDb.IntegrationTests.csproj --filter "SimpleTable|PkSkTable|SaveChangesTable|EfKey"
```

**Acceptance:**

- Real DynamoDB/local integration confirms HasKey-only query/write paths.
- Existing integration tests still pass or are unaffected.
- Self-review/fix/commit done.

______________________________________________________________________

### Bead 253-15 — Full regression pass and final cleanup

**Type:** task\
**Priority:** 1\
**Depends on:** 253-01, 253-02, 253-03, 253-04, 253-05, 253-06, 253-07, 253-08, 253-09, 253-10, 253-11, 253-12, 253-13, 253-14\
**Commit message:** `Finalize EF key semantics regression fixes`

**Scope:** Final bug fixes only. No broad refactors unless required by failed tests or review findings.

**Files:** any touched by failed tests/self-review.

**Implementation notes:**

- Run full unit suite.
- Run targeted integration smoke if environment available.
- Search for stale semantics in code/docs/tests:
  - `HasKey` rejection;
  - no inference from `HasKey`;
  - shadow table key unsupported;
  - raw annotation inference in runtime/query/write paths.
- Inspect runtime/query/write paths for divergent key role inference.
- Fix only issues found.

**Commands:**

```bash
dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj

dotnet test tests/EntityFrameworkCore.DynamoDb.IntegrationTests/EntityFrameworkCore.DynamoDb.IntegrationTests.csproj --filter "SimpleTable|PkSkTable|SaveChangesTable|EfKey"

grep -R "do not use HasKey\|does not infer table keys from HasKey\|shadow key properties are not supported" src tests docs decisions README.md || true

grep -R "GetPartitionKeyProperty()\|GetSortKeyProperty()" src/EntityFrameworkCore.DynamoDb -n
```

**Acceptance:**

- Full unit suite passes.
- Integration smoke passes or documented as environment-skipped.
- No stale docs/messages remain except intentional compatibility text.
- Runtime/query/write role consumers use finalized roles or runtime descriptors.
- Final self-review/fix/commit done.

______________________________________________________________________

## Suggested dependency graph

```text
253-EPIC
 └─ 253-01
     ├─ 253-02
     │   ├─ 253-04
     │   ├─ 253-05
     │   │   ├─ 253-06
     │   │   │   ├─ 253-07
     │   │   │   │   ├─ 253-08
     │   │   │   │   ├─ 253-09
     │   │   │   │   └─ 253-10
     │   │   │   └─ 253-11
     │   │   └─ 253-12
     │   └─ 253-03
     └─ 253-13 (after 253-12)
253-14 after 253-08, 253-09, 253-10, 253-13
253-15 after all implementation/docs/integration beads
```

## Notes for bead creation

- Actual `bd create` commands should include dependency links according to graph above.
- Do not combine commits across beads. Each bead should leave repo green and commit its own complete slice.
- If a bead uncovers necessary prerequisite work, create a new bead rather than expanding scope silently.
