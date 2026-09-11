---
title: Precompiled Queries and NativeAOT
description: Configure EF Core query interceptors and publish the DynamoDB provider with NativeAOT.
---

# Precompiled Queries and NativeAOT

The provider supports EF Core's generated query interceptors. It does not add a separate source
generator. `Microsoft.EntityFrameworkCore.Tasks` finds query calls during the build, asks the
provider to compile them, and writes the interceptors.

!!! warning "Experimental support"

    Precompiled-query and NativeAOT support relies on EF Core's internal precompilation APIs and
    a version-specific rewrite of EF Core-generated code. It is not yet a production-stability
    guarantee. CI exercises interceptor generation and NativeAOT publish-and-run execution for
    both EF Core 10 and EF Core 11. Test against your model and workload before using it in
    production.

## Project setup

Add the EF Core Tasks package at the same version as your other EF Core packages, enable NativeAOT,
and allow EF Core's generated interceptor namespace:

```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <InterceptorsNamespaces>
    $(InterceptorsNamespaces);Microsoft.EntityFrameworkCore.GeneratedInterceptors
  </InterceptorsNamespaces>
</PropertyGroup>

<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore.Tasks"
                    Version="10.0.12"
                    PrivateAssets="all" />
</ItemGroup>
```

Use the Tasks package version matching the EF Core version selected by your provider package. The
example uses EF Core 10.0.12. For EF Core 11, pin to the exact build your application's other EF
Core 11 packages use (for example `11.0.0-rc.1.26425.128`) rather than a floating range — EF Core
11 is still prerelease, and an unpinned range can silently resolve to a different Tasks build than
your EF Core packages.

Publish for a concrete runtime identifier:

```bash
dotnet publish --configuration Release --framework net10.0 --runtime linux-x64
```

The build generates both the compiled model and query interceptors. No generated files need to be
checked into source control.

## Query shape

Write queries as normal LINQ calls in application code:

```csharp
internal static async Task<List<Order>> LoadOrdersAsync(
    OrdersContext db,
    string customerId,
    string[] statuses,
    CancellationToken cancellationToken)
    => await db.Orders
        .Where(order => order.CustomerId == customerId
            && ((IEnumerable<string>)statuses).Contains(order.Status))
        .ToListAsync(cancellationToken);
```

The generated interceptor contains a compact PartiQL template. Scalar values become positional
parameters. Local collections are expanded to the required number of positional parameters at
runtime; an empty or null collection becomes a false predicate. Property reads are generated from
the compiled model so configured value converters are retained.

The explicit `IEnumerable<T>` cast avoids the compiler selecting a span-based `Contains` overload
for local arrays, which EF Core's query precompiler cannot currently translate.

## Restrictions

- Query calls must be visible to the EF Core build task. Dynamically assembled expression trees
    are not supported by query precompilation.
- The normal provider translation limits still apply. Unsupported LINQ operators fail during the
    build instead of first failing at runtime.
- A local collection used with `Contains` is limited to DynamoDB's supported PartiQL parameter
    count — 50 values when the comparison targets the partition key and 100 otherwise. DynamoDB
    enforces these limits per `ExecuteStatement`, and the generated interceptor expands the
    collection into positional parameters at runtime, so the collection shares that budget. The
    provider stops reading after the first excess item and throws.
- NativeAOT publishing may emit trim and dynamic-code analysis warnings from EF Core, the AWS SDK,
    and provider features outside precompiled query execution. Reviewed warning IDs are pinned in
    CI with documented rationale; warnings are accepted only when their source is known.
- EF Core's precompiler currently rejects the provider's C# 14 `Limit(n)` extension member and
    nullable-coalescing projections before provider translation. This affects precompiled-query
    generation only (both EF10 and EF11, with or without NativeAOT): the build fails instead of
    falling back to runtime execution. Queries that are not precompiled translate `Limit(n)` at
    runtime as usual.
- The tested NativeAOT path covers entity materialization with string, nullable numeric, Boolean,
    binary, converted scalar, and one-dimensional array properties with non-nullable elements.
    Entity materialization requiring a read from a non-public mapped field is not supported. This
    includes mutable field-backed `List<T>`, `HashSet<T>`, and `Dictionary<string, T>` properties,
    and may include auto-properties when EF Core chooses their backing field. See
    [Limitations](../limitations.md) before using NativeAOT with collection-valued entity members.

## Verification

Run generated-interceptor checks for both supported EF Core versions:

```bash
task test:aot-generation FRAMEWORK=net10.0
task test:aot-generation FRAMEWORK=net11.0
```

The provider verifies the generated EF Core 10 and EF Core 11 executor templates in its
per-framework generation tests. Publish and run the native smoke app with `task test:aot-publish
FRAMEWORK=net10.0` (or `net11.0`) — this is the NativeAOT path gated by CI for both EF Core
versions. The smoke app runs parameterized and materializing queries plus a `SaveChanges` write
against DynamoDB Local. It checks NativeAOT execution and materialized values. Generation and
parity tests separately check generated PartiQL templates and execution behavior.
If interceptor generation reports an incompatible EF Core version or executor preamble, update the
provider rewrite and its compatibility tests together.

For query translation details, see [How Queries Execute](how-queries-execute.md). For all provider
restrictions, see [Limitations](../limitations.md).
