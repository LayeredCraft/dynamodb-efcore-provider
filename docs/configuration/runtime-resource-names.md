---
title: Runtime Resource Names
description: An advanced facility for EF compiled models and NativeAOT applications whose physical DynamoDB table and index names differ between environments.
icon: lucide/replace
---

# Runtime resource names

_An advanced facility for **EF compiled models and NativeAOT applications** that are built once and promoted between environments whose physical DynamoDB table or index names differ. Most applications do not need it._

!!! warning "This is not the general way to configure resource names"

    Configure tables and indexes in the model with `ToTable(...)` and `HasSecondaryIndexName(...)`, as
    described in [Table and Key Mapping](table-key-mapping.md) and
    [Secondary Indexes](../modeling/secondary-indexes.md). Do not introduce logical table identities
    and runtime mappings unless you are in the situation described below. This page documents a
    facility for that specific situation, not a recommended default.

## Do you need this?

You need runtime resource names only when **all** of these are true:

- EF uses a **compiled model** (required for NativeAOT), so `OnModelCreating` does not run at runtime;
- the **physical** DynamoDB table or index names differ between the environments you deploy to;
- you want to promote **one** compiled or native artifact unchanged between those environments.

You do **not** need it when:

- you use ordinary EF model building (JIT): read the names from configuration inside
  `OnModelCreating` or the entity configuration, as usual;
- the physical table and index names are the same in every environment, for example separate AWS
  accounts or Regions that keep identical resource names;
- rebuilding the compiled model separately for each environment is acceptable.

## Why DynamoDB needs a facility for this

Many databases give you a namespace layer between the model and the physical objects: a connection
string, a database, or a schema. DynamoDB has no equivalent indirection. Every PartiQL statement the
provider sends names the physical table, and the physical index, directly.

An EF compiled model is generated once, with whatever names the model was configured with at
generation time, and it skips `OnModelCreating` at runtime. If the names differ per environment (for
example CDK or CloudFormation generates `trivia-dev-a8f34…` and `trivia-prod-f42a1…`), the artifact
cannot know them when it is built. A runtime mapping from a stable **logical identity** to the
environment's **physical name** closes that gap.

## Terminology

| Term | Meaning | Configured with |
|---|---|---|
| **Logical table identity** | Stable model identity of a table, independent of its physical name | `HasLogicalTableName("Trivia")` |
| **Logical index identity** | Stable model identity of a secondary index: its EF index name | The name passed to `HasGlobalSecondaryIndex` / `HasLocalSecondaryIndex` |
| **Physical resource name** | The actual DynamoDB table or index name | `ToTable(...)`, `HasSecondaryIndexName(...)`, overridden at runtime by `RuntimeResourceNames` |

`ToTable(...)` and `HasSecondaryIndexName(...)` keep their normal meaning: the physical name. The
values you give them in the model are the **design-time** physical names. They are what the model
uses when no runtime mapping is configured, and what a compiled model is generated with.

## Declare the logical identities in the model

Only needed when you use runtime mappings.

```csharp
modelBuilder.Entity<Player>(b =>
{
    b.ToTable(designTimeTableName).HasLogicalTableName("Trivia");
    b.HasPartitionKey(x => x.Pk);
    b.HasSortKey(x => x.Sk);
    b.HasGlobalSecondaryIndex("Gsi2", x => x.Gs2Pk, x => x.Gs2Sk)
        .HasSecondaryIndexName(designTimeGsi2Name);
});
```

- **Tables.** The logical table identity belongs to the table, not to one entity type. When several
  entity types share a table (single-table design), declare it on any one of them; you do not have to
  repeat it on every entity type. Two entity types in the same table that declare *different* logical
  identities, or two different tables that declare the *same* one, are model validation errors.
- **Indexes.** The logical index identity is the EF index name. This follows EF's own convention: the
  model name of an index is also its default database name, and an explicit override replaces the
  database name without changing the model identity. Entity types that share a table and declare an
  index with the same name refer to the same physical index.

The logical table identity is stored in the model and survives compiled-model generation. The runtime
physical names below are never written into a compiled model.

## Supply the physical names at runtime

```csharp
services.AddDbContext<TriviaContext>(options => options.UseDynamo(dynamo => dynamo
    .RuntimeResourceNames(names => names
        .Table("Trivia", configuration["Dynamo:TableName"]!)
        .SecondaryIndex("Trivia", "Gsi1", configuration["Dynamo:Gsi1"]!)
        .SecondaryIndex("Trivia", "Gsi2", configuration["Dynamo:Gsi2"]!))));
```

The provider does not read `IConfiguration`, environment variables or any hosting concept. Your
application resolves the names and hands them to the provider.

An unmapped table or index keeps the physical name configured in the model, so partial mappings are
allowed. The mapping is value-based: equal mappings share one EF internal service provider and one
model, even when each context instance builds its own configuration.

## How it works

The mapping is applied once, when the model is initialized, before any query or write can use it:

1. The provider resolves each table's logical identity from the model.
2. The runtime physical names replace the design-time physical names as runtime metadata.
3. The runtime table model is built from the effective names.

Normal queries, precompiled queries, `SaveChanges`, `ExecuteUpdate`/`ExecuteDelete` and the table
lifecycle operations all read the effective names, so they agree without further configuration.

This behaves the same whether the model was built at runtime (JIT) or compiled: an explicit runtime
mapping wins over the physical name configured in the model. With no mapping configured, nothing
changes.

## Validation

The provider fails fast, when the model is initialized, with a message that names the offending
resource. It reports:

- a mapped logical table that the model does not declare (for example a typo such as `Triva` when the
  model declares `Trivia`), including the declared logical tables;
- a mapped index that the table does not declare, including the declared indexes;
- an index mapping for an unknown logical table;
- two tables, or two indexes of one table, mapped to the same physical name;
- conflicting or duplicated logical table identities in the model.

Mapping the same logical table or index twice is rejected when the mapping is configured, and empty
names are rejected.

## Table lifecycle and seeding

`EnsureCreatedAsync`, `EnsureDeletedAsync` and table/GSI validation use the effective physical names,
so a table is created, seeded and deleted under its runtime physical name. See
[Table Lifecycle](lifecycle.md).

## Lifetime: fixed per model, not tenant routing

The mappings are fixed for the lifetime of the initialized model. A compiled model is shared across
the process, so:

- Contexts that use the same compiled model must use equal mappings. Using an already initialized
  compiled model with a different mapping (or with none) throws an `InvalidOperationException`
  instead of silently using stale names.
- This feature is **not** a per-request table switch, a tenant-routing mechanism, or a way to
  reconfigure a running process.

## Native AOT and `dotnet ef`

Compiled-model and precompiled-query generation should see the design-time physical names only, so
the generated artifact stays independent of any environment. The runtime mapping belongs to the
running application, not the generator. If your context configures its options in `OnConfiguring`,
skip the mapping while EF is running its design-time tooling:

```csharp
if (!EF.IsDesignTime)
    dynamo.RuntimeResourceNames(names => names.Table("Trivia", physicalTableName));
```

Precompiled queries carry the design-time physical names in generated code and resolve the effective
names from the runtime model when they run. See
[Precompiled Queries and NativeAOT](../querying/precompiled-queries.md).

## `WithIndex(...)` and runtime resource names

The argument of `WithIndex("name")` is always a **physical index name**, never the logical index
identity. Which physical name depends on how the query runs:

| Execution mode | `WithIndex` argument |
|---|---|
| JIT model, no runtime mapping | The physical index name configured in the model (`HasSecondaryIndexName`, or the EF index name when no override is set). The logical identity is accepted only when it happens to equal the physical name. |
| JIT model with `RuntimeResourceNames` | The **runtime** physical index name. The logical identity and the design-time physical name are both rejected. |
| Precompiled query (NativeAOT) | The **design-time** physical index name. The query is translated against the design-time model when the code is generated; at runtime the precompiled query uses the mapped physical index. |

The same `WithIndex("…")` text is therefore **not portable** between a JIT model with a mapping and a
precompiled query. When you use runtime resource names, prefer
[automatic index selection](../querying/index-selection.md), which does not name an index at all.

## Limitations

- Mappings cannot change after the model is initialized.
- `WithIndex(...)` takes a physical index name, as described above; it does not accept logical index
  identities.
