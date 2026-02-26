# ADR-001: Discriminator strategy for shared DynamoDB tables

## Status

- Proposed
- **Date:** 2026-02-26
- **Deciders:** LayeredCraft.EntityFrameworkCore.DynamoDb maintainers
- **Supersedes:** none

---

## Context

This provider targets DynamoDB via PartiQL `ExecuteStatement` and supports query operators such as
`Where`, `Select`, `First*`, and `ToList`, while intentionally excluding `Single*`, aggregates, and
other shapes that are not a good fit for current scope.

Shared-table models are expected in DynamoDB (multiple root entity types in one table). EF Core
still requires typed query semantics: a `DbSet<User>` query must not materialize `Order` items.
That requires discriminator enforcement.

Example shared table items:

| PK | SK | `__type` |
| --- | --- | --- |
| `TENANT#1` | `USER#123` | `User` |
| `TENANT#1` | `ORDER#9001` | `Order` |

If discriminator enforcement is weak, a query rooted at `DbSet<User>` can return an `Order` row
when caller-provided key predicates target the wrong keyspace.

DynamoDB/PartiQL constraints relevant to this decision:

- PartiQL `SELECT` can behave like a scan unless `WHERE` includes partition-key equality or `IN`.
- `ExecuteStatement` applies DynamoDB semantics (request limits and 1 MB processing bounds), and
  filtering does not guarantee key-efficient access patterns.
- Relying only on sort-key-based discrimination makes correctness depend on caller SK predicates,
  which is hard to prove in a general LINQ pipeline.

## Decision Drivers

- Guarantee typed-query correctness for shared tables (`DbSet<T>` returns only `T`).
- Keep query behavior predictable under DynamoDB access-pattern constraints.
- Avoid complex SK predicate algebra and brittle edge-case handling.
- Align with EF Core provider patterns where discriminator is explicit and validated.
- Keep room for future inheritance and broader query-shape support.

## Options Considered

### Option A: Dedicated discriminator attribute

Use a dedicated non-key attribute discriminator (default name `__type`, configurable), and enforce
it in query translation and materialization.

**Pros:**
- Preserves EF Core typed-query semantics across shared tables.
- Does not compete with user-provided sort-key predicates.
- Simpler validation and translation model than SK-only predicate merging.
- Clear diagnostics for missing/unknown/mismatched discriminator values.

**Cons:**
- Requires storing an additional attribute in each item.
- Introduces model configuration requirements for shared-table mappings.

### Option B: Discriminator encoded in SK only

Treat sort-key predicates (prefix/range/equality shapes) as the sole discriminator mechanism.

**Pros:**
- No additional discriminator attribute required.
- Matches common single-table key-encoding practices.

**Cons:**
- Correctness competes with caller SK predicates; hard to guarantee generally.
- Requires strict predicate compatibility checks or many unsupported query paths.
- Increases risk of runtime mismatches and ambiguous behavior.

## Decision

We will use **Option A: dedicated discriminator attribute** for shared-table discrimination.

## Rationale

Option A best satisfies correctness and predictability. It gives a clear, provider-controlled
contract: shared-table typed queries are always discriminator-filtered, independent of caller SK
conditions. This avoids making correctness contingent on sort-key predicate composition and avoids
complex query-shape-specific SK logic.

Option B remains attractive for pure key-encoding designs, but as a sole discrimination mechanism it
pushes too much complexity and ambiguity into translation and runtime validation. For this provider
phase, we prioritize robust typed semantics over SK-only discrimination.

## Consequences

**Positive:**
- Shared-table `DbSet<T>` queries have a consistent and enforceable type-safety contract.
- Query translation remains simpler and easier to reason about.
- Model validation can detect discriminator misconfiguration early.

**Negative / Trade-offs:**
- Item shape includes an extra discriminator attribute.
- Shared-table mappings require discriminator configuration and value management.

**Neutral / Follow-on work:**
- Add discriminator metadata annotations and fluent APIs.
- Add per-table-group discriminator validation in model validation.
- Inject discriminator predicates in root entity query translation.
- Add materialization guard behavior for missing/unknown/mismatched discriminator values.
- Update docs where current text says discriminator behavior is still pending.

## References

- AWS ExecuteStatement API: <https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_ExecuteStatement.html>
- DynamoDB PartiQL SELECT: <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.select.html>
- DynamoDB PartiQL overview: <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.html>
- Repository docs: `docs/operators.md`, `docs/configuration.md`, `docs/limitations.md`
