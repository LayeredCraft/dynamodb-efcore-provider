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

| PK         | SK           | `$type` |
|------------|--------------|---------|
| `TENANT#1` | `USER#123`   | `User`  |
| `TENANT#1` | `ORDER#9001` | `Order` |

If discriminator enforcement is weak, a query rooted at `DbSet<User>` can return an `Order` row
when caller-provided key predicates target the wrong keyspace.

DynamoDB/PartiQL constraints relevant to this decision:

- PartiQL `SELECT` via `ExecuteStatement` may require reading many items (scan-like) unless the
  statement can be satisfied with partition-key equality (or partition-key `IN`) and a key-selective
  access path.
- DynamoDB evaluates predicates in two stages (even when PartiQL appears as one `WHERE`):
  - **Item selection (key-selective):** key predicates determine which items are read/evaluated.
  - **Filtering (post-read):** remaining predicates discard items after read but before return.
- Filtering reduces returned items but does **not** reduce consumed read capacity for items already
  read/evaluated.
- A dedicated attribute discriminator is therefore always enforceable as a filter predicate, while
  SK-based discrimination is key-selective only when the effective SK predicate includes the
  discriminator keyspace.
- Relying only on sort-key-based discrimination makes correctness depend on caller SK predicates,
  unless the provider performs strict compatibility checks or rejects unsupported query shapes.

## Decision Drivers

- Guarantee typed-query correctness for shared tables (`DbSet<T>` returns only `T`).
- Keep correctness independent from caller-provided SK predicates.
- Prefer key-selective access patterns when possible, but never at the cost of typed correctness.
- Keep query behavior predictable under DynamoDB access-pattern constraints.
- Avoid complex SK predicate algebra and brittle edge-case handling.
- Align with EF Core provider patterns where discriminator is explicit and validated.
- Keep room for future inheritance and broader query-shape support.

## Options Considered

### Option A: Dedicated discriminator attribute

Use a dedicated non-key attribute discriminator (default name `$type`, configurable), and enforce
it in query translation and materialization.

In DynamoDB terms, this discriminator predicate is a filter: it enforces type correctness, but it
does not participate in key-based item selection.

**Pros:**

- Preserves EF Core typed-query semantics across shared tables.
- Does not compete with user-provided sort-key predicates.
- Simpler validation and translation model than SK-only predicate merging.
- Clear diagnostics for missing/unknown/mismatched discriminator values.
- Works consistently with current and future index access paths.

**Cons:**

- Requires storing an additional attribute in each item.
- Introduces model configuration requirements for shared-table mappings.
- Can still be expensive when many non-matching items are read and then filtered out.

### Option B: Discriminator encoded in SK only

Treat sort-key predicates (prefix/range/equality shapes) as the sole discriminator mechanism.

**Pros:**

- No additional discriminator attribute required.
- Matches common single-table key-encoding practices.
- Can reduce consumed read capacity when discriminator is part of the effective SK selection.

**Cons:**

- Correctness competes with caller SK predicates; hard to guarantee generally.
- Requires strict predicate compatibility checks or many unsupported query paths.
- Increases risk of runtime mismatches and ambiguous behavior.
- If caller SK predicates do not imply the discriminator keyspace, provider must reject or risk
  returning unmappable items.

## Decision

We will use **Option A: dedicated discriminator attribute** for shared-table discrimination.

The provider default discriminator attribute name is `$type`, it is configurable, and the default
name is treated as provider-reserved.

## Rationale

Option A best satisfies correctness and predictability. It gives a clear, provider-controlled
contract: shared-table typed queries are always discriminator-filtered, independent of caller SK
conditions. This avoids making correctness contingent on sort-key predicate composition and avoids
complex query-shape-specific SK logic.

This choice accepts DynamoDB's cost trade-off: non-key discriminator filtering does not lower read
capacity for items already read/evaluated. We accept that for this phase to keep typed semantics
robust under arbitrary caller SK predicates.

Option B remains attractive for pure key-encoding designs, but as a sole discrimination mechanism it
pushes too much complexity and ambiguity into translation and runtime validation. For this provider
phase, we prioritize robust typed semantics over SK-only discrimination.

As follow-on optimization work, the provider may validate caller SK predicates against known
entity keyspace conventions and emit diagnostics when predicates are likely type-incompatible. This
does not change correctness enforcement, which remains discriminator-attribute-based.

## Consequences

**Positive:**

- Shared-table `DbSet<T>` queries have a consistent and enforceable type-safety contract.
- Query translation remains simpler and easier to reason about.
- Model validation can detect discriminator misconfiguration early.

**Negative / Trade-offs:**

- Item shape includes an extra discriminator attribute.
- Shared-table mappings require discriminator configuration and value management.
- Non-key discriminator filtering does not reduce read capacity for items already read/evaluated.

**Neutral / Follow-on work:**

- Add discriminator metadata annotations and fluent APIs.
- Ensure write paths always set discriminator values for inserted/updated items (when persistence is
  added in a later phase).
- Add per-table-group discriminator validation in model validation.
- Inject discriminator predicates in root entity query translation.
- Add materialization guard behavior for missing/unknown/mismatched discriminator values (default:
  throw).
- Clarify inheritance behavior when introduced: querying `DbSet<Base>` should map to discriminator
  sets (for example `IN (...)`) for base + derived types.
- Add optional diagnostics for SK keyspace compatibility when model encodings are known.
- Document guidance for efficient shared-table access patterns (partitioning strategy, SK
  segmentation by type, and projection practices).
- For future index support, keep discriminator enforcement as correctness baseline and treat index
  design as the efficiency lever.
- Update docs where current text says discriminator behavior is still pending.

## References

- AWS ExecuteStatement
  API: <https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_ExecuteStatement.html>
- DynamoDB PartiQL
  SELECT: <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.select.html>
- DynamoDB PartiQL
  overview: <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.html>
- DynamoDB Query filter
  expressions: <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/Query.FilterExpression.html>
- Repository docs: `docs/operators.md`, `docs/configuration.md`, `docs/limitations.md`
