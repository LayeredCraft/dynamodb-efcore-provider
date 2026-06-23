---
name: dynamodb-efcore-specification-tests
description: Use when implementing, extending, classifying, or debugging EF Core cross-provider specification tests for the DynamoDB EF Core provider, especially Specification.Tests bases, *TestBase<TFixture> inheritance, Dynamo test overrides, ComplianceDynamoTest inventory, skip/pass/fail classification, and spec-test coverage planning.
---

# DynamoDB EF Core Specification Tests

Use this skill when working on EF Core specification-test coverage for this provider: selecting
upstream base classes, adding or repairing DynamoDB spec tests, classifying inherited methods,
updating compliance inventory, and keeping coverage docs aligned.

Spec tests are expensive because each inherited EF Core method needs a decision: pass, adapt, skip
for a real DynamoDB constraint, or expose a provider gap. Before editing, understand the upstream
fixture model and DynamoDB mapping constraints for every entity involved.

## Start by loading context

1. Read repo instructions:
  - `AGENTS.md`
  - `AGENTS.local.md` if present
  - `tests/AGENTS.md`
  - `tests/EntityFrameworkCore.DynamoDb.SpecificationTests/AGENTS.md`
2. Read `docs/spec-test-coverage.md` first. Treat it as coverage planning truth, then cross-check
   implemented entries with `ComplianceDynamoTest` and concrete files because inventory docs can
   drift:
  - candidate status (`Implemented`, `Implement Next`, `Future`, `Skip`)
  - method counts and notes
  - threshold rule for newly implemented base classes: implement only when DynamoDB can meaningfully
    cover about 70% or more of unique methods, including accurate explicit skips
3. Read target upstream EF Core base test from
   `~/Repos/CSharp/efcore/test/EFCore.Specification.Tests/`.
4. Read closest existing DynamoDB spec class in same family. Prefer newer/compliant examples over
   legacy outliers; do not copy empty skipped overrides, `Task.CompletedTask` skip bodies, or
   missing override guards from older classes.
5. Use Cosmos and MongoDB providers only as references, not authorities:
  - Cosmos spec tests: `~/Repos/CSharp/efcore/test/EFCore.Cosmos.FunctionalTests/`
  - Cosmos provider source: `~/Repos/CSharp/efcore/src/EFCore.Cosmos/`
  - MongoDB provider/tests: `~/Repos/CSharp/mongo-efcore-provider/`

## Implementation workflow

1. Identify the target base spec class or existing DynamoDB spec class from the user request and
   `docs/spec-test-coverage.md`.
2. Create or extend `*DynamoTest.cs` in `tests/EntityFrameworkCore.DynamoDb.SpecificationTests/`.
3. Reuse existing shared family fixtures when present, especially
   `NorthwindQueryDynamoFixture<TModelCustomizer>` and `BasicTypesQueryDynamoFixture`. Otherwise
   create a fixture by extending the base fixture type from the upstream spec test.
4. Wire shared DynamoDB infrastructure:
  - all live-DynamoDB fixtures: `DynamoTestStoreFactory.Instance`
  - all live-DynamoDB fixtures:
    `.UseDynamo(o => o.DynamoDbClient(DynamoTestStoreFactory.Instance.Client))`
  - query/baseline fixtures that assert emitted PartiQL: implement `IDynamoSpecificationFixture`,
    expose `TestSqlLoggerFactory`, override `ShouldLogCategory` via `ShouldLogDynamoSql`, and call
    `fixture.ClearSql()` from the provider test constructor
  - non-query/spec utility fixtures may omit `IDynamoSpecificationFixture` when they do not use
    `AssertSql`
  - adapt constructor arguments and service hooks to the upstream fixture shape, but keep DynamoDB
    store factory/client wiring consistent with existing spec tests
  - override upstream `ClearLog()` to call `Fixture.ClearSql()` when the base class exposes a
    log-clearing hook
5. Map every entity DynamoDB needs:
  - table name
  - partition key via `HasPartitionKey(...)`
  - sort key only when the EF key shape or test identity requires a two-part DynamoDB key
  - ignore only fixture-only types not needed for supported cases
  - any inherited test requiring ignored navigations/keyless/owned/FK-heavy types needs an explicit
    skip override with a canonical `SkipReason`
6. Add an xUnit-discovered concrete test class using the closest existing pattern:
  - if provider test class is abstract, add nested `*DynamoTestDefault` (or multiple concrete
    variants when upstream requires it) and pass provider fixture to base constructor
  - if provider test class can be concrete, annotate that class with
    `[Collection(DynamoSpecificationCollection.Name)]` instead of adding a nested default class
  - if upstream base does not provide fixture injection but expects a fixture object, use
    `IClassFixture<TFixture>` as existing interception tests do
  - if upstream exposes `TestStore`/context factory hooks instead, create a `DynamoTestStore`,
    implement lifetime cleanup, and configure contexts through `TestStore.AddProviderOptions(...)`
    as existing seeding tests do
  - inject/assign `DynamoSpecificationContainerFixture` for live-DynamoDB tests when needed to force
    container startup before `DynamoTestStoreFactory.Instance.Client` is used
  - spec utility tests that do not touch DynamoDB Local do not need the collection/container
7. Add override guard:

```csharp
[ConditionalFact]
public virtual void Check_all_tests_overridden()
    => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(CurrentDynamoTest));
```

Use the provider test type being audited: the abstract provider base for nested concrete xUnit
subclasses, or the concrete provider class when the class itself is discovered. Do not use
`GetType()`.

Concrete class pattern:

```csharp
[Collection(DynamoSpecificationCollection.Name)]
public sealed class XxxDynamoTestDefault : XxxDynamoTest
{
    public XxxDynamoTestDefault(
        XxxDynamoFixture fixture,
        DynamoSpecificationContainerFixture containerFixture)
        : base(fixture)
        => _ = containerFixture;
}
```

8. Override every inherited test method. No method left undecided.
9. Update `ComplianceDynamoTest.GetBaseTestClasses()` when adding/removing implemented base class.
10. Update `docs/spec-test-coverage.md` in same change.
11. Run focused tests, then compliance/broader spec tests when practical.

## Override decision taxonomy

For each inherited method, classify before coding.

### Supported async query or behavior

Call base implementation. Assert generated PartiQL when query emits SQL.

```csharp
public override Task Where_simple(bool async)
    => DynamoTestHelpers.Instance.NoSyncTest(async, async a =>
    {
        await base.Where_simple(a);
        AssertSql("""
        SELECT ...
        """);
    });
```

### Sync query path

DynamoDB query enumeration is async-only. Wrap sync query-enumeration variants with
`DynamoTestHelpers.Instance.NoSyncTest(...)` or a local wrapper instead of accepting raw sync
failures.

```csharp
public override void Some_sync_query_test()
    => DynamoTestHelpers.Instance.NoSyncTest(() => base.Some_sync_query_test());
```

Do not use `NoSyncTest` for non-query sync APIs such as `SaveChanges`, `EnsureCreated`,
`EnsureDeleted`, or `CanConnect`; those throw different provider exceptions and need method-specific
handling or skips.

### Real DynamoDB architectural constraint

Skip only after verifying test shape requires something DynamoDB/PartiQL/provider model cannot
support. Use `SkipReason` constants from
`tests/EntityFrameworkCore.DynamoDb.SpecificationTests/SkipReason.cs`
for shared durable constraints; this shared file supersedes stale guidance that says to define skip
constants per class. Method-specific translation limitations may use a local/literal skip reason;
promote repeated reasons to `SkipReason` when they recur across classes.

Keep skipped overrides wired to the inherited base implementation whenever possible. Do not copy
legacy empty skipped overrides or `Task.CompletedTask` skip bodies from older classes; introduce
such exceptions only when calling base is unsafe, and document why.

```csharp
[ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
public override Task Join_customers_orders(bool async)
    => base.Join_customers_orders(async);
```

Common durable constraints:

- joins, Include, navigations, foreign-key relationship graphs
- `GROUP BY`, set operations, unsupported aggregate query shapes
- keyless entities; missing partition key
- nullable/shadow keys when DynamoDB table identity cannot represent them
- composite keys beyond partition key + optional sort key
- owned entity types where provider requires complex types instead
- explicit EF transaction scopes unsupported by DynamoDB provider

### Provider gap

If test expectation is compatible with DynamoDB and PartiQL, do not hide it behind an architectural
skip. Fix provider, or document an explicit tracked provider-gap skip if fix exceeds task scope.

Typical provider-gap areas:

- LINQ translation in
  `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryableMethodTranslatingExpressionVisitor.cs`
- PartiQL generation in `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQuerySqlGenerator.cs`
- query compilation/materialization in
  `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoShapedQueryCompilingExpressionVisitor.cs`
- execution in `src/EntityFrameworkCore.DynamoDb/Storage/DynamoClientWrapper.cs`
- type mapping in `src/EntityFrameworkCore.DynamoDb/Storage/DynamoTypeMappingSource.cs`

## Scout/subagent failure triage

After first implementation pass, run target tests and split failures. If subagents are available in
the parent/orchestrator session, use scout-style delegation: one scout per failing method or small
cluster. Do not launch subagents from child-worker sessions. If delegation is unavailable, perform
the same evidence checklist inline.

Scout prompt shape:

```text
Analyze failing spec test <Class.Method>. Read upstream base method, Dynamo override, fixture, and failure output.
Return: upstream intent, observed failure, generated PartiQL/error, DynamoDB support classification
(architecture constraint vs provider gap vs test/fixture bug vs expected sync path), recommended code change,
needed SkipReason/docs update, and confidence.
```

Require each scout to answer with evidence, not vibes:

- exact base method behavior
- exact exception or PartiQL mismatch
- whether Cosmos/Mongo implement/skip comparable method
- DynamoDB/PartiQL limitation if claiming unsupported
- provider file likely responsible if claiming provider gap

Then reconcile scouts centrally. Do not let scouts mass-skip failures; classify each one from
evidence. Human/lead agent makes final classification.

## Failure triage checklist

For each failure ask, in order:

1. Did sync variant run? Use `DynamoTestHelpers.Instance.NoSyncTest(...)` or a local wrapper if
   failure is expected sync query enumeration.
2. Is fixture wrong? Missing table/key/ignored navigation/seed data often masquerades as provider
   bug.
3. Is `AssertSql` stale? Inspect captured `PartiQL baseline mismatch` details.
4. Does base test require an intentional scan or filtered key-condition escape hatch? Add/verify
   `AllowScan()` or `AsUnsafeFilteredQuery()` before classifying as skip/provider gap.
5. Does base test require joins, navigations, Include, FK graph fixup, keyless type, `GROUP BY`, set
   ops, or >2-part key? If yes, skip with canonical reason and update coverage notes if class status
   changes.
6. Can DynamoDB PartiQL express query? If yes, treat failure as provider gap until proven otherwise.
7. Is result ordering assumed without stable DynamoDB order? Use existing ordered-result skip reason
   or adapt assertion only when base allows.
8. Is failure environmental? DynamoDB Local/Testcontainers/setup failure should not change test
   classification.

## Coverage doc rules

`docs/spec-test-coverage.md` is the planning inventory for status and rationale; cross-check
implemented entries with `ComplianceDynamoTest` and concrete files. Update it when:

- adding a new implemented base class
- deciding class is future/skip instead of implementable
- discovering large unsupported area inside implemented class
- changing method counts, feasibility, or notes

Do not update coverage docs for transient investigation notes unless classification, method counts,
status, or rationale changes.

Write user-facing reasons, not internal blame. Good note: “Navigation-dependent cases are skipped
because DynamoDB does not support navigation relationships.” Bad note: “test failed, skipped.”

Also update `ComplianceDynamoTest` for executable implemented-base inventory. Docs and compliance
must agree.

## Commands

Use the .NET test MCP server when available. CLI fallback focused class/method:

```bash
dotnet test tests/EntityFrameworkCore.DynamoDb.SpecificationTests/EntityFrameworkCore.DynamoDb.SpecificationTests.csproj --filter "FullyQualifiedName~ClassOrMethod"
```

Compliance inventory:

```bash
dotnet test tests/EntityFrameworkCore.DynamoDb.SpecificationTests/EntityFrameworkCore.DynamoDb.SpecificationTests.csproj --filter "FullyQualifiedName~ComplianceDynamoTest"
```

Full spec project when practical:

```bash
dotnet test tests/EntityFrameworkCore.DynamoDb.SpecificationTests/EntityFrameworkCore.DynamoDb.SpecificationTests.csproj
```

When debugging query baselines, inspect assertion failures and captured `PartiQL baseline mismatch`
text. The current runner may not support older xUnit live-output switches such as
`--show-live-output`; use `--output Detailed` only if supported by `dotnet test --help`.

## Done means

- `Check_all_tests_overridden` guard present for behavioral spec test classes with inherited virtual
  test surface; utility/compliance/API consistency classes may be exceptions when guard is not
  meaningful
- every inherited test method explicitly overridden
- skips use accurate reasons; call base where safe and document any no-op/`Task.CompletedTask`
  exception
- supported methods call base and assert PartiQL/results as appropriate
- provider gaps fixed or explicitly left with documented rationale
- `ComplianceDynamoTest` updated when implemented-base inventory changes
- `docs/spec-test-coverage.md` updated as coverage inventory
- focused tests run; broader tests run or reason documented
