---
name: dynamodb-efcore-specification-tests
description: "Use for DynamoDB EF Core specification-test work: adding, extending, classifying, or debugging tests under tests/EntityFrameworkCore.DynamoDb.SpecificationTests, overriding EF Core *TestBase<TFixture> bases, triaging pass/skip/fail outcomes, updating ComplianceDynamoTest, or editing docs/spec-test-coverage.md. Not for normal unit/integration suites."
---

# DynamoDB EF Core Specification Tests

Use this skill when working on EF Core specification-test coverage for this provider: selecting
upstream base classes, adding or repairing DynamoDB spec tests, classifying inherited methods,
updating compliance inventory, and keeping coverage docs aligned.

Spec tests are expensive because each inherited EF Core method needs an evidence-backed decision:
pass, adapt, skip for a real DynamoDB constraint, fix a provider gap, or repair a test/fixture bug.
Before editing, understand the upstream fixture model and DynamoDB mapping constraints for every
entity involved. Do not classify failures by instinct; run the inherited test surface first, then
triage failures with evidence.

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
    cover about 70% or more of unique methods. Count passing/adapted methods, intentional sync
    wrappers, and durable DynamoDB architectural skips. Do not count provider-gap deferrals as
    coverage. If a class would be mostly skips, implement only when the docs explicitly choose to
    document that unsupported surface.
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
   create a fixture by extending the base fixture type from the upstream spec test. Inspect upstream
   fixture hooks before copying patterns: `CreateContext`, `AddOptions`, `OnModelCreating`,
   `CleanAsync`, `SeedAsync`, expected data, entity sorters/assertors, model customizers, service
   replacement, logging, and test-store lifetime.
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

8. Override every inherited test method. No method left undecided. First pass should keep methods
   unblocked: call `base` for inherited tests and wrap expected sync-query paths with `NoSyncTest`.
   Avoid support-classification skips before the first red run; failures are the evidence used for
   classification. Exception: when the upstream method body clearly and statically requires a
   durable DynamoDB constraint (navigation graph, FK relationship, keyless type, explicit
   transaction, >2-part key), an early skip is acceptable only with a cited upstream shape and
   centralized `SkipReason`. Never pre-skip provider gaps.
9. For large inherited surfaces, bootstrap mechanically: create the class/fixture skeleton, compile,
   run only the override guard, copy missing method names/signatures from guard failures, preserve
   upstream attributes/parameters/return types, and repeat until the guard passes. Redirect test
   output to a file if the runner truncates guard failures. Use simple base calls or `NoSyncTest`
   wrappers first; do not hand-classify 100+ methods from memory.
10. Run the whole target class or method family with all overrides present. Treat the first red run as
   the classification input, not as failure of the implementation approach.
11. Split failures into small clusters and use scout/research subagents for triage when there is more
    than one failure or a failure has unclear support status; see "Subagent failure triage workflow"
    below. The parent agent owns final classification.
12. Update `ComplianceDynamoTest.GetBaseTestClasses()` when adding/removing implemented base class.
    If a base exists only for some target frameworks, wrap `using` statements and `yield return`
    entries in matching `#if` guards.
13. Update `docs/spec-test-coverage.md` in same change.
14. Run focused tests, then compliance/broader spec tests when practical.

## Override decision taxonomy

For each inherited method, classify before finalizing the override. The first implementation pass
runs inherited methods to collect evidence; the final pass turns that evidence into supported calls,
fixture fixes, provider fixes, sync wrappers, or centralized skips.

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
support. DynamoDB PartiQL supports a DynamoDB-specific subset centered on `SELECT`, `INSERT`,
`UPDATE`, and `DELETE`; it is not relational SQL. Durable unsupported areas include joins,
relational navigation graphs, arbitrary multi-table relationship behavior, `GROUP BY`, set
operations, explicit EF transaction scopes, and key shapes DynamoDB tables cannot represent.

Centralize skip reasons in
`tests/EntityFrameworkCore.DynamoDb.SpecificationTests/SkipReason.cs`. Use existing constants or add
new constants there. Do not add per-class constants or local/literal skip strings for new work;
legacy tests may still contain older patterns.

Keep skipped overrides wired to the inherited base implementation whenever possible. Do not copy
legacy empty skipped overrides or `Task.CompletedTask` skip bodies from older classes. Introduce a
no-op skipped body only when calling base is unsafe (for example, model creation fails before xUnit
can apply the skip), and add an adjacent comment naming the unsafe side effect or exception.

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
skip. Treat it as provider work and fix provider behavior so the spec test passes. Defer with a
provider-gap skip only when the user explicitly approves deferral or the task scope forbids provider
changes; the skip reason must be centralized in `SkipReason.cs` and mention the tracked gap. Existing
provider-gap constants are not blanket permission to skip new failures; cite the tracked issue or
scope decision when using them.

Typical provider-gap areas:

- LINQ translation in
  `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryableMethodTranslatingExpressionVisitor.cs`
- PartiQL generation in `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQuerySqlGenerator.cs`
- query compilation/materialization in
  `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoShapedQueryCompilingExpressionVisitor.cs`
- execution in `src/EntityFrameworkCore.DynamoDb/Storage/DynamoClientWrapper.cs`
- type mapping in `src/EntityFrameworkCore.DynamoDb/Storage/DynamoTypeMappingSource.cs`

Route by symptom:

- “could not be translated” or method-call pattern missing → translator visitors/method translators
- malformed or baseline-mismatched PartiQL → `DynamoQuerySqlGenerator` or stale `AssertSql`
- AWS SDK validation/runtime item mismatch → `DynamoClientWrapper` or serializer/wire-shape code
- wrong CLR values/materialization → shaped-query compiler or type mapping

## Subagent failure triage workflow

Use subagents to save parent-context tokens after the first red run. This is a core part of the
workflow, not optional polish, when many inherited tests fail.

1. Ensure every inherited method is overridden and runnable. Nothing should be left blocked by the
   override guard.
2. Run the whole target class or focused method family.
3. Group failures by method or by same exception/query-shape cluster.
4. In the parent/orchestrator session, launch scout/research subagents for each failure or cluster
   when there is more than one failure or a failure has unclear support status. Prefer fresh-context
   scouts with the failure output and exact file paths. Use researcher only when external DynamoDB,
   PartiQL, or EF Core evidence is needed. Do not launch subagents from child-worker sessions; if
   running as a child/subagent, do local targeted triage and return failure clusters to the parent.
5. Require each scout to classify the failure as exactly one primary category:
   - **DynamoDB architectural constraint**: DynamoDB/PartiQL cannot express the behavior, e.g.
     joins, relationship navigation graphs, `GROUP BY`, set operations, unsupported key shape, or
     explicit transaction semantics. Result: skip with canonical `SkipReason` and update docs/counts
     when coverage meaning changes.
   - **Provider gap**: DynamoDB/PartiQL can express the behavior, but this provider cannot translate,
     generate, execute, materialize, or map it yet. Result: update provider and make test pass unless
     the user explicitly approves deferral or scope forbids provider changes; any deferred skip uses
     a tracked centralized `SkipReason`.
   - **Test/fixture bug**: mapping, table/key config, seed data, logging hook, scan opt-in, or
     assertion baseline is wrong. Result: fix spec fixture/test code.
   - **Expected sync-query path**: failure is from unsupported sync query enumeration. Result: wrap
     with `NoSyncTest`, not a skip.
   - **Environmental failure**: DynamoDB Local/Testcontainers/SDK setup issue. Result: fix or report
     environment; do not change support classification.
6. Parent agent reconciles scout reports, applies fixes/skips centrally, and keeps `SkipReason.cs`,
   `ComplianceDynamoTest`, and `docs/spec-test-coverage.md` consistent.

Scout prompt shape:

```text
Analyze failing spec test <Class.Method>. Read upstream base method, Dynamo override, fixture, and failure output.
Return: upstream intent, observed failure, generated PartiQL/error, DynamoDB support classification
(DynamoDB architectural constraint vs provider gap vs test/fixture bug vs expected sync path vs environmental),
recommended code change, needed SkipReason/docs update, and confidence.
If claiming unsupported, cite the DynamoDB/PartiQL capability gap. If claiming provider gap, name likely provider file.
```

Require each scout to answer with evidence, not vibes. A compact table is ideal:

| Method | Upstream intent/line | Dynamo override | Async/sync | Exception or PartiQL | Capability evidence | Classification | Action | Skip/docs impact |
| ------ | -------------------- | --------------- | ---------- | -------------------- | ------------------- | -------------- | ------ | ---------------- |

Include:

- exact base method behavior and assertion intent
- exact exception, generated PartiQL, or baseline mismatch
- whether Cosmos/Mongo implement/skip comparable method, when relevant
- DynamoDB/PartiQL limitation if claiming unsupported
- provider file likely responsible if claiming provider gap
- smallest safe code/test/doc change

Then reconcile scouts centrally. Do not let scouts mass-skip failures; classify each one from
evidence. Human/lead agent makes final classification.

## Pressure-test support decisions

Before marking a class or method cluster done, challenge every skip and provider-gap decision:

- Did every inherited method run in the first red pass, then end as a supported call, intentional
  sync-query wrapper, fixture/provider fix, or centralized `SkipReason` skip?
- For every skip, is the reason a durable DynamoDB/PartiQL/model constraint rather than current
  provider behavior?
- For every provider-gap failure, did the agent identify the likely provider layer and attempt the
  fix when in scope?
- Did at least one scout report, external source, upstream base method, or comparable provider test
  support each non-obvious classification?
- Are `SkipReason.cs`, `ComplianceDynamoTest`, and `docs/spec-test-coverage.md` consistent with the
  final classification?

If any answer is no, keep triaging. Do not convert uncertainty into a skip.

## Failure triage checklist

For each failure ask, in order:

1. Did sync variant run? Use inherited/local `NoSyncTest(...)` when existing family classes expose
   one; otherwise use `DynamoTestHelpers.Instance.NoSyncTest(...)` if failure is expected sync query
   enumeration.
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
   or adapt assertion only when base allows. Sort-key queries may have stable DynamoDB order; scans
   and many filtered shapes do not.
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
task test:spec CONFIG="Debug EF10" FILTER="FullyQualifiedName~ClassOrMethod"
```

Compliance inventory:

```bash
task test:spec CONFIG="Debug EF10" FILTER="FullyQualifiedName~ComplianceDynamoTest"
```

Full spec project when practical:

```bash
task test:spec:all
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
