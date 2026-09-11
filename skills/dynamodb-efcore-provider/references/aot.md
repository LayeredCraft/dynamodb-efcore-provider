# Precompiled Queries and NativeAOT

NativeAOT support is experimental and uses EF Core precompiled queries. Keep queries statically
discoverable at build time; queries assembled from runtime expression trees cannot be precompiled.

Check the current provider documentation before enabling it. Important restrictions include complex
properties, some field-backed values, some collection converter shapes, and query expressions EF
Core cannot convert to generated code. Unsupported precompiled shapes fail during build rather than
falling back at runtime.

Native publish-and-run coverage is currently EF10-only. EF11 interceptor generation is covered, but
native publish-and-run remains blocked upstream. Treat warnings from trimming or dynamic-code paths
as part of the experimental support boundary and test the exact deployed query shapes.
