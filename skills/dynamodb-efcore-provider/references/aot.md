# Precompiled Queries and NativeAOT

NativeAOT support is experimental and builds on EF Core precompiled queries. Queries must be
statically discoverable at build time. Known-invalid shapes fail the build, but two silent cases
also exist — a query with no generated interceptor (`ToPageAsync`) or one whose interceptor failed
to bind publishes fine and throws at first execution:
`Query wasn't precompiled and dynamic code isn't supported with NativeAOT`. Test the exact deployed
query shapes; a warning-free trimmed app is not yet guaranteed.

## Project setup

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <InterceptorsNamespaces>$(InterceptorsNamespaces);Microsoft.EntityFrameworkCore.GeneratedInterceptors</InterceptorsNamespaces>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore.Tasks" Version="10.0.12" PrivateAssets="all" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.12" PrivateAssets="all" />
</ItemGroup>
```

Publish for a concrete runtime identifier:
`dotnet publish -c Release -f net10.0 -r linux-x64`.

- `PublishAot` belongs only in the published startup project; every project that declares a context
  or query needs Tasks, `InterceptorsNamespaces`, and Design. Without the namespace entry the
  generated interceptor file fails to compile (CS9137).
- `Microsoft.EntityFrameworkCore.Design` must be resolvable in each generating project. EF Core 10's
  Tasks package brings it transitively; EF Core 11's has no dependencies, so reference it explicitly.
- Version pins must line up: Tasks, Design, EF Core, and the provider release. A mismatch fails
  generation with `generated an incompatible precompiled-query executor template`. On EF Core 11
  pin the exact prerelease build.
- `PublishAot` alone generates nothing: without Tasks there is no compiled model and no interceptor.
  Generation runs at publish — on EF Core 10 only when `PublishAot` (or `EFOptimizeContext`) is set,
  on EF Core 11 on any publish. To generate during build instead, set `EFScaffoldModelStage` and
  `EFPrecompileQueriesStage` to `build`, plus `EFOptimizeContext=true` on EF Core 10. `publish` plus
  `build` is a build error, and `EFOptimizeContext` itself is rejected on EF Core 11. No
  provider-specific MSBuild property exists.
- Multi-targeted or custom-`Configuration` projects can lose build context inside the generator's
  inner build; publish a single-targeted project.

### Context and query in different assemblies

When the published application contains the query but references a library that declares the
`DbContext`, add this assembly attribute to the **query/startup project**. It lets the EF build task
discover which referenced context must compile its query:

```csharp
using Microsoft.EntityFrameworkCore.Infrastructure;
using MyApplication.Data;

[assembly: DbContext(typeof(ApplicationDbContext))]
```

The query project still needs Tasks, Design, and `InterceptorsNamespaces`; the context library needs
the same packages and namespace entry. If the library already checks in a compiled model, set
`EFScaffoldModelStage` to `none` there and generate only query interceptors in the query project.
Otherwise EF generates a second compiled model with duplicate types. A checked-in compiled model is
compatible with generated query interceptors.

### Reflection-disabled JSON applications

EF's generation inner build can load application code. If an application sets
`JsonSerializerIsReflectionEnabledByDefault` to `false` and precompilation fails with
`Reflection-based serialization has been disabled for this application`, enable reflection only for
the EF generation stage, not for the published app:

```xml
<PropertyGroup>
  <JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
  <JsonSerializerIsReflectionEnabledByDefault
    Condition="'$(_EFGenerationStage)' != ''">true</JsonSerializerIsReflectionEnabledByDefault>
</PropertyGroup>
```

## Query shape

Write normal LINQ calls. For a local array with `Contains`, cast it to `IEnumerable<T>`
(`((IEnumerable<string>)keys).Contains(item.Pk)`) when the compiler binds the span-based overload,
which EF Core's precompiler cannot translate. An empty or null local collection becomes a false
predicate at runtime.

Precompilable beyond entity materialization: `ToListAsync`/`FirstAsync`/`SingleAsync` variants,
scalar projections (including nullable and enum types), `Limit`/`WithNextToken`, `AllowScan`,
`WithConsistentRead`, `WithIndex`, `ExecuteUpdateAsync`, `ExecuteDeleteAsync`. Execution is
async-only; synchronous operators fail at generation time.

## Restrictions

- Query roots must be visible to the EF Core build task; runtime-built expression trees are not
  supported. Provider translation limits still apply and fail at build instead of runtime.
- `DbContext.FindAsync` is not discovered by EF's precompiler. It can appear to work in JIT builds,
  but NativeAOT throws at first execution because it needs dynamic query compilation. Replace it
  with a visible `DbSet<TEntity>.Where(...).SingleOrDefaultAsync(...)` query. Copy method arguments
  used in the expression to locals first when needed for interceptor binding.
- A parameterized argument to a provider fluent method must be a local variable or lambda parameter,
  not the enclosing method's parameter, a field, or a constant. Assign it to a local first
  (`var pageSize = pageSizeArg;`).
- `ToPageAsync(...)` is never discovered, so it cannot run under NativeAOT: publish succeeds and the
  first execution throws. Use `Limit(pageSize).WithNextToken(nextToken).ToListAsync()` and read
  `EntityEntry.GetExecuteStatementResponse()?.NextToken` from a tracked, non-empty result.
  Projections, no-tracking queries, and empty pages have no AOT pagination path.
- A local collection used with `Contains` shares DynamoDB's PartiQL parameter budget: 50 values
  against the partition key, 100 otherwise. The provider stops at the first excess item and throws.
- EF Core's precompiler rejects nullable-coalescing projections.
- Query constants and parameters must bind to a mapped entity property; otherwise precompilation
  throws `InvalidOperationException` rather than failing via reflection at runtime.
- Materialization supports string, nullable numerics, Boolean, binary, converted scalars,
  one-dimensional arrays with non-nullable elements, and enums (numeric unless a converter is
  configured). Primitive `List<T>`/`HashSet<T>`/`Dictionary<string, T>` work with primitive or
  element-converted values; a property-level value converter composed over the collection fails at
  compiled-model generation. Use element-level conversion instead.
- Reading a non-public mapped field during materialization is unsupported, affecting field-backed
  collection properties and some auto-properties. Either keep those members off the materialized
  entity or set `UsePropertyAccessMode(PropertyAccessMode.PreferProperty)`. Complex collections are
  unaffected (materialization only writes them).
- Complex properties and complex collections materialize, but under NativeAOT query them with
  `AsNoTracking()`: a tracked query throws
  `Model building is not supported when publishing with NativeAOT` (dotnet/efcore#37750). On EF Core
  10, every precompiled no-tracking query over an entity in a normal namespace generates invalid C#,
  so the workaround is a global-namespace entity type — not viable for most apps.
- Filtering on or projecting complex members is untested and can fail during `dotnet publish`.
- `ExecuteUpdateAsync` and `ExecuteDeleteAsync` precompile only for full-primary-key, single-item
  operations. On EF Core 10 one precompiled update cannot mix constant and computed setters — split
  it; the provider fails the build rather than risk a wrong write. EF Core 11 supports the mix.
- `SaveChanges` works under NativeAOT. Table lifecycle and `HasData` seeding rely on the design-time
  model, which is unavailable there: provision tables and seed data out of band.
- Trim and dynamic-code warnings from EF Core, the AWS SDK, and non-precompiled provider paths are
  expected; suppress only IDs whose source is known.

## Runtime resource names (compiled models only)

A compiled model embeds the physical names it was generated with and bypasses `OnModelCreating`.
Only when one artifact is promoted unchanged across environments with differing names, declare a
logical identity and remap at runtime:

```csharp
b.ToDynamoTable(designTimeTable).HasLogicalTableName("Trivia");
b.HasGlobalSecondaryIndex("Gsi1", x => x.Gs1Pk).HasSecondaryIndexName(designTimeGsi1);

options.UseDynamo(d => d.RuntimeResourceNames(r => r
    .Table("Trivia", physicalTable)
    .SecondaryIndex("Trivia", "Gsi1", physicalGsi1)));
```

Guard the mapping with `if (!EF.IsDesignTime)`. Partial mappings are allowed; unmapped resources keep
their model names. The mapping is fixed for the model's lifetime — reusing an initialized compiled
model with a different or missing mapping throws `InvalidOperationException` — and unknown logical
names fail fast at model initialization. Not needed for JIT builds, identical names, or
per-environment rebuilds.

`WithIndex("name")` in a precompiled query takes the **design-time** physical index name; the mapped
runtime name is applied at execution. The same text is not portable to JIT-with-mapping, where the
runtime name is required, so prefer automatic index selection when using runtime resource names.
