# Primitive Collection Query Support

Primitive collection storage shape depends on the CLR collection type. List-like CLR shapes (`T[]`, `List<T>`, `IList<T>`) use DynamoDB `L` attributes. Set-like CLR shapes (`HashSet<T>`, `ISet<T>`, `IReadOnlySet<T>`) use DynamoDB scalar set attributes (`SS`, `NS`, or `BS`) when the element provider type maps to a DynamoDB set type. This page summarizes which primitive collection query shapes currently translate and which categories remain unsupported.

## Supported query shapes

The provider currently supports these primitive collection patterns in server-side query predicates and scalar expressions:

- Inline scalar membership, for example `new[] { 1, 2, 3 }.Contains(entity.Value)`.
- Captured or parameterized scalar collection membership, for example `ids.Contains(entity.Id)`.
- `HashSet<T>`, arrays, lists, collection initializers, `Enumerable.Contains`, `MemoryExtensions.Contains`, and `EF.Parameter` membership shapes when element mapping is known.
- Primitive element types including integers, strings, booleans, `DateTime`, enums, nullable primitive values, and supported scalar value-converted values.
- Empty and null parameter collections in membership predicates.
- Native DynamoDB primitive collection membership with `entity.Collection.Contains(value)` for list or set attributes.
- Non-predicate `entity.Collection.Any()`, translated as a DynamoDB `size(...) > 0` check.
- `entity.Collection.Count`, `entity.Collection.Count()`, and `entity.Collection.Length`, translated as DynamoDB `size(...)`.
- Direct list element access with `entity.Collection[index]` and `entity.Collection.ElementAt(index)` when `index` is a constant numeric position; `First()` and `FirstOrDefault()` translate as constant index `0`.
- Inline finite predicate rewrites such as `new[] { 1, 2 }.Any(x => x == entity.Value)` and `new[] { 1, 2 }.All(x => x != entity.Value)`.

Multidimensional primitive arrays are rejected. Supported list-like primitive collection attributes must be one-dimensional values.

## Known limitations

These limitations are known gaps in otherwise related supported areas:

- Forcing constant expansion for parameter collections with `EF.Constant` is not supported for primitive collection parameters.
- Some nullable value-converted `IN` comparisons remain unsupported, such as comparing a nullable converted collection to a non-nullable converted property.
- Subqueries over primitive collection parameters are not supported.
- Scan-like `Single` patterns remain unsupported even when the predicate references a primitive collection value.
- Inline finite collection aggregates beyond supported predicate rewrites, including `Count(predicate)`, `Min`, and `Max`, are not translated server-side.
- Projection-only shaping of primitive collection values is limited. See [Projection status](#projection-status).

## Long-term unsupported categories

The following categories depend on relational sequence operations inside a DynamoDB collection attribute. DynamoDB PartiQL does not provide a general relational pipeline over collection elements, so these shapes remain unsupported unless the provider gains a dedicated client/projection pipeline or DynamoDB capabilities change.

| Category                                    | Examples                                                                           | Reason                                                                                                                                                                         |
| ------------------------------------------- | ---------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| Joins over collection elements              | Joining an attribute collection with a parameter or inline collection              | DynamoDB PartiQL has no relational join pipeline over collection elements.                                                                                                     |
| Set operations over collection elements     | `Distinct`, `Union`, `Concat`, `Except`, `Intersect`                               | DynamoDB PartiQL does not support these operators over unnested collection elements.                                                                                           |
| Paging inside collection elements           | `Skip`, `Take`, `Skip(...).Take(...)` on list contents                             | DynamoDB cannot apply offset paging inside a single collection attribute server-side.                                                                                          |
| Ordering or filtering before element access | Filtering or ordering list elements before `ElementAt`, projection, or aggregation | Provider does not have a list-element subquery pipeline.                                                                                                                       |
| Owned entity primitive collections          | Primitive collections reached through owned entity mappings                        | Owned entity types are outside provider support; use complex types where applicable.                                                                                           |
| Very large in-memory collections            | Hundreds of values in a membership predicate                                       | Provider limits `IN` predicates to 50 partition-key values or 100 non-key values.                                                                                              |
| Primitive list equality                     | LIST equality and `SequenceEqual`                                                  | Provider does not bind full list literals/parameters for equality and must account for null, missing attributes, converters, and DynamoDB Local behavior before enabling this. |

## Projection status

Full entity materialization can read mapped primitive collection properties using the configured list or set storage shape.

Projection-only queries are narrower. Whole-collection projection, such as `Select(entity => entity.Values)` or projecting one or more primitive collection properties into an anonymous or DTO result, remains unsupported. Project the entity first, or materialize entities and select collection values client-side.

Fixed list element access is supported for query translation in predicate and scalar expression contexts. This page does not claim broad support for projecting collection elements as standalone result shapes until those projection shapes are covered and documented explicitly.

## Future work

Likely future improvements are:

1. Broader nullable `IN` support for nullable collection parameters compared to non-nullable converted properties.
2. Additional inline finite collection rewrites where semantics map cleanly to scalar predicates.
3. Projection-only materialization for primitive collection attributes.
4. Clear rejection messages for unsupported list-element sequence pipelines.
