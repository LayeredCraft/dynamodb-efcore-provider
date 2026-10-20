# Precompiled Queries and NativeAOT

NativeAOT support is experimental and uses EF Core precompiled queries. Queries must be statically
discoverable at build time; runtime-assembled expression trees cannot be precompiled. Unsupported
shapes fail during build, with no runtime fallback.

## Project setup

Add all three to every project that declares a context or a query:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <InterceptorsNamespaces>$(InterceptorsNamespaces);Microsoft.EntityFrameworkCore.GeneratedInterceptors</InterceptorsNamespaces>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore.Tasks" Version="10.0.12" PrivateAssets="all" />
</ItemGroup>
```

Publish for a concrete runtime identifier:
`dotnet publish -c Release -f net10.0 -r linux-x64`.

- The Tasks package must match the application's EF Core version; on EF Core 11 pin the exact
  prerelease build, not a range.
- `Microsoft.EntityFrameworkCore.Design` must also be resolvable: EF Core 10's Tasks package brings
  it transitively, EF Core 11's does not.
- `PublishAot` alone does nothing — without Tasks there is no compiled model or interceptor and the
  binary has no query executors. Generation runs at publish; `EFOptimizeContext=true` moves it to
  ordinary build. No provider-specific MSBuild property exists.

## Query shape

Write normal LINQ calls in application code. For a local array with `Contains`, cast it to
`IEnumerable<T>` (`((IEnumerable<string>)statuses).Contains(order.Status)`) so the compiler does not
pick the span-based overload, which EF Core's precompiler cannot translate.

## Restrictions

- Unsupported LINQ fails during build instead of at runtime.
- A local collection used with `Contains` shares DynamoDB's PartiQL parameter budget: 50 values
  against the partition key, 100 otherwise. The provider stops reading after the first excess item
  and throws.
- `Limit(n)` and `WithNextToken(...)` precompile with literal or runtime-varying local arguments;
  the generated interceptor is reusable across values.
- A parameterized argument to a provider fluent method must be a local variable, not the enclosing
  method's parameter or a field. Assign it to a local first (`var pageSize = pageSizeArg;`) or EF
  Core's precompiler fails with `Encountered unknown identifier name '...'`.
- `ToPageAsync(...)` never precompiles and therefore cannot run under NativeAOT — no interceptor,
  no build error (dotnet/efcore#38962, upstream). Use
  `Limit(pageSize).WithNextToken(nextToken).ToListAsync()` and read the token from a tracked entity
  via `EntityEntry.GetExecuteStatementResponse()`. `ToPageAsync` is still needed for projections,
  no-tracking queries, and empty pages.
- EF Core's precompiler rejects nullable-coalescing projections.
- Tested materialization: string, nullable numeric, Boolean, binary, converted scalar, and
  one-dimensional array properties. Reading a non-public mapped field is unsupported, including
  mutable field-backed `List<T>`, `HashSet<T>`, and `Dictionary<string, T>`. Complex properties and
  collections materialize only with `AsNoTracking()` (dotnet/efcore#37750); on EF Core 10 that also
  requires a globally-namespaced entity type, an EF10 precompiler defect fixed in EF11.
- `ExecuteUpdateAsync` precompiles under the same static-discovery rules. On EF Core 10 one
  precompiled update cannot mix constant and computed setters — split it; EF11 supports the mix.
  `ExecuteDeleteAsync` precompiles under the normal key-targeting restrictions.
- Trim and dynamic-code warnings from EF Core, the AWS SDK, and non-precompiled provider features
  are expected; accept them only when the source is known.

## Runtime resource names (compiled models only)

A compiled model embeds the physical names it was generated with and bypasses `OnModelCreating`.
Only when one artifact is promoted unchanged across environments with differing names, declare a
logical identity and remap at runtime:

```csharp
b.ToTable(designTimeTable).HasLogicalTableName("Trivia");
b.HasGlobalSecondaryIndex("Gsi1", x => x.Gs1Pk).HasSecondaryIndexName(designTimeGsi1);

options.UseDynamo(d => d.RuntimeResourceNames(r => r
    .Table("Trivia", physicalTable)
    .SecondaryIndex("Trivia", "Gsi1", physicalGsi1)));
```

Not for JIT applications, identical names, or per-environment rebuilds. Guard with
`if (!EF.IsDesignTime)`, keep it fixed for the model's lifetime, and note `WithIndex("name")` always
takes a physical index name.
