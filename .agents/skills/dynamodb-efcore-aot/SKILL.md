---
name: dynamodb-efcore-aot
description: Implement, debug, or verify EF Core precompiled queries and NativeAOT for this DynamoDB provider. Use for generated query interceptors, compiled-model failures, AOT materializers, generated PartiQL templates, Microsoft.EntityFrameworkCore.Tasks integration, or NativeAOT publish failures.
---

# DynamoDB EF Core AOT

Keep AOT support on EF Core's existing query-interceptor generator. Do not add a provider source
generator unless EF Core can no longer represent a required query shape.

## Start here

1. Read `AGENTS.md` and `AGENTS.local.md`.
2. Inspect these files based on the failure:
   - interceptor generation:
     `src/EntityFrameworkCore.DynamoDb/Design/Internal/DynamoPrecompiledQueryCodeGenerator.cs`
   - command templates:
     `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQuerySqlGenerator.cs`
   - generated runtime entry points:
     `src/EntityFrameworkCore.DynamoDb/Infrastructure/DynamoGeneratedQueryRuntime.cs`
   - materializers:
     `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoProjectionBindingRemovingExpressionVisitor.cs`
   - compiled models:
     `src/EntityFrameworkCore.DynamoDb/Design/Internal/DynamoCSharpRuntimeAnnotationCodeGenerator.cs`

## Design rules

- Generate structured PartiQL command segments, not serialized provider expression trees.
- Keep table/index selection, scan checks, limits, tokens, consistency, and result cardinality in the
  generated template.
- Resolve property-specific type mappings from the compiled model so value converters survive.
- Enumerate collection parameters once. Stop at the first item beyond DynamoDB's supported limit.
- Keep generated-code-only APIs grouped under `DynamoGeneratedQueryRuntime` and hidden from normal
  IntelliSense.
- Support both EF10 and EF11 configurations. Treat generated code as compiler input, not text that
  is only inspected by assertions.

## Verification

Run the smallest relevant check first:

```bash
task test:aot-generation CONFIG="Debug EF10"
task test:aot-generation CONFIG="Debug EF11"
task test:aot-publish
```

The generation test must compile generated interceptors and cover scalar parameters, collection
parameters, entity materialization, and a property value converter. The publish test must build the
native executable, execute an intercepted query, and materialize the fake DynamoDB response.

Before completion, also run:

```bash
task test:ef10
task test:ef11
task docs:build
```

NativeAOT warnings from EF Core, the AWS SDK, or provider features outside precompiled query
execution are expected while support is experimental. New errors, missing interceptors, serialized
query trees, or unbounded generated-source growth are not acceptable.

## Documentation

If behavior or setup changes, update:

- `docs/querying/precompiled-queries.md`
- `docs/querying/how-queries-execute.md`
- `docs/limitations.md`
- `zensical.toml` when navigation changes
