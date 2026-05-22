---
title: Limitations
description: Known limitations and unsupported features in the DynamoDB EF Core provider.
icon: lucide/triangle-alert
---

# Limitations

_The DynamoDB EF Core provider does not support all standard EF Core features. This page is the
authoritative reference for what is not supported, why, and what workaround (if any) applies._

## Database lifecycle

- `SaveChanges` never creates DynamoDB tables. Call `EnsureCreatedAsync` explicitly or provision
    tables outside the provider.
- Lifecycle APIs are async-only. Synchronous `EnsureCreated`, `EnsureDeleted`, and `CanConnect`
    throw `NotSupportedException`.
- `EnsureCreatedAsync` creates missing tables with `PAY_PER_REQUEST` billing. It can add missing
    GSIs to existing on-demand tables, but cannot add GSIs to provisioned tables because lifecycle
    throughput configuration is not exposed yet. It cannot add LSIs after table creation. Lifecycle
    wait polling, backoff, timeout, and whether to wait for completion are configurable.
- Existing schema validation is limited to table key schema and secondary-index key/projection
    shape.
- `Include` secondary-index projection cannot be created yet because non-key projected attributes
    are not represented in provider metadata.

See [Table Lifecycle](configuration/lifecycle.md) for full behavior and seeding semantics.

## Unsupported LINQ Operators

The following operators throw `InvalidOperationException` at translation time. The provider does
not fall back to in-process evaluation for these — the exception surfaces before any DynamoDB
request is sent.

See [Supported Operators](querying/operators.md) for the full list of what does translate.

| Category             | Operators                                                                    | Why                                                                         |
| -------------------- | ---------------------------------------------------------------------------- | --------------------------------------------------------------------------- |
| Aggregation          | `Count`, `LongCount`, `Sum`, `Average`, `Min`, `Max`                         | DynamoDB PartiQL has no aggregate functions                                 |
| Grouping             | `GroupBy`                                                                    | `GROUP BY` is not supported in DynamoDB PartiQL                             |
| Joins                | `Join`, `GroupJoin`, `LeftJoin`, `RightJoin`, `SelectMany`, `DefaultIfEmpty` | DynamoDB does not support cross-item joins                                  |
| Set operations       | `Union`, `Concat`, `Except`, `Intersect`                                     | Not supported in DynamoDB PartiQL                                           |
| Offset / paging      | `Skip`, `Take`, `ElementAt`, `ElementAtOrDefault`                            | DynamoDB has no offset semantics — use `Limit(n)` for an evaluation budget  |
| Element operators    | `Single`, `SingleOrDefault`, `Any`, `All`                                    | Not supported server-side                                                   |
| Reverse traversal    | `Last`, `LastOrDefault`, `Reverse`                                           | Requires reverse index traversal, not implemented                           |
| Deduplication        | `Distinct`                                                                   | `SELECT DISTINCT` is not supported in DynamoDB PartiQL                      |
| Type filtering       | `OfType<T>`, `Cast<T>`                                                       | Not supported                                                               |
| Conditional skipping | `SkipWhile`, `TakeWhile`                                                     | Not supported                                                               |
| Queryable `Contains` | `Queryable.Contains(source, item)`                                           | Not supported; in-memory `collection.Contains(property)` translates to `IN` |

Value-converted enum numeric casts are also rejected when compared to parameters. For example, `(int)entity.Status == value` is not translated if `Status` uses `.HasConversion<string>()`, because DynamoDB stores the converted string value. Compare `entity.Status` to an enum value directly, or map the enum numerically.

Complex property-to-property equality and equality against complex object parameters are supported. Inline complex object constants are not translated; assign the value to a variable first or compare individual nested members instead.

**Workaround for unsupported operators:** switch to `AsAsyncEnumerable()` before the unsupported
operator to move evaluation in-process:

```csharp
// ❌ Throws at translation time
var count = await context.Orders.CountAsync();

// ✅ In-process
var count = await context.Orders.AsAsyncEnumerable().CountAsync();
```

In-process evaluation fetches all matching pages from DynamoDB before applying the operator.
Use with care on large result sets.

### `Take` vs `Limit(n)`

`Take(n)` is not translated — use the DynamoDB-specific `Limit(n)` extension instead. The
distinction matters: `Limit(n)` maps to `ExecuteStatementRequest.Limit`, which is an *evaluation
budget* (DynamoDB reads up to `n` items then filters). It is not a result count. See
[Ordering and Limiting](querying/ordering-limiting.md) for details.

## Query Shape Constraints

### `First` / `FirstOrDefault` — Key-Only Safe Path

`FirstAsync` and `FirstOrDefaultAsync` set an implicit `Limit=1` on the server request. Because
DynamoDB counts *evaluated* items against `Limit` (not matched items), this is only safe when the
`WHERE` clause guarantees at most one evaluation pass before a match:

1. No user-specified `Limit(n)` on the query.
2. The `WHERE` clause includes a partition-key equality condition.
3. Any sort-key predicate is a valid DynamoDB key condition (`=`, `<`, `<=`, `>`, `>=`,
    `BETWEEN`, `begins_with`).

By default, filtered `First*` queries that fail the partition-key or sort-key safety checks throw
`InvalidOperationException` at translation time. `AsUnsafeFilteredQuery()` and
`AllowUnsafeFilteredQueries()` can bypass only that filtered `First*` safety validation for
controlled legacy code or tests. Explicit `Limit(n)` combined with `First*` is never supported.

**Sort-key filter expressions are unsafe.** `SK IN (...)` and `SK = A OR SK = B` reference only
key attributes but are DynamoDB *filter expressions*, not key conditions. `Limit=1` on a filter
predicate can silently miss matching rows later in the partition:

```csharp
var skValues = new[] { "ORDER#1", "ORDER#2" };

// ❌ Throws — SK IN is a filter expression
await context.Orders
    .Where(x => x.Pk == pk && skValues.Contains(x.Sk))
    .FirstOrDefaultAsync(ct);

// ✅ Client-side selection via AsAsyncEnumerable()
var result = await context.Orders
    .Where(x => x.Pk == pk && skValues.Contains(x.Sk))
    .AsAsyncEnumerable()
    .FirstOrDefaultAsync(ct);
```

!!! warning "Unsafe filtered First* is not a best practice"

    `AsUnsafeFilteredQuery()` bypasses the provider's `First` / `FirstOrDefault` safety
    validation for one query. `AllowUnsafeFilteredQueries()` applies the same bypass to every
    query in the context.

    This does not disable scan-like query protection, does not allow explicit `Limit(n)` or
    `WithNextToken()` with `First*`, and does not change `First*` execution: the provider still
    sends one request with implicit `Limit=1` when no user limit is specified.

    DynamoDB applies filters after evaluating items, so `FirstOrDefaultAsync` can return `null`
    and `FirstAsync` can throw even when a later item would match. See AWS' notes on
    [filter expressions](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/Query.FilterExpression.html).
    Use this only for tests or controlled legacy code.

```csharp
var result = await context.Orders
    .Where(x => x.Pk == pk && skValues.Contains(x.Sk))
    .AsUnsafeFilteredQuery()
    .FirstOrDefaultAsync(ct);
```

**Exception — PK-only table.** When the base table has no sort key, each partition holds at most
one item and `First*` with a PK equality condition is always safe.

**Shared-table / inheritance.** The provider injects a discriminator predicate automatically.
Server-side `First*` is safe only when the query evaluates at most one base-table item before
filtering: a PK-only lookup on a PK-only table, or a PK+SK equality on a PK+SK table. By default,
all other shapes throw — use `AsAsyncEnumerable().FirstOrDefaultAsync()`, or explicitly opt in to
unsafe filtered `First*` behavior when you accept the DynamoDB filter-expression risk.

### `Find` — Primary-Key Lookup

`FindAsync` is supported for primary-key lookup. It checks the change tracker first and otherwise
executes a base-table key-equality PartiQL query with `Limit=1`. It does not use secondary indexes
or automatic index selection; use LINQ with `.WithIndex(...)` or automatic selection for
secondary-index lookups.

Synchronous `Find` follows EF Core's normal change-tracker behavior: it can return an already
tracked entity without DynamoDB I/O. If it needs to query DynamoDB, synchronous query execution
throws `InvalidOperationException`. Use `FindAsync` for database lookups:

```csharp
var order = await context.Orders.FindAsync([customerId, orderId], ct);
```

### `WithNextToken` Cannot Combine with `First*`

Combining `.WithNextToken(token)` with `FirstAsync` or `FirstOrDefaultAsync` throws
`InvalidOperationException`. A seeded continuation token implies resuming an arbitrary position in
a result set, which is incompatible with the server-side `Limit=1` required by `First*`.

### `OrderBy` — Only Key Columns

`OrderBy` and `OrderByDescending` only accept partition-key and sort-key column expressions.
Non-key attribute ordering throws at translation time. For multi-partition queries, the partition
key must be the first `ORDER BY` column.

### Automatic Index Selection — `ALL` Projection Only

Automatic index selection (`On` or `SuggestOnly` mode) rejects GSI/LSI candidates
whose projection type is not `ALL`. `KEYS_ONLY` and `INCLUDE` index candidates are logged as
rejected (`DYNAMO_IDX005`) and excluded from selection. Use an explicit `.WithIndex("name")`
hint to route to a non-ALL index.

### No `string.StartsWith` or `string.Contains` Overloads with Culture / Char

`string.StartsWith(s)` and `string.Contains(s)` translate to `begins_with` and `contains` in
PartiQL only for the single-`string`-argument overloads. Overloads that accept a `char`, a
`StringComparison`, or a `CultureInfo` argument throw at translation time.

### `SELECT *` Never Emitted

The provider always emits an explicit column list. This means projected types must have
all required attributes available in the index or table projection. See
[Projection](querying/projection.md).

## Write Constraints

### Synchronous `SaveChanges` Not Supported

`SaveChanges()` throws `NotSupportedException`. Use `SaveChangesAsync()`.

The AWS SDK for .NET exposes only async I/O for DynamoDB; the provider does not wrap async calls
synchronously to avoid deadlocks in ASP.NET Core and other async-first hosts.

### Key Mutation Not Supported

Changing a primary key (partition key or sort key) value on an entity and calling
`SaveChangesAsync` throws `NotSupportedException`. DynamoDB items are identified by their key
attributes; updating a key requires deleting the old item and inserting a new one. The provider
does not perform this two-step operation automatically — detach and re-add the entity with the
new key instead.

### DynamoDB Transaction Limits

DynamoDB `ExecuteTransaction` enforces two hard limits:

1. **Maximum 100 write statements per transaction.** When `AutoTransactionBehavior` is
    `WhenNeeded` or `Always` and the save unit exceeds `MaxTransactionSize` (default 100,
    max 100), the provider throws `InvalidOperationException` unless
    `TransactionOverflowBehavior.UseChunking` is configured.

2. **No duplicate items within a single transaction.** Writing the same DynamoDB item more than
    once in a single transaction throws `InvalidOperationException` — the provider validates this
    client-side before sending the request to DynamoDB.

See [Transactions](saving/transactions.md) for configuration details.

### `acceptAllChangesOnSuccess: false` Restrictions

Chunked transactional writes (`TransactionOverflowBehavior.UseChunking`) and non-atomic batched
writes (`AutoTransactionBehavior.Never`) both require `acceptAllChangesOnSuccess: true`.
Calling `SaveChangesAsync(acceptAllChangesOnSuccess: false)` with either path throws, because
partial chunk commits must be accepted immediately in the change tracker to avoid replaying
already-persisted writes on retry.

### PartiQL Statement Length Limit

DynamoDB enforces an 8 192-byte limit on `ExecuteStatement` statement text. The provider
validates statement length before sending and throws `InvalidOperationException` if the limit is
exceeded. This can happen with entities that have a large number of scalar properties. Consider
splitting such entities across multiple `SaveChanges` calls or reducing the number of mapped
properties.

### EF Core Bulk Operations Not Supported

`ExecuteUpdateAsync()` and `ExecuteDeleteAsync()` (EF Core 7+ bulk operations) are not
implemented. Bulk mutations must be performed by loading entities, modifying them in the change
tracker, and calling `SaveChangesAsync()`.

### `BatchExecuteStatement` Partial Success

When `AutoTransactionBehavior.Never` is set, the provider executes writes via `BatchExecuteStatement`.
DynamoDB executes each statement independently — a batch can partially succeed, meaning some
writes commit while others fail. The provider throws if the response contains any failed
operations, but successful statements within that batch have already been persisted.

## Modeling Constraints

### Relationships and Foreign Keys Are Not Supported

The provider does not support EF Core relationship modeling. `HasOne(...)`, `HasMany(...)`,
`WithOne(...)`, `WithMany(...)`, `HasForeignKey(...)`, skip navigations, and relationship
attributes such as `[ForeignKey]` and `[InverseProperty]` throw during model building or model
validation.

DynamoDB has no relational foreign-key enforcement or joins. Model embedded document data with
EF Core complex types, and model separate DynamoDB items or tables as separate root entity types
without EF navigation relationships.

```csharp
// ✅ Embedded data: complex types
modelBuilder.Entity<Customer>(b =>
{
    b.ComplexProperty(x => x.Profile);
    b.ComplexCollection(x => x.Contacts);
});

// ❌ Not supported: relational navigation/foreign-key modeling
modelBuilder.Entity<Order>()
    .HasOne(x => x.Customer)
    .WithMany(x => x.Orders)
    .HasForeignKey(x => x.CustomerId);
```

### Owned Entity Types Are Not Supported

The provider does not support EF Core owned entity types. `OwnsOne(...)`, `OwnsMany(...)`, and
other owned-type configuration paths throw during model validation with guidance to switch to
complex types instead.

Use EF Core complex types for embedded document data:

```csharp
modelBuilder.Entity<Customer>(b =>
{
    b.ComplexProperty(x => x.Profile);
    b.ComplexCollection(x => x.Contacts);
});
```

### Key Configuration

Root entities must use `HasPartitionKey(...)` and, when needed, `HasSortKey(...)`. Using
`HasKey(...)` or `[Key]` on a root entity throws during model validation.

Key properties must be non-nullable and resolve to a DynamoDB key-compatible provider type:
`string`, a numeric type (`int`, `long`, `decimal`, etc.), or `byte[]`. `bool` key properties
are rejected — `bool` has no built-in converter to a key-compatible type. Other non-primitive
types such as `Guid`, `DateTime`, and `enum` work because EF Core's built-in converters map
them to key-compatible store types (for example `Guid`/`DateTime` to `string`, and enum to
its numeric underlying value).

All entity types mapped to the same table must agree on key shape (PK-only or PK+SK) and must
use identical physical attribute names for the partition key and sort key.

See [Entities and Keys](modeling/entities-keys.md).

### Secondary-Index Key Constraints

Secondary-index key properties follow the same type requirements as table keys but may be nullable
(items without a scalar key-compatible value for a GSI/LSI key attribute are simply not indexed).

Local secondary indexes additionally require the table to define a sort key.

See [Secondary Indexes](modeling/secondary-indexes.md).

### Primitive Collection CLR Shapes

Primitive collection properties are supported only for specific CLR shapes. Custom or derived
collection types throw during model validation.

| Collection kind | Supported CLR shapes                                                                                                                     |
| --------------- | ---------------------------------------------------------------------------------------------------------------------------------------- |
| List            | `T[]`, `List<T>`, `IList<T>`                                                                                                             |
| Set             | `HashSet<T>`, `ISet<T>`, `IReadOnlySet<T>`                                                                                               |
| Dictionary      | `Dictionary<string, TValue>`, `IDictionary<string, TValue>`, `IReadOnlyDictionary<string, TValue>`, `ReadOnlyDictionary<string, TValue>` |

Dictionary keys must be `string`. Non-string-keyed dictionary types are not supported.

Complex collection properties use a narrower CLR shape set than primitive collections. Complex
collections support only `List<T>` and `IList<T>`. `ICollection<T>`, `IReadOnlyList<T>`, and
arrays are not supported for complex collections. See [Complex Types](modeling/complex-types.md)
for complex collection mapping details.

### Concurrency Tokens — Application-Managed Only

Concurrency tokens (`IsConcurrencyToken()` / `[ConcurrencyCheck]`) are supported, but the
provider does not generate or increment token values automatically. Your application code must
update the token value before calling `SaveChangesAsync`.

`IsRowVersion()` and `ValueGenerated.OnAddOrUpdate` throw during model validation because the
provider cannot guarantee auto-increment semantics on DynamoDB item writes.

### Shared-Table Discriminator Constraints

When multiple entity types share the same DynamoDB table, a discriminator is required. The
following constraints are validated at startup:

- Discriminator values must be unique within the table group.
- All entity types in the group must use the same discriminator attribute name.
- The discriminator attribute name must not collide with the partition key or sort key attribute
    names.

The default discriminator attribute name is `$type`.

See [Single-Table Design](modeling/single-table-design.md).

## Behavioral Differences from Standard EF Core

### Async-Only Execution

Synchronous query execution throws `InvalidOperationException`. This applies to all query
enumeration, not just `SaveChanges`. Methods like `ToList()`, `First()`, and `Count()` on a
`DbSet` will throw. `Find()` can still return an already-tracked entity without querying. Use
`ToListAsync()`, `FirstAsync()`, `FindAsync()`, `AsAsyncEnumerable()`, etc.

### `ToQueryString()` Is Debug-Only

`IQueryable<T>.ToQueryString()` returns generated PartiQL and formatted parameter comments without
sending a request to DynamoDB. It does not execute scan warnings, log command events, or validate
that DynamoDB accepts the statement at runtime. See [Diagnostics and Logging](diagnostics.md).

### Parameterized Null Inconsistency

When a nullable variable is `null` at runtime in a comparison (`x.Prop == someVar` where
`someVar` is `null`), the provider parameterizes the query as `WHERE "Prop" = ?` with an
`AttributeValue { NULL = true }`. This matches attributes stored with the DynamoDB NULL type but
does **not** match MISSING attributes (attributes absent from the item).

By contrast, a constant null comparison (`x.Prop == null`) translates to
`"Prop" IS NULL OR "Prop" IS MISSING`, which covers both representations.

DynamoDB PartiQL does not support `attr IS ?` (parameterized IS), so the two behaviors cannot
be unified. If you need to match both NULL and MISSING via a runtime variable, use explicit
functions:

```csharp
// Explicit: matches both NULL and MISSING at runtime
.Where(x => EF.Functions.IsNull(x.Prop) || EF.Functions.IsMissing(x.Prop))
```

### Two-Column Nullable Comparison

Comparing two nullable columns directly (`x.A == x.B` where both are nullable) generates a
binary `=` predicate. When either column holds a NULL type or is MISSING, DynamoDB PartiQL
returns MISSING (not TRUE) for the equality comparison — the row is excluded from results.
There is no provider-level workaround for this shape.

### Consistent Read Semantics Follow the Final Query Source

The provider can set `ExecuteStatementRequest.ConsistentRead` globally with
`options.ConsistentRead(true)` or per query with `.WithConsistentRead()`. Per-query settings take
precedence, including `.WithConsistentRead(false)` overriding a global strongly consistent default.

Strong consistency is sent only when the finalized query source is the base table or an LSI. If a
global strongly consistent default query is finalized to a GSI through explicit index routing or
automatic index selection, the provider leaves `ConsistentRead` unset because DynamoDB GSIs are
always eventually consistent. If a query explicitly calls `.WithConsistentRead()` and the finalized
source is a GSI, the provider throws before sending the request.

The provider does not warn or fail for scan-like queries. It passes allowed consistency settings
through to DynamoDB and lets DynamoDB apply the service semantics for the specific statement.

### Per-Entity Response Metadata Requires Tracking

`context.Entry(entity).GetExecuteStatementResponse()` returns `null` for entities loaded via
`AsNoTracking()`. The `ExecuteStatementResponse` is stored in a shadow property that only exists
on tracked entity entries. See [Diagnostics and Logging](diagnostics.md#response-metadata).

### Owned Types in `Select` Project the Full Container

Accessing a nested owned property path in a `Select` projection
(`x.Profile.Address.City`) triggers client-side extraction: the full owned container attribute
(`"Profile"`) is fetched from DynamoDB and the nested value is read in-process. The path does
translate server-side in `Where` predicates.

## See Also

- [Supported Operators](querying/operators.md)
- [Querying](querying/index.md)
- [Saving Data](saving/index.md)
- [Diagnostics and Logging](diagnostics.md)
