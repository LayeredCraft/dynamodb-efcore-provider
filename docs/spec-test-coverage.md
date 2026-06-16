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
Tests that are always skipped still have value: they document known limitations and prevent false regressions.

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

| Test Class                            | Methods | Cosmos | MongoDB | Notes                                                                        |
| ------------------------------------- | ------: | :----: | :-----: | ---------------------------------------------------------------------------- |
| `ApiConsistencyTestBase`              |      18 |   ✓    |    ✓    | Provider API surface/naming conventions                                      |
| `BuiltInDataTypesTestBase`            |      28 |   ✓    |    ✓    | Scalar type round-trips (bool, int, string, DateTime, etc.)                  |
| `ComplexTypesTrackingTestBase`        |     128 |   ✓    |    ✗    | Complex type change tracking; DynamoDB complex types fully supported         |
| `ConcurrencyDetectorDisabledTestBase` |       1 |   ✓    |    ✗    | `ConcurrencyDetector` opt-out                                                |
| `ConcurrencyDetectorEnabledTestBase`  |       1 |   ✓    |    ✗    | `ConcurrencyDetector` opt-in                                                 |
| `FindTestBase`                        |      69 |   ✓    |    ✗    | `Find`/`FindAsync` by primary key                                            |
| `ComplianceTestBase`                  |       1 |   ✗    |    ✗    | Compliance marker for implemented provider spec bases                        |
| `OverzealousInitializationTestBase`   |       1 |   ✓    |    ✗    | Navigation-based fixup test is explicitly skipped                            |
| `LoggingTestBase`                     |       1 |   ✗    |    ✗    | Context-initialization logging covered; unsupported include path skipped     |
| `SaveChangesInterceptionTestBase`     |      13 |   ✗    |    ✗    | Transaction-dependent cases are explicitly skipped                           |
| `QueryExpressionInterceptionTestBase` |       4 |   ✓    |    ✗    | Spec `Single` shapes are skipped when they are not key-condition-only         |
| `MaterializationInterceptionTestBase` |       7 |   ✓    |    ✗    | Materialization interceptor coverage; owned/complex collection cases skipped |
| `CompositeKeyEndToEndTestBase`        |       3 |   ✗    |    ✗    | PK+SK round-trip covered; three-part composite-key cases skipped             |
| `ConvertToProviderTypesTestBase`      |       2 |   ✗    |    ✗    | Additional enum/provider-type conversion query methods beyond `BuiltInDataTypesTestBase` |
| `CustomConvertersTestBase`            |      29 |   ✓    |    ✗    | Value converter round-trips; unsupported converter edge cases explicitly skipped |
| `SeedingTestBase`                     |       2 |   ✗    |    ✗    | `HasData` seeding is covered; keyless entity seeding is skipped because DynamoDB requires partition keys |
| `ValueConvertersEndToEndTestBase`     |       1 |   ✗    |    ✗    | End-to-end converter insert/readback; DynamoDB fixture maps `ConvertingEntity` with partition key |
| `KeysWithConvertersTestBase`          |      47 |   ✓    |    ✗    | Converted partition-key mapping has DynamoDB-specific coverage; inherited FK, shadow-FK, and owned-entity cases are explicitly skipped |

Additional provider-specific coverage: `DynamoConcurrencyTest` has 6 local tests for DynamoDB optimistic concurrency behavior. It intentionally does not inherit `OptimisticConcurrencyTestBase` because DynamoDB concurrency semantics differ from the EF Core spec fixture.

### Implement Next

No non-query specification test classes are currently queued here.

### Future

Feasible but requires investigation or additional provider work before adding.

| Test Class                        | Methods | Cosmos | MongoDB | Feasibility | Blocker                                                                                                                                                                                             |
| --------------------------------- | ------: | :----: | :-----: | ----------: | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `WithConstructorsTestBase`        |      41 |   ✗    |    ✗    |        ~70% | Entities using non-default constructors; DynamoDB materializes via EF's normal pipeline                                                                                                             |
| `PropertyValuesTestBase`          |     167 |   ✗    |    ✗    |        ~55% | `CurrentValues`/`OriginalValues`/`GetDatabaseValues`; `GetDatabaseValues` requires a read which DynamoDB supports, but relational semantics for shadow keys and navigation tracking reduce coverage |
| `StoreGeneratedTestBase`          |      58 |   ✗    |    ✗    |        ~50% | Store-generated keys and concurrency tokens; partially supported (DynamoDB auto-generates string PKs but not sequences)                                                                             |
| `DataAnnotationTestBase`          |      84 |   ✗    |    ✗    |        ~40% | Attribute-driven model configuration; many tests exercise relational-only annotations (schema, unique indexes, FK attributes)                                                                       |
| `FieldMappingTestBase`            |     101 |   ✗    |    ✗    |        ~40% | Backing field mapping; basic field mapping works but tests assume relational load scenarios                                                                                                         |

### Skip — Architectural Constraints

| Test Class                                 | Methods | Cosmos | MongoDB | Reason                                                                     |
| ------------------------------------------ | ------: | :----: | :-----: | -------------------------------------------------------------------------- |
| `OptimisticConcurrencyTestBase`            |      33 |   ✓    |    ✗    | Covered by provider-specific `DynamoConcurrencyTest`; EF spec fixture semantics do not match DynamoDB concurrency |
| `LazyLoadTestBase`                         |      97 |   ✗    |    ✗    | Lazy loading requires navigation property support                          |
| `LazyLoadProxyTestBase`                    |      75 |   ✗    |    ✗    | Lazy load via proxies; requires navigations                                |
| `LoadTestBase`                             |     106 |   ✗    |    ✗    | Explicit/implicit load operations; navigation-dependent                    |
| `FieldsOnlyLoadTestBase`                   |     120 |   ✗    |    ✗    | Load entities with field-only backing; navigation-dependent                |
| `ManyToManyLoadTestBase`                   |      26 |   ✗    |    ✗    | M:M load; requires navigation and join table semantics                     |
| `ManyToManyFieldsLoadTestBase`             |      23 |   ✗    |    ✗    | Same as above with field backing                                           |
| `ManyToManyTrackingTestBase`               |      46 |   ✗    |    ✗    | M:M relationship tracking; no relational tracking                          |
| `UnidirectionalManyToManyLoadTestBase`     |      22 |   ✗    |    ✗    | Unidirectional M:M load; navigation-dependent                              |
| `UnidirectionalManyToManyTrackingTestBase` |      20 |   ✗    |    ✗    | Unidirectional M:M tracking; no relational tracking                        |
| `MonsterFixupTestBase`                     |       3 |   ✗    |    ✗    | Graph fixup for complex navigation graphs                                  |
| `ConferencePlannerTestBase`                |      22 |   ✗    |    ✗    | Real-world app with navigation properties and joins                        |
| `MusicStoreTestBase`                       |      18 |   ✗    |    ✗    | Real-world app; navigation properties and aggregations                     |
| `NotificationEntitiesTestBase`             |       2 |   ✗    |    ✗    | `INotifyPropertyChanged` entities; relational tracking semantics           |
| `DataBindingTestBase`                      |      37 |   ✗    |    ✗    | WPF/WinForms data binding; not applicable to document stores               |
| `SpatialTestBase`                          |       5 |   ✗    |    ✗    | Geometry/geography types; DynamoDB has no spatial support                  |
| `SerializationTestBase`                    |       1 |   ✗    |    ✗    | EF model serialization; provider-specific serialization not implemented    |
| `StoreGeneratedFixupTestBase`              |     118 |   ✗    |    ✗    | Store-generated value graph fixup; navigation fixup required               |
| `JsonTypesTestBase`                        |     242 |   ✓    |    ✗    | JSON column types (`ToJson()`); DynamoDB does not have JSON column mapping |
| `InterceptionTestBase`                     |       1 |   ✗    |    ✗    | Generic interception base; superseded by specific interceptor tests above  |

______________________________________________________________________

## BulkUpdates Tests

`ExecuteUpdate` and `ExecuteDelete` are not yet implemented in the provider. All bulk-update tests
are deferred until those features land.

| Test Class                              | Methods | Cosmos | MongoDB | Status                                              |
| --------------------------------------- | ------: | :----: | :-----: | --------------------------------------------------- |
| `NorthwindBulkUpdatesTestBase`          |      90 |   ✗    |    ✗    | Future — blocked on `ExecuteUpdate`/`ExecuteDelete` |
| `FiltersInheritanceBulkUpdatesTestBase` |      16 |   ✗    |    ✗    | Future — same blocker                               |
| `InheritanceBulkUpdatesTestBase`        |      18 |   ✗    |    ✗    | Future — same blocker                               |
| `NonSharedModelBulkUpdatesTestBase`     |      11 |   ✗    |    ✗    | Future — same blocker                               |
| `AssociationsBulkUpdateTestBase`        |      33 |   ✗    |    ✗    | Skip — also requires navigations                    |
| `ComplexPropertiesBulkUpdateTestBase`   |       — |   ✗    |    ✗    | Future — complex types + bulk update                |

______________________________________________________________________

## Northwind Query Tests

Uses the `Customer / Employee / Order / Product` dataset with `NorthwindQueryDynamoFixture<TModelCustomizer>`.

### Implemented

| Test Class                             | Methods | Cosmos | MongoDB | Notes                                                                                           |
| -------------------------------------- | ------: | :----: | :-----: | ----------------------------------------------------------------------------------------------- |
| `NorthwindWhereQueryTestBase`          |     203 |   ✓    |    ✓    | Predicate filtering; core WHERE coverage                                                        |
| `NorthwindSelectQueryTestBase`         |     186 |   ✓    |    ✓    | Projections and SELECT shapes                                                                   |
| `NorthwindAsNoTrackingQueryTestBase`   |      11 |   ✗    |    ✓    | `AsNoTracking()` passthrough; join/navigation-shaped cases skipped                              |
| `NorthwindAsTrackingQueryTestBase`     |       5 |   ✗    |    ✓    | `AsTracking()` on `IQueryable`; sync-only base tests assert async-only provider behavior        |
| `NorthwindQueryTaggingQueryTestBase`   |       9 |   ✗    |    ✓    | `TagWith()` has no translation impact; sync-only base tests assert async-only provider behavior |
| `NorthwindChangeTrackingQueryTestBase` |      17 |   ✗    |    ✓    | Query tracking behavior and state transitions; join-shaped modifier-precedence cases skipped    |
| `NorthwindFunctionsQueryTestBase`      |      10 |   ✓    |    ✓    | Static equality function covered; navigation/unsupported function cases skipped                 |

### Implement Next

No Northwind query specification test classes are currently queued here.

### Future

| Test Class                            | Methods | Cosmos | MongoDB | Feasibility | Blocker                                                                                                                                                   |
| ------------------------------------- | ------: | :----: | :-----: | ----------: | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `NorthwindQueryFiltersQueryTestBase`  |      17 |   ✗    |    ✓    |        ~65% | Global `HasQueryFilter`; feasible but not yet validated at scale                                                                                          |
| `NorthwindDbFunctionsQueryTestBase`   |       5 |   ✓    |    ✓    |        ~60% | `EF.Functions.*`; small surface, mostly skippable                                                                                                         |
| `NorthwindMiscellaneousQueryTestBase` |     469 |   ✓    |    ✓    |        ~35% | Very broad: Take/Skip, cast, null semantics, subqueries, async patterns; too many unsupported operators — below 70% threshold until core coverage matures |

### Skip — Architectural Constraints

| Test Class                                 | Methods | Cosmos | MongoDB | Reason                                                                                                       |
| ------------------------------------------ | ------: | :----: | :-----: | ------------------------------------------------------------------------------------------------------------ |
| `NorthwindGroupByQueryTestBase`            |     258 |   ✗    |    ✓    | PartiQL has no `GROUP BY`                                                                                    |
| `NorthwindJoinQueryTestBase`               |      68 |   ✗    |    ✓    | PartiQL has no `JOIN`                                                                                        |
| `NorthwindSetOperationsQueryTestBase`      |      91 |   ✗    |    ✓    | `Union`, `Intersect`, `Except` not supported in PartiQL                                                      |
| `NorthwindIncludeQueryTestBase`            |     118 |   ✗    |    ✓    | Eager loading requires navigation properties                                                                 |
| `NorthwindIncludeNoTrackingQueryTestBase`  |       — |   ✗    |    ✓    | Include + AsNoTracking; blocked by navigations                                                               |
| `NorthwindEFPropertyIncludeQueryTestBase`  |       — |   ✗    |    ✓    | `EF.Property`-named Include; blocked by navigations                                                          |
| `NorthwindStringIncludeQueryTestBase`      |       — |   ✗    |    ✓    | String-name Include; blocked by navigations                                                                  |
| `NorthwindNavigationsQueryTestBase`        |      73 |   ✗    |    ✓    | Navigation property traversal in LINQ                                                                        |
| `NorthwindKeylessEntitiesQueryTestBase`    |      18 |   ✓    |    ✓    | Keyless entities require no partition key; all DynamoDB entities need a key                                  |
| `NorthwindAggregateOperatorsQueryTestBase` |     211 |   ✓    |    ✓    | Below 70% threshold — Sum, Avg, Min, Max, Count aggregate functions unsupported in PartiQL; ~52% feasibility |
| `NorthwindCompiledQueryTestBase`           |      32 |   ✗    |    ✓    | `EF.CompileQuery` is sync-focused; DynamoDB provider is async-only                                           |
| `Ef6GroupByTestBase`                       |      55 |   ✗    |    ✓    | Legacy EF6 GROUP BY patterns; no GROUP BY in PartiQL                                                         |

______________________________________________________________________

## Other Query Tests

These tests use non-Northwind models and fixtures.

### Implemented

| Test Class | Methods | Cosmos | MongoDB | Notes |
|---|---:|:---:|:---:|---|
| `ComplexTypeQueryTestBase` | 74 | ✓ | ✗ | All inherited methods are registered/overridden with explicit outcomes; supported projection/filter, nested struct projection, and complex equality subsets execute, while navigation, set-operation, GroupBy, subquery/Contains, and pushdown cases are explicitly skipped |

### Future

| Test Class                                   | Methods | Cosmos | MongoDB | Feasibility | Rationale                                                                                                                   |
| -------------------------------------------- | ------: | :----: | :-----: | ----------: | --------------------------------------------------------------------------------------------------------------------------- |
| `AdHocComplexTypeQueryTestBase`              |      13 |   ✓    |    ✗    |        ~65% | Ad-hoc complex type query scenarios; same fixture dependency                                                                |
| `InheritanceQueryTestBase`                   |      52 |   ✓    |    ✗    |        ~60% | Single-table inheritance with discriminator; DynamoDB has discriminator support                                             |
| `FiltersInheritanceQueryTestBase`            |      11 |   ✗    |    ✗    |        ~55% | Query filters on inherited types                                                                                            |
| `FunkyDataQueryTestBase`                     |      19 |   ✗    |    ✗    |        ~60% | Edge-case strings (null chars, Unicode, SQL injection chars); WHERE translation should handle these                         |
| `PrimitiveCollectionsQueryTestBase`          |     156 |   ✓    |    ✗    |        ~50% | DynamoDB LIST/SET attribute querying; PartiQL supports `CONTAINS` on lists; complex collection operations not supported     |
| `NonSharedPrimitiveCollectionsQueryTestBase` |      27 |   ✗    |    ✗    |        ~45% | Primitive collections on non-shared models; same constraints as above                                                       |
| `QueryFilterFuncletizationTestBase`          |      28 |   ✗    |    ✗    |        ~60% | Parameter funcletization in query filters; translation-level feature                                                        |
| `AdHocMiscellaneousQueryTestBase`            |      39 |   ✓    |    ✗    |        ~40% | Mixed ad-hoc scenarios; some require unsupported operators                                                                  |
| `AdHocQueryFiltersQueryTestBase`             |      21 |   ✗    |    ✗    |        ~55% | Ad-hoc global query filter scenarios                                                                                        |
| `AdHocAdvancedMappingsQueryTestBase`         |      15 |   ✗    |    ✗    |        ~40% | Advanced mapping queries (TPT, TPC, owned types); mixed applicability                                                       |
| `CompositeKeysQueryTestBase`                 |       7 |   ✗    |    ✗    |        ~70% | Composite key queries; DynamoDB supports PK + SK; small test surface                                                        |
| `SharedTypeQueryTestBase`                    |       1 |   ✗    |    ✗    |        ~70% | Shared-type entity queries; single test                                                                                     |

### Skip — Architectural Constraints

| Test Class                                   | Methods | Cosmos | MongoDB | Reason                                                                  |
| -------------------------------------------- | ------: | :----: | :-----: | ----------------------------------------------------------------------- |
| `ComplexNavigationsQueryTestBase`            |     308 |   ✗    |    ✗    | Navigation-heavy hierarchical queries                                   |
| `ComplexNavigationsCollectionsQueryTestBase` |     156 |   ✗    |    ✗    | Navigation collection queries                                           |
| `GearsOfWarQueryTestBase`                    |     594 |   ✗    |    ✗    | Large navigation/join-heavy test suite                                  |
| `InheritanceRelationshipsQueryTestBase`      |      48 |   ✗    |    ✗    | Inheritance + navigation relationships                                  |
| `IncludeOneToOneTestBase`                    |      12 |   ✗    |    ✗    | One-to-one Include; navigation-dependent                                |
| `ManyToManyQueryTestBase`                    |     104 |   ✗    |    ✓    | Many-to-many join table queries                                         |
| `ManyToManyNoTrackingQueryTestBase`          |       1 |   ✗    |    ✗    | M:M + no-tracking; navigation-dependent                                 |
| `OwnedQueryTestBase`                         |      95 |   ✓    |    ✗    | Owned entity queries; owned entities not supported in DynamoDB provider |
| `OwnedEntityQueryTestBase`                   |      11 |   ✓    |    ✗    | Owned entity edge cases; same constraint                                |
| `JsonQueryTestBase`                          |     203 |   ✓    |    ✗    | JSON column (`ToJson()`) queries; DynamoDB has no JSON column mapping   |
| `AdHocJsonQueryTestBase`                     |      40 |   ✓    |    ✓    | Ad-hoc JSON column scenarios; same constraint                           |
| `AdHocNavigationsQueryTestBase`              |      20 |   ✗    |    ✗    | Ad-hoc navigation scenarios                                             |
| `AdHocManyToManyQueryTestBase`               |       2 |   ✗    |    ✗    | Ad-hoc M:M queries                                                      |
| `NullKeysTestBase`                           |       5 |   ✗    |    ✗    | Nullable partition keys; DynamoDB does not support null keys            |
| `SpatialQueryTestBase`                       |      84 |   ✗    |    ✗    | Geometry/geography spatial queries                                      |
| `FilteredQueryTestBase`                      |       — |   ✗    |    ✗    | Filtered include queries; navigation-dependent                          |

______________________________________________________________________

## Associations Tests

The `Associations` folder contains tests organized around relationship types. Navigation-based
sub-families are skipped; complex-property sub-families are future work.

### Future

| Test Class                                    | Methods | Cosmos | MongoDB | Feasibility | Notes                                  |
| --------------------------------------------- | ------: | :----: | :-----: | ----------: | -------------------------------------- |
| `ComplexPropertiesMiscellaneousTestBase`      |       3 |   ✗    |    ✗    |        ~70% | Miscellaneous complex property queries |
| `ComplexPropertiesProjectionTestBase`         |       4 |   ✗    |    ✗    |        ~70% | Complex type projections               |
| `ComplexPropertiesStructuralEqualityTestBase` |       1 |   ✗    |    ✗    |        ~70% | Structural equality on complex types   |

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

| Test Class                               | Methods | Cosmos | Notes                                                      |
| ---------------------------------------- | ------: | :----: | ---------------------------------------------------------- |
| `ComparisonOperatorTranslationsTestBase` |       6 |   ✓    | Equal, not-equal, less/greater-than; core PartiQL comparisons |
| `LogicalOperatorTranslationsTestBase`    |       6 |   ✓    | `AND`, `OR`, `NOT`, and bool-property predicates |

#### Future

| Test Class                                  | Methods | Cosmos | Feasibility | Notes                                                             |
| ------------------------------------------- | ------: | :----: | ----------: | ----------------------------------------------------------------- |
| `ArithmeticOperatorTranslationsTestBase`    |       5 |   ✓    |        ~80% | `+`, `-`, `*`, `/`, `%`; PartiQL arithmetic on numerics           |
| `BitwiseOperatorTranslationsTestBase`       |      15 |   ✓    |        ~20% | `&`, `\|`, `^`, `~`, `<<`, `>>`; PartiQL has no bitwise operators |

#### Skip — DynamoDB Expression Constraints

| Test Class                                  | Methods | Cosmos | Reason                                                                                   |
| ------------------------------------------- | ------: | :----: | ---------------------------------------------------------------------------------------- |
| `MiscellaneousOperatorTranslationsTestBase` |       2 |   ✓    | Provider does not translate conditional expressions, and coalesce is not supported today. |

### Type Translations

| Test Class                          | Methods | Cosmos | Feasibility | Notes                                                                                                      |
| ----------------------------------- | ------: | :----: | ----------: | ---------------------------------------------------------------------------------------------------------- |
| `StringTranslationsTestBase`        |     100 |   ✓    |        ~75% | `StartsWith`, `EndsWith`, `Contains`, `ToUpper`, `Substring`, etc.; PartiQL supports core string functions |
| `MathTranslationsTestBase`          |      66 |   ✓    |        ~50% | `Abs`, `Ceiling`, `Floor`, `Round`, `Sqrt`, `Log`, `Power`; PartiQL supports a subset                      |
| `MiscellaneousTranslationsTestBase` |      18 |   ✓    |        ~60% | Coalesce, null checks, type conversions                                                                    |
| `EnumTranslationsTestBase`          |      18 |   ✓    |        ~60% | Enum → numeric/string storage; mostly translatable                                                         |
| `GuidTranslationsTestBase`          |       4 |   ✓    |        ~60% | GUID stored as string; small surface                                                                       |
| `ByteArrayTranslationsTestBase`     |       7 |   ✓    |        ~40% | DynamoDB Binary type; limited PartiQL function support                                                     |

### Temporal Translations

DynamoDB has no native date/time types; values are stored as ISO 8601 strings. Temporal
translations are low-feasibility until dedicated temporal translation support is added.

| Test Class                           | Methods | Cosmos | Feasibility | Notes                               |
| ------------------------------------ | ------: | :----: | ----------: | ----------------------------------- |
| `DateTimeTranslationsTestBase`       |      19 |   ✓    |        ~35% | No native date functions in PartiQL |
| `DateTimeOffsetTranslationsTestBase` |      24 |   ✓    |        ~30% | Same constraint as `DateTime`       |
| `DateOnlyTranslationsTestBase`       |      18 |   ✓    |        ~30% | No native date-only type            |
| `TimeOnlyTranslationsTestBase`       |      17 |   ✓    |        ~25% | No native time type                 |
| `TimeSpanTranslationsTestBase`       |       6 |   ✓    |        ~25% | No native duration type             |

______________________________________________________________________

## Coverage Summary

| Category                    |              Implemented | Implement Next |                   Future |                       Skip |
| --------------------------- | -----------------------: | -------------: | -----------------------: | -------------------------: |
| Non-Query (top-level)       | 18 classes / 356 methods |              — |  5 classes / 451 methods | 20 classes / 1,017 methods |
| BulkUpdates                 |                        — |              — | 5 classes / 135+ methods |       1 class / 33 methods |
| Northwind Query             |  7 classes / 441 methods |              — |  3 classes / 491 methods |  12 classes / 924+ methods |
| Other Query                 |   1 class / 74 methods |              — | 12 classes / 389 methods | 16 classes / 1,683 methods |
| Associations                |                        — |              — |    3 classes / 8 methods | 13+ classes / 123+ methods |
| Translations                |   2 classes / 12 methods |              — | 13 classes / 307 methods |       1 class / 2 methods |

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

### Near-term (small, high confidence)

No near-term specification test classes are currently queued here.

### Medium-term (requires investigation or new fixture)

18. `ArithmeticOperatorTranslationsDynamoTest`
19. `StringTranslationsDynamoTest`

### Long-term (after core coverage is stable)

20. `InheritanceQueryDynamoTest` — blocked on `OfType`, `is`/`GetType()` discriminator translation, and fixture work
21. `PrimitiveCollectionsQueryDynamoTest`
22. `NorthwindQueryFiltersDynamoTest`
23. `BulkUpdates` family — blocked on `ExecuteUpdate`/`ExecuteDelete`
24. Remaining translation tests (Math, Miscellaneous, Enum, Guid)

### Current totals

| Status         | Classes | Methods |
| -------------- | ------: | ------: |
| Implemented    |      28 |     883 |
| Implement Next |       0 |       0 |
| Future         |      41 |  1,781+ |
| Skip           |     63+ |  3,782+ |
