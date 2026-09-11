# Precompiled Queries and NativeAOT

NativeAOT support is experimental and uses EF Core precompiled queries. Keep queries statically
discoverable at build time; queries assembled from runtime expression trees cannot be precompiled.

Check the current provider documentation before enabling it. Important restrictions include complex
properties, some field-backed values, some collection converter shapes, and query expressions EF
Core cannot convert to generated code. Unsupported precompiled shapes fail during build rather than
falling back at runtime.

Native publish-and-run is validated for both EF10 and EF11. Treat warnings from trimming or
dynamic-code paths as part of the experimental support boundary and test the exact deployed query
shapes.

`ExecuteUpdateAsync` precompiles under the same rules. On EF10 a single precompiled `ExecuteUpdate`
cannot mix constant and computed (self-referencing) setter values — the build fails with a clear
error; split such updates into separate calls. EF11 supports the mixed shape.
