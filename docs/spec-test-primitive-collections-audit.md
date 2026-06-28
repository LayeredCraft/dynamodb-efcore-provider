# Primitive Collections Specification Test Audit

Detailed audit of skipped `PrimitiveCollectionsQueryDynamoTest` methods. Classifications come from upstream EF Core test bodies, DynamoDB provider translation code, and focused scout review.

## Summary

- Many current skips are provider gaps, not DynamoDB architectural limits.
- Fast unskip candidates exist for simple scalar `Contains`, model-validation, `Any`, list indexing, and simple scalar types.
- Larger fix clusters are `IN` null semantics, value-converted collection values, `size()`/index translations, and primitive-list projection/client shaping.
- True long-term blocks are joins, set operations, `Skip`/`Take`, ordering inside list elements, owned types, and broad list-element query pipelines.

## Quick unskip results

Focused EF10/EF11 runs now pass these formerly skipped methods:

- Inline scalar `Contains`: zero/one/two/three values, captured parameters, EF.Parameter/EF.Constant, Enumerable, MemoryExtensions, and ListInit shapes.
- Parameter collection `Contains`: `HashSet<int>`, DateTime, bool, null collection, empty collection.
- Column collection: bool `Contains`, `Any`, `Count`, `Length`, direct index/`ElementAt` for int/string/DateTime, and `First`/`FirstOrDefault` as index 0.

Kept skipped after focused EF11 failures:

- `Multidimensional_array_is_not_supported`: provider throws `ArgumentException` while upstream expects `InvalidOperationException`.
- `Column_with_custom_converter`: upstream uses `SingleAsync` on a scan-like path, which DynamoDB provider intentionally rejects.
- `Parameter_collection_in_subquery_and_Convert_as_compiled_query`: still requires subquery support.

## Provider gaps likely fixable

Likely files:

- `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoSqlTranslatingExpressionVisitor.cs`
- `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryableMethodTranslatingExpressionVisitor.cs`
- `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQuerySqlGenerator.cs`
- `src/EntityFrameworkCore.DynamoDb/Storage/DynamoTypeMappingSource.cs`

Clusters:

1. Nullable `IN` semantics for nullable columns and nullable collection values.
2. Value-converted structs/enums in collection parameters and inline constants.
3. Inline finite collection `Any`/`All`/`Count`/`Min`/`Max` rewrites.
4. Primitive list equality / `SequenceEqual` if DynamoDB PartiQL list equality proves stable.
5. Simple primitive collection projections currently blocked by order/assertion or missing client projection shaping.

## True or long-term architectural blocks

Keep skipped unless provider gains a primitive-list subquery/client-projection pipeline:

- Joins over collection elements.
- Set operations: `Union`, `Concat`, `Except`, `Intersect`, `Distinct` over list elements.
- `Skip`/`Take` over primitive list elements.
- `OrderBy` inside primitive list elements.
- Filtered list subqueries feeding `ElementAt`, `Count`, `Contains`.
- Owned entity scenarios.
- Huge `IN` values beyond DynamoDB/provider limits: 50 partition-key values, 100 non-key values.

## Recommended implementation order

1. Fix `Contains` recognizers and simple scalar type-mapping gaps.
2. Revisit nullable `IN` semantics and value-converted values.
3. Defer list-element sequence pipeline work until larger design.
