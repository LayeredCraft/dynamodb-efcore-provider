# Multi-Version EF Core Support: MongoDB Provider Analysis & DynamoDB Strategy

## Overview

This document analyzes how the MongoDB EF Core provider supports EF8, EF9, and EF10 from a single codebase — no separate release branches — and maps those findings into a concrete strategy for the DynamoDB provider to support EF10, EF11, and future versions.

---

## Part 1: How MongoDB Does It

### 1.1 The Core Mechanism: Configuration-Driven Compilation

MongoDB does **not** use:
- Separate branches per EF version
- Traditional multi-targeting (`<TargetFrameworks>net8.0;net9.0;net10.0</TargetFrameworks>`)

Instead, they use **six named build configurations** (Debug/Release × EF8/EF9/EF10). Each configuration injects a preprocessor symbol (`EF8`, `EF9`, or `EF10`) via `DefineConstants`, and all version-specific code is guarded by `#if` directives.

```
Debug EF8   → net8.0  → #define EF8
Debug EF9   → net8.0  → #define EF9
Debug EF10  → net10.0 → #define EF10
```

The target framework itself is conditional:

**`src/MongoDB.EntityFrameworkCore/MongoDB.EntityFrameworkCore.csproj` (lines 3, 44, 51):**
```xml
<!-- Default (EF8 / EF9) -->
<TargetFramework>net8.0</TargetFramework>

<!-- EF10 Debug configuration overrides to net10.0 -->
<PropertyGroup Condition=" '$(Configuration)' == 'Debug EF10' ">
  <TargetFramework>net10.0</TargetFramework>
  <DefineConstants>TRACE;DEBUG;EF10</DefineConstants>
</PropertyGroup>

<!-- EF10 Release configuration overrides to net10.0 -->
<PropertyGroup Condition=" '$(Configuration)' == 'Release EF10' ">
  <TargetFramework>net10.0</TargetFramework>
  <DefineConstants>TRACE;RELEASE;EF10</DefineConstants>
</PropertyGroup>
```

**`src/MongoDB.EntityFrameworkCore/MongoDB.EntityFrameworkCore.csproj` (line 7):**
```xml
<Configurations>Debug EF8;Release EF8;Debug EF9;Release EF9;Debug EF10;Release EF10</Configurations>
```

### 1.2 Centralized Version Management

All EF Core version pins live in one file:

**`Versions.props` (lines 3–5):**
```xml
<EF8Version>8.0.27</EF8Version>
<EF9Version>9.0.16</EF9Version>
<EF10Version>10.0.8</EF10Version>
```

Each build configuration references the matching constant:

**`src/MongoDB.EntityFrameworkCore/MongoDB.EntityFrameworkCore.csproj` (lines 65–75):**
```xml
<ItemGroup Condition=" '$(Configuration)' == 'Release EF8' Or '$(Configuration)' == 'Debug EF8' ">
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="$(EF8Version)" />
</ItemGroup>

<ItemGroup Condition=" '$(Configuration)' == 'Release EF9' Or '$(Configuration)' == 'Debug EF9' ">
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="$(EF9Version)" />
</ItemGroup>

<ItemGroup Condition=" '$(Configuration)' == 'Release EF10' Or '$(Configuration)' == 'Debug EF10' ">
  <PackageReference Include="Microsoft.EntityFrameworkCore" Version="$(EF10Version)" />
</ItemGroup>
```

### 1.3 CI: Each Version Tested Independently

**`evergreen/evergreen.yml` (lines 165, 179, 193):**
```bash
BUILD_CONFIGURATION="Debug EF8"
BUILD_CONFIGURATION="Debug EF9"
BUILD_CONFIGURATION="Debug EF10"
```

**`evergreen/run-tests.sh` (line 12):**
```bash
dotnet test "./MongoDB.EFCoreProvider.sln" -c "${BUILD_CONFIGURATION}" ...
```

The configuration variable flows straight into `dotnet build`/`dotnet test` — no wrapper scripts needed.

---

## Part 2: Complete Compiler Directive Inventory

### 2.1 Pattern Reference Table

| Pattern | Meaning |
|---|---|
| `#if EF8` | EF8 only — typically legacy code that EF9+ absorbed |
| `#if EF8 \|\| EF9` | Pre-EF10 code — API shapes that EF10 reworked |
| `#if EF10` | EF10-specific code (new API signature) |
| `#if !EF8` | EF9+ code (feature added in EF9) |
| `#if !EF8 && !EF9` | EF10-only (equivalent to `#if EF10` when three versions are supported) |

### 2.2 ChangeTracking: Entire Files Behind `#if`

Some files are wrapped entirely — the compiler skips them when the symbol is absent.

**`src/MongoDB.EntityFrameworkCore/ChangeTracking/ListOfNullableValueTypesComparer.cs` (lines 6–221):**
```csharp
#if EF8
// Entire class: legacy nullable-value-type list comparer
// EF9+ includes native equivalents in EF Core itself
namespace MongoDB.EntityFrameworkCore.ChangeTracking;

internal sealed class ListOfNullableValueTypesComparer<TConcreteList, TElement>
    : ValueComparer<TConcreteList>
    where TConcreteList : class, IEnumerable<TElement?>
    where TElement : struct
{
    // ...
}
#endif
```

**`src/MongoDB.EntityFrameworkCore/ChangeTracking/StringDictionaryComparerLegacy.cs` (lines 6–102):**
```csharp
#if EF8 || EF9
// Pre-EF10 dictionary comparer — two types (nullable + non-nullable)
// EF10 replaced these with a single unified type
public sealed class StringDictionaryComparer<TElement, TCollection>
    : ValueComparer<TCollection>
{
    public StringDictionaryComparer(ValueComparer elementComparer, bool readOnly)
    // ...
}
#endif
```

**`src/MongoDB.EntityFrameworkCore/ChangeTracking/StringDictionaryComparer.cs` (lines 6–180):**
```csharp
#if !EF8 && !EF9
// EF10 redesigned the dictionary comparer entirely
// Note: generic parameter ORDER is reversed vs. EF8/9 version
public sealed class StringDictionaryComparer<TDictionary, TElement>
    : ValueComparer<object>, IInfrastructure<ValueComparer>
{
    public StringDictionaryComparer(ValueComparer elementComparer)
    // No 'readOnly' param — EF10 dropped it
    // ...
}
#endif
```

**Key observation:** The EF10 comparer has reversed generic parameter order (`<TDictionary, TElement>` vs. EF8/9's `<TElement, TCollection>`). Without the `#if` guards, you'd get compile errors from conflicting type definitions.

### 2.3 Storage: API Shape Changes

**`src/MongoDB.EntityFrameworkCore/Storage/MongoTypeMappingSource.cs` (lines 129–133):**
```csharp
// Element comparer composition API changed between EF9 and EF10
#if EF8 || EF9
    var comparer = elementMapping.Comparer.ToNullableComparer(elementType);
#else
    var comparer = elementMapping.Comparer.ComposeConversion(elementType);
#endif
```

**`src/MongoDB.EntityFrameworkCore/Storage/MongoTypeMappingSource.cs` (lines 197–209):**
```csharp
// Dictionary comparer instantiation differs by version
#if EF8 || EF9
    var unwrappedType = elementType.UnwrapNullableType();
    return (ValueComparer)Activator.CreateInstance(
        elementType == unwrappedType
            ? typeof(StringDictionaryComparer<,>).MakeGenericType(elementType, dictType)
            : typeof(NullableStringDictionaryComparer<,>).MakeGenericType(unwrappedType, dictType),
        elementMapping.Comparer,
        readOnly)!;
#else
    // EF10: single type, different generic order, no readOnly param
    return (ValueComparer)Activator.CreateInstance(
        typeof(StringDictionaryComparer<,>).MakeGenericType(dictType, elementType),
        elementMapping.Comparer.ComposeConversion(elementType))!;
#endif
```

### 2.4 Query: Parameter Handling (Most Pervasive Change)

EF10 introduced a dedicated `QueryParameterExpression` type. EF8/9 used `ParameterExpression` with a name-prefix string check. This pattern appears in four query files.

**`src/MongoDB.EntityFrameworkCore/Query/MongoProjectionBindingExpressionVisitor.cs` (lines 85–112):**
```csharp
#if EF8 || EF9
case ParameterExpression parameterExpression:
    if (_collectionShaperMapping.ContainsKey(parameterExpression)) { /* ... */ }
    if (parameterExpression.Name?.StartsWith(QueryCompilationContext.QueryParameterPrefix, ...) == true)
    {
        // Manual name-prefix check to detect query parameters
    }
    throw new InvalidOperationException(/* ... */);
#else
case QueryParameterExpression queryParameter:
    // EF10: dedicated expression type — no name check needed
    return Expression.Call(GetParameterValueMethodInfo.MakeGenericMethod(/* ... */), /* ... */);
case ParameterExpression parameterExpression:
    return _collectionShaperMapping.ContainsKey(parameterExpression) ? /* ... */ : throw /* ... */;
#endif
```

**`src/MongoDB.EntityFrameworkCore/Query/MongoProjectionBindingExpressionVisitor.cs` (lines 675–685):**
```csharp
// QueryContext API: ParameterValues → Parameters
#if EF8 || EF9
private static T GetParameterValue<T>(QueryContext queryContext, string parameterName)
    => (T)queryContext.ParameterValues[parameterName];
#else
private static T GetParameterValue<T>(QueryContext queryContext, string parameterName)
    => (T)queryContext.Parameters[parameterName];
#endif
```

**`src/MongoDB.EntityFrameworkCore/Query/MongoShapedQueryCompilingExpressionVisitor.cs` (lines 237–241):**
```csharp
// EF10 renamed entity materializer injection method
#if EF8 || EF9
shaperBody = InjectEntityMaterializers(shaperBody);
#else
shaperBody = InjectStructuralTypeMaterializers(shaperBody);
#endif
```

**`src/MongoDB.EntityFrameworkCore/Query/MongoShapedQueryCompilingExpressionVisitor.cs` (lines 621–633):**
```csharp
#if EF8 || EF9
if (expression is ParameterExpression param
    && param.Name?.StartsWith(QueryCompilationContext.QueryParameterPrefix, ...) == true
    && queryContext.ParameterValues.TryGetValue(param.Name, out var value))
{
    return value;
}
#else
if (expression is Microsoft.EntityFrameworkCore.Query.QueryParameterExpression queryParam)
{
    return queryContext.Parameters[queryParam.Name];
}
#endif
```

### 2.5 Query: Bulk Operations (EF9 → EF10 Signature Break)

**`src/MongoDB.EntityFrameworkCore/Query/MongoQueryableMethodTranslatingExpressionVisitor.cs` (lines 84–89):**
```csharp
// ExecuteDelete/ExecuteUpdate didn't exist in EF8
#if !EF8
    if (method.DeclaringType == typeof(EntityFrameworkQueryableExtensions))
        return base.VisitMethodCall(methodCallExpression);
#endif
```

**`src/MongoDB.EntityFrameworkCore/Query/MongoQueryableMethodTranslatingExpressionVisitor.cs` (lines 196–246):**
```csharp
// TranslateExecuteUpdate signature completely changed in EF10
#if EF10
protected override Expression? TranslateExecuteUpdate(
    ShapedQueryExpression source,
    IReadOnlyList<ExecuteUpdateSetter> setters)  // EF10: pre-parsed structured setters
{
    // EF Core already parsed the lambda — receive structured data
}
#else
protected override Expression? TranslateExecuteUpdate(
    ShapedQueryExpression source,
    LambdaExpression setPropertyCalls)  // EF9: raw lambda, must parse manually
{
    // Must walk the AST to find SetProperty(selector, value) chains
}
#endif
```

### 2.6 Query: New Join Methods in EF10

**`src/MongoDB.EntityFrameworkCore/Query/MongoQueryableMethodTranslatingExpressionVisitor.cs` (lines 121–130):**
```csharp
switch (methodDefinition.Name)
{
    // ...
#if !EF8 && !EF9
    case nameof(Queryable.LeftJoin) when methodDefinition == QueryableMethods.LeftJoin:
#endif

#if !EF8 && !EF9
    case nameof(Queryable.RightJoin) when methodDefinition == QueryableMethods.RightJoin:
#endif
    // ...
}
```

`LeftJoin` and `RightJoin` became public `Queryable` methods in EF10. The case branches don't exist for EF8/9 because the methods themselves aren't on `Queryable` yet.

### 2.7 Diagnostics Telemetry

**`src/MongoDB.EntityFrameworkCore/Query/QueryingEnumerable.cs` (lines 158–164):**
```csharp
// Query telemetry API changed between EF8 and EF9+
#if !EF8
#pragma warning disable EF9101
    EntityFrameworkMetricsData.ReportQueryExecuting();  // EF9+ Metrics API
#pragma warning restore EF9101
#else
    EntityFrameworkEventSource.Log.QueryExecuting();    // EF8 EventSource API
#endif
```

### 2.8 Metadata: Internal Namespace Relocation

**`src/MongoDB.EntityFrameworkCore/Metadata/Conventions/BsonIgnoreAttributeConvention.cs` (lines 23–26):**
```csharp
// EF9 moved internal convention types to a new namespace
#if EF8
using Microsoft.EntityFrameworkCore.Metadata.Internal;
#else
using Microsoft.EntityFrameworkCore.Internal;
#endif
```

---

## Part 3: What Changes Between EF Versions (Summary)

This is the full catalog of breaking API changes that required `#if` guards:

| Area | EF8 → EF9 | EF9 → EF10 |
|---|---|---|
| **Bulk Operations** | Added `ExecuteDelete`/`ExecuteUpdate` | `TranslateExecuteUpdate` receives structured `ExecuteUpdateSetter[]` instead of a lambda |
| **Query Parameters** | `ParameterExpression` + name prefix | `QueryParameterExpression` type; `queryContext.Parameters` (was `.ParameterValues`) |
| **ValueComparer API** | No change | `.ToNullableComparer()` → `.ComposeConversion()` |
| **Dictionary Comparers** | Two types (nullable + non-nullable) | One type; reversed generic params; no `readOnly` ctor param |
| **Materializer injection** | `InjectEntityMaterializers` | `InjectStructuralTypeMaterializers` |
| **Diagnostics telemetry** | `EventSource.Log.QueryExecuting()` | `MetricsData.ReportQueryExecuting()` |
| **Internal namespace** | `Metadata.Internal` | `.Internal` |
| **Join methods** | Internal only | `LeftJoin`/`RightJoin` on `Queryable` become public |

---

## Part 4: Recommended Strategy for DynamoDB Provider

### 4.1 What We Borrow from MongoDB (and What We Don't)

The DynamoDB provider uses the **same custom build-configuration approach as MongoDB** — `"Debug EF10"`, `"Release EF10"`, `"Debug EF11"`, `"Release EF11"` as named MSBuild configurations. This is required because we version each EF line independently (`10.x.x` for EF10, `11.x.x` for EF11), which means separate builds with separate `/p:Version=` values. Standard `<TargetFrameworks>` multi-targeting produces one package with one version — incompatible with that model.

**The one thing we do differently:** MongoDB uses custom `DefineConstants` (`EF8`, `EF9`, `EF10`) because EF8 and EF9 both target `net8.0` — the SDK can't tell them apart. The DynamoDB provider always has a 1:1 mapping (EF10 = `net10.0`, EF11 = `net11.0`), so the SDK's built-in TFM symbols (`NET10_0`, `NET11_0`) are sufficient. **No custom `DefineConstants` needed.**

| | MongoDB | DynamoDB |
|---|---|---|
| Custom build configurations | ✓ `Debug EF8` / `Release EF8` etc. | ✓ `Debug EF10` / `Release EF10` etc. |
| Custom `DefineConstants` | ✓ `EF8`, `EF9`, `EF10` — required (EF8+EF9 share `net8.0`) | ✗ not needed — each config has a unique TFM |
| `#if` symbols used | `#if EF8`, `#if EF9`, `#if EF10` | `#if NET10_0`, `#if NET11_0` (built-in) |
| Separate version lines | ✓ `v8.*`, `v9.*`, `v10.*` | ✓ `v10.*`, `v11.*` |

The comparison against the currently-commented branch-per-version approach:

| | Branch-per-version | Configuration-based (our approach) |
|---|---|---|
| **Bug fixes** | Cherry-pick to N branches | Fix once — all configurations get it |
| **New features** | Parallel development on N branches | Single PR with `#if` guards |
| **Code review** | Review same change N times | Review once |
| **Merge conflicts** | Constant divergence risk | None — single source of truth |
| **CI complexity** | N separate pipelines | Add one matrix entry per version |

### 4.2 Migration Plan

#### Step 1: Update EF Core version pinning

The DynamoDB provider uses `ManagePackageVersionsCentrally=true`. EF Core packages need per-configuration versions, so move them out of `Directory.Packages.props` and into the provider csproj using `VersionOverride`:

**`Directory.Packages.props`** — remove the EF Core entries (they'll be managed in the csproj):
```xml
<!-- Remove these — now managed per-configuration in the provider csproj -->
<!-- <PackageVersion Include="Microsoft.EntityFrameworkCore" Version="10.0.8" /> -->
<!-- <PackageVersion Include="Microsoft.EntityFrameworkCore.Relational" Version="10.0.8" /> -->
<!-- <PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.8" /> -->
```

All other packages (AWS SDK, test packages, etc.) remain in central management unchanged.

#### Step 2: Add build configurations to the provider csproj

**`src/EntityFrameworkCore.DynamoDb/EntityFrameworkCore.DynamoDb.csproj`:**
```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <!-- Declare all valid configurations -->
    <Configurations>Debug EF10;Release EF10;Debug EF11;Release EF11</Configurations>
  </PropertyGroup>

  <!-- EF10 configurations — NET10_0 defined automatically by the SDK, no DefineConstants needed -->
  <PropertyGroup Condition="'$(Configuration)' == 'Debug EF10'">
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)' == 'Release EF10'">
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>

  <!-- EF11 configurations — NET11_0 defined automatically by the SDK -->
  <PropertyGroup Condition="'$(Configuration)' == 'Debug EF11'">
    <TargetFramework>net11.0</TargetFramework>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)' == 'Release EF11'">
    <TargetFramework>net11.0</TargetFramework>
  </PropertyGroup>

  <!-- VersionPrefix per EF line — drives NuGet package version major -->
  <PropertyGroup Condition="'$(TargetFramework)' == 'net10.0'">
    <VersionPrefix>10.0.0</VersionPrefix>
  </PropertyGroup>
  <PropertyGroup Condition="'$(TargetFramework)' == 'net11.0'">
    <VersionPrefix>11.0.0</VersionPrefix>
  </PropertyGroup>

  <!-- EF Core 10 packages -->
  <ItemGroup Condition="'$(Configuration)' == 'Debug EF10' Or '$(Configuration)' == 'Release EF10'">
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.8" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="10.0.8" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.8" />
  </ItemGroup>

  <!-- EF Core 11 packages (fill in version when EF11 ships) -->
  <ItemGroup Condition="'$(Configuration)' == 'Debug EF11' Or '$(Configuration)' == 'Release EF11'">
    <PackageReference Include="Microsoft.EntityFrameworkCore" Version="11.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Relational" Version="11.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="11.0.0" />
  </ItemGroup>

</Project>
```

The .NET SDK automatically defines `NET10_0` when `TargetFramework=net10.0` and `NET11_0` when `TargetFramework=net11.0`. These built-in symbols drive all `#if` guards — no `<DefineConstants>` required.

#### Step 3: Apply the same configuration pattern to all test projects

Each test project mirrors the provider's configuration structure:

```xml
<!-- tests/EntityFrameworkCore.DynamoDb.Tests/...csproj -->
<PropertyGroup>
  <TargetFramework>net10.0</TargetFramework>
  <Configurations>Debug EF10;Release EF10;Debug EF11;Release EF11</Configurations>
</PropertyGroup>

<PropertyGroup Condition="'$(Configuration)' == 'Debug EF10' Or '$(Configuration)' == 'Release EF10'">
  <TargetFramework>net10.0</TargetFramework>
</PropertyGroup>
<PropertyGroup Condition="'$(Configuration)' == 'Debug EF11' Or '$(Configuration)' == 'Release EF11'">
  <TargetFramework>net11.0</TargetFramework>
</PropertyGroup>

<ItemGroup Condition="'$(Configuration)' == 'Debug EF10' Or '$(Configuration)' == 'Release EF10'">
  <PackageReference Include="Microsoft.EntityFrameworkCore.Specification.Tests" Version="10.0.8" />
</ItemGroup>
<ItemGroup Condition="'$(Configuration)' == 'Debug EF11' Or '$(Configuration)' == 'Release EF11'">
  <PackageReference Include="Microsoft.EntityFrameworkCore.Specification.Tests" Version="11.0.0" />
</ItemGroup>
```

#### Step 4: Update `global.json`

Currently `global.json` pins `10.0.103` with `rollForward: latestMinor`. Change to `latestMajor` so the .NET 11 SDK can be resolved when building the `Debug EF11` / `Release EF11` configurations:

```json
{
  "sdk": {
    "version": "10.0.103",
    "rollForward": "latestMajor"
  }
}
```

#### Step 5: Write version-conditional code when EF11 ships

When EF11 introduces breaking API changes, guard them using the built-in TFM symbols:

```csharp
// Different implementation per EF version
#if NET10_0
    // EF10 approach
    var result = queryContext.Parameters[paramName];
#else
    // EF11+ approach (hypothetical)
    var result = queryContext.QueryParameters.Get(paramName);
#endif

// Entire file compiled for EF10 only
#if NET10_0
namespace EntityFrameworkCore.DynamoDb.ChangeTracking;
internal sealed class SomeLegacyComparer<T> : ValueComparer<T>
{
    // ...
}
#endif
```

### 4.3 Where to Expect `#if` Guards (DynamoDB-Specific)

Based on MongoDB's experience, these DynamoDB provider areas are highest-risk for EF11 breaks:

| Area | Files | Likely Break Pattern |
|---|---|---|
| **Query parameter handling** | `DynamoShapedQueryCompilingExpressionVisitor.cs` | `QueryParameterExpression` type changes; `queryContext.Parameters` API |
| **Bulk operations** | Query translator | `TranslateExecuteUpdate` signature change (lambda → structured setters) |
| **ValueComparer** | `ChangeTracking/*.cs` — `ListValueComparer`, `StringDictionaryValueComparer`, etc. | `.ToNullableComparer()` → `.ComposeConversion()` |
| **Materializer injection** | `DynamoShapedQueryCompilingExpressionVisitor.cs` | Method renames (`InjectEntityMaterializers` → ...) |
| **Diagnostics** | `Diagnostics/` | Telemetry API changes (EventSource → Metrics pattern) |
| **Internal namespaces** | Convention classes in `Metadata/Conventions/` | Namespace relocations across EF versions |

### 4.4 The Killer Workflow When EF11 Ships

Adding EF11 support reduces to a mechanical sequence driven entirely by the compiler:

1. Add `Debug EF11` / `Release EF11` configuration blocks to all `.csproj` files
2. Add the EF11 `<PackageReference>` block conditioned on those configurations
3. Run `dotnet build --configuration "Debug EF11"` — compiler errors are your **complete checklist** of required `#if` guards; every breaking API change surfaces immediately
4. For each error, wrap the diverging code in `#if NET10_0` / `#else` / `#endif`
5. Add the EF11 matrix entry to `pr-build.yaml` and `publish-preview.yaml`
6. Run `dotnet test --configuration "Debug EF10"` and `dotnet test --configuration "Debug EF11"` — both must pass
7. Ship — create GitHub Releases tagged `v10.x.x` and `v11.x.x`; the publish workflow derives everything from the tag

The compiler errors from step 3 are a **complete and exact list** of required `#if` guards. No guesswork, no documentation spelunking — the compiler tells you everything that changed.

---

## Part 5: NuGet Packaging

Each build configuration produces its own `.nupkg` with its own version number. The EF10 configuration builds `EntityFrameworkCore.DynamoDb 10.x.x`; the EF11 configuration builds `EntityFrameworkCore.DynamoDb 11.x.x`. These are published as separate GitHub Releases with separate NuGet versions.

Consumers install one package (`dotnet add package EntityFrameworkCore.DynamoDb`), and NuGet's dependency resolution selects the correct version based on their project's `<TargetFramework>`:
- `net10.0` project → NuGet resolves `10.x.x` (the highest compatible `10.*` release)
- `net11.0` project → NuGet resolves `11.x.x`

The major version in the package version IS the EF Core compatibility signal. This is the same convention used by EF Core itself, Npgsql, Pomelo, and MongoDB's own provider.

---

## Appendix: Quick Reference

### Build Commands

```bash
# Build a single EF version
dotnet build --configuration "Debug EF10"
dotnet build --configuration "Debug EF11"

# Test a single EF version
dotnet test --configuration "Debug EF10"
dotnet test --configuration "Debug EF11"

# Pack for release (version injected by CI — see publish-release workflow)
dotnet pack --configuration "Release EF10" /p:Version=10.1.2
dotnet pack --configuration "Release EF11" /p:Version=11.0.0
```

### `#if` Pattern Quick Reference

The DynamoDB provider uses the .NET SDK's built-in TFM symbols — no custom symbols needed.

```csharp
// Code only for EF10 / net10.0 (backwards compat)
#if NET10_0
    oldApi.DoThing();
#endif

// Code only for EF11+ 
#if !NET10_0
    newApi.DoThing();
#endif

// Different implementation per version
#if NET10_0
    oldApi.DoThing();
#else
    newApi.DoThing();
#endif

// Entire file is EF10-only
#if NET10_0
namespace EntityFrameworkCore.DynamoDb.ChangeTracking;
internal sealed class SomeLegacyComparer<T> : ValueComparer<T>
{
    // ...
}
#endif

// Multi-version ladder
#if NET10_0
    // EF10
#elif NET11_0
    // EF11
#else
    // EF12+
#endif
```

---

## Part 6: CI & Release Workflow Strategy

### 6.1 Why the Shared devops-templates Workflows Need to Change

The current publishing workflows delegate to shared reusable workflows in `LayeredCraft/devops-templates`:

| Caller workflow | Trigger | Shared template |
|---|---|---|
| `pr-build.yaml` | PR against `main` | `devops-templates/pr-build.yaml@v8.2` |
| `publish-preview.yaml` | Push to `main` | `devops-templates/publish-preview.yml@v8.2` |
| `publish-release.yaml` | GitHub Release published | `devops-templates/publish-release.yml@v8.2` |

All three shared templates hardcode `--configuration Release`:

```yaml
# devops-templates/.github/workflows/publish-release.yml (lines 49, 53, 56)
dotnet build ${{ inputs.solution }} --configuration Release --no-restore
dotnet test  --solution ${{ inputs.solution }} --no-build --configuration Release
dotnet pack  ${{ inputs.solution }} --configuration Release --no-build -o artifacts
```

`Release` is not a valid configuration in the multi-version csproj — the valid configurations are `Release EF10`, `Release EF11`, etc. The shared templates also have no mechanism to:

1. Derive the correct build configuration from a release tag (`v10.1.2` → `Release EF10`)
2. Derive the correct .NET SDK version from that tag (`v10.*` → `10.0.x`)
3. Run two separate Release Drafter instances with different `tag-filter` values for preview versioning

### 6.2 Required Changes to devops-templates

All three shared templates need a `buildConfiguration` input. All changes default to the existing behaviour so no existing callers break.

#### `publish-release.yml`

Add a `buildConfiguration` input and a `resolve` job that derives the configuration and SDK version from the release tag. The `publish` job receives them as outputs:

```yaml
# devops-templates/.github/workflows/publish-release.yml

on:
  workflow_call:
    inputs:
      solution:
        required: true
        type: string
      dotnetVersion:
        required: true
        type: string
      hasTests:
        required: false
        default: true
        type: boolean
      buildConfiguration:                               # NEW
        description: "Build configuration. When 'auto', derived from the release tag (vN.* → Release EFN)."
        required: false
        default: "Release"
        type: string
    secrets:
      NUGET_API_KEY:
        required: true

jobs:
  resolve:                                              # NEW job — only does work when buildConfiguration=auto
    runs-on: ubuntu-latest
    outputs:
      configuration: ${{ steps.cfg.outputs.configuration }}
      dotnet_version: ${{ steps.cfg.outputs.dotnet_version }}
      version:        ${{ steps.cfg.outputs.version }}
    steps:
      - name: Resolve from tag
        id: cfg
        run: |
          TAG="${{ github.event.release.tag_name }}"
          VERSION="${TAG#v}"
          if [ "${{ inputs.buildConfiguration }}" = "auto" ]; then
            MAJOR="${VERSION%%.*}"
            echo "configuration=Release EF${MAJOR}" >> "$GITHUB_OUTPUT"
            echo "dotnet_version=${MAJOR}.0.x"      >> "$GITHUB_OUTPUT"
          else
            echo "configuration=${{ inputs.buildConfiguration }}" >> "$GITHUB_OUTPUT"
            echo "dotnet_version=${{ inputs.dotnetVersion }}"     >> "$GITHUB_OUTPUT"
          fi
          echo "version=${VERSION}" >> "$GITHUB_OUTPUT"

  publish:
    needs: resolve
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v5

      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: ${{ needs.resolve.outputs.dotnet_version }}

      - name: Restore
        run: dotnet restore ${{ inputs.solution }}

      - name: Build
        run: |
          dotnet build ${{ inputs.solution }} \
            --configuration "${{ needs.resolve.outputs.configuration }}" \
            --no-restore \
            /p:Version=${{ needs.resolve.outputs.version }}

      - name: Test
        if: inputs.hasTests == true
        run: |
          dotnet test --solution ${{ inputs.solution }} \
            --configuration "${{ needs.resolve.outputs.configuration }}" \
            --no-build

      - name: Pack
        run: |
          dotnet pack ${{ inputs.solution }} \
            --configuration "${{ needs.resolve.outputs.configuration }}" \
            --no-build -o artifacts \
            /p:Version=${{ needs.resolve.outputs.version }}

      - name: Publish to NuGet
        run: |
          dotnet nuget push artifacts/**/*.nupkg \
            -k ${{ secrets.NUGET_API_KEY }} \
            -s https://api.nuget.org/v3/index.json \
            --skip-duplicate
```

**Backwards compatibility:** existing callers pass no `buildConfiguration` → gets `Release` → resolves the tag for version only, configuration stays `Release`. No existing caller breaks.

**Multi-version callers:** pass `buildConfiguration: auto` → the `resolve` job derives `Release EF10` and `10.0.x` from the tag. The caller's `dotnetVersion` input is ignored when in `auto` mode.

#### `publish-preview.yml`

Add `buildConfiguration` and `drafterConfig` inputs. The `drafterConfig` input allows callers to specify which Release Drafter config file to use — enabling the per-EF-line `tag-filter` pattern. The DynamoDB provider calls this template in a matrix, once per EF version:

```yaml
# devops-templates/.github/workflows/publish-preview.yml

on:
  workflow_call:
    inputs:
      solution:
        required: true
        type: string
      dotnetVersion:
        required: true
        type: string
      hasTests:
        required: false
        default: true
        type: boolean
      prereleaseIdentifier:
        required: false
        default: "preview"
        type: string
      buildConfiguration:                               # NEW
        description: "Build configuration (e.g. 'Release EF10'). Defaults to 'Release'."
        required: false
        default: "Release"
        type: string
      drafterConfig:                                    # NEW
        description: "Release Drafter config file name (e.g. release-drafter-ef10.yml)."
        required: false
        default: "release-drafter.yml"
        type: string
    secrets:
      NUGET_API_KEY:
        required: true

jobs:
  publish:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v5

      - name: Resolve next version via Release Drafter
        id: release-drafter
        uses: release-drafter/release-drafter@v7
        with:
          config-name: ${{ inputs.drafterConfig }}      # CHANGED — was hardcoded release-drafter.yml
          dry-run: true
        env:
          GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}

      - name: Resolve preview version
        id: version
        run: |
          resolved="${{ steps.release-drafter.outputs.resolved_version }}"
          base="${resolved%%-*}"
          package_version="${base}-${{ inputs.prereleaseIdentifier }}.${{ github.run_number }}"
          printf 'package_version=%s\n' "$package_version" | tee -a "$GITHUB_OUTPUT"

      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v5
        with:
          dotnet-version: ${{ inputs.dotnetVersion }}

      - name: Restore
        run: dotnet restore ${{ inputs.solution }}

      - name: Build
        run: |
          dotnet build ${{ inputs.solution }} \
            --configuration "${{ inputs.buildConfiguration }}" \  # CHANGED
            --no-restore \
            /p:Version=${{ steps.version.outputs.package_version }}

      - name: Test
        if: inputs.hasTests == true
        run: |
          dotnet test --solution ${{ inputs.solution }} \
            --configuration "${{ inputs.buildConfiguration }}" \  # CHANGED
            --no-build

      - name: Pack
        run: |
          dotnet pack ${{ inputs.solution }} \
            --configuration "${{ inputs.buildConfiguration }}" \  # CHANGED
            --no-build -o artifacts \
            /p:Version=${{ steps.version.outputs.package_version }}

      - name: Publish to NuGet
        run: |
          dotnet nuget push artifacts/**/*.nupkg \
            -k ${{ secrets.NUGET_API_KEY }} \
            -s https://api.nuget.org/v3/index.json \
            --skip-duplicate
```

**Backwards compatibility:** `buildConfiguration` defaults to `Release`, `drafterConfig` defaults to `release-drafter.yml` — all existing callers unchanged.

#### `pr-build.yaml`

Add `buildConfiguration` input, defaulting to `release`:

```yaml
# devops-templates/.github/workflows/pr-build.yaml (inputs section — add alongside existing inputs)

buildConfiguration:                                     # NEW
  description: "Build configuration (e.g. 'Debug EF10')"
  required: false
  default: "release"
  type: string
```

Replace the two hardcoded `--configuration ${{ inputs.buildConfiguration }}` occurrences (Build Code step, line 169; Test Code steps, lines 177, 185, 203) with `--configuration "${{ inputs.buildConfiguration }}"`.

**Backwards compatibility:** defaults to `release` — existing callers unaffected.

### 6.3 Updated DynamoDB Caller Workflows

With devops-templates updated, the DynamoDB caller workflows remain thin. The multi-version logic lives in the matrix and the `auto` configuration mode.

#### `pr-build.yaml`

A matrix over EF versions — each entry specifies the configuration and SDK, calls the shared template once per version:

```yaml
name: PR Build

on:
  pull_request:
    branches:
      - main

permissions: write-all

jobs:
  build:
    strategy:
      matrix:
        include:
          - configuration: "Debug EF10"
            dotnetVersion: "10.0.x"
          - configuration: "Debug EF11"
            dotnetVersion: "11.0.x"
    uses: LayeredCraft/devops-templates/.github/workflows/pr-build.yaml@v9.0
    with:
      solution: EntityFrameworkCore.DynamoDb.slnx
      hasTests: true
      useMtpRunner: true
      buildConfiguration: ${{ matrix.configuration }}
      dotnetVersion: ${{ matrix.dotnetVersion }}
      runCdk: false
    secrets: inherit
```

Adding EF12: one new matrix entry. Nothing else changes.

#### `publish-preview.yaml`

Same matrix pattern — the shared template runs once per EF version, each with its own drafter config and SDK:

```yaml
name: Publish Preview

on:
  workflow_dispatch:
  push:
    branches:
      - main
    paths-ignore:
      - 'docs/**'
      - 'README.md'
      - 'ROADMAP.md'

permissions: write-all

jobs:
  publish:
    strategy:
      matrix:
        include:
          - configuration: "Release EF10"
            dotnetVersion: "10.0.x"
            drafterConfig: "release-drafter-ef10.yml"
          - configuration: "Release EF11"
            dotnetVersion: "11.0.x"
            drafterConfig: "release-drafter-ef11.yml"
    uses: LayeredCraft/devops-templates/.github/workflows/publish-preview.yml@v9.0
    with:
      solution: EntityFrameworkCore.DynamoDb.slnx
      buildConfiguration: ${{ matrix.configuration }}
      dotnetVersion: ${{ matrix.dotnetVersion }}
      drafterConfig: ${{ matrix.drafterConfig }}
      hasTests: true
    secrets: inherit
```

#### `publish-release.yaml`

No matrix needed — each release tag targets exactly one EF version. Pass `buildConfiguration: auto` and let the shared template derive everything from the tag:

```yaml
name: Publish Release

on:
  release:
    types: [published]

permissions: write-all

jobs:
  publish:
    uses: LayeredCraft/devops-templates/.github/workflows/publish-release.yml@v9.0
    with:
      solution: EntityFrameworkCore.DynamoDb.slnx
      dotnetVersion: "10.0.x"    # fallback only — overridden when buildConfiguration=auto
      buildConfiguration: auto
      hasTests: true
    secrets: inherit
```

Tag `v10.1.2` published → shared template resolves `Release EF10` + `10.0.x` → builds + pushes `10.1.2`.
Tag `v11.0.0` published → shared template resolves `Release EF11` + `11.0.x` → builds + pushes `11.0.0`.
No changes to this file ever needed when adding a new EF version.

### 6.4 Release Drafter Config Per EF Line

Two separate config files in `.github/`, each scoped to its own tag namespace via `tag-filter`:

**`.github/release-drafter-ef10.yml`:**
```yaml
tag-template: 'v10.$MINOR.$PATCH'
tag-filter: '^v10\.'
version-template: '10.$MINOR.$PATCH'
name-template: 'v$RESOLVED_VERSION (EF Core 10)'
change-template: '- $TITLE @$AUTHOR (#$NUMBER)'
categories:
  - title: 'New Features'
    labels: ['feature', 'enhancement']
  - title: 'Bug Fixes'
    labels: ['fix', 'bugfix', 'bug']
  - title: 'Breaking Changes'
    labels: ['breaking-change']
```

**`.github/release-drafter-ef11.yml`:**
```yaml
tag-template: 'v11.$MINOR.$PATCH'
tag-filter: '^v11\.'
version-template: '11.$MINOR.$PATCH'
name-template: 'v$RESOLVED_VERSION (EF Core 11)'
change-template: '- $TITLE @$AUTHOR (#$NUMBER)'
categories:
  - title: 'New Features'
    labels: ['feature', 'enhancement']
  - title: 'Bug Fixes'
    labels: ['fix', 'bugfix', 'bug']
  - title: 'Breaking Changes'
    labels: ['breaking-change']
```

Each drafter only considers tags in its own version namespace. On the first EF11 run there are no `v11.*` tags yet, so it starts from `11.0.0` — matching the `VersionPrefix` in the csproj.

### 6.5 Complete Change Checklist When Adding EF11

**`devops-templates` repo (do this first):**
- [ ] Add `buildConfiguration` input (default: `Release`) + `resolve` job to `publish-release.yml`
- [ ] Add `buildConfiguration` (default: `Release`) and `drafterConfig` (default: `release-drafter.yml`) inputs to `publish-preview.yml`
- [ ] Add `buildConfiguration` input (default: `release`) to `pr-build.yaml`
- [ ] Bump template version tag (e.g. `v9.0`)

**DynamoDB provider — project files:**
- [ ] Add `Debug EF11` / `Release EF11` configuration blocks to provider csproj (set `<TargetFramework>net11.0</TargetFramework>` only — no `DefineConstants`)
- [ ] Add conditional EF11 `<PackageReference>` block to provider csproj
- [ ] Add `<VersionPrefix Condition="'$(TargetFramework)' == 'net11.0'">11.0.0</VersionPrefix>` to provider csproj
- [ ] Mirror all configuration changes in all test project csproj files
- [ ] Update `global.json` — set `rollForward: latestMajor` (or pin .NET 11 SDK)

**DynamoDB provider — GitHub workflows:**
- [ ] Update `pr-build.yaml` — add EF11 matrix entry
- [ ] Update `publish-preview.yaml` — add EF11 matrix entry
- [ ] Bump devops-templates version reference in all three caller workflows (e.g. `@v8.2` → `@v9.0`)
- [ ] Add `.github/release-drafter-ef10.yml`
- [ ] Add `.github/release-drafter-ef11.yml`
- [ ] Remove old `.github/release-drafter.yml`

**DynamoDB provider — source code:**
- [ ] Run `dotnet build --configuration "Debug EF11"` — each compiler error = one required `#if` guard
- [ ] Wrap each breaking API difference in `#if NET10_0` / `#else` / `#endif`
- [ ] Run `dotnet test --configuration "Debug EF10"` and `dotnet test --configuration "Debug EF11"` — both must pass

### 6.6 Updating the Old Workflow Comments

Replace the branch-per-version comment in the existing caller workflow files:

```yaml
# OLD — remove this:
# To add support for additional .NET versions (e.g. net9, net11), add release branches
# (e.g. release/net9) and wire up separate caller workflows targeting those branches.

# NEW:
# Multi-version support: add a new matrix entry for the new EF version.
# The release tag prefix (vN.*) drives configuration and SDK selection automatically
# in publish-release.yaml via buildConfiguration: auto.
# See docs/multi-version-ef-strategy.md for the full strategy.
```
