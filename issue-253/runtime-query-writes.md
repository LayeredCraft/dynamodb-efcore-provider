# Issue #253 runtime/query/writes context — ADR-003 key roles

Scope from request: runtime paths consume finalized resolved key metadata only; writes use finalized roles; query key-condition recognition uses finalized roles. No source edits made.

## Story target from ADR-003

`decisions/ADR-003-ef-key-semantics-for-dynamodb-keys.md` says runtime must not decide roles from API origin:

- both `HasPartitionKey`/`HasSortKey` and EF `HasKey` become equivalent after finalization;
- model finalization resolves one table-key shape: partition property, optional sort property, provenance, finalized order;
- runtime write/query/table-generation code consumes only resolved roles after finalization;
- runtime should not re-infer from raw annotations or primary-key order.

## Current key metadata shape

### Raw annotations still source of truth

- `src/EntityFrameworkCore.DynamoDb/Metadata/Internal/DynamoAnnotationNames.cs`
  - `PartitionKeyPropertyName`, `SortKeyPropertyName` raw entity annotations.
  - `RuntimeTableModel` runtime annotation stores compiled table/index descriptors.
- `src/EntityFrameworkCore.DynamoDb/Extensions/DynamoEntityTypeExtensions.cs`
  - `GetPartitionKeyPropertyName()` / `GetSortKeyPropertyName()` read raw annotation values.
  - `GetPartitionKeyProperty()` / `GetSortKeyProperty()` find property by those names.

### Current conventions contradict ADR-003 HasKey support

- `DynamoKeyAnnotationConvention` says EF primary keys are only validated; `HasKey(...)` is not used to infer DynamoDB roles (`src/.../DynamoKeyAnnotationConvention.cs:18-20`).
- `DynamoModelValidator.Validate()` rejects explicit/data-annotation primary key before base validation (`src/.../DynamoModelValidator.cs:22-27`, `161-179`).
- `ValidateRootEntityHasPartitionKey()` message says provider does not infer table keys from `HasKey(...)` or `[Key]` (`src/.../DynamoModelValidator.cs:236-254`).
- Existing tests assert rejection:
  - `tests/.../DynamoEntityKeyMappingConventionTests.cs:75-85` explicit two-part `HasKey` rejected.
  - `tests/.../DynamoEntityKeyMappingConventionTests.cs:145-155` three-part `HasKey` rejected.
- Current provider-key synthesis exists only from annotations:
  - `DynamoKeyInPrimaryKeyConvention.ProcessPrimaryKey()` preserves explicit keys and rebuilds convention keys only from `PartitionKeyPropertyName`/`SortKeyPropertyName` (`src/.../DynamoKeyInPrimaryKeyConvention.cs:102-143`).
  - Existing positive tests: `DynamoKeyInPrimaryKeyConventionTests.cs:54-67`, `97-110`.

Implication for this issue: runtime work depends on upstream resolver/finalizer producing finalized resolved roles. Current runtime can use final annotations if resolver writes them, but raw-annotation readers remain leaky source names.

## Runtime metadata paths

### Runtime model builder

File: `src/EntityFrameworkCore.DynamoDb/Infrastructure/Internal/DynamoModelRuntimeInitializer.cs`

Key methods:

- `InitializeModel(...)` attaches runtime table model after validation, skipping prevalidation (`22-34`).
- `BuildRuntimeTableModel(...)` groups root entity types by table group and builds source descriptors (`47-84`).
- `BuildSourceDescriptors(...)` uses `entityType.ResolveKeyMappedEntityType()`, then `GetPartitionKeyProperty()` / `GetSortKeyProperty()` to create base-table `DynamoIndexDescriptor` (`87-105`).
- GSI and LSI descriptors come from EF indexes: GSI `index.Properties[0/1]` (`172-186`), LSI table partition key + index property (`189-205`).

Current behavior:

```csharp
var sourceEntityType = entityType.ResolveKeyMappedEntityType();
var partitionKeyProperty = sourceEntityType.GetPartitionKeyProperty() ?? throw ...;
new DynamoIndexDescriptor(null, Table, null, partitionKeyProperty, sourceEntityType.GetSortKeyProperty(), All)
```

Gap vs story:

- Base-table descriptor is built from raw annotation readers, not explicit resolved-key object.
- `DynamoRuntimeTableModel` has no separate resolved table-key descriptor/provenance; only `DynamoIndexDescriptor` has partition/sort properties (`src/.../DynamoRuntimeTableModel.cs:5-23`).
- Shared-table source compatibility compares attribute names/type categories on descriptors (`DynamoModelRuntimeInitializer.cs:265-282`), good once descriptors use resolved roles.

Likely change:

- Add/attach finalized resolved key roles in runtime metadata or make `DynamoRuntimeTableModel` base table source canonical resolved key.
- `BuildSourceDescriptors()` should consume resolver output, not call raw `GetPartitionKeyProperty()` unless those extension methods are changed to read finalized roles.
- Preserve derived/shared-table behavior from `ResolveKeyMappedEntityType()` (`DynamoModelExtensions.cs:69-80`, `92-103`) or replace with resolved root/table-key lookup.

### Runtime table model consumers

File: `src/EntityFrameworkCore.DynamoDb/Extensions/DynamoModelExtensions.cs`

- `GetDynamoRuntimeTableModel()` reads model runtime annotation (`9-14`).
- `GetTableGroupName()` uses runtime table-group annotation if present (`84-90`).
- `ResolveKeyMappedEntityType()` walks table-mapped type/base types and checks `GetPartitionKeyProperty()` (`69-80`, `92-103`).

Gap:

- `ResolveKeyMappedEntityType()` uses raw annotation presence to decide key-owner type. If resolved roles move elsewhere, this method must change or be avoided by writes/runtime.

## Table definition path

File: `src/EntityFrameworkCore.DynamoDb/Storage/Internal/DynamoTableDefinitionBuilder.cs`

Current behavior:

- Public entry: `BuildCreateTableRequests(DynamoRuntimeTableModel)` consumes runtime model only (`24-31`).
- `BuildCreateTableRequest(DynamoTableDescriptor)` gets distinct sources, picks `Kind == Table` as base source, emits key schema from descriptor (`97-120`).
- `BuildKeySchema(DynamoIndexDescriptor)` emits `HASH` for descriptor partition property and optional `RANGE` for sort property (`203-211`).
- Attribute definitions come from descriptor properties and type mapping (`104-108`, `230+`).
- Existing tests verify runtime model -> table key schema:
  - `DynamoTableDefinitionBuilderTests.cs:15-36` PK-only emits HASH.
  - `DynamoTableDefinitionBuilderTests.cs:39-60` PK+SK/GSI/LSI emits expected schema.

Good fit:

- Table creation already consumes `DynamoRuntimeTableModel`, not live entity annotations directly.

Gap:

- Runtime model itself is built from raw annotation readers. Once runtime model base descriptor is sourced from finalized roles, table definition likely needs minimal/no change.

Tests to add:

- `BuildCreateTableRequests_HasKeyOnlySinglePart_UsesEfKeyAsHash`.
- `BuildCreateTableRequests_HasKeyOnlyTwoPart_UsesEfKeyOrderAsHashRange`.
- `BuildCreateTableRequests_HasKeyOnlyWithAttributeNames_UsesMappedAttributeNames`.
- Shared table: different CLR key names, same physical attr names, one configured via `HasKey`, one via `HasPartitionKey`/`HasSortKey`, request has one consistent schema.

## Writes path

### Update/delete statements

File: `src/EntityFrameworkCore.DynamoDb/Storage/DynamoPartiqlStatementFactory.cs`

Current behavior:

- `BuildModifiedUpdateStatement(...)` uses table group name and rejects modified `property.IsPrimaryKey()` (`21-40`).
- `BuildDeleteStatement(...)` resolves key entity type, reads partition/sort via `GetPartitionKeyProperty()`/`GetSortKeyProperty()`, serializes original values, emits `WHERE pk = ? [AND sk = ?]` (`91-130`).
- `FinalizeUpdateStatement(...)` same for update `WHERE`, using original values (`166-215`).
- Existing derived write tests verify base key attributes in update/delete WHERE:
  - `DynamoDerivedSaveChangesTests.cs:43-65`, `69-90`.
  - Model config uses provider annotations and attr names at `178-189`.

Gaps vs story:

- Update/delete key predicates are resolved from raw annotations through `ResolveKeyMappedEntityType()` + `GetPartitionKeyProperty()`, not finalized resolved roles.
- Key mutation guard uses `property.IsPrimaryKey()`, not resolved table-key roles. ADR says EF PK and resolved Dynamo key must agree, so this may remain equivalent; but issue wording says writes use finalized roles, so safer to test/possibly switch guard to resolved key roles.
- Insert path serializes whole item; no WHERE key roles. `DynamoSaveChangesPlanner` removes sparse GSI null key attrs by scanning `entry.EntityType.GetIndexes()` (`DynamoSaveChangesPlanner.cs:46-65`), unrelated to base table roles.

### Transaction target identity

File: `src/EntityFrameworkCore.DynamoDb/Storage/DynamoTransactionTargetIdentityFactory.cs`

Current behavior:

- `Create(...)` resolves key entity type, reads partition/sort via `GetPartitionKeyProperty()`/`GetSortKeyProperty()`, serializes original values into comparison identity (`12-28`).

Gap:

- Same raw annotation dependency as statements.

Likely change:

- Add central runtime/table-key accessor, e.g. `DynamoRuntimeTableModel.GetTableKey(entityType)` or `entityType.GetResolvedDynamoTableKey()`; use in:
  - `DynamoPartiqlStatementFactory.BuildDeleteStatement`
  - `DynamoPartiqlStatementFactory.FinalizeUpdateStatement`
  - `DynamoTransactionTargetIdentityFactory.Create`
  - maybe `DynamoPartiqlStatementFactory` key-mutation/concurrency skip checks.
- Preserve original-value serializers for update/delete/transaction identity.

Tests to add:

- SaveChanges update/delete for entity configured only with `HasKey(e => e.Id)` uses `WHERE "id_attr" = ?`.
- SaveChanges update/delete for `HasKey(e => new { e.TenantId, e.OrderId })` uses first key as partition attr, second as sort attr, original values.
- Combined config (`HasKey` + matching provider keys) emits same WHERE.
- Mismatched config never reaches writes; validation test covers.
- Key mutation: modifying first/second resolved key property throws current key mutation error.
- Transaction batching duplicate target identity uses finalized roles for HasKey-only two-part key.

## Query translation / key-condition paths

### Candidate descriptors and effective source

File: `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryTranslationPostprocessor.cs`

Current behavior:

- After projection/discriminator finalization, pre-fetches candidates from `Model.GetDynamoRuntimeTableModel()` (`57-69`).
- Runs `DynamoConstraintExtractionVisitor(candidates)` (`71-76`).
- Analyzer chooses selected index; postprocessor writes selected source and effective keys to `SelectExpression` (`97-113`).
- `ClassifyScanQuery(...)` uses `selectExpression.EffectivePartitionKeyPropertyNames` and effective sort key attr (`186-228`).
- `ResolveCandidateDescriptors(...)` scopes runtime descriptors by table + query entity type (`261-295`).
- `ResolveEffectivePartitionKeyPropertyNames(...)` and `ResolveEffectiveSortKeyAttributeName(...)` read active descriptor attr names (`303-353`).
- ORDER BY/First/Single validation use effective partition/sort attr names after source selection (`384-457`, `480-595`).

Good fit:

- Query postprocessor already works from runtime descriptors after model runtime init.
- If `DynamoRuntimeTableModel` descriptors are final resolved roles, key-condition recognition mostly follows story.

### Constraint extraction

File: `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoConstraintExtractionVisitor.cs`

Current behavior:

- Constructor derives PK/SK attribute-name sets from candidate descriptors (`56-66`).
- Extracts equality/IN constraints by attribute name, SK conditions only for descriptor sort-key attrs (`76-95`, `347-367`, `374-430`, `488-507`).
- Handles cross-role attrs across indexes; non-key attrs do not become SK constraints.
- Unit tests cover these rules in `DynamoConstraintExtractionVisitorTests.cs`.

Good fit:

- This already consumes runtime `DynamoIndexDescriptor` roles, not raw annotations.

### Auto-index selection

File: `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoAutoIndexSelectionAnalyzer.cs`

Current behavior:

- Evaluates candidates from context (`60-130`).
- Gate: equality or IN on candidate partition key attr required (`271-281`).
- Full base-table lookup uses base descriptor partition/sort attrs (`89-93`, helper later).
- Explicit `.WithIndex()` validates against candidate descriptors (`229-259`).

Good fit:

- Analyzer already consumes descriptor roles only.

### SQL translation and IN-limit caveat

File: `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoSqlTranslatingExpressionVisitor.cs`

Current behavior:

- Direct member and `EF.Property` translation call `IsEffectivePartitionKey(...)` to mark `SqlPropertyExpression.IsPartitionKey` (`440-455`, `956-968`).
- Without explicit `.WithIndex()`, `IsEffectivePartitionKey` uses `ResolveKeyMappedEntityType().GetPartitionKeyProperty()?.Name == property.Name` (`1000-1007`) — raw annotation path.
- With explicit `.WithIndex()`, it uses runtime model descriptors (`1009-1023`).
- `Contains` translation stores early `isPartitionKeyComparison = item is SqlPropertyExpression property && property.IsPartitionKey` (`1296-1316`).

File: `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQuerySqlGenerator.cs`

- SQL generator re-evaluates `IN` partition-key comparison from finalized `SelectExpression.EffectivePartitionKeyPropertyNames` when available (`439-451`, `468-483`, `IsPartitionKeyComparison` after `482`).
- Fallback uses original `SqlInExpression.IsPartitionKeyComparison` when current select/effective keys unavailable.

Gap vs story:

- Early property marker still uses raw annotations for base table before postprocessor. Usually SQL generator corrects IN limit after postprocessor; still a runtime/query role consumer outside finalized metadata.
- `DynamoEntityProjectionExpression.BindProperty()` creates `SqlPropertyExpression` without partition-key marker; direct predicate translation has marker. Not central if SQL generator uses effective keys.

Likely change:

- Prefer removing role decisions from `DynamoSqlTranslatingExpressionVisitor.IsEffectivePartitionKey` or make it consult finalized/runtime descriptors only.
- Ensure `SelectExpression.EffectivePartitionKeyPropertyNames` is populated before any SQL generation requiring IN limits. Existing postprocessor does this.
- Add tests proving HasKey-only base-table partition `IN` uses 50-value partition-key limit and non-key `IN` uses 100.

Tests to add:

- Query translation with `HasKey(e => e.Id)`: `Where(e => e.Id == id)` classified as keyed, no scan warning/error when scan guard active.
- Query translation with `HasKey(e => new { e.TenantId, e.OrderId })`: `TenantId == x && OrderId == y` produces key-only safe path for `First/Single`.
- Sort-key range with HasKey-only two-part key: `TenantId == x && OrderId.CompareTo(...) > 0`/supported operators recognized as SK key condition, not scan-like.
- `OrderBy(e => e.OrderId)` valid only when PK equality exists for HasKey-only two-part key.
- `OrderBy(e => e.NonKey)` rejected with key attrs named from resolved roles.
- Auto index selection unchanged: query key-condition recognition for base table uses resolved table keys; GSI/LSI still from index descriptors.
- `Contains`/`IN`: HasKey-only partition key with 51 values throws partition-key max 50; non-key 101 throws non-key max 100.

## High-risk gaps / design constraints

1. **No explicit resolved-key runtime object yet.** `DynamoRuntimeTableModel` stores source descriptors; table descriptor lacks provenance/finalized-key identity separate from descriptors. ADR suggests internal resolved descriptor may be needed.
2. **Raw annotation readers are widespread.** `GetPartitionKeyProperty()` / `GetSortKeyProperty()` are used by validator, runtime builder, writes, query translator, discriminator collision, secondary-index validation. For issue scope, avoid touching validation unless needed, but runtime/writes/query should stop depending on raw annotations.
3. **Derived/shared-table key-owner resolution depends on annotation presence.** `ResolveKeyMappedEntityType()` checks `GetPartitionKeyProperty()`. If resolved roles live only in runtime model, write paths need entity->table descriptor lookup instead of this method.
4. **EF primary key and Dynamo roles expected identical after ADR.** Current write guard/concurrency skip use `IsPrimaryKey()`. If resolver guarantees exact agreement, okay. If runtime roles separate, tests should lock behavior to resolved roles.
5. **Table definitions likely minimal change.** They already consume runtime descriptors; main work is ensuring descriptors are populated from finalized roles.
6. **Query constraint extraction already final-role-friendly.** It is descriptor-driven. Main query risk is early `IsEffectivePartitionKey` in SQL translator and candidate descriptor sourcing.

## Likely implementation direction for next agent

- First ensure ADR resolver/finalizer writes finalized key roles to one place. Runtime issue should consume that place, not recreate resolution.
- Build runtime base-table `DynamoIndexDescriptor` from finalized roles.
- Route write/delete/update/transaction identity through finalized runtime key descriptor/accessor.
- Keep table definition builder consuming `DynamoRuntimeTableModel`; only adjust if model shape changes.
- Keep constraint extraction/analyzer descriptor-driven; add HasKey-only tests to prove descriptors contain inferred roles.
- Audit remaining runtime/query/writes raw calls:
  - `DynamoModelRuntimeInitializer.BuildSourceDescriptors`
  - `DynamoModelExtensions.ResolveKeyMappedEntityType`
  - `DynamoPartiqlStatementFactory.BuildDeleteStatement` / `FinalizeUpdateStatement`
  - `DynamoTransactionTargetIdentityFactory.Create`
  - `DynamoSqlTranslatingExpressionVisitor.IsEffectivePartitionKey`

## Validation commands

Targeted unit tests after implementation:

```bash
dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj \
  --filter "FullyQualifiedName~DynamoKeyInPrimaryKeyConventionTests|FullyQualifiedName~DynamoEntityKeyMappingConventionTests|FullyQualifiedName~TableKeySchema|FullyQualifiedName~DynamoTableDefinitionBuilderTests|FullyQualifiedName~DynamoDerivedSaveChangesTests|FullyQualifiedName~DynamoConstraintExtractionVisitorTests|FullyQualifiedName~DynamoAutoIndexSelectionAnalyzerTests|FullyQualifiedName~ScanQueryGuardTests|FullyQualifiedName~OrderByPartitionKeyValidationTests|FullyQualifiedName~FirstOrDefaultSafePathTests|FullyQualifiedName~SingleSafePathTests"
```

Broader:

```bash
dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj
```

Integration smoke if query/write behavior touched:

```bash
dotnet test tests/EntityFrameworkCore.DynamoDb.IntegrationTests/EntityFrameworkCore.DynamoDb.IntegrationTests.csproj --filter "PkSkTable|SimpleTable|SaveChangesTable"
```
