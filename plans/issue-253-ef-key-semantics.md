# Issue 253 / ADR-003 Plan: EF Key Semantics + DynamoDB Table Keys

## Bead breakdown

- Full self-contained bead list: [`plans/issue-253-beads.md`](issue-253-beads.md)
- Each bead includes scope, files, implementation notes, tests, acceptance, self-review/fix/commit requirements.

## Context

- Goal: make EF Core key configuration (`HasKey`, `[Key]`, `[PrimaryKey]`, `Id`, `{Entity}Id`) and DynamoDB key APIs (`HasPartitionKey`, `HasSortKey`) converge into one finalized DynamoDB table-key model.
- Current provider replaces EF `KeyDiscoveryConvention` with `DynamoKeyDiscoveryConvention`, adds `DynamoKeyAnnotationConvention`, and uses `DynamoKeyInPrimaryKeyConvention` to rebuild convention-source EF PKs from Dynamo annotations.
- Current validator explicitly rejects explicit EF primary-key config.
- Desired outcome: query/write/runtime/table-definition paths consume same resolved partition/sort key roles regardless of configuration style.

## Approach

- Make EF primary key canonical identity and DynamoDB partition/sort roles finalization output.
- Stop replacing EF `KeyDiscoveryConvention`; keep EF-native discovery for `Id`, `{Entity}Id`, `HasKey`, `[Key]`, `[PrimaryKey]`.
- Replace current split key conventions with one source-aware model-finalizing resolver:
  - raw explicit provider annotations from `HasPartitionKey` / `HasSortKey` are constraints;
  - explicit/data-annotation EF primary key is a constraint;
  - DynamoDB conventional names (`PK`, `PartitionKey`, fallback `Id`, `SK`, `SortKey`) are low-precedence hints;
  - current EF convention-created PK is replaceable;
  - explicit/data-annotation EF PK is never rewritten.
- Use existing `Dynamo:PartitionKeyPropertyName` / `Dynamo:SortKeyPropertyName` annotations as finalized output after resolution. Before resolution, only explicit provider API calls should write them; conventional hints should not be pre-written by a separate convention.
- Build `DynamoRuntimeTableModel` from finalized roles and treat it as canonical runtime metadata for query/table-definition/write paths.
- Keep current query-planning safety rules unchanged; only change where effective key roles come from.
- Allow typed, mapped EF shadow key properties; reject runtime-only properties. `HasPartitionKey("Name")` / `HasSortKey("Name")` should still require a property that already exists or is created separately (for example `Property<string>("Name")`); string key APIs should not invent unknown-type shadow properties.
- Update validator, tests, docs, and ADR to reflect EF key APIs as supported.

## Resolution rules

01. Root, non-owned table entity only. Derived types inherit root/table-mapped resolved key roles.
02. Final key shape must be one or two top-level scalar EF properties.
03. EF PK order is store-significant: first property = partition key, second property = sort key.
04. Three or more EF PK properties: invalid DynamoDB table-key shape.
05. Provider partition annotation, when present, must equal final first EF PK property.
06. Provider sort annotation, when present, must equal final second EF PK property.
07. Explicit/data EF PK + no explicit provider key annotations: infer final roles from EF PK order, ignoring lower-precedence convention hints.
08. Explicit provider key annotations + no explicit/data EF PK: synthesize EF PK from resolved provider/convention roles if current PK is absent or convention-source.
09. Explicit/data EF PK + explicit provider annotations: validate all configured roles match; fill missing unconfigured role from EF PK when shape has two parts.
10. No explicit inputs: choose DynamoDB conventional names (`PK`/`PartitionKey`, fallback `Id`, `SK`/`SortKey`) and synthesize convention EF PK.
11. DynamoDB-specific conventional partition names beat EF convention `Id` only when EF PK is convention-source.
12. Ambiguous conventional names throw only when no explicit EF key or explicit provider role resolves that role.
13. Sort-key-only provider config can be valid only when partition role is resolved from explicit EF PK or convention; otherwise invalid.
14. Alternate keys, unique indexes, and secondary indexes do not participate in table-key resolution.
15. Shadow keys are valid only when EF metadata has typed mapped scalar properties; runtime-only keys are always invalid.

## Files to modify

### Metadata/conventions

- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoConventionSetBuilder.cs`
  - stop replacing EF `KeyDiscoveryConvention`;
  - remove registration of `DynamoKeyAnnotationConvention` and `DynamoKeyInPrimaryKeyConvention`;
  - register new resolver after `DynamoAttributeNamingConventionApplier`.
- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoTableKeyResolutionConvention.cs` (new)
  - model-finalizing convention that calls resolver and writes finalized annotations/EF PK.
- `src/EntityFrameworkCore.DynamoDb/Metadata/Internal/DynamoTableKeyResolver.cs` (new)
  - pure internal resolution helper with inputs, sources, result, mismatch errors.
- `src/EntityFrameworkCore.DynamoDb/Metadata/Internal/DynamoResolvedTableKey.cs` (new, if useful)
  - internal record for partition/sort properties plus origin/provenance during resolution.
- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoKeyDiscoveryConvention.cs`
  - remove as convention replacement or reduce to static name helper moved into resolver.
- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoKeyAnnotationConvention.cs`
  - delete/stop registering or convert into resolver internals.
- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoKeyInPrimaryKeyConvention.cs`
  - delete/stop registering; no event-driven PK rewrites.
- `src/EntityFrameworkCore.DynamoDb/Extensions/DynamoEntityTypeBuilderExtensions.cs`
  - update XML docs; optionally add convention-builder `CanSetPartitionKey`/`CanSetSortKey` helpers if resolver needs them.
- `src/EntityFrameworkCore.DynamoDb/Extensions/DynamoEntityTypeExtensions.cs`
  - update XML docs: getters return finalized key roles after model finalization; keep configuration-source readers.
- `src/EntityFrameworkCore.DynamoDb/Metadata/Internal/DynamoAnnotationNames.cs`
  - update comments; add resolved-key annotation only if final annotations cannot carry needed state.

### Validation/runtime/query/write

- `src/EntityFrameworkCore.DynamoDb/Infrastructure/Internal/DynamoModelValidator.cs`
  - remove explicit EF PK rejection;
  - validate resolved roles, EF PK agreement, type/nullability/shared-table/index/discriminator constraints.
- `src/EntityFrameworkCore.DynamoDb/Infrastructure/Internal/DynamoModelRuntimeInitializer.cs`
  - build base-table source descriptors from finalized roles.
- `src/EntityFrameworkCore.DynamoDb/Metadata/Internal/DynamoRuntimeTableModel.cs`
  - optionally add table-key lookup/helper shape if write paths need direct runtime key access.
- `src/EntityFrameworkCore.DynamoDb/Extensions/DynamoModelExtensions.cs`
  - replace/augment `ResolveKeyMappedEntityType` or add runtime descriptor lookup helper.
- `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoSqlTranslatingExpressionVisitor.cs`
  - base-table `IsEffectivePartitionKey` should consult runtime descriptors/final roles.
- `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoProjectionBindingExpressionVisitor.cs`
  - minimal projection fallback should use key-mapped/final descriptor partition property.
- `src/EntityFrameworkCore.DynamoDb/Storage/DynamoPartiqlStatementFactory.cs`
  - update/delete WHERE key predicates from finalized table-key descriptor.
- `src/EntityFrameworkCore.DynamoDb/Storage/DynamoTransactionTargetIdentityFactory.cs`
  - target identity from finalized table-key descriptor.
- `src/EntityFrameworkCore.DynamoDb/Storage/DynamoEntityWritePlanFactory.cs`
  - include typed shadow key properties in insert/update serializers if shadow keys allowed.
- `src/EntityFrameworkCore.DynamoDb/Storage/Internal/DynamoTableDefinitionBuilder.cs`
  - likely no logic change; add tests proving runtime descriptors from `HasKey` drive schema.

### Tests/docs

- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/Conventions/DynamoEntityKeyMappingConventionTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/Conventions/DynamoKeyDiscoveryConventionTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/Conventions/DynamoKeyInPrimaryKeyConventionTests.cs` (rename/replace around resolver)
- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/TableKeySchemaTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/TableKeySchemaValidationTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/SecondaryIndexMetadataTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Storage/DynamoTableDefinitionBuilderTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Storage/DynamoDerivedSaveChangesTests.cs` plus new HasKey write tests.
- `tests/EntityFrameworkCore.DynamoDb.Tests/Infrastructure/DynamoFindTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Query/ScanQueryGuardTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Query/OrderByPartitionKeyValidationTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Query/LimitTranslationTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.Tests/Query/ToQueryStringTests.cs`
- `tests/EntityFrameworkCore.DynamoDb.IntegrationTests/**` targeted smoke for HasKey-only query/write if integration coverage desired.
- `docs/configuration/table-key-mapping.md`
- `docs/modeling/entities-keys.md`
- `docs/limitations.md`
- `docs/diagnostics.md`
- `docs/saving/add-update-delete.md`
- `README.md` (only if examples/feature summary need EF-native key mention)
- `decisions/ADR-003-ef-key-semantics-for-dynamodb-keys.md`

## Reuse

- Existing annotation accessors in `src/EntityFrameworkCore.DynamoDb/Extensions/DynamoEntityTypeExtensions.cs`; they already expose configuration-source readers for partition/sort annotations.
- Existing fluent APIs in `src/EntityFrameworkCore.DynamoDb/Extensions/DynamoEntityTypeBuilderExtensions.cs`; string overloads currently only set annotations, lambda overloads call `Property(...)` then set annotations.
- Existing key-shape/type/shared-table validation logic in `DynamoModelValidator`, especially configured property existence, type category, nullability, shared-table key schema, LSI/GSI validation, discriminator-key collision checks.
- Existing runtime table/source descriptors in `DynamoRuntimeTableModel` and `DynamoModelRuntimeInitializer`; descriptors already carry effective partition/sort properties for base table/GSI/LSI query planning.
- Existing query constraint/index-selection machinery in `DynamoConstraintExtractionVisitor`, `DynamoAutoIndexSelectionAnalyzer`, and `DynamoQueryTranslationPostprocessor`; these already operate on `DynamoIndexDescriptor` and attribute names.
- Existing table-definition flow in `DynamoTableDefinitionBuilder`; it already consumes runtime source descriptors.
- Existing write key extraction in `DynamoPartiqlStatementFactory` and `DynamoTransactionTargetIdentityFactory`, plus serializer plans in `DynamoEntityWritePlanFactory`.

## Steps

### 1. Metadata/convention refactor

- [ ] Keep EF base `KeyDiscoveryConvention` by removing `conventionSet.Replace<KeyDiscoveryConvention>(new DynamoKeyDiscoveryConvention(...))`.
- [ ] Remove `DynamoKeyAnnotationConvention` registration so conventional hints are not written before source-aware resolution.
- [ ] Remove `DynamoKeyInPrimaryKeyConvention` registration so annotation/property-added events no longer rewrite PKs early.
- [ ] Add `DynamoTableKeyResolver`:
  - collect EF primary key + `ConfigurationSource`;
  - collect raw provider partition/sort annotations + `ConfigurationSource`;
  - collect DynamoDB conventional candidates with ambiguity state;
  - compute final partition/sort properties and desired EF PK shape;
  - produce targeted errors for missing key, >2 parts, conflicting first/second role, duplicate role, missing configured property, runtime-only key.
- [ ] Add `DynamoTableKeyResolutionConvention : IModelFinalizingConvention`:
  - skip owned/derived types;
  - call resolver;
  - if resolver needs EF PK synthesis/replacement, call `entityTypeBuilder.PrimaryKey(...)` only when current key is null or convention-source;
  - write final `PartitionKeyPropertyName` / `SortKeyPropertyName` annotations after resolution;
  - never rewrite explicit/data-annotation EF PK.
- [ ] Preserve current conventional behavior:
  - `PK`/`PartitionKey` preferred over fallback `Id`;
  - `SK`/`SortKey` appended as sort key;
  - explicit provider APIs beat convention names;
  - ambiguous convention names throw unless explicit EF/provider config resolves role.
- [ ] Update extension XML docs to say provider key APIs are preferred but `HasKey` / data annotations are supported.

### 2. Validator changes

- [ ] Remove `ValidateRootEntityDoesNotUseExplicitPrimaryKeyConfiguration` call and method.
- [ ] Rewrite `ValidateRootEntityHasPartitionKey` message/rule to require finalized partition role, not raw provider annotation.
- [ ] Rewrite `ValidateSortKeyHasResolvablePartitionKey` or fold into resolver/validated final role checks.
- [ ] Change configured-key property support:
  - keep runtime-only rejection;
  - allow shadow properties when typed, mapped, scalar, non-nullable, and key-compatible;
  - keep missing property error for `HasPartitionKey("Name")` / `HasSortKey("Name")` when property was not configured.
- [ ] Keep `ValidateKeyPropertyNames` but make message reflect final resolved table key vs EF PK mismatch; support matching explicit `HasKey`.
- [ ] Reuse existing type/nullability/shared-table/discriminator/secondary-index validators after final annotations are present.
- [ ] Ensure base EF validation still sees an EF primary key for every valid root entity.

### 3. Runtime model and table definition

- [ ] Ensure `DynamoModelRuntimeInitializer.BuildSourceDescriptors` uses finalized roles to create base-table `DynamoIndexDescriptor`.
- [ ] Add runtime helper if needed, e.g. `GetBaseTableDescriptor(IEntityType)` / `GetResolvedTableKey(IEntityType)`, backed by `DynamoRuntimeTableModel`.
- [ ] Keep GSI/LSI descriptor generation unchanged.
- [ ] Keep `DynamoTableDefinitionBuilder` descriptor-driven; add HasKey-only schema tests.
- [ ] Keep shared-table source signature checks by physical attribute name/type category.

### 4. Query planning/safety

- [ ] Leave `DynamoConstraintExtractionVisitor` logic unchanged; it already consumes candidate descriptors.
- [ ] Leave `DynamoAutoIndexSelectionAnalyzer` logic unchanged; it already gates by descriptor PK/SK attrs.
- [ ] Leave scan classification / `First` / `Single` / `OrderBy` safety rules unchanged.
- [ ] Update `DynamoSqlTranslatingExpressionVisitor.IsEffectivePartitionKey` base-table branch to use runtime descriptor/final roles, not raw annotation inference.
- [ ] Update `DynamoProjectionBindingExpressionVisitor.EnsureNonEmptyProjectionForClientOnlyProjection` to prefer finalized base-table partition property through key-mapped/runtime descriptor.
- [ ] Add HasKey-only tests proving partition equality/IN, sort-key ranges, ordering, `First`, and `Single` safety use finalized roles.

### 5. Write/save/transaction paths

- [ ] Update `DynamoPartiqlStatementFactory` update/delete key predicates to use finalized table-key descriptor, preserving original-value serializers.
- [ ] Update `DynamoTransactionTargetIdentityFactory` to use finalized table-key descriptor.
- [ ] Keep key mutation rejection tied to `IsPrimaryKey()` if resolver guarantees EF PK == final table key; otherwise switch to finalized key role check.
- [ ] Update `DynamoEntityWritePlanFactory` to serialize typed shadow key properties; remove shadow-key-specific serializer error.
- [ ] Confirm concurrency-token predicates still skip primary key/final table key properties.

### 6. Tests and docs

- [ ] Update old tests that assert explicit `HasKey` rejection to assert valid inference or targeted mismatch errors.
- [ ] Add metadata, validation, runtime, query, write, table-definition, data-annotation, and shadow-key tests listed below.
- [ ] Update docs and ADR as listed below.

### Completed discovery

- [x] Inspect current key conventions.
  - `DynamoConventionSetBuilder` replaces EF key discovery and wires model-finalizing annotation convention plus annotation/property-change PK rewrite convention.
  - `DynamoKeyDiscoveryConvention` chooses `PK`/`PartitionKey` over fallback `Id`, appends `SK`/`SortKey`, skips shadow/runtime-only, and does not set annotations.
  - `DynamoKeyAnnotationConvention` finalizes raw partition/sort annotations from conventional names only; it explicitly does not infer from `HasKey`.
  - `DynamoKeyInPrimaryKeyConvention` rebuilds EF PK only when current PK is convention-source, stands down for explicit/data-annotation EF PKs.
- [x] Inspect current key extension APIs.
  - Public `HasPartitionKey(string)` / `HasSortKey(string)` set raw entity annotations and do not ensure properties exist.
  - Lambda overloads ensure selected property participates in model before setting raw annotation.
  - Convention extension APIs have `SetPartitionKeyPropertyName(..., fromDataAnnotation)` and configuration-source readers, but no `CanSetPartitionKey` helpers.
- [x] Inspect validator and runtime table model assumptions.
  - `DynamoModelValidator.Validate` runs pre-base checks first: no FKs/navigation, explicit EF PK rejection, partition-key presence, sort-without-partition, configured property existence/runtime-only/shadow checks.
  - `ValidateRootEntityHasPartitionKey` currently errors when no partition annotation exists, even if EF PK exists; message says provider does not infer table keys from `HasKey` / `[Key]`.
  - `ValidateKeyPropertyNames` requires EF PK exactly equal `[PartitionKey]` or `[PartitionKey, SortKey]` after raw annotations exist.
  - Type/nullability/shared-table/index/discriminator validators already consume `GetPartitionKeyProperty()` / `GetSortKeyProperty()` and can be reused after final annotations are written.
  - `DynamoModelRuntimeInitializer` builds `DynamoRuntimeTableModel` after validation and gets base-table keys from `ResolveKeyMappedEntityType().GetPartitionKeyProperty()` / `GetSortKeyProperty()`.
  - `DynamoRuntimeTableModel` already gives query planner final source descriptors; missing piece is guaranteeing descriptors are built from resolved key roles.
- [x] Inspect query planner/write/table-definition hotspots that read raw key annotations.
  - Query postprocessor, constraint extraction, auto-index selection, SQL generator, and table definition are mostly descriptor-driven.
  - Base-table `IsEffectivePartitionKey`, minimal projection fallback, write WHERE predicates, transaction identity, and write-plan shadow exclusion need updates.
- [x] Inspect docs/ADR update needs.
  - Docs currently state `HasKey` / `[Key]` rejected and shadow keys invalid; ADR shadow policy conflicts with issue shadow support.

## Test additions and updates

### Metadata/resolution tests

- Add/replace tests for `HasKey(e => e.Id)` only:
  - EF PK `[Id]` remains;
  - finalized partition key `Id`;
  - finalized sort key null.
- Add `HasKey(e => new { e.TenantId, e.OrderId })` only:
  - EF PK order preserved;
  - partition `TenantId`, sort `OrderId`.
- Update three-part explicit `HasKey(A, B, C)` test:
  - expect targeted DynamoDB shape error, not `HasKey` rejection.
- Add explicit EF key beating convention hints:
  - entity has `Id` and `CustomId`, config `HasKey(CustomId)`;
  - finalized partition `CustomId`.
- Add conventional DynamoDB name beating EF convention key:
  - entity has `Id` and `PK`, no explicit config;
  - final EF PK/partition is `PK`, not `Id`.
- Preserve provider-only synthesis tests:
  - `HasPartitionKey(CustomPk)` => EF PK `[CustomPk]`;
  - `HasPartitionKey(CustomPk)` + `HasSortKey(CustomSk)` => EF PK `[CustomPk, CustomSk]`.
- Add combined matching config:
  - `HasKey(TenantId, OrderId)` + matching `HasPartitionKey(TenantId)` + `HasSortKey(OrderId)` is valid.
- Add partial combined config:
  - `HasKey(TenantId, OrderId)` + `HasPartitionKey(TenantId)` infers sort `OrderId`.
  - `HasKey(TenantId, OrderId)` + `HasSortKey(OrderId)` infers partition `TenantId`.
- Add mismatch tests:
  - provider partition != first EF key;
  - provider sort != second EF key;
  - one-part EF key + provider sort;
  - provider partition/sort same property;
  - provider sort with no resolvable partition.
- Add ambiguity tests:
  - `PK` + `PartitionKey` no explicit config still throws;
  - ambiguity ignored when explicit EF key resolves role;
  - ambiguity ignored when explicit provider role resolves role.
- Add data annotation tests:
  - `[Key]` single property infers partition;
  - `[PrimaryKey(nameof(TenantId), nameof(OrderId))]` infers partition/sort;
  - data annotation + conflicting provider annotation throws mismatch.
- Add ignored-source tests:
  - `HasAlternateKey` ignored;
  - unique index ignored;
  - secondary indexes still configured only via provider APIs.

### Shadow/runtime-only tests

- Add typed shadow single key:
  - `b.Property<string>("PK"); b.HasKey("PK");` valid; finalized partition `PK`.
- Add typed shadow composite key:
  - `b.Property<string>("PK"); b.Property<string>("SK"); b.HasKey("PK", "SK");` valid.
- Add provider API with preconfigured shadow property:
  - `Property<string>("PK"); HasPartitionKey("PK")` valid.
- Add provider API without property:
  - `HasPartitionKey("PK")` without `Property<T>("PK")` throws missing property.
- Update old shadow rejection tests to match allowed typed shadow policy.
- Add runtime-only key rejection:
  - key annotation/EF key pointing at provider runtime-only property throws targeted error.
- Add shadow invalid type/nullability tests reuse current bool/nullable provider-type messages.

### Validation/shared table tests

- Add shared table with HasKey-only entities using different CLR names but same `HasAttributeName("PK"/"SK")`: valid.
- Add shared table mixed config style:
  - one entity provider APIs, one entity `HasKey`, same physical schema: valid.
- Add shared table inferred key attr mismatch/type category mismatch: existing errors still throw.
- Add derived type tests:
  - derived type uses root/table-mapped final key roles;
  - derived type cannot introduce conflicting root table key.
- Add LSI tests with HasKey-only base table:
  - LSI sees resolved table partition/sort keys;
  - LSI without resolved sort key still invalid.
- Add value generation smoke:
  - string/numeric HasKey-only key remains application-assigned;
  - Guid single HasKey-only key keeps EF client-side generation behavior.

### Runtime/table definition tests

- Add `DynamoRuntimeTableModel` assertions for HasKey-only:
  - base table descriptor partition/sort properties are inferred from EF PK.
- Add table-definition tests:
  - one-part HasKey => `HASH` only;
  - two-part HasKey => `HASH` + `RANGE` in EF PK order;
  - `HasAttributeName` on HasKey-only keys drives physical key schema;
  - shared table mixed config emits one consistent schema.

### Query tests

- Add HasKey-only PK equality test:
  - `Where(e => e.Id == id)` is keyed, no scan warning/error when scan guard active.
- Add HasKey-only composite equality:
  - `TenantId == x && OrderId == y` qualifies for safe `First`/`Single` path.
- Add HasKey-only sort range:
  - partition equality + sort range recognized as sort-key condition, not filter-only scan.
- Add HasKey-only missing partition:
  - sort-only predicate remains scan-like/unsafe.
- Add HasKey-only unsafe OR:
  - OR touching partition/sort key remains unsafe.
- Add HasKey-only ordering:
  - `OrderBy(sort)` valid only with partition equality;
  - `OrderBy(nonKey)` rejected;
  - partition `IN` + sort ordering still follows current multi-partition rules.
- Add `Contains`/`IN` limits:
  - 51 partition-key values throws max-50 message;
  - 101 non-key values throws max-100 message.
- Add `EF.Property<T>` shadow key query tests for partition equality and sort condition.
- Keep GSI/LSI query tests unchanged except add one mixed HasKey/provider base-table setup to prove descriptor source compatibility.

### Write/find tests

- Add `Find`/`FindAsync` HasKey-only tests:
  - one-part key builds base-table lookup and `Limit = 1`;
  - two-part key uses key order and `Limit = 1`;
  - wrong value count/type errors unchanged.
- Add SaveChanges insert/update/delete HasKey-only tests:
  - insert includes key attributes;
  - update/delete WHERE uses finalized partition and optional sort attributes with original values;
  - combined matching config emits same SQL as provider-only config.
- Add shadow key write test if shadow keys allowed:
  - insert serializes shadow key attributes;
  - update/delete WHERE can serialize original shadow key values.
- Add transaction duplicate-target test:
  - HasKey-only composite key identity uses finalized partition/sort serialized values.

### Docs/ADR updates

- Update `docs/configuration/table-key-mapping.md`:
  - precedence now includes EF key APIs;
  - provider APIs preferred, not exclusive;
  - `HasKey`, `[Key]`, `[PrimaryKey]` examples;
  - conflict examples and errors;
  - shadow key policy.
- Update `docs/modeling/entities-keys.md`:
  - remove “Do not use HasKey” warning;
  - add EF-native mapping section;
  - update composite-key guidance.
- Update `docs/limitations.md`:
  - remove root `HasKey` limitation;
  - keep DynamoDB max two key parts.
- Update `docs/diagnostics.md`:
  - replace “Root entity uses HasKey” row with mismatch/too-many-key-parts/missing-key rows.
- Update `docs/saving/add-update-delete.md`:
  - key mutation text says finalized table key/EF PK cannot be modified.
- Update ADR-003:
  - mark accepted/implemented if appropriate;
  - resolve shadow policy to typed mapped shadow allowed, runtime-only rejected;
  - clarify partial provider annotation + explicit EF PK behavior.

## Verification

- Run focused metadata/convention tests:
  - `dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj --filter "FullyQualifiedName~Metadata"`
  - `dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj --filter "FullyQualifiedName~DynamoKey|FullyQualifiedName~TableKeySchema"`
- Run runtime/query/write focused tests:
  - `dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj --filter "FullyQualifiedName~DynamoTableDefinitionBuilderTests|FullyQualifiedName~DynamoFindTests|FullyQualifiedName~ScanQueryGuardTests|FullyQualifiedName~OrderByPartitionKeyValidationTests|FullyQualifiedName~LimitTranslationTests"`
  - `dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj --filter "FullyQualifiedName~SaveChanges|FullyQualifiedName~Transaction"`
- Run full unit suite:
  - `dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj`
- Run targeted integration smoke if write/query/table lifecycle changed broadly:
  - `dotnet test tests/EntityFrameworkCore.DynamoDb.IntegrationTests/EntityFrameworkCore.DynamoDb.IntegrationTests.csproj --filter "SimpleTable|PkSkTable|SaveChangesTable"`
- Manual checks:
  - `ToQueryString()` for HasKey-only one/two-part models shows expected key predicates and no index drift.
  - Create-table request for HasKey-only model uses expected HASH/RANGE names and types.
  - `git status --short` clean except intended implementation/docs/test files.

## Decisions baked into plan

- Typed mapped shadow EF key properties allowed; runtime-only properties rejected.
- `HasPartitionKey(string)` / `HasSortKey(string)` do not create properties by themselves; use `Property<T>("Name")` for shadow keys.
- Partial provider roles plus explicit EF PK are allowed when configured roles match the corresponding EF PK positions; missing provider role is inferred from EF PK.
- Existing key annotation names become finalized output after resolver; `DynamoRuntimeTableModel` is canonical runtime key model.
