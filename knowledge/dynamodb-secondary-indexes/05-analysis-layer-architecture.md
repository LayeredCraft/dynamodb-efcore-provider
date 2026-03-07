# Analysis Layer Architecture

Back to index: [README](README.md)

## Best insertion point

Primary insertion point for index decision logic:

- `src/LayeredCraft.EntityFrameworkCore.DynamoDb/Query/Internal/DynamoShapedQueryCompilingExpressionVisitor.cs`
- method: `VisitShapedQuery`

Why this is best:

- deferred discriminator predicate has already been applied,
- projection is finalized,
- SQL has not yet been generated,
- still one central place before execution.

## Responsibility split

Keep selection logic centralized and keep other layers simple:

- `DynamoModelRuntimeInitializer`
  - build canonical runtime source metadata once on the finalized EF Core runtime model.
- `DynamoQueryableMethodTranslatingExpressionVisitor`
  - capture explicit hints (`WithIndex`) only.
- `DynamoShapedQueryCompilingExpressionVisitor`
  - read runtime source metadata, invoke analyzer, and apply selected source to query model.
- `DynamoQuerySqlGenerator`
  - render only (`FROM "Table"` or `FROM "Table"."Index"`).
- `DynamoClientWrapper`
  - execute request only.

## Suggested analyzer contract

```csharp
public interface IDynamoIndexSelectionAnalyzer
{
    DynamoIndexSelectionDecision Analyze(
        SelectExpression selectExpression,
        IEntityType rootEntityType,
        DynamoQueryCompilationContext queryContext,
        IModel model);
}
```

Decision payload:

```csharp
public sealed record DynamoIndexSelectionDecision(
    string? IndexName,
    bool IsExplicit,
    bool IsAutomatic,
    string Reason,
    IReadOnlyList<string> Diagnostics);
```

## Suggested internal components

- `DynamoRuntimeTableModel`
  - canonical model-level runtime cache of per-table source descriptors.
- `DynamoConstraintExtractionVisitor`
  - extracts key constraints from provider SQL expression tree.
- `DynamoIndexCandidateBuilder`
  - builds candidate sources from runtime-model metadata.
- `DynamoIndexCandidateScorer`
  - scores candidates by key-coverage and ordering fit.
- `DynamoProjectionCoverageValidator`
  - checks projection safety for chosen index.
- `DynamoIndexSelectionAnalyzer`
  - orchestrates and returns final decision.

## End-to-end flow

1. runtime model initialization builds and caches source descriptors once.
2. translation creates `SelectExpression` with predicate/orderings/projection.
3. compilation finalizes projection and discriminator predicate.
4. analyzer examines query shape + runtime index metadata.
5. analyzer returns table or index decision.
6. query model stores index name when selected.
7. SQL generator emits final `FROM` source.
8. execution sends statement unchanged.

## Why this architecture scales

- one location for all heuristics,
- easier unit testing of decision behavior,
- cleaner diagnostics,
- avoids logic leakage across translation/render/execution.

## Testing strategy

Unit tests for analyzer:

- partition-key equality/IN detection,
- sort-key range handling,
- ordering compatibility,
- tie and ambiguity behavior,
- projection safety rejection.

Integration tests:

- verify generated PartiQL `FROM` source,
- verify explicit `WithIndex` override behavior,
- verify conservative fallback to table.
