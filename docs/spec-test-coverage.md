---
title: EF Core Specification Test Coverage
description: Inventory of EF Core specification tests and their implementation status for the DynamoDB provider.
---

# EF Core Specification Test Coverage

EF Core ships a suite of cross-provider specification tests (`Microsoft.EntityFrameworkCore.Specification.Tests`)
that validate LINQ query translation and ORM behavior. This document tracks which test classes are
implemented, which should be added, and which are permanently out of scope due to DynamoDB architectural
constraints.

**Threshold rule:** A test class is worth implementing if the DynamoDB provider can meaningfully cover
≥70% of its methods — either by passing or by providing an explicit, accurate skip reason.
If no inherited methods are supportable and every test would be skipped, do not implement a DynamoDB
spec class; track the class only in this document with the unsupported rationale.

> Each test method typically runs as both an async and sync variant in the test runner,
> so method counts below are unique test methods (not total test runs).

Reference implementations used as guides:

- **Cosmos DB** — `EFCore.Cosmos.FunctionalTests` (closest architectural analogue)
- **MongoDB** — `MongoDB.EntityFrameworkCore.SpecificationTests` (broadest non-relational coverage)

______________________________________________________________________

## Non-Query Tests

These test classes live at the root of the spec project and cover ORM fundamentals: data types,
change tracking, concurrency, Find, value converters, interceptors, and more.

### Implemented

| Test Class                                           | Methods | Cosmos | MongoDB | Notes                                                                                                                                     |
| ---------------------------------------------------- | ------: | :----: | :-----: | ----------------------------------------------------------------------------------------------------------------------------------------- |
| `ApiConsistencyTestBase`                             |      18 |   ✓    |    ✓    | Provider API surface/naming conventions                                                                                                   |
| `BuiltInDataTypesTestBase`                           |      28 |   ✓    |    ✓    | Scalar type round-trips (bool, int, string, DateTime, etc.)                                                                               |
| `ComplexTypesTrackingTestBase`                       |     128 |   ✓    |    ✗    | Complex type change tracking; DynamoDB complex types fully supported                                                                      |
| `ConcurrencyDetectorDisabledTestBase`                |       1 |   ✓    |    ✗    | `ConcurrencyDetector` opt-out; sync rows are explicitly skipped because DynamoDB has async-only query/save execution                      |
| `ConcurrencyDetectorEnabledTestBase`                 |       1 |   ✓    |    ✗    | `ConcurrencyDetector` opt-in; sync rows are explicitly skipped because DynamoDB has async-only query/save execution                       |
| `FindTestBase`                                       |      69 |   ✓    |    ✗    | `Find`/`FindAsync` by primary key                                                                                                         |
| `ComplianceTestBase`                                 |       1 |   ✗    |    ✗    | Compliance marker for implemented provider spec bases plus guardrail for new skipped no-op override drift                                 |
| `OverzealousInitializationTestBase`                  |       1 |   ✓    |    ✗    | Navigation-based fixup test is explicitly skipped                                                                                         |
| `LoggingTestBase`                                    |       1 |   ✗    |    ✗    | Context-initialization logging covered; unsupported include path skipped                                                                  |
| `SaveChangesInterceptionTestBase`                    |      13 |   ✗    |    ✗    | Transaction-dependent cases are explicitly skipped                                                                                        |
| `QueryExpressionInterceptionTestBase`                |       4 |   ✓    |    ✗    | Spec `Single` shapes are skipped when they are not key-condition-only                                                                     |
| `MaterializationInterceptionTestBase`                |       7 |   ✓    |    ✗    | Materialization interceptor coverage; owned/complex collection cases skipped                                                              |
| `CompositeKeyEndToEndTestBase`                       |       3 |   ✗    |    ✗    | PK+SK round-trip covered; three-part composite-key cases skipped                                                                          |
| `ConvertToProviderTypesTestBase`                     |       2 |   ✗    |    ✗    | Additional enum/provider-type conversion query methods beyond `BuiltInDataTypesTestBase`                                                  |
| `CustomConvertersTestBase`                           |      29 |   ✓    |    ✗    | Value converter round-trips; scalar DynamoDB converter coverage is split from inherited FK/navigation cases, which are explicitly skipped |
| `SeedingTestBase`                                    |       2 |   ✗    |    ✗    | `HasData` seeding is covered; keyless entity seeding is skipped because DynamoDB requires partition keys                                  |
| `ValueConvertersEndToEndTestBase`                    |       1 |   ✗    |    ✗    | End-to-end converter insert/readback; DynamoDB fixture maps `ConvertingEntity` with partition key                                         |
| `KeysWithConvertersTestBase`                         |      47 |   ✓    |    ✗    | Converted partition-key mapping has DynamoDB-specific coverage; inherited FK, shadow-FK, and owned-entity cases are explicitly skipped    |
| `EntityFrameworkServiceCollectionExtensionsTestBase` |       3 |   ✗    |    ✗    | Provider service registration is idempotent and expected service lifetimes are covered without a DynamoDB table fixture                   |

Additional provider-specific coverage: `DynamoConcurrencyTest` has 6 local tests for DynamoDB optimistic concurrency behavior. It intentionally does not inherit `OptimisticConcurrencyTestBase` because DynamoDB concurrency semantics differ from the EF Core spec fixture.

`TypeTestBase<T>` scalar matrix coverage is implemented through dedicated DynamoDB classes for bool, numeric types, string, GUID, temporal types, and byte arrays. Equality query coverage runs on EF10/EF11, EF11 save/readback coverage runs, and primitive collection aggregate cases are skipped because DynamoDB PartiQL has no `COUNT` aggregate support.

### Implement Next

No non-query specification test classes are currently queued here.

### Future

Feasible but requires investigation or additional provider work before adding.

No non-query specification test classes are currently queued here.

### Skip — Architectural Constraints

| Test Class                                 | Methods | Cosmos | MongoDB | Reason                                                                                                                                                                                                   |
| ------------------------------------------ | ------: | :----: | :-----: | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `PropertyValuesTestBase`                   |      97 |   ✗    |    ✗    | Below threshold: inherited coverage relies heavily on shadow properties, relationship/join entities, synchronous lookups, and `GetDatabaseValues` readback semantics that do not map cleanly to DynamoDB |
| `StoreGeneratedTestBase`                   |      58 |   ✗    |    ✗    | Below threshold: most inherited cases require relational store-generated/computed values, identity columns, database defaults, or key-generation semantics that DynamoDB does not provide                |
| `DataAnnotationTestBase`                   |      89 |   ✗    |    ✗    | Below threshold: inherited coverage is dominated by relationship annotations, FK/index behavior, schema/table/column relational facets, owned entity attributes, and relational save/readback semantics  |
| `FieldMappingTestBase`                     |     101 |   ✗    |    ✗    | Below threshold: inherited coverage is dominated by Include/Load/navigation scenarios, relationship backing fields, and sync query/update patterns not meaningful for DynamoDB                           |
| `OptimisticConcurrencyTestBase`            |      33 |   ✓    |    ✗    | Covered by provider-specific `DynamoConcurrencyTest`; EF spec fixture semantics do not match DynamoDB concurrency                                                                                        |
| `LazyLoadTestBase`                         |      97 |   ✗    |    ✗    | Lazy loading requires navigation property support                                                                                                                                                        |
| `LazyLoadProxyTestBase`                    |      75 |   ✗    |    ✗    | Lazy load via proxies; requires navigations                                                                                                                                                              |
| `LoadTestBase`                             |     106 |   ✗    |    ✗    | Explicit/implicit load operations; navigation-dependent                                                                                                                                                  |
| `FieldsOnlyLoadTestBase`                   |     120 |   ✗    |    ✗    | Load entities with field-only backing; navigation-dependent                                                                                                                                              |
| `ManyToManyLoadTestBase`                   |      26 |   ✗    |    ✗    | M:M load; requires navigation and join table semantics                                                                                                                                                   |
| `ManyToManyFieldsLoadTestBase`             |      23 |   ✗    |    ✗    | Same as above with field backing                                                                                                                                                                         |
| `ManyToManyTrackingTestBase`               |      46 |   ✗    |    ✗    | M:M relationship tracking; no relational tracking                                                                                                                                                        |
| `UnidirectionalManyToManyLoadTestBase`     |      22 |   ✗    |    ✗    | Unidirectional M:M load; navigation-dependent                                                                                                                                                            |
| `UnidirectionalManyToManyTrackingTestBase` |      20 |   ✗    |    ✗    | Unidirectional M:M tracking; no relational tracking                                                                                                                                                      |
| `MonsterFixupTestBase`                     |       3 |   ✗    |    ✗    | Graph fixup for complex navigation graphs                                                                                                                                                                |
| `ConferencePlannerTestBase`                |      22 |   ✗    |    ✗    | Real-world app with navigation properties and joins                                                                                                                                                      |
| `MusicStoreTestBase`                       |      18 |   ✗    |    ✗    | Real-world app; navigation properties and aggregations                                                                                                                                                   |
| `NotificationEntitiesTestBase`             |       2 |   ✗    |    ✗    | `INotifyPropertyChanged` entities; relational tracking semantics                                                                                                                                         |
| `DataBindingTestBase`                      |      37 |   ✗    |    ✗    | WPF/WinForms data binding; not applicable to document stores                                                                                                                                             |
| `SpatialTestBase`                          |       5 |   ✗    |    ✗    | Geometry/geography types; DynamoDB has no spatial support                                                                                                                                                |
| `SerializationTestBase`                    |       1 |   ✗    |    ✗    | EF model serialization; provider-specific serialization not implemented                                                                                                                                  |
| `StoreGeneratedFixupTestBase`              |     118 |   ✗    |    ✗    | Store-generated value graph fixup; navigation fixup required                                                                                                                                             |
| `JsonTypesTestBase`                        |     242 |   ✓    |    ✗    | JSON column types (`ToJson()`); DynamoDB does not have JSON column mapping                                                                                                                               |
| `InterceptionTestBase`                     |       1 |   ✗    |    ✗    | Generic interception base; superseded by specific interceptor tests above                                                                                                                                |
| `WithConstructorsTestBase`                 |      41 |   ✗    |    ✗    | Inherited coverage is dominated by sync-only queries, keyless types, Include/navigation, and lazy-loader navigation scenarios unsupported by DynamoDB                                                    |

______________________________________________________________________

## BulkUpdates Tests

`ExecuteUpdate` and `ExecuteDelete` are not yet implemented in the provider. All bulk-update tests
are deferred until those features land.

| Test Class                              | Methods | Cosmos | MongoDB | Status                                              |
| --------------------------------------- | ------: | :----: | :-----: | --------------------------------------------------- |
| `NorthwindBulkUpdatesTestBase`          |      91 |   ✗    |    ✗    | Future — blocked on `ExecuteUpdate`/`ExecuteDelete` |
| `FiltersInheritanceBulkUpdatesTestBase` |      16 |   ✗    |    ✗    | Future — same blocker                               |
| `InheritanceBulkUpdatesTestBase`        |      18 |   ✗    |    ✗    | Future — same blocker                               |
| `NonSharedModelBulkUpdatesTestBase`     |      11 |   ✗    |    ✗    | Future — same blocker                               |
| `AssociationsBulkUpdateTestBase`        |      33 |   ✗    |    ✗    | Skip — also requires navigations                    |
| `ComplexPropertiesBulkUpdateTestBase`   |       — |   ✗    |    ✗    | Future — complex types + bulk update                |

______________________________________________________________________

## Northwind Query Tests

Uses the `Customer / Employee / Order / Product` dataset with `NorthwindQueryDynamoFixture<TModelCustomizer>`.

### Implemented

| Test Class                             | Methods | Cosmos | MongoDB | Notes                                                                                                                            |
| -------------------------------------- | ------: | :----: | :-----: | -------------------------------------------------------------------------------------------------------------------------------- |
| `NorthwindWhereQueryTestBase`          |     203 |   ✓    |    ✓    | Predicate filtering; skipped unsupported shapes delegate to upstream base methods so inherited intent stays visible              |
| `NorthwindSelectQueryTestBase`         |     186 |   ✓    |    ✓    | Projections and SELECT shapes                                                                                                    |
| `NorthwindAsNoTrackingQueryTestBase`   |      11 |   ✗    |    ✓    | `AsNoTracking()` passthrough; join/navigation-shaped cases skipped                                                               |
| `NorthwindAsTrackingQueryTestBase`     |       5 |   ✗    |    ✓    | `AsTracking()` on `IQueryable`; sync-only base tests assert async-only provider behavior                                         |
| `NorthwindQueryTaggingQueryTestBase`   |       9 |   ✗    |    ✓    | `TagWith()` has no translation impact; sync-only base tests assert async-only provider behavior                                  |
| `NorthwindChangeTrackingQueryTestBase` |      17 |   ✗    |    ✓    | Query tracking behavior and state transitions; join-shaped modifier-precedence cases skipped                                     |
| `NorthwindFunctionsQueryTestBase`      |      10 |   ✓    |    ✓    | Static equality function covered; navigation/unsupported function cases skipped                                                  |
| `NorthwindQueryFiltersQueryTestBase`   |      17 |   ✗    |    ✓    | Global `HasQueryFilter` on Northwind entities; navigation/include, aggregate count, and unsupported `Find` filter shapes skipped |

### Implement Next

No Northwind query specification test classes are currently queued here.

### Future

No Northwind query specification test classes are currently queued here.

### Skip — Architectural Constraints

| Test Class                                 | Methods | Cosmos | MongoDB | Reason                                                                                                                                                                                                                             |
| ------------------------------------------ | ------: | :----: | :-----: | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `NorthwindGroupByQueryTestBase`            |     258 |   ✗    |    ✓    | PartiQL has no `GROUP BY`                                                                                                                                                                                                          |
| `NorthwindJoinQueryTestBase`               |      68 |   ✗    |    ✓    | PartiQL has no `JOIN`                                                                                                                                                                                                              |
| `NorthwindSetOperationsQueryTestBase`      |      91 |   ✗    |    ✓    | `Union`, `Intersect`, `Except` not supported in PartiQL                                                                                                                                                                            |
| `NorthwindIncludeQueryTestBase`            |     118 |   ✗    |    ✓    | Eager loading requires navigation properties                                                                                                                                                                                       |
| `NorthwindIncludeNoTrackingQueryTestBase`  |       — |   ✗    |    ✓    | Include + AsNoTracking; blocked by navigations                                                                                                                                                                                     |
| `NorthwindEFPropertyIncludeQueryTestBase`  |       — |   ✗    |    ✓    | `EF.Property`-named Include; blocked by navigations                                                                                                                                                                                |
| `NorthwindStringIncludeQueryTestBase`      |       — |   ✗    |    ✓    | String-name Include; blocked by navigations                                                                                                                                                                                        |
| `NorthwindNavigationsQueryTestBase`        |      73 |   ✗    |    ✓    | Navigation property traversal in LINQ                                                                                                                                                                                              |
| `NorthwindKeylessEntitiesQueryTestBase`    |      18 |   ✓    |    ✓    | Keyless entities require no partition key; all DynamoDB entities need a key                                                                                                                                                        |
| `NorthwindAggregateOperatorsQueryTestBase` |     211 |   ✓    |    ✓    | Below 70% threshold — Sum, Avg, Min, Max, Count aggregate functions unsupported in PartiQL; ~52% feasibility                                                                                                                       |
| `NorthwindMiscellaneousQueryTestBase`      |     469 |   ✓    |    ✓    | Below threshold: broad suite mixes Take/Skip, casts, null semantics, subqueries, async/sync patterns, and many unsupported operators; not meaningful until core query surface expands                                              |
| `NorthwindDbFunctionsQueryTestBase`        |       5 |   ✓    |    ✓    | Inherited coverage is entirely `EF.Functions.Like` through `Count`; DynamoDB PartiQL has no query aggregates, and the provider intentionally exposes native `Contains`/`StartsWith`/equality instead of partial SQL LIKE semantics |
| `NorthwindCompiledQueryTestBase`           |      32 |   ✗    |    ✓    | `EF.CompileQuery` is sync-focused; DynamoDB provider is async-only                                                                                                                                                                 |
| `Ef6GroupByTestBase`                       |      55 |   ✗    |    ✓    | Legacy EF6 GROUP BY patterns; no GROUP BY in PartiQL                                                                                                                                                                               |

______________________________________________________________________

## Other Query Tests

These tests use non-Northwind models and fixtures.

### Implemented

| Test Class                             | Methods | Cosmos | MongoDB | Notes                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| -------------------------------------- | ------: | :----: | :-----: | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ComplexTypeQueryTestBase`             |      74 |   ✓    |    ✗    | All inherited methods are registered/overridden with explicit outcomes; supported projection/filter, nested struct projection, and complex equality subsets execute, while navigation, set-operation, GroupBy, subquery/Contains, and pushdown cases are explicitly skipped                                                                                                                                                                                                                                                                                                                                                |
| `InheritanceQueryTestBase`             |      52 |   ✓    |    ✗    | Single-table inheritance with discriminator predicates is covered, including `OfType`, `is`, `GetType()` leaf checks, derived-property filters, and discriminator projections. Skips remain for keyless views, navigations/includes, transactions, set operations, non-key ordered result assumptions, scan-like `Single`, and a few unsupported projection/query shapes.                                                                                                                                                                                                                                                  |
| `FiltersInheritanceQueryTestBase`      |      11 |   ✗    |    ✗    | Global query filters on inherited types execute for discriminator predicates, projections, and derived sets; ordered-result and sync-only `GetDatabaseValues` cases are skipped                                                                                                                                                                                                                                                                                                                                                                                                                                            |
| `InheritanceComplexTypesQueryTestBase` |       9 |   ✗    |    ✗    | Complex-type filters and projections over inheritance execute on EF11; complex collection aggregate subquery is skipped because DynamoDB PartiQL has no `COUNT` aggregate support                                                                                                                                                                                                                                                                                                                                                                                                                                          |
| `PrimitiveCollectionsQueryTestBase`    |     172 |   ✓    |    ✗    | Primitive collection coverage distinguishes list-like DynamoDB `L` attributes from set-like `SS`/`NS`/`BS` attributes where applicable. Current focused validation passes 49 supported methods on EF10 and 56 on EF11 across inline/parameter `Contains`, MemoryExtensions/ListInit `Contains`, scalar and value-converted collection parameters, column `Contains`/`Any`/`Count`/`Length`, direct list indexing/first-element access, and the EF11 multidimensional-array guard; `docs/spec-test-primitive-collections-audit.md` summarizes user-facing primitive collection query support, limitations, and future work. |
| `FunkyDataQueryTestBase`               |      19 |   ✗    |    ✗    | Edge-case string data with wildcard-like characters; `Contains` and provider-specific non-null `StartsWith` cases execute, while inherited null-argument `StartsWith` branches, column cross-products, `EndsWith`, and unsupported character operators are explicitly skipped                                                                                                                                                                                                                                                                                                                                              |

### Implement Next

No other query specification test classes are currently queued here.

### Future

No other query specification test classes are currently queued here.

### Skip — Architectural Constraints

| Test Class                                   | Methods | Cosmos | MongoDB | Reason                                                                                                                                                                                     |
| -------------------------------------------- | ------: | :----: | :-----: | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `ComplexNavigationsQueryTestBase`            |     308 |   ✗    |    ✗    | Navigation-heavy hierarchical queries                                                                                                                                                      |
| `ComplexNavigationsCollectionsQueryTestBase` |     156 |   ✗    |    ✗    | Navigation collection queries                                                                                                                                                              |
| `GearsOfWarQueryTestBase`                    |     594 |   ✗    |    ✗    | Large navigation/join-heavy test suite                                                                                                                                                     |
| `InheritanceRelationshipsQueryTestBase`      |      48 |   ✗    |    ✗    | Inheritance + navigation relationships                                                                                                                                                     |
| `IncludeOneToOneTestBase`                    |      12 |   ✗    |    ✗    | One-to-one Include; navigation-dependent                                                                                                                                                   |
| `ManyToManyQueryTestBase`                    |     104 |   ✗    |    ✓    | Many-to-many join table queries                                                                                                                                                            |
| `ManyToManyNoTrackingQueryTestBase`          |       1 |   ✗    |    ✗    | M:M + no-tracking; navigation-dependent                                                                                                                                                    |
| `OwnedQueryTestBase`                         |      95 |   ✓    |    ✗    | Owned entity queries; owned entities not supported in DynamoDB provider                                                                                                                    |
| `OwnedEntityQueryTestBase`                   |      11 |   ✓    |    ✗    | Owned entity edge cases; same constraint                                                                                                                                                   |
| `JsonQueryTestBase`                          |     203 |   ✓    |    ✗    | JSON column (`ToJson()`) queries; DynamoDB has no JSON column mapping                                                                                                                      |
| `AdHocJsonQueryTestBase`                     |      40 |   ✓    |    ✓    | Ad-hoc JSON column scenarios; same constraint                                                                                                                                              |
| `AdHocNavigationsQueryTestBase`              |      20 |   ✗    |    ✗    | Ad-hoc navigation scenarios                                                                                                                                                                |
| `AdHocManyToManyQueryTestBase`               |       2 |   ✗    |    ✗    | Ad-hoc M:M queries                                                                                                                                                                         |
| `NullKeysTestBase`                           |       5 |   ✗    |    ✗    | Nullable partition keys; DynamoDB does not support null keys                                                                                                                               |
| `SpatialQueryTestBase`                       |      84 |   ✗    |    ✗    | Geometry/geography spatial queries                                                                                                                                                         |
| `FilteredQueryTestBase`                      |       — |   ✗    |    ✗    | Filtered include queries; navigation-dependent                                                                                                                                             |
| `CompositeKeysQueryTestBase`                 |       7 |   ✗    |    ✗    | Inherited composite-key query specs are navigation-expansion tests over multi-level related collections; DynamoDB supports PK + SK keys but not EF relationship/navigation queries         |
| `SharedTypeQueryTestBase`                    |       1 |   ✗    |    ✗    | Single inherited test queries a keyless entity and filters through subquery Contains; DynamoDB requires a partition key for every root entity and does not support this subquery shape     |
| `AdHocComplexTypeQueryTestBase`              |      18 |   ✓    |    ✗    | Below threshold: ad-hoc scenarios are dominated by owned types, optional complex discriminators, indexes/alternate keys, and update/delete semantics outside DynamoDB query spec coverage  |
| `QueryFilterFuncletizationTestBase`          |      28 |   ✗    |    ✗    | Inherited coverage is sync-query-only (`ToList`/`Single`), while DynamoDB provider supports async query execution only; no meaningful executable async surface                             |
| `AdHocMiscellaneousQueryTestBase`            |      40 |   ✓    |    ✗    | Broad ad-hoc scenarios are dominated by joins, includes, navigations, `GROUP BY`, set operations, compiled sync queries, query cache internals, and other unsupported/non-DynamoDB shapes  |
| `AdHocQueryFiltersQueryTestBase`             |      23 |   ✗    |    ✗    | Inherited ad-hoc query-filter coverage is sync-query-heavy and includes weak entities, relationship/FK optimizations, joins, `GROUP BY`, and aggregate shapes unsupported by DynamoDB      |
| `AdHocAdvancedMappingsQueryTestBase`         |      14 |   ✗    |    ✗    | Advanced mapping scenarios are sync-query-heavy and dominated by navigations, joins, owned types, interface/EF.Property over owned members, relational type facets, and hierarchy mappings |

______________________________________________________________________

## Associations Tests

The `Associations` folder contains tests organized around relationship types. Navigation-based
sub-families are skipped; complex-property sub-families are partially implemented.

### Implemented

| Test Class                               | Methods | Cosmos | MongoDB | Notes                                                                                                                   |
| ---------------------------------------- | ------: | :----: | :-----: | ----------------------------------------------------------------------------------------------------------------------- |
| `ComplexPropertiesMiscellaneousTestBase` |       6 |   ✗    |    ✗    | Complex property scalar filters execute, including nullable value-type complex property `.Value` and `.HasValue` shapes |

### Implemented

| Test Class                                    | Methods | Cosmos | MongoDB | Notes                                                                                                                                                                                                                                                                                                                                                                                |
| --------------------------------------------- | ------: | :----: | :-----: | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `ComplexPropertiesStructuralEqualityTestBase` |      16 |   ✗    |    ✗    | Complex property structural equality; scalar and nested complex comparisons execute, while collection equality (issue #257), collection Contains, and complex-null parameter comparison are explicitly skipped                                                                                                                                                                       |
| `ComplexPropertiesProjectionTestBase`         |      20 |   ✗    |    ✗    | Complex property projections execute for root, scalar, structural, nullable value-type, and collection projections. Ordered base variants are adapted to unordered scans where DynamoDB cannot guarantee global ordering. Explicit skips cover navigation traversal, SelectMany/list flattening, subquery pushdown, and the remaining duplicate-root projection materialization gap. |

### Future

No association specification test classes are currently queued here.

### Skip — Navigation or Set Operation Dependent

| Test Class                                     | Methods | Reason                                       |
| ---------------------------------------------- | ------: | -------------------------------------------- |
| `AssociationsCollectionTestBase`               |      14 | Navigation collection traversal              |
| `AssociationsMiscellaneousTestBase`            |       3 | Navigation-dependent miscellaneous           |
| `AssociationsPrimitiveCollectionTestBase`      |       6 | Primitive collections on navigation entities |
| `AssociationsProjectionTestBase`               |      31 | Navigation projections                       |
| `AssociationsSetOperationsTestBase`            |       5 | Set operations on navigations                |
| `AssociationsStructuralEqualityTestBase`       |      15 | Navigation-based equality                    |
| `AssociationsBulkUpdateTestBase`               |      33 | Navigation + bulk update                     |
| All `Navigations/*` tests                      |      9+ | Navigation property traversal                |
| All `OwnedNavigations/*` tests                 |      7+ | Owned entity navigations                     |
| `ComplexPropertiesCollectionTestBase`          |       — | Complex type collections (not yet supported) |
| `ComplexPropertiesPrimitiveCollectionTestBase` |       — | Complex type + primitive collections         |
| `ComplexPropertiesSetOperationsTestBase`       |       — | Set operations; no PartiQL support           |
| `ComplexPropertiesBulkUpdateTestBase`          |       — | Blocked on `ExecuteUpdate`                   |

______________________________________________________________________

## Translations Tests

These tests use the `BasicTypesModel` fixture (separate from Northwind). The shared
`BasicTypesQueryDynamoFixture` now covers DynamoDB translation tests over scalar CLR types.
Cosmos DB implements all translation categories; MongoDB implements none.

### Operators

#### Implemented

| Test Class                               | Methods | Cosmos | Notes                                                                                                                                                                                                                                      |
| ---------------------------------------- | ------: | :----: | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `ComparisonOperatorTranslationsTestBase` |       6 |   ✓    | Equal, not-equal, less/greater-than; core PartiQL comparisons                                                                                                                                                                              |
| `LogicalOperatorTranslationsTestBase`    |       6 |   ✓    | `AND`, `OR`, `NOT`, and bool-property predicates                                                                                                                                                                                           |
| `GuidTranslationsTestBase`               |       4 |   ✓    | GUID equality, parameterization, and projection; `Guid.NewGuid()` predicate skipped                                                                                                                                                        |
| `EnumTranslationsTestBase`               |      18 |   ✓    | Enum equality for constant, parameter, and nullable shapes; bitwise/`HasFlag` shapes explicitly skipped                                                                                                                                    |
| `StringTranslationsTestBase`             |     100 |   ✓    | Explicit outcomes for all inherited string translation specs; equality, `Length`/`SIZE`, `IsNullOrEmpty`, `StartsWith(string)`, `Contains(string)`, and sign-based string comparisons execute; unsupported functions/overloads are skipped |
| `ByteArrayTranslationsTestBase`          |       8 |   ✓    | Binary length, non-empty, and equality execute via DynamoDB `size`/binary `=`; 5 byte-level membership/indexing methods are explicitly skipped because `byte[]` maps to one Binary attribute, not a byte collection                        |
| `DateTimeTranslationsTestBase`           |      19 |   ✓    | Constant/parameter `DateTime` equality executes for `Parse` and `new DateTime(...)`; date/time member and current-time functions are skipped because DynamoDB PartiQL has no temporal functions                                            |

#### Future

No operator translation test classes are currently queued here.

### Type Translations

No type translation test classes are currently queued here. `MathTranslationsTestBase` is skipped
because DynamoDB PartiQL does not support server-side math functions in `WHERE` or projection
expressions.

### Temporal Translations

DynamoDB has no native date/time types; values are stored as ISO 8601 strings. Temporal
translations are low-feasibility until dedicated temporal translation support is added.

No temporal translation test classes are currently queued here.

### Skip — DynamoDB PartiQL Constraints

| Test Class                                  | Methods | Cosmos | Reason                                                                                                                                                                                                                        |
| ------------------------------------------- | ------: | :----: | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ArithmeticOperatorTranslationsTestBase`    |       5 |   ✓    | DynamoDB rejects arithmetic operators in `WHERE` conditions; provider currently lets `+`, `-`, and `*` reach execution, while `%` and unary minus fail before execution                                                       |
| `BitwiseOperatorTranslationsTestBase`       |      15 |   ✓    | DynamoDB PartiQL has no bitwise operators or shifts; boolean `&`/\`                                                                                                                                                           |
| `DateOnlyTranslationsTestBase`              |      18 |   ✓    | Inherited coverage has no meaningful executable DynamoDB surface: every method requires unsupported date-part extraction, date arithmetic, or date/time conversion in server-side predicates                                  |
| `DateTimeOffsetTranslationsTestBase`        |      24 |   ✓    | Inherited coverage has no meaningful executable DynamoDB surface: temporal members/functions require unsupported temporal operations, and the only constant equality case is expressed through unsupported `COUNT` aggregates |
| `TimeOnlyTranslationsTestBase`              |      17 |   ✓    | Inherited coverage has no meaningful executable DynamoDB surface: every method requires unsupported time-part extraction, time arithmetic, or date/time conversion in server-side predicates                                  |
| `TimeSpanTranslationsTestBase`              |       6 |   ✓    | Inherited coverage has no meaningful executable DynamoDB surface: every method requires unsupported duration-part extraction in server-side predicates                                                                        |
| `MathTranslationsTestBase`                  |      66 |   ✓    | DynamoDB PartiQL does not support server-side math functions in `WHERE` or projection expressions                                                                                                                             |
| `MiscellaneousOperatorTranslationsTestBase` |       2 |   ✓    | Conditional and null-coalescing predicate shapes are not translated in server-side DynamoDB predicates                                                                                                                        |
| `MiscellaneousTranslationsTestBase`         |      18 |   ✓    | Random, `System.Convert`, and `Compare`/`CompareTo` methods are not translated in server-side DynamoDB predicates                                                                                                             |

______________________________________________________________________

## Coverage Summary

| Category              |              Implemented | Implement Next |                   Future |                        Skip |
| --------------------- | -----------------------: | -------------: | -----------------------: | --------------------------: |
| Non-Query (top-level) | 19 classes / 359 methods |              — |                        — |  25 classes / 1,403 methods |
| BulkUpdates           |                        — |              — | 5 classes / 135+ methods |        1 class / 33 methods |
| Northwind Query       |  8 classes / 458 methods |              — |                        — | 14 classes / 1,398+ methods |
| Other Query           |  6 classes / 337 methods |              — |                        — |  23 classes / 1,814 methods |
| Associations          |   3 classes / 42 methods |              — |                        — |  13+ classes / 123+ methods |
| Translations          |  7 classes / 161 methods |              — |                        — |     9 classes / 171 methods |

______________________________________________________________________

## Provider Feature Backlog vs DynamoDB Limits

This section separates spec-test work that can plausibly become executable after provider work from
coverage that is permanently blocked by DynamoDB/PartiQL capabilities. DynamoDB PartiQL supports
`SELECT`, `INSERT`, `UPDATE`, and `DELETE`; writes target one item per statement, and query support is
bounded by DynamoDB key access, document paths, and the documented function/operator subset.

### Future-supportable with provider work

| Provider feature                           | DynamoDB capability                                                                                         | Spec areas unlocked or improved                                                                                |
| ------------------------------------------ | ----------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| Key-addressable `ExecuteDelete`            | `DELETE FROM table WHERE key = ...` is supported for a single item                                          | Bulk update/delete specs where predicates can be reduced to item keys                                          |
| Key-addressable `ExecuteUpdate`            | `UPDATE table SET/REMOVE ... WHERE key = ...` is supported for a single item                                | Bulk update specs for simple scalar/complex-property assignments over known keys                               |
| Client-expanded batch/transaction writes   | Batch PartiQL supports up to 25 statements; transactions support up to 100 all-read or all-write statements | Broader bulk specs when the provider can first select keys, then issue per-item statements                     |
| Temporal equality/range translation        | ISO 8601 strings can be compared lexically when stored in normalized form                                   | More `DateTime` coverage and limited `DateOnly`/`DateTimeOffset`/`TimeOnly`/`TimeSpan` equality or range cases |
| `Take`/limit and continuation-token paging | DynamoDB paginates PartiQL results with continuation tokens                                                 | Miscellaneous query specs that need bounded result sets without relational offset semantics                    |
| Key-based ordering                         | `ORDER BY` is available for key-query patterns                                                              | Ordered-result specs over partition/sort-key queries                                                           |
| Document/complex-type path querying        | DynamoDB supports map/list document attributes and paths                                                    | More complex-property and primitive-collection query/projection cases                                          |
| Additional supported string/list functions | DynamoDB supports `begins_with`, `contains`, `size`, `attribute_type`, and `missing`                        | More string, collection, and null/missing translation coverage                                                 |
| Sync query execution policy                | DynamoDB SDK capability is not the blocker; provider currently chooses async-only query execution           | Sync-only spec methods and compiled-query specs, if provider deliberately adds sync execution                  |
| Provider-side defaults/conventions         | Application/provider code can synthesize values before writes                                               | Narrow non-relational subsets of store-generated/default-value specs, not database-computed values             |

### Permanently blocked by DynamoDB limitations

| Requested capability                              | DynamoDB limitation                                                                                         | Affected spec areas                                                                                      |
| ------------------------------------------------- | ----------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------- |
| Joins and navigation traversal                    | DynamoDB PartiQL has no `JOIN` and DynamoDB has no relational FK graph query model                          | Include, navigation, load/lazy-load, associations, many-to-many, GearsOfWar, complex-navigation suites   |
| `GROUP BY` and server aggregates                  | DynamoDB PartiQL has no `GROUP BY` or aggregate query functions such as `COUNT`, `SUM`, `AVG`, `MIN`, `MAX` | Northwind aggregate/group-by specs, aggregate collection subqueries, `EF.Functions.Like` through `Count` |
| Set operations                                    | `Union`, `Intersect`, and `Except` are not supported by DynamoDB PartiQL                                    | Northwind set-operation specs, complex-property set-operation specs                                      |
| Offset `Skip`                                     | DynamoDB uses continuation tokens, not offset pagination                                                    | Relational paging specs that require stable offset semantics                                             |
| Global non-key ordering                           | DynamoDB does not guarantee globally ordered scans; ordering is key-query oriented                          | Ordered-result specs over non-key scans                                                                  |
| Keyless root entities and null keys               | DynamoDB items require non-null table key attributes                                                        | Keyless entity specs, null-key specs, shared-type keyless query specs                                    |
| Composite keys beyond partition key plus sort key | DynamoDB table identity has at most partition key and sort key                                              | Multi-column composite-key specs beyond two key attributes                                               |
| JSON-column parity                                | DynamoDB has document attributes, not relational JSON column mapping via `ToJson()`                         | JSON type/query/ad-hoc JSON column specs                                                                 |
| Owned-entity relational semantics                 | Provider maps nested shapes as complex/document values; DynamoDB has no owned table/relationship model      | Owned query/entity specs unless rewritten as complex-type coverage                                       |
| Spatial types/functions                           | DynamoDB has no native spatial type or spatial query functions                                              | Spatial tests and spatial query tests                                                                    |
| Bitwise and broad math/temporal functions         | DynamoDB PartiQL exposes only a small operator/function subset                                              | Bitwise, math-function, temporal-member/function translation specs                                       |
| Database-computed/store-generated values          | DynamoDB has no relational computed columns, identity columns, or database defaults                         | Store-generated and store-generated-fixup specs                                                          |
| Relational transactions                           | DynamoDB transactions are limited item-statement transactions, not EF relational transaction semantics      | Specs requiring relational transaction scopes or cross-query relational semantics                        |

______________________________________________________________________

## Recent Implementation Order

This list records recently completed additions; authoritative implemented/not-implemented status is in the tables above.

### Immediate (complete)

1. `ComplianceDynamoTest` — 1 method
2. `OverzealousInitializationDynamoTest` — 1 method
3. `SaveChangesInterceptionDynamoTest` — 13 methods
4. `QueryExpressionInterceptionDynamoTest` — 4 methods
5. `NorthwindAsNoTrackingQueryDynamoTest` — 11 methods
6. `NorthwindAsTrackingQueryDynamoTest` — 5 methods
7. `NorthwindQueryTaggingQueryDynamoTest` — 9 methods
8. `NorthwindChangeTrackingQueryDynamoTest` — 17 methods
9. `CompositeKeyEndToEndDynamoTest` — 3 methods
10. `NorthwindFunctionsQueryDynamoTest` — 10 methods
11. `ConvertToProviderTypesDynamoTest` — 2 methods
12. `CustomConvertersDynamoTest` — 29 methods
13. `ComplexTypeQueryDynamoTest` — 74 methods
14. `SeedingDynamoTest` — 2 methods
15. `KeysWithConvertersDynamoTest` — 47 methods
16. `ComparisonOperatorTranslationsDynamoTest` — 6 methods
17. `LogicalOperatorTranslationsDynamoTest` — 6 methods
18. `ComplexPropertiesMiscellaneousDynamoTest` — 6 methods
19. `GuidTranslationsDynamoTest` — 4 methods
20. `EnumTranslationsDynamoTest` — 18 methods
21. `StringTranslationsDynamoTest` — 100 methods
22. `ComplexPropertiesStructuralEqualityDynamoTest` — 16 methods
23. `ComplexPropertiesProjectionDynamoTest` — 20 methods
24. `InheritanceQueryDynamoTest` — 52 methods
25. `PrimitiveCollectionsQueryDynamoTest` — 172 methods
26. `FunkyDataQueryDynamoTest` — 19 methods
27. `ByteArrayTranslationsDynamoTest` — 8 methods
28. `DateTimeTranslationsDynamoTest` — 19 methods
29. `FiltersInheritanceQueryDynamoTest` — 11 methods
30. `InheritanceComplexTypesQueryDynamoTest` — 9 methods
31. `EntityFrameworkServiceCollectionExtensionsDynamoTest` — 3 methods

### Near-term (small, high confidence)

No near-term specification test classes are currently queued here.

### Medium-term (requires investigation or new fixture)

No medium-term specification test classes are currently queued here.

### Long-term (after core coverage is stable)

1. `BulkUpdates` family — blocked on `ExecuteUpdate`/`ExecuteDelete`
2. Remaining translation tests (temporal)

### Current totals

| Status         | Classes | Methods |
| -------------- | ------: | ------: |
| Implemented    |      43 |   1,357 |
| Implement Next |       0 |       0 |
| Future         |       5 |    126+ |
| Skip           |     85+ |  4,942+ |
