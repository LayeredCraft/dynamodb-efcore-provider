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
- Parameter collection `Contains`: `HashSet<int>`, DateTime, bool, enum, value-converted `WrappedId`, nullable `WrappedId` property comparisons, null collection, empty collection.
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
2. Remaining value-converted nullable-comparer and nullable-collection edge cases.
3. Inline finite collection `Any`/`All`/`Count`/`Min`/`Max` rewrites.
4. Simple primitive collection projections currently blocked by order/assertion or missing client projection shaping.

## True or long-term architectural blocks

Keep these skipped unless provider gains a primitive-list subquery/client-projection pipeline or DynamoDB limits change:

| Block                                     | Representative methods                                                                                                                                                                                                                                       | Reason                                                                                                                                                                                                                                                                                                       |
| ----------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Joins over collection elements            | `Parameter_collection_empty_Join`, `Column_collection_Join_parameter_collection`, `Inline_collection_Join_ordered_column_collection`                                                                                                                         | DynamoDB PartiQL has no relational join pipeline over LIST elements.                                                                                                                                                                                                                                         |
| Set operations over list elements         | `Column_collection_Distinct`, `Column_collection_Union_parameter_collection`, `Column_collection_Intersect_inline_collection`, `Inline_collection_Except_column_collection`, `Project_inline_collection_with_Union`, `Project_inline_collection_with_Concat` | DynamoDB PartiQL does not support `Union`, `Concat`, `Except`, `Intersect`, or `Distinct` over unnested list elements.                                                                                                                                                                                       |
| Paging inside list elements               | `Column_collection_Skip`, `Column_collection_Take`, `Column_collection_Skip_Take`, `Column_collection_Where_Skip_Take`, nullable collection paging projections                                                                                               | DynamoDB cannot apply `Skip`/`Take` within a single attribute list server-side.                                                                                                                                                                                                                              |
| Ordering/filtering inside list elements   | `Column_collection_OrderByDescending_ElementAt`, `Column_collection_Where_ElementAt`, `Project_collection_of_datetimes_filtered`                                                                                                                             | Provider has no list-element subquery pipeline to filter/order before indexing or projection.                                                                                                                                                                                                                |
| Owned entity primitive collections        | `Project_collection_from_entity_type_with_owned`                                                                                                                                                                                                             | Provider intentionally does not support EF owned entity types; use complex types instead.                                                                                                                                                                                                                    |
| Huge in-memory collections                | `Parameter_collection_*_huge_number_of_values*`                                                                                                                                                                                                              | DynamoDB/provider limits cap `IN` values: 50 partition-key values, 100 non-key values.                                                                                                                                                                                                                       |
| Primitive-list projection shaping         | `Project_collection_of_ints_simple`, `Project_multiple_collections`, `Project_primitive_collections_element`                                                                                                                                                 | Requires stable client-side materialization/order semantics for list projections.                                                                                                                                                                                                                            |
| Primitive list equality / `SequenceEqual` | `Column_collection_equality_parameter_collection`, `Column_collection_equality_inline_collection`, `Column_collection_equality_inline_collection_with_parameters`, `Column_collection_Where_equality_inline_collection`                                      | DynamoDB PartiQL can compare document values, but provider lacks list literal/parameter equality binding and does not translate filtered/concatenated list pipelines. Keep skipped until equality gets a dedicated design covering null/missing attributes, element type mapping, and DynamoDB Local parity. |

## Primitive collection projection pipeline design

Goal: support projection-only primitive LIST reads without pretending DynamoDB can run relational operators inside a single list attribute.

Feasible first slice:

1. Server projects whole LIST attributes and scalar list indexes only.
2. Materializer shapes DynamoDB `L` attributes into CLR primitive arrays/lists using existing element type mappings and converters.
3. Client assertion/order semantics remain entity-row based; element order is DynamoDB list order.
4. Translation explicitly rejects list-element `Where`, `OrderBy`, `Skip`, `Take`, joins, and set operations before projection.

Implementation boundaries:

- Add tests only for direct projections such as `Select(e => e.Ints)`, anonymous projections containing one or more primitive lists, and scalar index projections already backed by `DynamoListIndexExpression`.
- Keep filtered/ordered/paged element pipelines skipped until there is a separate client-evaluation design. Running those client-side after server projection could silently change query semantics by moving filters from server to client.
- Keep owned-entity primitive collection projection skipped because owned types are outside provider support.

## Recommended implementation order

1. Fix `Contains` recognizers and simple scalar type-mapping gaps.
2. Revisit nullable `IN` semantics and remaining value-converted nullable edge cases.
3. Add projection-only primitive LIST materialization for direct list projections.
4. Defer list-element sequence pipeline work until larger design.
