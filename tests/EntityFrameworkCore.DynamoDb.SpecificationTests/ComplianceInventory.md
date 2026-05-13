# DynamoDB EF Core specification coverage inventory

Status values: Implemented, Partial, Unsupported.

| Area                                | Status      | Local coverage                                                                                                                                                                                           |
| ----------------------------------- | ----------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Northwind Where predicates          | Partial     | `NorthwindWhereQueryDynamoTest` covers equality, inequality, ranges, boolean predicates, OR predicates, and PartiQL baselines.                                                                           |
| Northwind Select projections        | Partial     | `NorthwindSelectQueryDynamoTest` covers scalar-wrapper, anonymous object, and DTO projections.                                                                                                           |
| Northwind string functions          | Partial     | `NorthwindFunctionsQueryDynamoTest` covers `StartsWith`/`Contains`; `EndsWith` is explicitly unsupported.                                                                                                |
| Northwind ordering/paging           | Partial     | `NorthwindOrderByPagingQueryDynamoTest` covers sort-key ordering and `Limit`; `Skip` is unsupported.                                                                                                     |
| Scalar/single-result operators      | Partial     | `NorthwindScalarQueryDynamoTest` covers key-safe `FirstOrDefault`; aggregate scalar operators remain unsupported.                                                                                        |
| Aggregates                          | Unsupported | `NorthwindAggregateQueryDynamoTest` asserts `Count`, `Any`, and `Sum` unsupported.                                                                                                                       |
| Joins/includes/navigation queries   | Unsupported | `NorthwindUnsupportedQueryDynamoTest` asserts joins, includes, and navigation filters unsupported.                                                                                                       |
| Query filters                       | Partial     | `NorthwindQueryFilterDynamoTest` covers simple query filters and `IgnoreQueryFilters`.                                                                                                                   |
| Null semantics                      | Partial     | `NorthwindNullSemanticsQueryDynamoTest` covers nullable equality/inequality comparisons.                                                                                                                 |
| Find                                | Partial     | `FindSpecificationTests` covers supported finder APIs, async store lookup, tracked lookup, null/error paths, TPH discrimination, and documents DynamoDB-specific unsupported nullable/shadow key shapes. |
| SaveChanges                         | Partial     | `SaveChangesSpecificationTests` covers insert/update/delete and manual concurrency conflicts.                                                                                                            |
| Type mapping/value conversion       | Partial     | `TypeMappingSpecificationTests` covers supported scalar/null/enum conversion and unsupported custom scalar diagnostics.                                                                                  |
| Complex types/primitive collections | Partial     | `ComplexCollectionsSpecificationTests` covers complex type and primitive collection roundtrip; owned entity shape is unsupported.                                                                        |

Inventory is provider-local and intentionally narrower than EF Core's full specification suite. Add rows or update statuses when new suites are ported.
