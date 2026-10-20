---
name: dynamodb-efcore-aot
description: Implement, debug, or verify EF Core precompiled queries and NativeAOT for this DynamoDB provider. Use for generated query interceptors, compiled-model failures, AOT materializers, generated PartiQL templates, Microsoft.EntityFrameworkCore.Tasks integration, or NativeAOT publish failures.
metadata:
  internal: true
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

## Failure triage

Classify a precompiled-query failure before changing provider code.

- Errors from EF query discovery, C#-to-LINQ translation, Roslyn syntax analysis, or interceptor
  source-location binding occur before DynamoDB translation. Reduce them to a minimal EF Core repro;
  do not add a provider workaround unless the provider integration point causes the failure.
- Errors from Dynamo query translation, generated PartiQL templates, generated runtime entry points,
  or the provider's generated-code rewrite belong here. Cover the fix with the smallest relevant
  generation test and, when runtime behavior changes, an AOT publish-and-run test.
- Keep query examples as one statically visible chain. Runtime scalar values are valid query
  parameters; query composition through runtime-built expression trees is not. If an EF
  precompiler cannot bind a direct context reference or argument, local aliases are a consumer
  workaround pending a minimal upstream repro, not a provider query restriction.
- Generated interceptors are compiler input tied to source locations. A workflow that retains them
  must remove stale output before its tooling build, regenerate it, then compile the new output.
  This is consumer build hygiene, not provider query translation behavior.

## Verification

Run the smallest relevant check first:

```bash
task test:aot-generation FRAMEWORK=net10.0
task test:aot-generation FRAMEWORK=net11.0
task test:aot-publish FRAMEWORK=net10.0
task test:aot-publish FRAMEWORK=net11.0
```

The generation test must compile generated interceptors and cover scalar parameters, collection
parameters, entity materialization, and a property value converter. The publish test generates a
physical `TargetFrameworkOverride.props` (see `scripts/write-target-framework-override.sh`),
builds and runs `tests/EntityFrameworkCore.DynamoDb.NativeAotTests` against DynamoDB Local,
checks the reviewed AOT warning baseline with `scripts/verify-nativeaot-warnings.sh`, and clears
the override afterward. Publish/runtime validation is CI-gated for **both** EF10 and EF11.

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
