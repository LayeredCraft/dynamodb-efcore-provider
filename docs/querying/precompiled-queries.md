---
title: Precompiled Queries and NativeAOT
description: Configure EF Core query interceptors and publish the DynamoDB provider with NativeAOT.
---

# Precompiled Queries and NativeAOT

The provider supports EF Core's generated query interceptors. It does not add a separate source
generator. `Microsoft.EntityFrameworkCore.Tasks` finds query calls during the build, asks the
provider to compile them, and writes the interceptors.

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
                    Version="10.0.11"
                    PrivateAssets="all" />
</ItemGroup>
```

Use the Tasks package version matching the EF Core version selected by your provider package. The
example uses EF Core 10.0.11.

Publish for a concrete runtime identifier:

```bash
dotnet publish --configuration Release --runtime linux-x64
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
    count. The provider stops reading after the first excess item and throws.
- NativeAOT publishing may emit trim and dynamic-code analysis warnings from EF Core, the AWS SDK,
    and provider features outside precompiled query execution. Treat AOT support as experimental.

For query translation details, see [How Queries Execute](how-queries-execute.md). For all provider
restrictions, see [Limitations](../limitations.md).
