# ADR-004: Default discriminators for DynamoDB root items

## Status

- Proposed
- **Date:** 2026-05-27
- **Deciders:** EntityFrameworkCore.DynamoDb maintainers
- **Supersedes:** none

______________________________________________________________________

## Context

ADR-001 selected a dedicated discriminator attribute for shared-table discrimination. The current
implementation follows that narrower shape: discriminator metadata is configured while model
conventions are processing a table group, but convention-added discriminator metadata is removed at
model finalization when the final table group contains exactly one concrete type and no hierarchy.

That means a model with one table-mapped root entity does not persist a discriminator attribute by
default. If another CLR type is later mapped to the same table, item shape changes: new data starts
including `$type`, older single-type data does not, and query/materialization behavior becomes more
conditional.

The EF Core Cosmos provider uses a different default. It configures discriminator metadata for every
document root by convention and removes discriminator metadata from non-root embedded document
shapes. Cosmos does **not** add discriminator predicates to every query. It skips the predicate when
there is a single concrete type, discriminator mapping is complete, and no other mapped type in the
same container requires disambiguation.

The DynamoDB provider is not published yet, so breaking model-shape changes are acceptable. This
lets us choose the cleaner long-term contract rather than carrying legacy compatibility switches for
single-root items that were written without `$type`.

DynamoDB-specific constraints still matter:

- DynamoDB tables are schemaless beyond the key schema.
- A discriminator is a regular item attribute, not part of the partition key or sort key.
- A discriminator predicate is a filter predicate, not a key condition. It enforces type semantics
  but does not reduce read capacity for items already selected by the key path.
- Adding discriminator filters to `First*` / `Single*` queries can make otherwise key-only shapes
  unsafe when the filter is evaluated after reading multiple candidate items.
- Users may still encode item kind in PK/SK values for efficient access patterns; provider
  discriminator metadata is a type-safety and materialization contract, not an access-pattern
  replacement.

## Decision Drivers

- Match Cosmos provider semantics where they fit a schemaless document/item store.
- Keep a stable item contract: every provider-managed root item has type metadata from the start.
- Avoid future item-shape changes when another CLR type is added to a table.
- Preserve EF Core typed materialization semantics for inheritance and shared-table mappings.
- Avoid adding non-key discriminator filters when they are not needed for correctness.
- Keep DynamoDB key design responsible for read efficiency.
- Prefer a clean pre-release breaking change over compatibility modes.
- Keep an explicit escape hatch for exact-shape or externally managed tables.

## Options Considered

### Option A: Keep need-based discriminators

Only configure and persist a discriminator when a table group has multiple concrete mapped types or
an inheritance hierarchy.

**Pros:**

- Minimizes item size for single-root tables.
- Avoids adding a provider-managed attribute to exact-shape tables.
- Matches the current implementation.

**Cons:**

- Single-root and shared-table models have different item contracts.
- Adding a second mapped type later changes persisted item shape.
- Provider behavior is harder to explain: some roots have `$type`, some do not.
- Diverges from Cosmos document-root defaults.

### Option B: Write a root type marker without EF discriminator metadata

Persist `$type` for every root item, but do not configure EF discriminator metadata unless the table
group needs discrimination.

**Pros:**

- Avoids most materialization/query-pipeline consequences for single-root models.
- Gives new items a stable provider-managed type marker.
- Lower implementation risk than changing EF discriminator metadata globally.

**Cons:**

- Creates two concepts: a persisted provider type marker and EF discriminator metadata.
- Does not truly match Cosmos semantics.
- Makes query/materialization behavior less coherent when models evolve.
- Requires separate write-path logic outside EF's discriminator conventions.

### Option C: Configure root discriminator metadata by default

Configure discriminator metadata for every table-mapped root entity by convention. Persist the
discriminator on root items. Use discriminator query predicates only when needed for disambiguation
or when mapping is explicitly incomplete.

**Pros:**

- Gives every provider-managed root item the same type metadata contract.
- Aligns with Cosmos document-root behavior.
- Uses EF Core's discriminator metadata consistently for writes, projections, and materialization.
- Avoids a future shape break when another CLR type is mapped to the same table.
- Keeps non-key discriminator filters out of singleton complete-mapping queries.

**Cons:**

- Adds a `$type` attribute to every root item unless explicitly opted out.
- Manual seed/import data must include valid discriminator values.
- Requires broader validation for discriminator attribute collisions and index projection coverage.
- Complete mappings surface materialization errors for reached external/unmapped items with missing
  or unknown discriminator values; `IsComplete(false)` filters them by query predicate instead, and
  `HasNoDiscriminator()` leaves type safety to key design.

### Option D: Configure and filter by discriminator for every query

Configure root discriminator metadata by default and inject a discriminator predicate into every
entity query, including single-root queries.

**Pros:**

- Excludes external or unmapped rows at the server-side filter stage.
- Simplest query rule to describe.

**Cons:**

- Adds a non-key filter to otherwise key-only DynamoDB queries.
- Can make `First*` / `Single*` paths unsafe or unsupported.
- Does not reduce read capacity for items already selected by key conditions.
- Diverges from Cosmos, which skips discriminator predicates when safe.

## Decision

We will use **Option C: configure root discriminator metadata by default**.

Every table-mapped root entity has a discriminator by convention. The default discriminator
attribute name is `$type`, and the default discriminator value is the entity type short name.
Derived concrete types in a hierarchy receive their own discriminator values. Complex properties and
future embedded/non-root document shapes do not receive discriminator metadata.

The provider persists discriminator values for root items by default. Query translation skips a
discriminator predicate only when the queried type has a single possible concrete CLR type, that
concrete type's discriminator mapping is complete, and no other mapped type in the same table group
has a different discriminator value that needs disambiguation. Query translation adds a
discriminator predicate for multiple possible concrete types or when the mapping is explicitly
incomplete. With incomplete mapping (`IsComplete(false)`), unknown or missing discriminator values
are filtered out by the query predicate instead of being materialized.

`HasNoDiscriminator()` remains an explicit opt-out. For a shared table group, opting out on one root
entity opts out the table group. This is an advanced mode: the provider will not inject type-level
predicates, and key design must guarantee that queries cannot return wrong item shapes.

Because the provider has not shipped, there is no legacy compatibility mode for provider-managed
items that lack `$type`. Data written outside the provider must satisfy the finalized model
contract, opt out with `HasNoDiscriminator()`, or intentionally configure incomplete discriminator
mapping with `IsComplete(false)`. Incomplete mapping keeps discriminator predicates on queries so
unknown or missing discriminator values are filtered out; it does not make invalid provider-managed
rows materializable.

## Rationale

Option C gives the cleanest long-term model. DynamoDB and Cosmos are both schemaless stores where a
provider-managed root document/item benefits from a stable type marker. Configuring discriminator
metadata for every root means item shape does not depend on how many CLR types happen to share a
physical table today.

The important DynamoDB difference is query cost. A discriminator attribute is not a key component,
so a discriminator predicate is a filter. It should be present when it protects typed semantics, but
not added mechanically to every single-root query. This mirrors Cosmos' predicate optimization and
preserves DynamoDB key-only query paths where the discriminator adds no correctness value.

We accept the item-size cost and stricter manual-data contract because the provider is still
pre-release. Requiring `$type` now is simpler and safer than introducing compatibility behavior that
would need to be supported long term.

Option A is storage-minimal but makes the provider harder to reason about and delays a breaking item
shape change until users add a second type. Option B reduces implementation risk but creates a
separate type-marker concept outside EF's discriminator system. Option D is too expensive and
harmful for DynamoDB query semantics because it turns type metadata into a universal non-key filter.

## Consequences

**Positive:**

- Root item shape is predictable: provider-managed items include `$type` by default.
- Adding another mapped CLR type to a table does not introduce discriminator shape for the first
  time.
- EF Core discriminator metadata drives writes, projections, and materialization consistently.
- Cosmos parity improves the mental model for schemaless/document providers.
- Query predicates remain cost-aware: discriminators are used for correctness, not as a universal
  filter.

**Negative / Trade-offs:**

- Every root item stores an extra attribute unless the model opts out.
- Manual seed data, import jobs, and non-EF writers must write valid discriminator values for
  provider-managed rows.
- Unknown or missing discriminator values are model violations under complete mapping and should
  fail materialization when reached; incomplete mapping filters them out with discriminator
  predicates instead.
- Explicit secondary-index use must make attributes available for predicate evaluation, result
  projection, and materialization. Non-`All` GSI/LSI use must be rejected or fail clearly when
  required attributes may be unavailable, including discriminator predicates for scalar/DTO
  projections.
- Users with exact item-shape requirements must call `HasNoDiscriminator()` and accept weaker type
  safety.

**Neutral / Follow-on work:**

- Update `DynamoDiscriminatorConvention` so finalization no longer removes convention-added
  discriminators for single-concrete root table groups.
- Keep `HasNoDiscriminator()` behavior explicit and group-wide for shared table groups.
- Update discriminator predicate generation to follow Cosmos-style rules:
  - add predicates for queries with multiple possible concrete types;
  - add predicates when discriminator mapping is incomplete;
  - skip predicates only when the queried type has a single possible concrete type, mapping is
    complete, and no other mapped type in the table group has a different discriminator value that
    needs disambiguation.
- Validate discriminator attribute collisions for every discriminated root, not only shared-table
  groups; collisions must fail model validation:
  - discriminator attribute must not collide with PK/SK attribute names;
  - discriminator attribute must not collide with any other mapped top-level scalar or complex
    attribute.
- Validate duplicate discriminator values within a table group.
- Ensure projections include the discriminator attribute when predicate evaluation or materialization
  needs it, including scalar/DTO projections that still require discriminator predicates.
- Require selected secondary indexes to make all attributes required for predicate evaluation, result
  projection, and materialization available. Explicit non-`All` GSI/LSI use should be rejected or
  fail clearly when coverage cannot be proven.
- Update tests for:
  - single root gets `$type` by convention;
  - single-root insert writes `$type`;
  - single-root query does not add a discriminator predicate under complete mapping when no
    same-table type with a different discriminator value needs disambiguation;
  - incomplete discriminator mapping (`IsComplete(false)`) forces a discriminator predicate and
    filters unknown/missing discriminator values;
  - missing/unknown `$type` fails materialization when reached under complete mapping;
  - shared-table and hierarchy queries still filter by discriminator;
  - explicit non-`All` GSI/LSI use rejects or fails clearly when required predicate, projection, or
    materialization attributes may be unavailable;
  - scalar/DTO projections with discriminator predicates still require secondary-index projection
    coverage;
  - `HasNoDiscriminator()` suppresses write/filter/materialization discriminator behavior;
  - custom discriminator name/value are preserved;
  - duplicate discriminator values fail validation within a table group;
  - collision validation covers PK/SK and mapped top-level scalar/complex attributes.
- Update user-facing docs for discriminator defaults, opt-out, storage cost, manual seed data, and
  index projection guidance.

## References

- [ADR-001: Discriminator strategy for shared DynamoDB tables](./ADR-001-discriminator-strategy-for-shared-tables.md)
- [EF Core Cosmos discriminator convention](https://github.com/dotnet/efcore/blob/main/src/EFCore.Cosmos/Metadata/Conventions/CosmosDiscriminatorConvention.cs)
- [EF Core Cosmos discriminator predicate optimization](https://github.com/dotnet/efcore/blob/main/src/EFCore.Cosmos/Query/Internal/CosmosQueryableMethodTranslatingExpressionVisitor.cs)
- Current DynamoDB discriminator convention:
  `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoDiscriminatorConvention.cs`
- Current DynamoDB query discriminator predicate generation:
  `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryableMethodTranslatingExpressionVisitor.cs`
- DynamoDB filter expressions:
  <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/Query.FilterExpression.html>
- DynamoDB PartiQL `SELECT`:
  <https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.select.html>
