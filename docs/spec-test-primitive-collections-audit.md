# Primitive Collections Specification Test Audit

Detailed audit of skipped `PrimitiveCollectionsQueryDynamoTest` methods. Classifications come from upstream EF Core test bodies, DynamoDB provider translation code, and focused scout review.

## Summary

- Recently unskipped coverage now includes simple scalar `Contains`, several parameter-collection `Contains` shapes, column-list `Any`, `size()` translations, and direct list indexing.
- Remaining provider gaps are `IN` null semantics, one nullable value-converted collection edge case, inline finite collection aggregates beyond simple predicate rewrites, and primitive-list projection/client shaping.
- True long-term blocks are joins, set operations, `Skip`/`Take`, ordering/filtering inside list elements, owned types, and broad list-element query pipelines.

## Quick unskip results

Focused EF10/EF11 runs now pass these formerly skipped methods:

- Inline scalar `Contains`: zero/one/two/three values, captured parameters, EF.Parameter, Enumerable, MemoryExtensions, and ListInit shapes.
- Parameter collection `Contains`: `HashSet<int>`, DateTime, bool, enum, nullable int/string values, value-converted `WrappedId`, nullable `WrappedId` property comparisons including nullable-comparer properties, null collection, empty collection.
- Column collection: bool `Contains`, `Any`, `Count`, `Length`, direct index/`ElementAt` for int/string/DateTime, and `First`/`FirstOrDefault` as index 0.

Still skipped after focused EF11 failures:

- `Parameter_collection_Contains_with_EF_Constant`: forced constant expansion for parameter collections remains blocked by provider primitive collection translation limits.
- `Column_with_custom_converter`: upstream uses `SingleAsync` on a scan-like path, which DynamoDB provider intentionally rejects.
- `Parameter_collection_in_subquery_and_Convert_as_compiled_query`: still requires subquery support.

Now enabled as a guard test:

- `Multidimensional_array_is_not_supported`: provider list-shape validation rejects multidimensional arrays instead of treating them as supported one-dimensional LIST shapes.

## Provider gaps likely fixable

Likely files:

- `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoSqlTranslatingExpressionVisitor.cs`
- `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryableMethodTranslatingExpressionVisitor.cs`
- `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQuerySqlGenerator.cs`
- `src/EntityFrameworkCore.DynamoDb/Storage/DynamoTypeMappingSource.cs`

Clusters:

1. Remaining nullable collection `IN` edge case where `List<WrappedId?>` is compared to non-nullable `WrappedId`.
2. Inline finite collection `Count`/`Min`/`Max` rewrites; simple `Any(predicate)`/`All(predicate)` rewrites are now supported.
3. Primitive collection projection gaps split between direct LIST projection materialization/shaping and ordered-result assertion adaptations for scan-like test cases.
4. Direct primitive LIST projection materialization remains a design-only task; implementation follow-up is tracked by `dynamodb-efcore-provider-m62` and should start with whole-attribute projection only.

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
| Primitive-list projection shaping         | `Project_collection_of_ints_simple`, `Project_multiple_collections`, `Project_primitive_collections_element`                                                                                                                                                 | Direct LIST projections require client-side materialization/shaping; ordered base variants may also need unordered assertion adaptation for scan-like reads.                                                                                                                                                 |
| Primitive list equality / `SequenceEqual` | `Column_collection_equality_parameter_collection`, `Column_collection_equality_inline_collection`, `Column_collection_equality_inline_collection_with_parameters`, `Column_collection_Where_equality_inline_collection`                                      | DynamoDB PartiQL can compare document values, but provider lacks list literal/parameter equality binding and does not translate filtered/concatenated list pipelines. Keep skipped until equality gets a dedicated design covering null/missing attributes, element type mapping, and DynamoDB Local parity. |

## Primitive collection projection pipeline design

Goal: support projection-only primitive LIST reads without pretending DynamoDB can run relational operators inside a single list attribute. This section is design record for `dynamodb-efcore-provider-m62`; implementation should be split into follow-up beads before code changes.

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

1. Revisit nullable `IN` semantics for nullable collection parameters compared to non-nullable converted properties.
2. Add inline finite collection `Count`/`Min`/`Max` rewrites where semantics are clear.
3. Add projection-only primitive LIST materialization for direct list projections.
4. Defer list-element sequence pipeline work until larger design.
