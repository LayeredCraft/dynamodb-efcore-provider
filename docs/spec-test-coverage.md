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

---

## Non-Query Tests

These test classes live at the root of the spec project and cover ORM fundamentals: data types,
change tracking, concurrency, Find, value converters, interceptors, and more.

### Implemented

| Test Class | Methods | Cosmos | MongoDB | Notes |
|---|---:|:---:|:---:|---|
| `ApiConsistencyTestBase` | 18 | ✓ | ✓ | Provider API surface/naming conventions |
| `BuiltInDataTypesTestBase` | 28 | ✓ | ✓ | Scalar type round-trips (bool, int, string, DateTime, etc.) |
| `ComplexTypesTrackingTestBase` | 128 | ✓ | ✗ | Complex type change tracking; DynamoDB complex types fully supported |
| `ConcurrencyDetectorDisabledTestBase` | 1 | ✓ | ✗ | `ConcurrencyDetector` opt-out |
| `ConcurrencyDetectorEnabledTestBase` | 1 | ✓ | ✗ | `ConcurrencyDetector` opt-in |
| `OptimisticConcurrencyTestBase` | 33 | ✓ | ✗ | ETag / version token concurrency |
| `FindTestBase` | 69 | ✓ | ✗ | `Find`/`FindAsync` by primary key |
| `ComplianceTestBase` | 1 | ✗ | ✗ | Compliance marker for implemented provider spec bases |
| `OverzealousInitializationTestBase` | 1 | ✓ | ✗ | Navigation-based fixup test is explicitly skipped |
| `LoggingTestBase` | 1 | ✗ | ✗ | Context-initialization logging covered; unsupported include path skipped |
| `SaveChangesInterceptionTestBase` | 13 | ✗ | ✗ | Transaction-dependent cases are explicitly skipped |
| `QueryExpressionInterceptionTestBase` | 4 | ✓ | ✗ | `Single`-based query shapes are explicitly skipped |
| `MaterializationInterceptionTestBase` | 7 | ✓ | ✗ | Materialization interceptor coverage; owned/complex collection cases skipped |

### Implement Next

No non-query specification test classes are currently queued here.

### Future

Feasible but requires investigation or additional provider work before adding.

| Test Class | Methods | Cosmos | MongoDB | Feasibility | Blocker |
|---|---:|:---:|:---:|---:|---|
| `CustomConvertersTestBase` | 29 | ✓ | ✗ | ~75% | Value converter round-trips; core converter infrastructure supported |
| `KeysWithConvertersTestBase` | 47 | ✓ | ✗ | ~70% | Keys that pass through value converters; type mapping validation needed |
| `ValueConvertersEndToEndTestBase` | 1 | ✗ | ✗ | ~80% | Single E2E converter test; easy follow-on to `CustomConverters` |
| `ConvertToProviderTypesTestBase` | 2 | ✗ | ✗ | ~75% | `HasConversion<TProvider>` on properties; two tests |
| `WithConstructorsTestBase` | 41 | ✗ | ✗ | ~70% | Entities using non-default constructors; DynamoDB materializes via EF's normal pipeline |
| `SeedingTestBase` | 2 | ✗ | ✗ | ~80% | `HasData` seeding; two tests; DynamoDB seeding uses chunking |
| `PropertyValuesTestBase` | 167 | ✗ | ✗ | ~55% | `CurrentValues`/`OriginalValues`/`GetDatabaseValues`; `GetDatabaseValues` requires a read which DynamoDB supports, but relational semantics for shadow keys and navigation tracking reduce coverage |
| `StoreGeneratedTestBase` | 58 | ✗ | ✗ | ~50% | Store-generated keys and concurrency tokens; partially supported (DynamoDB auto-generates string PKs but not sequences) |
| `DataAnnotationTestBase` | 84 | ✗ | ✗ | ~40% | Attribute-driven model configuration; many tests exercise relational-only annotations (schema, unique indexes, FK attributes) |
| `FieldMappingTestBase` | 101 | ✗ | ✗ | ~40% | Backing field mapping; basic field mapping works but tests assume relational load scenarios |
| `CompositeKeyEndToEndTestBase` | 3 | ✗ | ✗ | ~70% | Composite PK round-trips (PK + SK); DynamoDB supports two-part keys; three tests |

### Skip — Architectural Constraints

| Test Class | Methods | Cosmos | MongoDB | Reason |
|---|---:|:---:|:---:|---|
| `LazyLoadTestBase` | 97 | ✗ | ✗ | Lazy loading requires navigation property support |
| `LazyLoadProxyTestBase` | 75 | ✗ | ✗ | Lazy load via proxies; requires navigations |
| `LoadTestBase` | 106 | ✗ | ✗ | Explicit/implicit load operations; navigation-dependent |
| `FieldsOnlyLoadTestBase` | 120 | ✗ | ✗ | Load entities with field-only backing; navigation-dependent |
| `ManyToManyLoadTestBase` | 26 | ✗ | ✗ | M:M load; requires navigation and join table semantics |
| `ManyToManyFieldsLoadTestBase` | 23 | ✗ | ✗ | Same as above with field backing |
| `ManyToManyTrackingTestBase` | 46 | ✗ | ✗ | M:M relationship tracking; no relational tracking |
| `UnidirectionalManyToManyLoadTestBase` | 22 | ✗ | ✗ | Unidirectional M:M load; navigation-dependent |
| `UnidirectionalManyToManyTrackingTestBase` | 20 | ✗ | ✗ | Unidirectional M:M tracking; no relational tracking |
| `MonsterFixupTestBase` | 3 | ✗ | ✗ | Graph fixup for complex navigation graphs |
| `ConferencePlannerTestBase` | 22 | ✗ | ✗ | Real-world app with navigation properties and joins |
| `MusicStoreTestBase` | 18 | ✗ | ✗ | Real-world app; navigation properties and aggregations |
| `NotificationEntitiesTestBase` | 2 | ✗ | ✗ | `INotifyPropertyChanged` entities; relational tracking semantics |
| `DataBindingTestBase` | 37 | ✗ | ✗ | WPF/WinForms data binding; not applicable to document stores |
| `SpatialTestBase` | 5 | ✗ | ✗ | Geometry/geography types; DynamoDB has no spatial support |
| `SerializationTestBase` | 1 | ✗ | ✗ | EF model serialization; provider-specific serialization not implemented |
| `StoreGeneratedFixupTestBase` | 118 | ✗ | ✗ | Store-generated value graph fixup; navigation fixup required |
| `JsonTypesTestBase` | 242 | ✓ | ✗ | JSON column types (`ToJson()`); DynamoDB does not have JSON column mapping |
| `InterceptionTestBase` | 1 | ✗ | ✗ | Generic interception base; superseded by specific interceptor tests above |

---

## BulkUpdates Tests

`ExecuteUpdate` and `ExecuteDelete` are not yet implemented in the provider. All bulk-update tests
are deferred until those features land.

| Test Class | Methods | Cosmos | MongoDB | Status |
|---|---:|:---:|:---:|---|
| `NorthwindBulkUpdatesTestBase` | 90 | ✗ | ✗ | Future — blocked on `ExecuteUpdate`/`ExecuteDelete` |
| `FiltersInheritanceBulkUpdatesTestBase` | 16 | ✗ | ✗ | Future — same blocker |
| `InheritanceBulkUpdatesTestBase` | 18 | ✗ | ✗ | Future — same blocker |
| `NonSharedModelBulkUpdatesTestBase` | 11 | ✗ | ✗ | Future — same blocker |
| `AssociationsBulkUpdateTestBase` | 33 | ✗ | ✗ | Skip — also requires navigations |
| `ComplexPropertiesBulkUpdateTestBase` | — | ✗ | ✗ | Future — complex types + bulk update |

---

## Northwind Query Tests

Uses the `Customer / Employee / Order / Product` dataset with `NorthwindQueryDynamoFixture<TModelCustomizer>`.

### Implemented

| Test Class | Methods | Cosmos | MongoDB | Notes |
|---|---:|:---:|:---:|---|
| `NorthwindWhereQueryTestBase` | 203 | ✓ | ✓ | Predicate filtering; core WHERE coverage |
| `NorthwindSelectQueryTestBase` | 186 | ✓ | ✓ | Projections and SELECT shapes |
| `NorthwindAsNoTrackingQueryTestBase` | 11 | ✗ | ✓ | `AsNoTracking()` passthrough; join/navigation-shaped cases skipped |
| `NorthwindAsTrackingQueryTestBase` | 5 | ✗ | ✓ | `AsTracking()` on `IQueryable`; sync-only base tests assert async-only provider behavior |
| `NorthwindQueryTaggingQueryTestBase` | 9 | ✗ | ✓ | `TagWith()` has no translation impact; sync-only base tests assert async-only provider behavior |

### Implement Next

No Northwind query specification test classes are currently queued here.

### Future

| Test Class | Methods | Cosmos | MongoDB | Feasibility | Blocker |
|---|---:|:---:|:---:|---:|---|
| `NorthwindFunctionsQueryTestBase` | 10 | ✓ | ✓ | ~70% | Math/string functions in WHERE; PartiQL coverage is partial |
| `NorthwindChangeTrackingQueryTestBase` | 17 | ✗ | ✓ | ~70% | Change tracking integration with queries; relational tracking not applicable |
| `NorthwindQueryFiltersQueryTestBase` | 17 | ✗ | ✓ | ~65% | Global `HasQueryFilter`; feasible but not yet validated at scale |
| `NorthwindDbFunctionsQueryTestBase` | 5 | ✓ | ✓ | ~60% | `EF.Functions.*`; small surface, mostly skippable |
| `NorthwindMiscellaneousQueryTestBase` | 469 | ✓ | ✓ | ~35% | Very broad: Take/Skip, cast, null semantics, subqueries, async patterns; too many unsupported operators — below 70% threshold until core coverage matures |

### Skip — Architectural Constraints

| Test Class | Methods | Cosmos | MongoDB | Reason |
|---|---:|:---:|:---:|---|
| `NorthwindGroupByQueryTestBase` | 258 | ✗ | ✓ | PartiQL has no `GROUP BY` |
| `NorthwindJoinQueryTestBase` | 68 | ✗ | ✓ | PartiQL has no `JOIN` |
| `NorthwindSetOperationsQueryTestBase` | 91 | ✗ | ✓ | `Union`, `Intersect`, `Except` not supported in PartiQL |
| `NorthwindIncludeQueryTestBase` | 118 | ✗ | ✓ | Eager loading requires navigation properties |
| `NorthwindIncludeNoTrackingQueryTestBase` | — | ✗ | ✓ | Include + AsNoTracking; blocked by navigations |
| `NorthwindEFPropertyIncludeQueryTestBase` | — | ✗ | ✓ | `EF.Property`-named Include; blocked by navigations |
| `NorthwindStringIncludeQueryTestBase` | — | ✗ | ✓ | String-name Include; blocked by navigations |
| `NorthwindNavigationsQueryTestBase` | 73 | ✗ | ✓ | Navigation property traversal in LINQ |
| `NorthwindKeylessEntitiesQueryTestBase` | 18 | ✓ | ✓ | Keyless entities require no partition key; all DynamoDB entities need a key |
| `NorthwindAggregateOperatorsQueryTestBase` | 211 | ✓ | ✓ | Below 70% threshold — Sum, Avg, Min, Max, Count aggregate functions unsupported in PartiQL; ~52% feasibility |
| `NorthwindCompiledQueryTestBase` | 32 | ✗ | ✓ | `EF.CompileQuery` is sync-focused; DynamoDB provider is async-only |
| `Ef6GroupByTestBase` | 55 | ✗ | ✓ | Legacy EF6 GROUP BY patterns; no GROUP BY in PartiQL |

---

## Other Query Tests

These tests use non-Northwind models and fixtures.

### Future

| Test Class | Methods | Cosmos | MongoDB | Feasibility | Rationale |
|---|---:|:---:|:---:|---:|---|
| `ComplexTypeQueryTestBase` | 76 | ✓ | ✗ | ~65% | Complex type queries (nested properties in WHERE/SELECT); DynamoDB supports complex types — needs fixture and investigation |
| `AdHocComplexTypeQueryTestBase` | 13 | ✓ | ✗ | ~65% | Ad-hoc complex type query scenarios; same fixture dependency |
| `InheritanceQueryTestBase` | 52 | ✓ | ✗ | ~60% | Single-table inheritance with discriminator; DynamoDB has discriminator support |
| `FiltersInheritanceQueryTestBase` | 11 | ✗ | ✗ | ~55% | Query filters on inherited types |
| `FunkyDataQueryTestBase` | 19 | ✗ | ✗ | ~60% | Edge-case strings (null chars, Unicode, SQL injection chars); WHERE translation should handle these |
| `PrimitiveCollectionsQueryTestBase` | 156 | ✓ | ✗ | ~50% | DynamoDB LIST/SET attribute querying; PartiQL supports `CONTAINS` on lists; complex collection operations not supported |
| `NonSharedPrimitiveCollectionsQueryTestBase` | 27 | ✗ | ✗ | ~45% | Primitive collections on non-shared models; same constraints as above |
| `QueryFilterFuncletizationTestBase` | 28 | ✗ | ✗ | ~60% | Parameter funcletization in query filters; translation-level feature |
| `AdHocMiscellaneousQueryTestBase` | 39 | ✓ | ✗ | ~40% | Mixed ad-hoc scenarios; some require unsupported operators |
| `AdHocQueryFiltersQueryTestBase` | 21 | ✗ | ✗ | ~55% | Ad-hoc global query filter scenarios |
| `AdHocAdvancedMappingsQueryTestBase` | 15 | ✗ | ✗ | ~40% | Advanced mapping queries (TPT, TPC, owned types); mixed applicability |
| `CompositeKeysQueryTestBase` | 7 | ✗ | ✗ | ~70% | Composite key queries; DynamoDB supports PK + SK; small test surface |
| `SharedTypeQueryTestBase` | 1 | ✗ | ✗ | ~70% | Shared-type entity queries; single test |

### Skip — Architectural Constraints

| Test Class | Methods | Cosmos | MongoDB | Reason |
|---|---:|:---:|:---:|---|
| `ComplexNavigationsQueryTestBase` | 308 | ✗ | ✗ | Navigation-heavy hierarchical queries |
| `ComplexNavigationsCollectionsQueryTestBase` | 156 | ✗ | ✗ | Navigation collection queries |
| `GearsOfWarQueryTestBase` | 594 | ✗ | ✗ | Large navigation/join-heavy test suite |
| `InheritanceRelationshipsQueryTestBase` | 48 | ✗ | ✗ | Inheritance + navigation relationships |
| `IncludeOneToOneTestBase` | 12 | ✗ | ✗ | One-to-one Include; navigation-dependent |
| `ManyToManyQueryTestBase` | 104 | ✗ | ✓ | Many-to-many join table queries |
| `ManyToManyNoTrackingQueryTestBase` | 1 | ✗ | ✗ | M:M + no-tracking; navigation-dependent |
| `OwnedQueryTestBase` | 95 | ✓ | ✗ | Owned entity queries; owned entities not supported in DynamoDB provider |
| `OwnedEntityQueryTestBase` | 11 | ✓ | ✗ | Owned entity edge cases; same constraint |
| `JsonQueryTestBase` | 203 | ✓ | ✗ | JSON column (`ToJson()`) queries; DynamoDB has no JSON column mapping |
| `AdHocJsonQueryTestBase` | 40 | ✓ | ✓ | Ad-hoc JSON column scenarios; same constraint |
| `AdHocNavigationsQueryTestBase` | 20 | ✗ | ✗ | Ad-hoc navigation scenarios |
| `AdHocManyToManyQueryTestBase` | 2 | ✗ | ✗ | Ad-hoc M:M queries |
| `NullKeysTestBase` | 5 | ✗ | ✗ | Nullable partition keys; DynamoDB does not support null keys |
| `SpatialQueryTestBase` | 84 | ✗ | ✗ | Geometry/geography spatial queries |
| `FilteredQueryTestBase` | — | ✗ | ✗ | Filtered include queries; navigation-dependent |

---

## Associations Tests

The `Associations` folder contains tests organized around relationship types. Navigation-based
sub-families are skipped; complex-property sub-families are future work.

### Future

| Test Class | Methods | Cosmos | MongoDB | Feasibility | Notes |
|---|---:|:---:|:---:|---:|---|
| `ComplexPropertiesMiscellaneousTestBase` | 3 | ✗ | ✗ | ~70% | Miscellaneous complex property queries |
| `ComplexPropertiesProjectionTestBase` | 4 | ✗ | ✗ | ~70% | Complex type projections |
| `ComplexPropertiesStructuralEqualityTestBase` | 1 | ✗ | ✗ | ~70% | Structural equality on complex types |

### Skip — Navigation or Set Operation Dependent

| Test Class | Methods | Reason |
|---|---:|---|
| `AssociationsCollectionTestBase` | 14 | Navigation collection traversal |
| `AssociationsMiscellaneousTestBase` | 3 | Navigation-dependent miscellaneous |
| `AssociationsPrimitiveCollectionTestBase` | 6 | Primitive collections on navigation entities |
| `AssociationsProjectionTestBase` | 31 | Navigation projections |
| `AssociationsSetOperationsTestBase` | 5 | Set operations on navigations |
| `AssociationsStructuralEqualityTestBase` | 15 | Navigation-based equality |
| `AssociationsBulkUpdateTestBase` | 33 | Navigation + bulk update |
| All `Navigations/*` tests | 9+ | Navigation property traversal |
| All `OwnedNavigations/*` tests | 7+ | Owned entity navigations |
| `ComplexPropertiesCollectionTestBase` | — | Complex type collections (not yet supported) |
| `ComplexPropertiesPrimitiveCollectionTestBase` | — | Complex type + primitive collections |
| `ComplexPropertiesSetOperationsTestBase` | — | Set operations; no PartiQL support |
| `ComplexPropertiesBulkUpdateTestBase` | — | Blocked on `ExecuteUpdate` |

---

## Translations Tests

These tests use the `BasicTypesModel` fixture (separate from Northwind). A dedicated
`BasicTypesDynamoFixture` must be created before any translation tests can be added.
Cosmos DB implements all translation categories; MongoDB implements none.

### Operators

| Test Class | Methods | Cosmos | Feasibility | Notes |
|---|---:|:---:|---:|---|
| `ComparisonOperatorTranslationsTestBase` | 6 | ✓ | ~95% | `<`, `<=`, `>`, `>=`, `==`, `!=`; core PartiQL comparisons |
| `LogicalOperatorTranslationsTestBase` | 6 | ✓ | ~95% | `AND`, `OR`, `NOT`; supported |
| `ArithmeticOperatorTranslationsTestBase` | 5 | ✓ | ~80% | `+`, `-`, `*`, `/`, `%`; PartiQL arithmetic on numerics |
| `MiscellaneousOperatorTranslationsTestBase` | 2 | ✓ | ~70% | Miscellaneous operators; mostly translatable |
| `BitwiseOperatorTranslationsTestBase` | 15 | ✓ | ~20% | `&`, `\|`, `^`, `~`, `<<`, `>>`; PartiQL has no bitwise operators |

### Type Translations

| Test Class | Methods | Cosmos | Feasibility | Notes |
|---|---:|:---:|---:|---|
| `StringTranslationsTestBase` | 100 | ✓ | ~75% | `StartsWith`, `EndsWith`, `Contains`, `ToUpper`, `Substring`, etc.; PartiQL supports core string functions |
| `MathTranslationsTestBase` | 66 | ✓ | ~50% | `Abs`, `Ceiling`, `Floor`, `Round`, `Sqrt`, `Log`, `Power`; PartiQL supports a subset |
| `MiscellaneousTranslationsTestBase` | 18 | ✓ | ~60% | Coalesce, null checks, type conversions |
| `EnumTranslationsTestBase` | 18 | ✓ | ~60% | Enum → numeric/string storage; mostly translatable |
| `GuidTranslationsTestBase` | 4 | ✓ | ~60% | GUID stored as string; small surface |
| `ByteArrayTranslationsTestBase` | 7 | ✓ | ~40% | DynamoDB Binary type; limited PartiQL function support |

### Temporal Translations

DynamoDB has no native date/time types; values are stored as ISO 8601 strings. Temporal
translations are low-feasibility until dedicated temporal translation support is added.

| Test Class | Methods | Cosmos | Feasibility | Notes |
|---|---:|:---:|---:|---|
| `DateTimeTranslationsTestBase` | 19 | ✓ | ~35% | No native date functions in PartiQL |
| `DateTimeOffsetTranslationsTestBase` | 24 | ✓ | ~30% | Same constraint as `DateTime` |
| `DateOnlyTranslationsTestBase` | 18 | ✓ | ~30% | No native date-only type |
| `TimeOnlyTranslationsTestBase` | 17 | ✓ | ~25% | No native time type |
| `TimeSpanTranslationsTestBase` | 6 | ✓ | ~25% | No native duration type |

---

## Coverage Summary

| Category | Implemented | Implement Next | Future | Skip |
|---|---:|---:|---:|---:|
| Non-Query (top-level) | 7 classes / 278 methods | 6 classes / 27 methods | 11 classes / ~535 methods | 19 classes / ~985 methods |
| BulkUpdates | — | — | 4 classes / 135 methods | 2 classes / 33 methods |
| Northwind Query | 2 classes / 389 methods | 3 classes / 25 methods | 5 classes / 518 methods | 9 classes / ~924 methods |
| Other Query | — | — | 13 classes / ~465 methods | 16 classes / ~1,680 methods |
| Associations | — | — | 3 classes / 8 methods | 12+ classes / 80+ methods |
| Translations (need fixture) | — | — | 16 classes / ~331 methods | — |

---

## Implementation Order

### Immediate (no new fixtures needed, builds on existing infrastructure)

1. `NorthwindAsNoTrackingQueryDynamoTest` — 11 methods, ~all pass
2. `NorthwindAsTrackingQueryDynamoTest` — 5 methods
3. `NorthwindQueryTaggingQueryDynamoTest` — 9 methods
4. `ComplianceDynamoTest` — 1 method
5. `OverzealousInitializationDynamoTest` — 1 method
6. `SaveChangesInterceptionDynamoTest` — 13 methods
7. `QueryExpressionInterceptionDynamoTest` — 4 methods

### Near-term (small, high confidence)

8. `NorthwindFunctionsQueryDynamoTest` — 10 methods
9. `NorthwindChangeTrackingQueryDynamoTest` — 17 methods
10. `MaterializationInterceptionDynamoTest` — 7 methods
11. `CompositeKeyEndToEndDynamoTest` — 3 methods (validates PK+SK)

### Medium-term (requires investigation or new fixture)

12. `CustomConvertersDynamoTest` / `KeysWithConvertersDynamoTest`
13. `InheritanceQueryDynamoTest` (discriminator support validation)
14. `ComplexTypeQueryDynamoTest` (needs `BasicTypesDynamoFixture`)
15. Translations operator tests (Comparison, Logical, Arithmetic) — needs `BasicTypesDynamoFixture`
16. `StringTranslationsDynamoTest` — needs `BasicTypesDynamoFixture`

### Long-term (after core coverage is stable)

17. `PrimitiveCollectionsQueryDynamoTest`
18. `NorthwindQueryFiltersDynamoTest`
19. `BulkUpdates` family — blocked on `ExecuteUpdate`/`ExecuteDelete`
20. Remaining translation tests (Math, Miscellaneous, Enum, Guid)