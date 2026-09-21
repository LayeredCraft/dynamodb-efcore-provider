# Precompiled Queries and NativeAOT

NativeAOT support is experimental and uses EF Core precompiled queries. Keep queries statically
discoverable at build time; queries assembled from runtime expression trees cannot be precompiled.

Check the current provider documentation before enabling it. Important restrictions include
filtering on or projecting complex members, field-backed primitive collections, some collection
converter shapes, and query expressions EF Core cannot convert to generated code. Entities with
complex properties and complex collections materialize, but must use `AsNoTracking()` under NativeAOT
(dotnet/efcore#37750). Unsupported precompiled shapes fail during build rather than
falling back at runtime.

Native publish-and-run is validated for both EF10 and EF11. Treat warnings from trimming or
dynamic-code paths as part of the experimental support boundary and test the exact deployed query
shapes.

`ExecuteUpdateAsync` precompiles under the same rules. On EF10 a single precompiled `ExecuteUpdate`
cannot mix constant and computed (self-referencing) setter values — the build fails with a clear
error; split such updates into separate calls. EF11 supports the mixed shape.

## Runtime resource names (advanced; compiled models only)

An EF compiled model bypasses `OnModelCreating`, and DynamoDB statements name physical tables and
indexes directly. Only when **all** of these hold — a compiled model, physical table or index names
that differ between environments, and one artifact promoted unchanged — declare a logical table
identity and supply the physical names at runtime:

```csharp
b.ToTable(designTimeTable).HasLogicalTableName("Trivia");                  // model
b.HasGlobalSecondaryIndex("Gsi1", x => x.Gs1Pk).HasSecondaryIndexName(designTimeGsi1);

options.UseDynamo(d => d.RuntimeResourceNames(r => r                       // runtime
    .Table("Trivia", physicalTable)
    .SecondaryIndex("Trivia", "Gsi1", physicalGsi1)));
```

- Do **not** recommend this for ordinary JIT applications, identical physical names, or when
  rebuilding the compiled model per environment is acceptable. Use `ToTable(...)` and
  `HasSecondaryIndexName(...)` instead.
- The logical table identity is `HasLogicalTableName`; the logical index identity is the EF index
  name; `ToTable`/`HasSecondaryIndexName` stay the physical (design-time) names.
- Skip the mapping while EF design-time tooling generates the compiled model and interceptors
  (`if (!EF.IsDesignTime)`), so the artifact carries design-time names only.
- Mappings are fixed for the model's lifetime: not per-request table switching or tenant routing. A
  compiled model instance cannot be reused with a different mapping.
- `WithIndex("name")` always takes a physical index name: the configured name in JIT, the runtime
  name in JIT with a mapping, and the design-time name in precompiled queries. It is not portable
  between those modes; prefer automatic index selection when using runtime resource names.
- Unknown or misspelled logical names fail fast at model initialization.

