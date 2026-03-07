# Metadata Annotations and Builder Design

Back to index: [README](README.md)

This is the deep-dive section for metadata and Fluent API architecture.

## Design objective

Provide a user-friendly API for defining GSIs/LSIs while preserving EF metadata consistency, validation, and query-time discoverability.

## Existing foundation to build on

This provider already uses:

- annotation constants (`DynamoAnnotationNames`),
- metadata extensions (`GetXxx`/`SetXxx` for entity/property),
- builder extensions (`HasPartitionKey`, `HasSortKey`, `HasAttributeName`),
- conventions and model validators for finalization and consistency.

So secondary indexes should follow the same pattern, but on `IIndex` metadata.

## Metadata storage options

## Option A: entity-level annotation blob

Store all secondary index definitions as one entity annotation payload.

Pros:

- simple conceptual package.

Cons:

- not naturally integrated with EF index metadata,
- more custom parsing/validation work,
- weaker interoperability with standard EF index builder flows.

## Option B (recommended): `IIndex` + Dynamo annotations

Represent each secondary index as EF `IIndex` metadata and attach Dynamo-specific annotations.

Pros:

- native support for multiple indexes,
- best fit with EF conventions and config-source precedence,
- straightforward candidate enumeration for auto-index selection.

Cons:

- requires clear docs that these are Dynamo metadata constructs, not relational DDL.

## Option C: options-time registry only

Define indexes in `UseDynamoDb(...)` options instead of model metadata.

Pros:

- quick prototype.

Cons:

- brittle string-based mapping,
- weak refactor safety,
- weaker model-time validation.

Recommendation: Option B.

Current status:

- this is now the implemented design-time metadata path in the provider.
- secondary-index metadata lives on EF `IIndex` plus Dynamo annotations.
- runtime analysis metadata is derived from the finalized EF Core runtime model, not from a separate registry service.

## Proposed annotation keys (illustrative)

- `Dynamo:SecondaryIndexName`
- `Dynamo:SecondaryIndexKind`
- `Dynamo:SecondaryIndexProjectionType`
- `Dynamo:SecondaryIndexProjectedProperties`

Optional extension keys:

- `Dynamo:SecondaryIndexReadModelOnly` (if explicitly flagged for projection-only read models)
- `Dynamo:SecondaryIndexNotes` (diagnostic/debug metadata)

## Separate annotations vs one options record

Two valid internal storage shapes:

### Separate annotations

- easiest to inspect and debug,
- aligns with Relational/Npgsql patterns.

### One options record annotation

```csharp
public readonly record struct DynamoSecondaryIndexOptions(
    string Name,
    DynamoSecondaryIndexKind Kind,
    DynamoProjectionType ProjectionType,
    IReadOnlyList<string>? ProjectedProperties);
```

- compact immutable updates with `with` expressions,
- aligns with Mongo vector index pattern.

Either works. Separate annotations are usually simpler for migrations/codegen-like tooling.

Current status:

- v1 currently uses separate annotations for `name`, `kind`, and `projection type`.
- include-list/projected-properties metadata remains deferred until the partial-projection milestone.

## Fluent API shape options

## Shape 1 (recommended): keys in method arguments + nested builder

```csharp
modelBuilder.Entity<Order>()
    .HasGlobalSecondaryIndex("ByCustomerCreatedAt", x => x.CustomerId, x => x.CreatedAtUtc)
    .ProjectsAll();

modelBuilder.Entity<Order>()
    .HasLocalSecondaryIndex("ByStatus", x => x.Status)
    .ProjectsInclude(x => x.Status, x => x.CustomerId, x => x.CreatedAtUtc);
```

Why this is the strongest UX:

- required key schema defined upfront,
- fewer invalid intermediate states,
- fluent projection configuration remains easy.

## Shape 2: chain keys after creation

```csharp
modelBuilder.Entity<Order>()
    .HasGlobalSecondaryIndex("ByCustomerCreatedAt")
    .HasPartitionKey(x => x.CustomerId)
    .HasSortKey(x => x.CreatedAtUtc)
    .ProjectsAll();
```

This is flexible but easier to misconfigure before model finalization.

## Shape 3: EF index builder first

```csharp
modelBuilder.Entity<Order>()
    .HasIndex(x => new { x.CustomerId, x.CreatedAtUtc })
    .HasDynamoIndexName("ByCustomerCreatedAt")
    .IsGlobalSecondaryIndex()
    .ProjectsAll();
```

Good for advanced EF users, but less focused and more prone to mixed semantics.

## Should there be a dedicated sub-builder object?

Yes.

Suggested type:

```csharp
public sealed class DynamoSecondaryIndexBuilder<TEntity>
    where TEntity : class
{
    public DynamoSecondaryIndexBuilder<TEntity> ProjectsAll();
    public DynamoSecondaryIndexBuilder<TEntity> ProjectsKeysOnly();
    public DynamoSecondaryIndexBuilder<TEntity> ProjectsInclude(
        params Expression<Func<TEntity, object?>>[] projectedProperties);
}
```

Why this helps:

- keeps Dynamo-specific API focused,
- avoids exposing unrelated relational index knobs,
- cleanly supports future additions without cluttering `EntityTypeBuilder`.

## Proposed extension signatures

```csharp
public static DynamoSecondaryIndexBuilder<TEntity> HasGlobalSecondaryIndex<TEntity>(
    this EntityTypeBuilder<TEntity> entityTypeBuilder,
    string indexName,
    Expression<Func<TEntity, object?>> partitionKey,
    Expression<Func<TEntity, object?>>? sortKey = null)
    where TEntity : class;

public static DynamoSecondaryIndexBuilder<TEntity> HasLocalSecondaryIndex<TEntity>(
    this EntityTypeBuilder<TEntity> entityTypeBuilder,
    string indexName,
    Expression<Func<TEntity, object?>> sortKey)
    where TEntity : class;
```

## Projection defaults and partial projection policy

Default should be `ProjectsAll()` when not explicitly set.

Why:

- safest for full entity materialization,
- avoids accidental null/default-data behavior,
- minimizes surprise for users adopting indexes incrementally.

For partial projection (`INCLUDE`, `KEYS_ONLY`):

- allow it for projection queries that only require available attributes,
- reject full entity materialization from GSI when required attributes are missing,
- optionally support read-model entity patterns for projected index rows.

Example projection-only usage:

```csharp
var rows = await db.Orders
    .WithIndex("ByCustomerCreatedAt")
    .Where(x => x.CustomerId == customerId)
    .Select(x => new { x.CustomerId, x.CreatedAtUtc, x.Status })
    .ToListAsync(cancellationToken);
```

## Dedicated read-model option for partial projections

If product wants partial index projections heavily, prefer dedicated read models over nullable "maybe missing" fields on core entities.

```csharp
public sealed class OrderByCustomerIndexRow
{
    public string TenantId { get; set; } = null!;
    public string OrderId { get; set; } = null!;
    public string CustomerId { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public string Status { get; set; } = null!;
}
```

This keeps materialization semantics explicit and avoids hidden null/default ambiguity.

## Validation additions required in `DynamoModelValidator`

Add secondary-index-specific validation steps:

1. index name required,
2. index names unique per table group,
3. LSI partition key equals table partition key,
4. index key types must be Dynamo key-compatible scalar types,
5. shared-table mappings with same index name must agree on schema/projection,
6. if discriminator is required for table group, index projection for entity materialization must include discriminator or be `ALL`.

## `IConventionIndexBuilder` support

To align with EF provider patterns, include convention overloads and `CanSet` methods:

```csharp
public static IConventionIndexBuilder? HasDynamoIndexName(
    this IConventionIndexBuilder indexBuilder,
    string? name,
    bool fromDataAnnotation = false);

public static bool CanSetDynamoIndexName(
    this IConventionIndexBuilder indexBuilder,
    string? name,
    bool fromDataAnnotation = false)
    => indexBuilder.CanSetAnnotation("Dynamo:SecondaryIndexName", name, fromDataAnnotation);
```

For list metadata (`ProjectedProperties`), use structural equality checks to avoid noisy configuration conflicts.

## Runtime-model consumption

The implemented runtime path is:

1. keep user configuration in design-time annotations,
2. resolve those annotations into EF metadata objects (`IReadOnlyProperty` / `IReadOnlyIndex`) on the finalized runtime model,
3. cache the resolved descriptors as runtime annotations for later query compilation.

This keeps model configuration simple while avoiding repeated string-based lookups during analysis.

## EF Core vs provider responsibilities (idiomatic boundary)

This section compares what EF Core base already handles for configuration and what each provider must implement.

### What EF Core base handles for us

EF Core provides the configuration precedence engine and annotation plumbing:

- configuration source precedence (`Convention` < `DataAnnotation` < `Explicit`):
  - `efcore/src/EFCore/Metadata/ConfigurationSource.cs`
  - `efcore/src/EFCore/Metadata/ConfigurationSourceExtensions.cs`
- annotation set/can-set mechanics:
  - `efcore/src/EFCore/Infrastructure/AnnotatableBuilder.cs`
  - `efcore/src/EFCore/Infrastructure/ConventionAnnotatable.cs`
  - `efcore/src/EFCore/Metadata/Builders/IConventionAnnotatableBuilder.cs`
- base model validation orchestration:
  - `efcore/src/EFCore/Infrastructure/ModelValidator.cs`
- convention composition infrastructure:
  - `efcore/src/EFCore/Metadata/Conventions/Infrastructure/ProviderConventionSetBuilder.cs`
  - `efcore/src/EFCore/Metadata/Conventions/ConventionSet.cs`

Meaning:

- we should not invent a custom precedence system,
- we should express Dynamo index metadata through standard annotation APIs and `CanSet...` gates.

Concrete EF Core precedence code (from `ConfigurationSourceExtensions`):

```csharp
public static bool Overrides(this ConfigurationSource newConfigurationSource, ConfigurationSource? oldConfigurationSource)
{
    if (oldConfigurationSource == null)
    {
        return true;
    }

    if (newConfigurationSource == ConfigurationSource.Explicit)
    {
        return true;
    }

    if (oldConfigurationSource == ConfigurationSource.Explicit)
    {
        return false;
    }

    if (newConfigurationSource == ConfigurationSource.DataAnnotation)
    {
        return true;
    }

    return oldConfigurationSource != ConfigurationSource.DataAnnotation;
}
```

Why this matters for Dynamo:

- your `IConventionIndexBuilder` `CanSet...` methods should rely on this precedence model,
- explicit Fluent API should always win over conventions.

Concrete EF Core annotation gate code (from `AnnotatableBuilder`):

```csharp
public virtual bool CanSetAnnotation(string name, object? value, ConfigurationSource configurationSource)
{
    var existingAnnotation = Metadata.FindAnnotation(name);
    return existingAnnotation == null
        || CanSetAnnotationValue(existingAnnotation, value, configurationSource, canOverrideSameSource: true);
}
```

Why this matters for Dynamo:

- annotation writes should route through `CanSetAnnotation` semantics (directly or via provider extension patterns),
- avoid custom ad-hoc conflict resolution except for structural list comparisons.

### What providers must implement themselves

Providers define domain semantics on top of the EF base primitives:

1. provider annotation names,
2. metadata extensions (`Get/Set/GetConfigurationSource`) on metadata types,
3. fluent builder APIs (`IndexBuilder`, `IConventionIndexBuilder`, and provider wrappers),
4. provider conventions (if needed),
5. provider model validation rules.

For this repository, those are the exact places to add secondary-index behavior.

Concrete validator layering code pattern:

```csharp
// EF Core base
public virtual void Validate(IModel model, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
{
    ValidateIgnoredMembers(model, logger);
    ValidateEntityClrTypes(model, logger);
    ValidatePropertyMapping(model, logger);
    // ...
    ValidateTriggers(model, logger);
}

// Relational provider layer
public override void Validate(IModel model, IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
{
    base.Validate(model, logger);

    ValidateMappingFragments(model, logger);
    ValidatePropertyOverrides(model, logger);
    ValidateSqlQueries(model, logger);
    // ...
    ValidateIndexProperties(model, logger);
}
```

Why this matters for Dynamo:

- `DynamoModelValidator` should remain the place for Dynamo-only index rules,
- run base validator and then enforce provider constraints.

Concrete convention composition code pattern:

```csharp
public virtual ConventionSet CreateConventionSet()
{
    var conventionSet = new ConventionSet();

    conventionSet.Add(new ModelCleanupConvention(Dependencies));
    conventionSet.Add(new KeyDiscoveryConvention(Dependencies));
    conventionSet.Add(new RelationshipDiscoveryConvention(Dependencies));
    // ...
    return conventionSet;
}
```

Why this matters for Dynamo:

- if you add index-specific conventions, register them through `DynamoConventionSetBuilder` in the same composition model.

## Compare and contrast: how other providers configure index metadata

| Provider | Metadata storage | Fluent configuration style | Special configuration behavior | Design-time propagation |
| --- | --- | --- | --- | --- |
| EF Relational base | index annotations like name/filter on `IIndex` | `IndexBuilder` + `IConventionIndexBuilder` extensions | canonical `CanSetAnnotation` pattern | annotation code generator maps to fluent calls |
| SQL Server | many index annotations (`Clustered`, `Include`, `FillFactor`, etc.) | chainable `SqlServerIndexBuilderExtensions` | custom `CanSetIncludeProperties` structural equality for list values | provider annotation/code generators + SQL generator consume index annotations |
| Npgsql | rich index annotations including list-valued options (`IndexOperators`, include, null sort order) | chainable `NpgsqlIndexBuilderExtensions` | list metadata and custom equality-aware `CanSet` for include properties | annotation provider + code generator + migrations SQL generator use index annotations |
| Cosmos | provider annotations for entity/property/index + query hint capture pipeline | flat extensions plus some provider builders | strong use of convention overloads and query-hint lifecycle (`WithPartitionKey`) | provider design-time generators map annotations |
| Mongo | provider annotations + options payloads (including vector index options) | includes nested sub-builder patterns (`VectorIndexBuilder`) | immutable options object updates and callback overloads | provider-specific extensions and metadata serialization patterns |

### Key file references for the patterns above

- Relational:
  - `efcore/src/EFCore.Relational/Metadata/RelationalAnnotationNames.cs`
  - `efcore/src/EFCore.Relational/Extensions/RelationalIndexExtensions.cs`
  - `efcore/src/EFCore.Relational/Extensions/RelationalIndexBuilderExtensions.cs`
- SQL Server:
  - `efcore/src/EFCore.SqlServer/Metadata/Internal/SqlServerAnnotationNames.cs`
  - `efcore/src/EFCore.SqlServer/Extensions/SqlServerIndexExtensions.cs`
  - `efcore/src/EFCore.SqlServer/Extensions/SqlServerIndexBuilderExtensions.cs`
  - `efcore/src/EFCore.SqlServer/Design/Internal/SqlServerAnnotationCodeGenerator.cs`
- Npgsql:
  - `efcore.pg/src/EFCore.PG/Metadata/Internal/NpgsqlAnnotationNames.cs`
  - `efcore.pg/src/EFCore.PG/Extensions/MetadataExtensions/NpgsqlIndexExtensions.cs`
  - `efcore.pg/src/EFCore.PG/Extensions/BuilderExtensions/NpgsqlIndexBuilderExtensions.cs`
  - `efcore.pg/src/EFCore.PG/Design/Internal/NpgsqlAnnotationCodeGenerator.cs`
- Cosmos:
  - `efcore/src/EFCore.Cosmos/Metadata/Internal/CosmosAnnotationNames.cs`
  - `efcore/src/EFCore.Cosmos/Extensions/CosmosIndexExtensions.cs`
  - `efcore/src/EFCore.Cosmos/Extensions/CosmosIndexBuilderExtensions.cs`
  - `efcore/src/EFCore.Cosmos/Extensions/CosmosQueryableExtensions.cs`
- Mongo:
  - `mongo-efcore-provider/src/MongoDB.EntityFrameworkCore/Metadata/MongoAnnotationNames.cs`
  - `mongo-efcore-provider/src/MongoDB.EntityFrameworkCore/Extensions/MongoIndexExtensions.cs`
  - `mongo-efcore-provider/src/MongoDB.EntityFrameworkCore/Extensions/MongoIndexBuilderExtensions.cs`
  - `mongo-efcore-provider/src/MongoDB.EntityFrameworkCore/Metadata/VectorIndexBuilder.cs`

## Public API configuration examples (what users write)

This section intentionally shows only public-facing API surfaces so you can evaluate ergonomics.

## Dynamo proposed API (recommended feel)

### 1) Minimal configuration (defaults to `ProjectsAll`)

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>(entity =>
    {
        entity.ToTable("Orders");
        entity.HasPartitionKey(x => x.TenantId);
        entity.HasSortKey(x => x.OrderId);

        entity.HasGlobalSecondaryIndex("ByCustomerCreatedAt", x => x.CustomerId, x => x.CreatedAtUtc);
        entity.HasLocalSecondaryIndex("ByStatus", x => x.Status);
    });
}
```

### 2) Explicit projection choices on nested builder

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>(entity =>
    {
        entity.HasGlobalSecondaryIndex("ByCustomerCreatedAt", x => x.CustomerId, x => x.CreatedAtUtc)
            .ProjectsInclude(x => x.CustomerId, x => x.CreatedAtUtc, x => x.Status);

        entity.HasLocalSecondaryIndex("ByStatus", x => x.Status)
            .ProjectsAll();
    });
}
```

### 3) Callback overload style (same API, different ergonomics)

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>(entity =>
    {
        entity.HasGlobalSecondaryIndex(
            "ByCustomerCreatedAt",
            x => x.CustomerId,
            x => x.CreatedAtUtc,
            index => index.ProjectsInclude(x => x.CustomerId, x => x.CreatedAtUtc, x => x.Status));

        entity.HasLocalSecondaryIndex("ByStatus", x => x.Status, index => index.ProjectsAll());
    });
}
```

### 4) Alternative chain-keys style (less preferred, but possible)

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>(entity =>
    {
        entity.HasGlobalSecondaryIndex("ByCustomerCreatedAt")
            .HasPartitionKey(x => x.CustomerId)
            .HasSortKey(x => x.CreatedAtUtc)
            .ProjectsAll();
    });
}
```

### 5) Advanced EF index-first style

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>(entity =>
    {
        entity.HasIndex(x => new { x.CustomerId, x.CreatedAtUtc })
            .HasDynamoIndexName("ByCustomerCreatedAt")
            .IsGlobalSecondaryIndex()
            .ProjectsAll();
    });
}
```

### 6) Query-time public API usage

```csharp
var rows = await db.Orders
    .WithIndex("ByCustomerCreatedAt")
    .Where(x => x.CustomerId == customerId && x.CreatedAtUtc >= startUtc)
    .OrderByDescending(x => x.CreatedAtUtc)
    .Take(50)
    .ToListAsync(cancellationToken);
```

### 7) Projection-only query against partial GSI

```csharp
var projected = await db.Orders
    .WithIndex("ByCustomerCreatedAt")
    .Where(x => x.CustomerId == customerId)
    .Select(x => new OrderSearchRow(x.CustomerId, x.CreatedAtUtc, x.Status))
    .ToListAsync(cancellationToken);
```

## Public-surface comparison with other providers

### EF Relational baseline

```csharp
modelBuilder.Entity<Blog>()
    .HasIndex(x => x.Url)
    .HasDatabaseName("IX_Blogs_Url")
    .HasFilter("[Url] IS NOT NULL");
```

### SQL Server style

```csharp
modelBuilder.Entity<Customer>()
    .HasIndex(x => x.LastName)
    .IsClustered(false)
    .IncludeProperties(x => new { x.FirstName, x.City })
    .HasFillFactor(90)
    .IsCreatedOnline();
```

### Npgsql style

```csharp
modelBuilder.Entity<Post>()
    .HasIndex(x => x.Tags)
    .HasMethod("GIN")
    .IncludeProperties(x => new { x.Title, x.PublishedOn })
    .IsCreatedConcurrently();
```

### Cosmos style

```csharp
modelBuilder.Entity<Session>(entity =>
{
    entity.ToContainer("Sessions");
    entity.HasPartitionKey(x => x.TenantId);

    entity.HasIndex(x => x.Embedding)
        .IsVectorIndex(VectorIndexType.DiskANN);
});

var tenantSessions = await db.Sessions
    .WithPartitionKey(tenantId)
    .Where(x => x.IsActive)
    .ToListAsync(cancellationToken);
```

### Mongo style (nested index builder)

```csharp
modelBuilder.Entity<Article>()
    .HasIndex(x => x.Embedding, "EmbeddingVector")
    .IsVectorIndex(VectorSimilarity.DotProduct, 1536, index =>
    {
        index.HasQuantization(VectorQuantization.Scalar);
        index.AllowsFiltersOn(x => x.CategoryId);
        index.AllowsFiltersOn(x => x.IsPublished);
    });
```

### Public API feel takeaway

- Relational/SQL Server/Npgsql: `HasIndex(...).Option(...).Option(...)` chain on `IndexBuilder`.
- Cosmos: flat entity/property/index mapping APIs + explicit query hint APIs.
- Mongo: nested builder callbacks for richer index options.
- Dynamo (recommended): Cosmos-like explicit query hint (`WithIndex`) + Mongo-like nested index builder for clear projection/key configuration.

## Idiomatic choices for Dynamo (derived from comparison)

1. Store secondary-index metadata on `IIndex` annotations, not custom side registries.
2. Expose both fluent and convention APIs (`HasX` + `CanSetX`) to preserve EF precedence behavior.
3. Use a dedicated nested Dynamo index builder for ergonomics, but persist via standard annotations.
4. Keep list-valued metadata (`ProjectedProperties`) structural-equality-aware in `CanSet` checks.
5. Keep provider-specific validation in `DynamoModelValidator` and table-group-aware checks.
6. Reuse EF query-hint capture pattern for explicit index selection (`WithIndex`).
7. Build runtime source descriptors through provider `IModelRuntimeInitializer`, not a separate DI registry.

If we follow these, our metadata configuration will feel idiomatic to EF users and consistent with established provider architecture.

## Final proposed public API surface (summary)

```csharp
modelBuilder.Entity<Order>(entity =>
{
    entity.HasGlobalSecondaryIndex("ByCustomerCreatedAt", x => x.CustomerId, x => x.CreatedAtUtc)
        .ProjectsAll();

    entity.HasLocalSecondaryIndex("ByStatus", x => x.Status)
        .ProjectsInclude(x => x.Status, x => x.CustomerId, x => x.CreatedAtUtc);
});

var page = await db.Orders
    .WithIndex("ByCustomerCreatedAt")
    .Where(x => x.CustomerId == customerId)
    .Take(50)
    .ToListAsync(cancellationToken);
```

This is the intended user experience target: explicit, chainable, and easy to read in `OnModelCreating` and query code.
