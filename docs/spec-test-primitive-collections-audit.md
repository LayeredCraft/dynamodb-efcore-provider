# Primitive Collections Specification Test Audit

Detailed audit of skipped `PrimitiveCollectionsQueryDynamoTest` methods. Classifications come from upstream EF Core test bodies, DynamoDB provider translation code, and focused scout review.

## Summary

- Many current skips are provider gaps, not DynamoDB architectural limits.
- Fast unskip candidates exist for simple scalar `Contains`, model-validation, `Any`, list indexing, and simple scalar types.
- Larger fix clusters are `IN` null semantics, value-converted collection values, `size()`/index translations, and primitive-list projection/client shaping.
- True long-term blocks are joins, set operations, `Skip`/`Take`, ordering inside list elements, owned types, and broad list-element query pipelines.

## Quick unskip / should run candidates

These should be tried first with focused tests. If they fail, likely small provider/test issue.

- Inline scalar `Contains`: zero/one/two/three values, captured parameters, EF.Parameter/EF.Constant.
- Parameter collection `Contains`: `HashSet<int>`, DateTime, bool, null collection, empty collection.
- Column collection: bool `Contains`, `Any`, direct index/`ElementAt` for int/string.
- Non-query/model checks: `Multidimensional_array_is_not_supported`, `Column_with_custom_converter` expected-failure path.
- Compiled scalar parameter cast: `Parameter_collection_in_subquery_and_Convert_as_compiled_query`.

## Provider gaps likely fixable

Likely files:

- `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoSqlTranslatingExpressionVisitor.cs`
- `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryableMethodTranslatingExpressionVisitor.cs`
- `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQuerySqlGenerator.cs`
- `src/EntityFrameworkCore.DynamoDb/Storage/DynamoTypeMappingSource.cs`

Clusters:

1. `ListInitExpression` and `MemoryExtensions.Contains` recognition.
2. Nullable `IN` semantics for nullable columns and nullable collection values.
3. Value-converted structs/enums in collection parameters and inline constants.
4. Primitive collection `Count()`/`Length` via PartiQL `size()`.
5. Primitive collection `First`/`FirstOrDefault`/basic `ElementAt` via list indexes.
6. Inline finite collection `Any`/`All`/`Count`/`Min`/`Max` rewrites.
7. Primitive list equality / `SequenceEqual` if DynamoDB PartiQL list equality proves stable.
8. Simple primitive collection projections currently blocked by order/assertion or missing client projection shaping.

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

1. Unskip quick candidates and run focused EF10/EF11 tests.
2. Fix `Contains` recognizers and simple scalar type-mapping gaps.
3. Add `size()` translations for primitive collection `Count`/`Length`/`Any` consistency.
4. Add basic list-index translations in projection/predicate paths.
5. Revisit nullable `IN` semantics and value-converted values.
6. Defer list-element sequence pipeline work until larger design.
