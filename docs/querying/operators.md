---
title: Supported Operators
description: Reference table of supported LINQ operators and their PartiQL translation.
---

# Supported Operators

_This page is the authoritative reference for which LINQ operators translate to PartiQL server-side, which are evaluated client-side, and which are unsupported._

## Filtering Operators

| LINQ Operator                                         | PartiQL Translation              | Notes                                                                                                                               |
| ----------------------------------------------------- | -------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------- |
| `Where`                                               | `WHERE`                          | Boolean properties normalized to `= true`                                                                                           |
| `==`, `!=`                                            | `=`, `<>`                        | Works on all scalar types                                                                                                           |
| `<`, `<=`, `>`, `>=`                                  | `<`, `<=`, `>`, `>=`             | Numeric and date/time properties only; C# does not define these operators on `string` — use string range helpers or compare methods |
| `EF.Functions.GreaterThan(a, b)`                      | `a > b`                          | String range helper; lexicographic DynamoDB ordering                                                                                |
| `EF.Functions.LessThan(a, b)`                         | `a < b`                          | String range helper; lexicographic DynamoDB ordering                                                                                |
| `EF.Functions.Between(a, low, high)`                  | `a BETWEEN low AND high`         | String range helper; inclusive lower and upper bounds                                                                               |
| `string.Compare(a, b) > 0` (any comparison against 0) | `a > b`                          | Alternative string range syntax; works with `==`, `!=`, `<`, `<=`, `>`, `>=` against the literal `0`                                |
| `a.CompareTo(b) > 0` (any comparison against 0)       | `a > b`                          | Same as above; `0 < a.CompareTo(b)` form also supported (operands mirrored)                                                         |
| `== null`                                             | `IS NULL OR IS MISSING`          | Covers both DynamoDB null representations, including nested complex-property paths such as `x.Profile.Address == null`              |
| `!= null`                                             | `IS NOT NULL AND IS NOT MISSING` | De Morgan inverse of `== null`; also works on nested complex-property paths                                                         |
| `EF.Functions.IsNull(prop)`                           | `prop IS NULL`                   | Explicit: NULL type only                                                                                                            |
| `EF.Functions.IsNotNull(prop)`                        | `prop IS NOT NULL`               | Explicit: not NULL type                                                                                                             |
| `EF.Functions.IsMissing(prop)`                        | `prop IS MISSING`                | Explicit: attribute absent from item                                                                                                |
| `EF.Functions.IsNotMissing(prop)`                     | `prop IS NOT MISSING`            | Explicit: attribute present                                                                                                         |
| `!expr`                                               | `NOT (expr)`                     | Operand always parenthesized                                                                                                        |
| `prop >= a && prop <= b`                              | `prop BETWEEN a AND b`           | Both bounds must be inclusive (`>=` and `<=`)                                                                                       |
| `string.Contains(s)`                                  | `contains(attr, ?)`              | Case-sensitive substring; no `char`/culture overloads                                                                               |
| `string.StartsWith(s)`                                | `begins_with(attr, ?)`           | Case-sensitive prefix; no `char`/`StringComparison` overloads                                                                       |
| `collection.Contains(prop)`                           | `prop IN [?, ...]`               | In-memory collection membership; max 50 PK values, 100 non-key values                                                               |

## Projection Operators

| LINQ Operator | PartiQL Translation  | Notes                                                                                                      |
| ------------- | -------------------- | ---------------------------------------------------------------------------------------------------------- |
| `Select`      | Explicit column list | `SELECT *` is never emitted; computed expressions and nested complex-property shaping evaluate client-side |

## Ordering Operators

| LINQ Operator                   | PartiQL Translation          | Notes                                                               |
| ------------------------------- | ---------------------------- | ------------------------------------------------------------------- |
| `OrderBy` / `OrderByDescending` | `ORDER BY col ASC\|DESC`     | Only partition key and sort key columns are valid                   |
| `ThenBy` / `ThenByDescending`   | Additional `ORDER BY` column | For multi-partition queries, partition key must be the first column |

## Terminal Operators

Terminal operators execute the query and return results. The provider supports the following terminals:

| LINQ Operator               | Behavior                                         | Notes                                                                                                                 |
| --------------------------- | ------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------- |
| `ToListAsync()`             | Fetches all pages; returns `List<T>`             | Provider follows `NextToken` until exhausted                                                                          |
| `AsAsyncEnumerable()`       | Streams results as `IAsyncEnumerable<T>`         | Marks the explicit client-side evaluation boundary; LINQ applied after this runs in-process                           |
| `ToPageAsync(limit, token)` | Single DynamoDB request; returns `DynamoPage<T>` | DynamoDB-specific; use for cursor-based pagination                                                                    |
| `First` / `FirstOrDefault`  | Implicit `Limit=1`; single request               | Key-only safe path by default; `AsUnsafeFilteredQuery()` bypasses safety validation but can miss later matching items |

## Primary-Key Lookup

Use `FindAsync` when you have the full DynamoDB table key and want one entity by primary key. It
checks the EF Core change tracker first. If the entity is not already tracked, the provider executes
a base-table key-equality PartiQL query with `ExecuteStatementRequest.Limit = 1`.

```csharp
// Partition-key-only table
var customer = await context.Customers.FindAsync(["CUSTOMER#123"], ct);

// Composite partition + sort key table; pass values in PK, SK order
var order = await context.Orders.FindAsync(["CUSTOMER#123", "ORDER#456"], ct);
```

`FindAsync` returns `null` when no item exists. It targets the base table and does not use secondary
indexes or automatic index selection; use normal LINQ with `.WithIndex(...)` or automatic selection
for secondary-index lookups. Synchronous `Find` can return an already-tracked entity, but throws if
it needs to query DynamoDB.

## DynamoDB-Specific Extensions

The following extension methods are unique to this provider and have no standard LINQ equivalent. They compose with the query before a terminal operator is called.

| Extension                 | Behavior                                                          | Notes                                                                                                                                                                                                                                                                          |
| ------------------------- | ----------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Limit(n)`                | Sets `ExecuteStatementRequest.Limit` — evaluation budget          | DynamoDB reads up to `n` items then applies filters; returned count is 0..n. Last call wins. See [Ordering and Limiting](ordering-limiting.md)                                                                                                                                 |
| `WithNextToken(token)`    | Seeds the first request with a saved continuation cursor          | Resumes a previous query from a known position. See [Pagination](pagination.md)                                                                                                                                                                                                |
| `WithConsistentRead()`    | Requests strongly consistent reads for this query                 | Sets `ExecuteStatementRequest.ConsistentRead = true` for base-table and LSI reads. Throws if the finalized source is a GSI because DynamoDB does not support strong consistency on GSIs. `WithConsistentRead(false)` explicitly uses eventually consistent reads for one query |
| `WithIndex(name)`         | Routes the query to a named GSI or LSI                            | Emits `FROM "Table"."Index"` in PartiQL. See [Index Selection](index-selection.md)                                                                                                                                                                                             |
| `WithoutIndex()`          | Forces base-table execution; suppresses automatic index selection | Emits diagnostic `DYNAMO_IDX006`. Cannot combine with `WithIndex(...)`                                                                                                                                                                                                         |
| `AsUnsafeFilteredQuery()` | Bypasses `First*` safety validation for this query                | Does not disable scan-like query protection or change `First*` execution semantics. Use only for tests or controlled legacy code                                                                                                                                               |

## Unsupported Operators

The following operators are not supported and throw `InvalidOperationException` at translation time. The provider does not silently fall back to in-process evaluation.

| Category              | Operators                                                                    | Reason                                                                                          |
| --------------------- | ---------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| Element operators     | `Single`, `SingleOrDefault`, `Any`                                           | Not supported server-side; use `AsAsyncEnumerable()` then LINQ-to-objects                       |
| Offset / paging       | `Skip`, `Take`, `ElementAt`, `ElementAtOrDefault`                            | DynamoDB has no offset semantics; use `Limit(n)` for evaluation budget                          |
| Reverse traversal     | `Last`, `LastOrDefault`, `Reverse`                                           | Requires reverse index traversal, not currently implemented                                     |
| Deduplication         | `Distinct`                                                                   | `SELECT DISTINCT` is not supported in DynamoDB PartiQL                                          |
| Aggregation           | `Count`, `LongCount`, `Sum`, `Average`, `Min`, `Max`                         | No aggregate functions in DynamoDB PartiQL; use `AsAsyncEnumerable()` for in-memory aggregation |
| Grouping              | `GroupBy`                                                                    | `GROUP BY` is not supported                                                                     |
| Joins                 | `Join`, `GroupJoin`, `SelectMany`, `LeftJoin`, `RightJoin`, `DefaultIfEmpty` | DynamoDB does not support cross-item joins                                                      |
| Set operations        | `Union`, `Concat`, `Except`, `Intersect`                                     | Not supported                                                                                   |
| Conditional filtering | `SkipWhile`, `TakeWhile`                                                     | Not supported                                                                                   |
| Type filtering        | `OfType<T>`, `Cast<T>`                                                       | Not supported                                                                                   |

## Scalar Type Support

All scalar entity properties must map to a DynamoDB attribute type. Types without a native wire representation use built-in EF Core value converters.

| .NET Type                               | DynamoDB Attribute Type | Wire Format                                                 |
| --------------------------------------- | ----------------------- | ----------------------------------------------------------- |
| `string`                                | `S`                     | —                                                           |
| `bool`                                  | `BOOL`                  | —                                                           |
| `byte`, `sbyte`, `short`, `int`, `long` | `N`                     | Numeric string                                              |
| `ushort`, `uint`, `ulong`               | `N`                     | Numeric string                                              |
| `float`, `double`, `decimal`            | `N`                     | Numeric string                                              |
| `byte[]`                                | `B`                     | Binary                                                      |
| `Guid`                                  | `S`                     | `"D"` format, e.g. `"550e8400-e29b-41d4-a716-446655440000"` |
| `DateTime`                              | `S`                     | ISO 8601 round-trip (`"O"`)                                 |
| `DateTimeOffset`                        | `S`                     | ISO 8601 round-trip (`"O"`)                                 |
| `DateOnly`                              | `S`                     | `"yyyy-MM-dd"`, e.g. `"2026-04-19"`                         |
| `TimeOnly`                              | `S`                     | `"HH:mm:ss"` (whole-second) or `"o"` (sub-second)           |
| `TimeSpan`                              | `S`                     | Constant (`"c"`) format, e.g. `"01:30:00"`                  |
| Enum                                    | `N`                     | Underlying numeric value (string names require a converter) |

Nullable variants of all types above are supported. Numeric comparisons use DynamoDB's single `N` wire type, so promotions to `int`, such as `short` to `int`, still bind as DynamoDB numbers. Custom types can be mapped using EF Core [value converters](https://learn.microsoft.com/en-us/ef/core/modeling/value-conversions); query parameters and constants use the property mapping so converters apply consistently.

Value-converted enums compare using the converter provider type. For example, an enum mapped with `.HasConversion<string>()` binds parameters as DynamoDB strings. Numeric casts of value-converted enums, such as `(int)entity.Status == value`, are not translated for parameter comparisons because the DynamoDB attribute stores the converted provider value. Compare enum values directly, or map the enum numerically if numeric comparisons are required.

## See also

- [How Queries Execute](how-queries-execute.md)
- [Filtering](filtering.md)
- [Limitations](../limitations.md)
