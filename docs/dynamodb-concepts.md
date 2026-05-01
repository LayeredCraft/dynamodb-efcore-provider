---
title: DynamoDB Concepts for EF Developers
description: Key DynamoDB concepts that affect how you model and query data with the EF Core provider.
icon: lucide/book-open
---

# DynamoDB Concepts for EF Developers

_DynamoDB is a key-value and document database with a fundamentally different access model from
relational databases — understanding its core concepts is essential before mapping your EF Core
model._

## Tables, Items, and Attributes

DynamoDB organizes data in **tables**. Each table contains **items** (analogous to rows), and each
item is a collection of **attributes** (analogous to columns). The similarity ends there.

| Relational         | DynamoDB                                              |
| ------------------ | ----------------------------------------------------- |
| Table              | Table                                                 |
| Row                | Item                                                  |
| Column             | Attribute                                             |
| Fixed schema       | Schemaless (only primary key attributes are required) |
| JOIN across tables | No joins — related data lives together                |

Unlike a relational table, a DynamoDB table has no fixed column schema beyond its primary key.
Two items in the same table can have entirely different attributes. Each item can be up to 400 KB.

See [Core components of Amazon DynamoDB](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/HowItWorks.CoreComponents.html)
for the full model.

## Partition Key and Sort Key

Every DynamoDB table requires a **primary key**, which takes one of two forms:

### Simple primary key (partition key only)

A single attribute whose value is hashed to determine which physical storage partition holds the
item. No two items can share the same partition key value.

```
Table: Customers
PK: CustomerId (e.g. "cust-42")
```

### Composite primary key (partition key + sort key)

Two attributes together form the key. Items with the same partition key are stored together,
physically sorted by the sort key. Multiple items can share a partition key as long as their sort
keys differ.

```
Table: ShopData
PK: CustomerId  (e.g. "cust-42")
SK: EntityType  (e.g. "CUSTOMER", "ORDER#2024-01-15", "ORDER#2024-03-01")
```

This layout — one partition key, many sort keys — is the foundation of
[single-table design](#single-table-design).

**Why partition key matters for queries**: DynamoDB can only locate items efficiently when it knows
the partition key. A query without a partition key condition performs a full table scan, reading
every item. Always include the partition key in `Where` filters.

See [Primary key](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/HowItWorks.CoreComponents.html#HowItWorks.CoreComponents.PrimaryKey)
in the AWS documentation.

## Schemaless Design

Beyond the primary key, DynamoDB imposes no schema. Each item can carry any attributes in any
combination. This makes DynamoDB well-suited for storing multiple entity types in a single table —
a `Customer` item and an `Order` item can coexist in the same table with completely different
attribute sets.

With this provider, each EF entity type maps to attributes in a table. You decide which attributes
each entity exposes through its C# properties; DynamoDB simply stores whatever you write.

```csharp
// Customer and Order can share one table — each has its own attribute shape
modelBuilder.Entity<Customer>(b =>
{
    b.ToTable("ShopData");
    b.HasPartitionKey(c => c.CustomerId);
    b.HasSortKey(c => c.EntityType); // value: "CUSTOMER"
});

modelBuilder.Entity<Order>(b =>
{
    b.ToTable("ShopData");
    b.HasPartitionKey(o => o.CustomerId);
    b.HasSortKey(o => o.SortKey); // value: "ORDER#<date>"
});
```

See [Supported data types and naming rules](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/HowItWorks.NamingRulesDataTypes.html)
for attribute naming constraints.

## Data Types

DynamoDB attributes fall into three categories:

### Scalar types

The primitive types. Only `String`, `Number`, and `Binary` can be used as primary key attributes.

| Type    | Descriptor | Notes                                                               |
| ------- | ---------- | ------------------------------------------------------------------- |
| String  | `S`        | Unicode (UTF-8); sort order is byte-level                           |
| Number  | `N`        | Variable precision, up to 38 digits; sent over the wire as a string |
| Binary  | `B`        | Raw bytes, base64-encoded in the API                                |
| Boolean | `BOOL`     | `true` / `false`                                                    |
| Null    | `NULL`     | Unknown or undefined state                                          |

!!! note "No native date/time type"

    DynamoDB has no built-in date or timestamp type. Store dates as ISO 8601 strings
    (e.g. `"2024-01-15T09:30:00Z"`) for human readability and lexicographic sort order, or as
    Unix epoch numbers when you need arithmetic. The provider uses EF Core's type mapping
    metadata to handle `DateTime` and `DateTimeOffset` conversions.

### Document types

Structured types for nesting data within an item.

- **Map** (`M`) — a nested key-value structure, equivalent to a JSON object. Nesting is supported
    up to 32 levels deep. Maps directly to EF Core complex properties.
- **List** (`L`) — an ordered collection that can hold values of mixed types. Maps to
    primitive lists and complex collections in the EF model.

### Set types

Unordered collections of unique scalar values. All values in a set must be the same type.

| Type       | Descriptor |
| ---------- | ---------- |
| String set | `SS`       |
| Number set | `NS`       |
| Binary set | `BS`       |

Sets enforce uniqueness and have no defined order. Use them only when the collection is truly a
mathematical set; for ordered or mixed-type collections, use a List instead.

See [Supported data types and naming rules](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/HowItWorks.NamingRulesDataTypes.html)
for full details including size limits.

## Secondary Indexes

When you need to query a table by attributes other than the primary key, DynamoDB provides two
types of secondary indexes:

**Global Secondary Index (GSI)** — defines an alternate partition key and optional sort key. GSI
data is stored separately from the main table and is replicated asynchronously, so reads from a
GSI are always eventually consistent. A table can have up to 20 GSIs.

**Local Secondary Index (LSI)** — shares the main table's partition key but uses a different sort
key. LSIs are created at table-creation time, are stored alongside the main table, and support
strongly consistent reads.

Both index types let you avoid full table scans for alternate access patterns. With this provider,
you declare indexes in `OnModelCreating`. Use `.WithIndex("...")` for explicit index routing, or
use the default automatic index selection for unambiguous compatible query shapes.

See [Secondary Indexes](modeling/secondary-indexes.md) for configuration details, and
[Using Global Secondary Indexes in DynamoDB](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/GSI.html)
in the AWS documentation.

## Single-Table Design

The most important concept for DynamoDB — and the one most unfamiliar to relational developers.

In a relational database, you create one table per entity type and use JOINs to combine them at
query time. DynamoDB has no JOIN operation, so retrieving related data efficiently requires
storing it in the same table, often in the same partition. AWS recommends using **as few tables
as possible** — in most cases, a single table per application.

See [Best practices for DynamoDB table design](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/bp-table-design.html)
and [NoSQL design for DynamoDB](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/bp-general-nosql-design.html).

### Design for access patterns, not normalization

Relational design normalizes the schema first and optimizes queries later. DynamoDB inverts this:
**start with the access patterns your application needs, then shape the data around them.**

Before settling on a schema, AWS recommends understanding three things upfront:

- **Data size** — how much data is stored and requested at once, to guide partition strategy
- **Data shape** — what the query results look like, so the stored shape matches the query output
- **Data velocity** — peak query volume, to distribute load evenly across partitions

### Item collections

All items that share the same partition key value form an **item collection** — they are stored
together on the same partition and can be fetched in a single efficient query. The sort key
determines the order within the collection and enables range queries.

This is how DynamoDB models one-to-many relationships without joins: store the parent and its
children in the same partition, differentiated by sort key prefix.

**Example: customer with orders**

```
PK          SK                  Attributes
─────────── ─────────────────── ────────────────────────────────────────────
cust-42     CUSTOMER            Name="Jane Smith", Email="jane@example.com"
cust-42     ORDER#2024-01-15    Total=149.99, Status="Shipped"
cust-42     ORDER#2024-03-01    Total=79.00,  Status="Processing"
```

Fetching a customer and all their orders is a single Query on `PK = "cust-42"`. Fetching only
orders uses a `begins_with` sort key condition — a native DynamoDB operator that matches all sort
keys starting with a given prefix: `begins_with(SK, 'ORDER#')`.

### Key overloading

In single-table design, the partition key and sort key attributes often have generic names like
`PK` and `SK` — their values carry the semantic meaning, not the attribute names. Multiple entity
types live in the same table, distinguished by sort key prefix or a dedicated `EntityType`
attribute.

```csharp
modelBuilder.Entity<Customer>(b =>
{
    b.ToTable("ShopData");
    b.HasPartitionKey(c => c.PK); // e.g. "cust-42"
    b.HasSortKey(c => c.SK);      // always "CUSTOMER"
});

modelBuilder.Entity<Order>(b =>
{
    b.ToTable("ShopData");
    b.HasPartitionKey(o => o.PK); // e.g. "cust-42" — same partition as the customer
    b.HasSortKey(o => o.SK);      // e.g. "ORDER#2024-01-15"
});
```

!!! tip "Single-table design with this provider"

    Map multiple entity types to the same table name using `b.ToTable("ShopData")` on each entity.
    Give each type a sort key whose value includes a distinguishing prefix. The provider routes
    reads and writes to the correct entity type based on the EF model; DynamoDB handles the storage.

### When multiple tables make sense

Single-table design is the right default, but there are cases where separate tables are justified:

- **High-volume time-series data** with a different retention policy than the rest of your data
- **Radically different access patterns** that would create conflicting throughput demands on
    a shared table
- **Per-tenant isolation** requirements where data must not share physical storage

## No Joins

DynamoDB provides no JOIN operation. This is a fundamental constraint, not a missing feature.

In a relational system you normalize data into many tables and combine them at query time. In
DynamoDB you **denormalize** — store related data together in the same item or the same item
collection so it can be retrieved in one operation.

The practical consequences for EF Core:

- **Owned types** (nested `Map` attributes) are fully supported — they live inside the same item
    and require no join.
- **Navigation properties across entity roots** are not supported. You cannot query a `Customer`
    and eagerly load its `Order` entities via `.Include()`. Structure your schema so that related
    data is co-located in the same partition.

See [Complex Properties and Collections](modeling/complex-types.md) for supported nesting patterns, and
[Limitations](limitations.md) for a complete list of unsupported EF Core features.

## Read Consistency

DynamoDB offers two consistency levels for read operations:

**Eventually consistent reads** (default) — the response may not reflect a write that completed
within the last second or two. DynamoDB replicates writes across multiple availability zones;
eventual consistency means you might read a slightly older version during that window. This is the
default for all reads and costs half as much as strongly consistent reads.

**Strongly consistent reads** — DynamoDB returns the most up-to-date data, guaranteed to reflect
all writes that received a successful response. Strong consistency is available on main tables and
LSIs; it is **not** available on GSIs or DynamoDB Streams.

For most application workloads, eventual consistency is acceptable and the right default. For
financial records, inventory counts, or other scenarios where stale reads are unacceptable,
use strongly consistent reads.

See [DynamoDB read consistency](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/HowItWorks.ReadConsistency.html)
in the AWS documentation.

## Async-Only API

!!! warning "Synchronous I/O is not supported"

    The DynamoDB SDK has no synchronous I/O surface — every network operation is async-only.
    All operations in this provider follow the same constraint. Use `ToListAsync`,
    `FirstOrDefaultAsync`, `SaveChangesAsync`, and other async methods throughout. `ToList()`,
    `SaveChanges()`, and other synchronous methods are not supported and will throw at runtime.

    Some query operators (including `Single` and `SingleOrDefault`) are not translated yet on
    `IQueryable` paths. For those shapes, switch to `AsAsyncEnumerable()` and apply
    `SingleAsync(...)` / `SingleOrDefaultAsync(...)` there.

## How This Affects Your EF Core Model

The concepts above translate directly into how you configure entities with this provider:

**Every entity requires a partition key** — declare it with `HasPartitionKey`. Without it, every
query is a full table scan.

```csharp
b.HasPartitionKey(c => c.CustomerId);
```

**Composite primary keys use a sort key** — declare with `HasSortKey`. Essential for single-table
designs where multiple entity types share a table.

```csharp
b.HasSortKey(c => c.EntityType);
```

**Complex properties model nested document attributes** — use `ComplexProperty` /
`ComplexCollection` to map C# objects to DynamoDB Maps and Lists. These are stored within the same
item and require no join.

```csharp
b.ComplexProperty(o => o.Address);
b.ComplexCollection(o => o.LineItems, li => { /* configure nested members */ });
```

**Secondary indexes enable alternate query patterns** — declare them in `OnModelCreating`, then use
`.WithIndex("...")` for explicit routing or the default automatic selection based on query shape.
See
[Secondary Indexes](modeling/secondary-indexes.md) and [Index Selection](querying/index-selection.md).

**LINQ translates to PartiQL** — the provider compiles your LINQ expressions to
[PartiQL](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.html)
statements. For efficient queries, always include a partition key equality condition in `Where`.
Queries without one result in a full table scan. See [How Queries Execute](querying/how-queries-execute.md).

**Navigation properties across entity roots are not supported** — `.Include()` does not work
across separately rooted entities. Design your schema so that data you need together is in the
same item (complex properties) or the same partition (single-table design).

## See Also

- [Getting Started](getting-started.md) — install, configure, and run your first query
- [Entities and Keys](modeling/entities-keys.md) — partition keys, sort keys, and composite keys
- [Secondary Indexes](modeling/secondary-indexes.md) — GSI and LSI configuration
- [Complex Properties and Collections](modeling/complex-types.md) — nested Maps and Lists
- [How Queries Execute](querying/how-queries-execute.md) — LINQ to PartiQL pipeline
- [Limitations](limitations.md) — unsupported EF Core features and why
