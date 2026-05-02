---
title: Filtering
description: How Where clauses are translated to PartiQL filter expressions.
---

# Filtering

_`Where` clauses translate to PartiQL `WHERE` conditions; the provider validates the predicate shape before executing the query and fails with a clear error for unsupported forms._

## Basic Where Clauses

Standard comparison operators — `==`, `!=`, `<`, `<=`, `>`, `>=` — all translate to their PartiQL equivalents. Multiple conditions composed with `&&` become `AND`; `||` becomes `OR`. Boolean properties are normalized to an explicit comparison so that a bare `bool` member is valid in a predicate.

```csharp
var results = await db.Orders
    .Where(o => o.CustomerId == customerId && o.Total >= 50m && o.IsActive)
    .ToListAsync(cancellationToken);

// WHERE "CustomerId" = ? AND "Total" >= ? AND "IsActive" = true
```

## Null and Missing Values

DynamoDB has two distinct representations of "no value" for an attribute: a `NULL` type (the attribute key is present in the item but holds `{ NULL: true }`) and `MISSING` (the attribute key is entirely absent from the item). Because EF Core's `== null` check should match both states from the user's perspective, the provider expands it to cover both:

| LINQ expression                     | PartiQL emitted                                |
| ----------------------------------- | ---------------------------------------------- |
| `x.Prop == null`                    | `"Prop" IS NULL OR "Prop" IS MISSING`          |
| `x.Prop != null`                    | `"Prop" IS NOT NULL AND "Prop" IS NOT MISSING` |
| `EF.Functions.IsNull(x.Prop)`       | `"Prop" IS NULL`                               |
| `EF.Functions.IsNotNull(x.Prop)`    | `"Prop" IS NOT NULL`                           |
| `EF.Functions.IsMissing(x.Prop)`    | `"Prop" IS MISSING`                            |
| `EF.Functions.IsNotMissing(x.Prop)` | `"Prop" IS NOT MISSING`                        |

The `EF.Functions` methods are for advanced cases where the `NULL` vs `MISSING` distinction matters — for example, when querying items written by a non-EF client that may omit attributes entirely.

!!! note

    Parameterized null — comparing a property to a `null` variable (`x.Prop == someVar` where `someVar` is `null` at runtime) — uses `= ?` with an `AttributeValue { NULL: true }` parameter. This only matches attributes with the `NULL` type, not `MISSING` attributes. This is a DynamoDB engine constraint: the `IS` operator does not accept parameters.

## String Predicates

Two string methods translate to DynamoDB PartiQL functions:

- `string.Contains(s)` → `contains(attr, ?)` — a case-sensitive substring check.
- `string.StartsWith(s)` → `begins_with(attr, ?)` — a case-sensitive prefix match.

```csharp
// Substring check
db.Products.Where(p => p.Name.Contains("Widget"));
// WHERE contains("Name", ?)

// Prefix match — useful for sort key range patterns
db.Orders.Where(o => o.Sk.StartsWith("ORDER#2026"));
// WHERE begins_with("Sk", ?)
```

Only the `string`-parameter overload is supported. Overloads that accept `char`, `StringComparison`, or culture arguments are not translated and throw at query compilation.

!!! note

    `entity.Tags.Contains("x")` — testing whether a *collection attribute* contains a value — is not supported. Only in-memory collection membership (`ids.Contains(entity.Id)`) and string substring checks are translated.

## String Comparisons

DynamoDB compares strings lexicographically (UTF-8 code-point order). C# does not define `<`, `<=`, `>`, `>=` for `string` directly, so use `string.Compare` or `.CompareTo` compared against `0`. Both translate to a direct PartiQL comparison operator and are interchangeable:

```csharp
// string.Compare — static form
var events = await db.Events
    .Where(e => e.Pk == streamId && string.Compare(e.Sk, lastProcessedSk) > 0)
    .OrderBy(e => e.Sk)
    .ToListAsync(cancellationToken);
// WHERE "Pk" = ? AND "Sk" > ?

// .CompareTo — instance form; identical result
var events2 = await db.Events
    .Where(e => e.Pk == streamId && e.Sk.CompareTo(lastProcessedSk) > 0)
    .OrderBy(e => e.Sk)
    .ToListAsync(cancellationToken);
// WHERE "Pk" = ? AND "Sk" > ?
```

Both forms accept any comparison against the literal `0` (`==`, `!=`, `<`, `<=`, `>`, `>=`). Writing the constant on the left — `0 < e.Sk.CompareTo(bound)` — is also valid; the operator is mirrored automatically.

Combining a lower and upper bound on the same property triggers the `BETWEEN` rewrite. A common pattern uses `~` (ASCII 126, sorts after all alphanumeric characters) as a sentinel upper bound to capture all items within a prefix:

```csharp
var orders = await db.Orders
    .Where(o => o.Pk == tenantId
             && string.Compare(o.Sk, "ORDER#2026-01") >= 0
             && string.Compare(o.Sk, "ORDER#2026-01~") <= 0)
    .ToListAsync(cancellationToken);
// WHERE "Pk" = ? AND "Sk" BETWEEN ? AND ?
```

!!! note

    String comparisons are always case-sensitive and use the byte value of each character. `"Z"` sorts before `"a"` because uppercase letters have lower UTF-8 code points than lowercase. Design sort key prefixes with consistent casing to avoid unexpected ordering.

## Range Predicates (BETWEEN)

A LINQ predicate of the form `prop >= low && prop <= high` (both bounds inclusive, property on the left of each comparison) is rewritten to a single `BETWEEN` clause in PartiQL.

```csharp
var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
var to   = new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.Zero);

// ✅ Both bounds inclusive → BETWEEN rewrite fires
db.Orders.Where(o => o.Pk == "CUSTOMER#42" && o.CreatedAt >= from && o.CreatedAt <= to);
// WHERE "Pk" = ? AND "CreatedAt" BETWEEN ? AND ?

// ❌ Exclusive lower bound → two separate comparisons, no BETWEEN
db.Orders.Where(o => o.Pk == "CUSTOMER#42" && o.CreatedAt > from && o.CreatedAt <= to);
// WHERE "Pk" = ? AND "CreatedAt" > ? AND "CreatedAt" <= ?
```

The rewrite matters for sort key range queries. DynamoDB allows only one key condition on the sort key per query; two separate `>=`/`<=` comparisons are both treated as key conditions and rejected. `BETWEEN` counts as a single key condition and avoids this limit.

!!! warning

    The provider does not validate that `low <= high`. An inverted range like `o.Score >= 500 && o.Score <= 100` rewrites to `"Score" BETWEEN 500 AND 100`, which DynamoDB evaluates as an empty range and returns no results.

## IN Predicate

`collection.Contains(entity.Property)` translates to a PartiQL `IN` expression. The collection is expanded into positional parameters at execution time.

```csharp
var ids = new[] { "CUSTOMER#1", "CUSTOMER#2", "CUSTOMER#3" };

db.Orders.Where(o => ids.Contains(o.CustomerId));
// WHERE "CustomerId" IN [?, ?, ?]
```

An empty collection translates to a constant-false predicate (`1 = 0`), which returns no results without executing a DynamoDB request. DynamoDB enforces limits on `IN` list size: up to 50 values when the property is a partition key, up to 100 values for non-key attributes. Exceeding the limit throws at execution time.

## Queries vs Scans

Every DynamoDB `ExecuteStatement` request is either a **Query** or a **Scan**. A Query reads only the items in a specific partition; a Scan reads every item in the table (or index). Scans are expensive — they consume read capacity across all partitions and slow down as the table grows.

By default, the provider blocks scan-like reads before any DynamoDB request is sent. A query must
target exactly one partition with an equality (`=`) predicate on the active partition key. The
active key is the base-table key unless `.WithIndex(...)` or automatic index selection chooses a
secondary index.

```csharp
// ✅ Query — partition key equality
db.Orders.Where(o => o.Pk == customerId);

// ✅ Query — PK equality + non-key filter (filter runs after read, still a Query)
db.Orders.Where(o => o.Pk == customerId && o.Status == "PENDING");

// ❌ Blocked by default — partition key IN is multi-partition
db.Orders.Where(o => ids.Contains(o.Pk));

// ❌ Scan — comparison on partition key, not equality
db.Orders.Where(o => o.Pk != customerId);

// ❌ Scan — no partition key condition at all
db.Orders.Where(o => o.Status == "PENDING");
```

### Multiple conditions on the partition key

Applying more than one condition to the partition key is almost always a mistake. The provider's
default safe form is a single equality. Comparisons (`>`, `<`, `>=`, `<=`), `BETWEEN`, or
`begins_with` on the partition key do not narrow to a partition and are scan-like.

The scan guard is stricter than raw PartiQL here: partition-key `IN` and partition-key `OR` are
treated as scan-like because they fan out across multiple partitions. Use separate keyed queries
when you need to load a known set of partition keys.

```csharp
// ❌ Blocked by default — multi-partition PK OR
db.Orders.Where(o => o.Pk == id1 || o.Pk == id2);

// ❌ Scan — OR mixes PK with a non-key attribute
db.Orders.Where(o => o.Pk == id1 || o.Status == "PENDING");
```

### Sort key conditions

A sort key condition narrows results *within* a partition — it is only meaningful when a PK equality condition is already present. Valid sort key conditions are `=`, `<`, `<=`, `>`, `>=`, `BETWEEN`, and `begins_with`. DynamoDB allows **exactly one** sort key condition per request.

```csharp
// ✅ One sort key condition
db.Orders.Where(o => o.Pk == customerId && string.Compare(o.Sk, "ORDER#2026") >= 0);

// ✅ BETWEEN counts as one condition (provider rewrites >= + <= automatically)
db.Orders.Where(o => o.Pk == customerId
                  && string.Compare(o.Sk, "ORDER#2026-01") >= 0
                  && string.Compare(o.Sk, "ORDER#2026-01~") <= 0);
// WHERE "Pk" = ? AND "Sk" BETWEEN ? AND ?
```

Two sort key conditions that cannot be collapsed into `BETWEEN` — for example, a `>=` and a `<` on the same property — are both emitted as separate comparisons. DynamoDB rejects this as an invalid key condition combination and falls back to treating the sort key predicates as filter expressions, which means all items in the partition are read first.

Sort-key `IN`, sort-key `OR`, and multiple independent sort-key constraints are scan-like and are
blocked by default.

### Intentional scans

Use `.AllowScan()` when a single query is intentionally scan-like:

```csharp
await db.Orders
    .Where(o => o.Status == "PENDING")
    .AllowScan()
    .ToListAsync();
```

For broader migrations or controlled workloads, configure the context:

```csharp
options.UseDynamo(dynamo =>
{
    dynamo.ScanQueryBehavior(DynamoScanQueryBehavior.Throw); // default
    // dynamo.ScanQueryBehavior(DynamoScanQueryBehavior.Warn);
    // dynamo.ScanQueryBehavior(DynamoScanQueryBehavior.Allow);
});
```

`Warn` logs `ScanLikeQueryDetected` and executes. `Allow` executes silently.

```csharp
// ❌ Two separate SK conditions — NOT collapsed to BETWEEN (exclusive lower bound)
//    DynamoDB treats both as filter expressions; all items in the partition are read
db.Orders.Where(o => o.Pk == customerId
                  && string.Compare(o.Sk, "ORDER#2026-01") > 0   // exclusive — no BETWEEN rewrite
                  && string.Compare(o.Sk, "ORDER#2026-01~") <= 0);
// WHERE "Pk" = ? AND "Sk" > ? AND "Sk" <= ?
```

## Key Conditions vs Filter Expressions

!!! note

    DynamoDB distinguishes between *key conditions* (predicates on the partition key or sort key evaluated during index traversal) and *filter expressions* (predicates on non-key attributes applied after items are read). Filter expressions do not reduce read capacity unit consumption — DynamoDB reads the items first, then discards the ones that don't match.

In practice, this means a `Limit(n)` query evaluates `n` items and then applies the filter. If only a small fraction of the evaluated items match, fewer results are returned — but DynamoDB consumed read capacity for all `n` evaluated items. With highly selective non-key filters, a page may return zero matching items while still producing a non-null `NextToken`.

The provider never silently drops a `WHERE` predicate. Every condition you write is sent to DynamoDB; there is no implicit server-side stripping of unsupported filter shapes.

## Discriminator Filtering (Single-Table Design)

When multiple entity types share a single DynamoDB table, the provider automatically injects a discriminator predicate into every query on that entity type. The discriminator predicate composes with any user-supplied `Where` clause using `AND`.

```sql
-- DbSet<OrderEntity> on a shared table
SELECT "Pk", "Sk", "Total", "$type"
FROM "app-table"
WHERE "Pk" = 'CUSTOMER#42' AND "$type" = 'OrderEntity'
```

For inheritance hierarchies, querying a base type includes all concrete discriminator values in the hierarchy. See [Single-Table Design](../modeling/single-table-design.md) for configuration details.

## See also

- [Supported Operators](operators.md)
- [Index Selection](index-selection.md)
- [Ordering and Limiting](ordering-limiting.md)
- [Limitations](../limitations.md)
