# Current Provider State and Gaps

Back to index: [README](README.md)

## Query pipeline reality today

## SQL model and generation

- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/Expressions/SelectExpression.cs`
  - stores `TableName` only for `FROM` source.
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQuerySqlGenerator.cs`
  - emits `FROM "<TableName>"`.

Gap:

- no `IndexName` slot in query model yet.
- runtime index metadata now exists, but the query model does not consume it yet.

## Execution layer

- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoShapedQueryCompilingExpressionVisitor.QueryingEnumerable.cs`
  - builds `ExecuteStatementRequest`.
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Storage/DynamoClientWrapper.cs`
  - runs `ExecuteStatementAsync` and follows `NextToken`.

Gap:

- no Query API path where `QueryRequest.IndexName` could be used.
- therefore, index targeting must happen in generated PartiQL text.

## Existing hint pattern we can extend

- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Extensions/DynamoDbQueryableExtensions.cs`
  - already has provider hints (`WithPageSize`, `WithoutPagination`).
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryableMethodTranslatingExpressionVisitor.cs`
  - intercepts those method calls.
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/DynamoQueryCompilationContext.cs`
  - stores per-query hint state.

Direct implication:

- `WithIndex("...")` already follows the same capture lifecycle, but is not yet applied to `FROM` source selection.

## Existing metadata patterns (reusable)

Provider already uses annotation-based metadata for:

- entity-level mapping (`ToTable`, partition key, sort key),
- property-level attribute names (`HasAttributeName`),
- discriminator strategy.

Relevant files:

- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Metadata/Internal/DynamoAnnotationNames.cs`
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Extensions/DynamoEntityTypeBuilderExtensions.cs`
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Extensions/DynamoEntityTypeExtensions.cs`
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Extensions/DynamoPropertyBuilderExtensions.cs`
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Extensions/DynamoPropertyExtensions.cs`

Gap:

- secondary-index annotation metadata now exists on `IIndex` (`name`, `kind`, `projection type`).
- runtime source metadata is now built from the finalized EF Core runtime model via provider `IModelRuntimeInitializer` and stored as runtime annotations.

## Conventions and validation baseline

Conventions already handle key/discriminator finalization:

- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoConventionSetBuilder.cs`
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoKeyDiscoveryConvention.cs`
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoKeyAnnotationConvention.cs`
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoDiscriminatorConvention.cs`

Validator already does table-group-level consistency checks:

- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Infrastructure/Internal/DynamoModelValidator.cs`

Gap:

- secondary-index validation exists for LSIs, but broader rules (name uniqueness, key schema, projection safety, shared-table consistency) are still incomplete.

## Key-awareness in translation today

- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoSqlTranslatingExpressionVisitor.cs`
  - marks table partition key property access.
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/Expressions/SqlInExpression.cs`
  - carries partition-key comparison marker for `IN` limit behavior.

Gap:

- no generalized index key-role analysis yet,
- no analyzer currently matching predicates against multiple candidate index schemas.

## Runtime metadata baseline

- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Infrastructure/Internal/DynamoModelRuntimeInitializer.cs`
  - attaches the canonical runtime table model to the finalized EF Core runtime model.
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Metadata/Internal/DynamoRuntimeTableModel.cs`
  - stores per-table-group source descriptors for the base table and configured secondary indexes.
- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Extensions/DynamoModelExtensions.cs`
  - exposes table-group helpers and runtime table-model access.

Current boundary:

- model/runtime metadata is now built once and cached on the runtime model,
- query compilation still needs to read that metadata and apply a source decision.
