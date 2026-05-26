# ADR-003: EF key semantics for DynamoDB keys

## Status

- Proposed
- **Date:** 2026-05-24
- **Deciders:** EntityFrameworkCore.DynamoDb maintainers
- **Supersedes:** none

______________________________________________________________________

## Context

The current provider treats DynamoDB key annotations as the source of truth:

- `HasPartitionKey(...)` stores the partition-key property annotation.
- `HasSortKey(...)` stores the sort-key property annotation.
- Conventions rebuild the EF primary key as `[partitionKey]` or
  `[partitionKey, sortKey]` when possible.
- Runtime write, query, and table-generation code resolves DynamoDB keys from those annotations.
- Validators reject explicit EF primary keys for root DynamoDB entities.

This works, but creates two identity concepts: the EF primary key and the DynamoDB key annotations.
Users coming from EF expect `HasKey(...)` and key discovery to participate in entity identity.
DynamoDB still allows only one partition key and optional sort key, so the provider can support
normal EF key semantics while still enforcing DynamoDB's key shape.

The desired direction is not to replace the provider APIs. Both configuration styles are
first-class and equivalent after model finalization: runtime behavior must depend on one finalized
resolved key model, not on which API the user called. `HasPartitionKey(...)` and
`HasSortKey(...)` remain the preferred DynamoDB-facing configuration style because they express
DynamoDB intent directly. The provider should also support regular EF `HasKey(...)` for users who
prefer EF-native modeling.

The provider is not published yet, so breaking changes are acceptable.

## Decision Drivers

- Keep `HasPartitionKey(...)` and `HasSortKey(...)` as the preferred public API for DynamoDB
  modeling.
- Do not require `HasKey(...)` when provider key APIs are used.
- Fully support regular EF `HasKey(...)` for one- and two-part keys.
- Avoid long-term mismatch between EF primary key and DynamoDB key metadata.
- Preserve DynamoDB constraints: partition key plus optional sort key, never more than two
  properties.
- Make validation errors precise when EF key configuration and DynamoDB key annotations conflict.
- Keep runtime write, query, and table-definition code working from one resolved key shape.

## Options Considered

### Option A: Keep annotation-first semantics

Continue treating `HasPartitionKey(...)` and `HasSortKey(...)` annotations as the primary source
of truth, with EF primary keys rebuilt from those annotations when possible.

**Pros:**

- Matches the current implementation.
- Keeps the DynamoDB-specific API dominant.
- Minimizes immediate code churn.

**Cons:**

- `HasKey(...)` remains second-class and surprising for EF users.
- Maintains dual identity sources.
- Requires conventions and validators to keep EF keys synchronized after the fact.
- Makes composite EF key semantics less intuitive.

### Option B: Make EF primary key the only source of truth

Treat `HasKey(...)` as the canonical configuration. Infer DynamoDB partition/sort keys exclusively
from EF primary key order. Convert `HasPartitionKey(...)` and `HasSortKey(...)` into optional
validation hints or de-emphasize them.

**Pros:**

- Most EF-native mental model.
- Simple rule: one key property is partition key; two key properties are partition plus sort key.
- Reduces provider-specific identity machinery.

**Cons:**

- Makes provider-specific APIs feel less important even though they are clearer for DynamoDB users.
- Could make `HasKey(...)` appear required.
- Risks breaking or confusing users who expect `HasPartitionKey(...)` / `HasSortKey(...)` to be
  enough.
- EF key property order becomes store-significant without an explicit DynamoDB API call.

### Option C: Dual-entry configuration with one resolved key model

Support both configuration styles:

- preferred DynamoDB style: `HasPartitionKey(...)` and optional `HasSortKey(...)`;
- EF-native style: regular `HasKey(...)` with one or two properties.

When provider key APIs are used without explicit `HasKey(...)`, synthesize the EF primary key.
When `HasKey(...)` is used without provider key APIs, infer DynamoDB key roles from EF key order.
When both are used, validate exact agreement.

**Pros:**

- Keeps provider-specific APIs preferred.
- Does not require `HasKey(...)` for DynamoDB users.
- Makes regular EF `HasKey(...)` fully supported.
- Gives runtime code one resolved key shape.
- Avoids silent mismatch between EF identity and DynamoDB identity.
- Aligns better with EF expectations while preserving DynamoDB vocabulary.

**Cons:**

- More convention and validation complexity than a single-source model.
- Must carefully define precedence when both styles are present.
- Needs strong documentation because both styles are valid.
- Existing tests and docs need updates.

## Decision

We will use **Option C: dual-entry configuration with one resolved key model**.

`HasPartitionKey(...)` and `HasSortKey(...)` remain the preferred DynamoDB-facing APIs and do not
require `HasKey(...)`. "Preferred" is documentation and ergonomics guidance only: after model
finalization, provider-key APIs and EF-key APIs are semantically equivalent. Regular EF
`HasKey(...)` is also supported for one- and two-part keys. If both styles are configured, they
must describe the same key shape and order.

### Public API shape

Preferred provider-specific configuration:

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.HasPartitionKey(e => e.TenantId);
    b.HasSortKey(e => e.OrderId);
});
```

This configures:

- DynamoDB partition key: `TenantId`
- DynamoDB sort key: `OrderId`
- EF primary key synthesized as `[TenantId, OrderId]` if no explicit `HasKey(...)` exists

Provider-specific partition-key-only configuration:

```csharp
modelBuilder.Entity<Order>().HasPartitionKey(e => e.Id);
```

This configures:

- DynamoDB partition key: `Id`
- no sort key
- EF primary key synthesized as `[Id]` if no explicit `HasKey(...)` exists

EF-native configuration is also supported:

```csharp
modelBuilder.Entity<Order>().HasKey(e => e.Id);
```

This configures:

- EF primary key: `[Id]`
- DynamoDB partition key: `Id`
- no sort key

```csharp
modelBuilder.Entity<Order>().HasKey(e => new { e.TenantId, e.OrderId });
```

This configures:

- EF primary key: `[TenantId, OrderId]`
- DynamoDB partition key: `TenantId`
- DynamoDB sort key: `OrderId`

Combined configuration is valid when both styles agree:

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.HasKey(e => new { e.TenantId, e.OrderId });
    b.HasPartitionKey(e => e.TenantId);
    b.HasSortKey(e => e.OrderId);
});
```

`HasKey(...)` is optional when `HasPartitionKey(...)` / `HasSortKey(...)` are present. If both
styles are present, provider validation checks that they match key order.

The following shapes are invalid:

```csharp
modelBuilder.Entity<Order>().HasKey(e => new { e.TenantId, e.OrderId, e.LineId });
```

DynamoDB tables support at most partition key plus sort key.

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.HasKey(e => new { e.Id, e.OrderId });
    b.HasPartitionKey(e => e.OrderId);
});
```

The partition-key configuration/annotation does not match the first EF key property.

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.HasKey(e => e.Id);
    b.HasSortKey(e => e.OrderId);
});
```

The explicit one-part EF key conflicts with the configured DynamoDB sort key.

### Table key resolution scope

Only the EF primary key and provider partition/sort-key annotations participate in DynamoDB table
key resolution.

- Alternate keys configured with `HasAlternateKey(...)` do not participate in table key resolution.
- EF indexes and unique indexes do not participate in table key resolution.
- DynamoDB secondary indexes remain configured through provider secondary-index APIs.
- Shadow and runtime-only properties cannot be DynamoDB table keys. If an EF primary key used for
  inference contains a shadow or runtime-only property, validation fails.
- Complex/embedded properties do not participate in table key resolution; table keys must be
  top-level scalar properties.

### Inference rules

During model finalization:

1. If `HasPartitionKey(...)` and optional `HasSortKey(...)` are configured and there is no primary
   key, or only a convention-created primary key, synthesize the EF primary key from the resolved
   DynamoDB key roles. Synthesis requires a resolved partition key; a sort key alone is invalid.
2. If an explicit or data-annotation EF primary key exists and no DynamoDB key annotations exist,
   infer DynamoDB key roles from EF key order.
3. If both explicit or data-annotation EF primary key and DynamoDB annotations exist, validate that
   they match exactly.
4. If an EF primary key exists:
   - the first key property maps to the partition key;
   - the second key property maps to the sort key;
   - no third key property is allowed.
5. Runtime code consumes only resolved key roles after finalization. It must not re-infer roles
   from raw annotations or primary-key order.

"Explicit EF primary key" means a key configured with `HasKey(...)`. A data-annotation key means
key metadata sourced from EF data annotations. A convention-created key means EF discovered the key
from naming conventions such as `Id`, `PK`, `PartitionKey`, `SK`, or `SortKey`. Provider key APIs
may replace convention-created keys, but should not silently rewrite explicit or data-annotation
keys.

### Resolution phases

Implementation should keep three conceptual phases separate:

1. **Raw configuration capture** stores EF primary-key metadata, provider key annotations, and
   convention-discovered key hints with configuration-source provenance.
2. **Resolution** runs in a provider model-finalizing convention after EF key discovery and provider
   key annotation conventions. It produces one resolved table key shape containing the partition
   property, optional sort property, source provenance, and finalized ordering.
3. **Runtime consumption** persists resolved key roles in runtime metadata, such as runtime
   annotations or the runtime table model. Query, write, and table-creation paths consume only that
   finalized runtime metadata.

Replacement of convention-created EF primary keys should happen only during resolution. The
resolver should compute the complete desired key shape first, replace an existing convention key
only when the desired shape is complete, and then let validation observe the finalized model.
Explicit and data-annotation primary keys must not be removed, reordered, or silently rewritten.

### Validation rules

For every root DynamoDB table entity:

- Must have an EF primary key or provider key annotations from which one can be synthesized.
- Final EF primary key must contain one or two properties.
- First EF key property must map to a DynamoDB key-compatible scalar attribute type: string (`S`),
  number (`N`, including supported CLR numeric types), or binary (`B`).
- Second EF key property, if present, must map to a DynamoDB key-compatible scalar attribute type:
  string (`S`), number (`N`, including supported CLR numeric types), or binary (`B`).
- Key properties must be required / non-nullable for DynamoDB key purposes.
- Key properties must be mapped CLR properties, not shadow or runtime-only properties.
- Alternate keys and EF indexes/unique indexes do not affect DynamoDB table key resolution.
- If `HasPartitionKey(...)` is configured, the property must equal the first EF key property after
  synthesis/inference.
- If `HasSortKey(...)` is configured, the property must equal the second EF key property after
  synthesis/inference.
- `HasSortKey(...)` without explicit `HasKey(...)` is valid only when a partition key is also
  resolved/configured; provider then synthesizes a two-part EF primary key from partition/sort key
  annotations.
- `HasSortKey(...)` with explicit one-part `HasKey(...)` is invalid because the explicit EF key
  conflicts with the DynamoDB sort key.
- Shared-table mappings must agree on partition/sort key attribute names and key type categories.
- Complex/embedded types are excluded from DynamoDB table key validation because they are stored
  inside a root document/item rather than as independent table items.
- Owned entity types remain governed by the provider's existing owned-type support policy; this ADR
  does not change owned-type support.
- Derived entity types use the root table entity's resolved key shape; inheritance mappings must
  not introduce a conflicting table key.

Shared-table mappings are valid when the physical DynamoDB table key schema matches even if CLR
property names differ:

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.ToTable("AppTable");
    b.Property(e => e.TenantId).HasAttributeName("PK");
    b.Property(e => e.OrderId).HasAttributeName("SK");
    b.HasPartitionKey(e => e.TenantId);
    b.HasSortKey(e => e.OrderId);
});

modelBuilder.Entity<User>(b =>
{
    b.ToTable("AppTable");
    b.Property(e => e.AccountId).HasAttributeName("PK");
    b.Property(e => e.UserId).HasAttributeName("SK");
    b.HasPartitionKey(e => e.AccountId);
    b.HasSortKey(e => e.UserId);
});
```

Shared-table mappings are invalid when any root type uses a different physical key schema:

```csharp
modelBuilder.Entity<Order>(b =>
{
    b.ToTable("AppTable");
    b.Property(e => e.TenantId).HasAttributeName("PK");
    b.Property(e => e.OrderId).HasAttributeName("SK");
    b.HasPartitionKey(e => e.TenantId);
    b.HasSortKey(e => e.OrderId);
});

modelBuilder.Entity<User>(b =>
{
    b.ToTable("AppTable");
    b.Property(e => e.AccountId).HasAttributeName("AccountId");
    b.HasPartitionKey(e => e.AccountId);
});
```

The invalid mapping mixes PK+SK and PK-only shapes and uses a different partition-key attribute
name for the same physical table.

### Expected codebase impact

Metadata and conventions likely affected:

- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoKeyDiscoveryConvention.cs`
- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoKeyAnnotationConvention.cs`
- `src/EntityFrameworkCore.DynamoDb/Metadata/Conventions/DynamoKeyInPrimaryKeyConvention.cs`
- `src/EntityFrameworkCore.DynamoDb/Extensions/DynamoEntityTypeExtensions.cs`
- `src/EntityFrameworkCore.DynamoDb/Extensions/DynamoEntityTypeBuilderExtensions.cs`
- `src/EntityFrameworkCore.DynamoDb/Infrastructure/Internal/DynamoModelValidator.cs`

Expected changes:

- Keep conventional key discovery for `PK`, `PartitionKey`, fallback `Id`, `SK`, and `SortKey`.
- Introduce an internal resolved table key descriptor (name TBD, for example
  `DynamoResolvedKeyModel`) that captures partition property, optional sort property, source
  provenance, and finalized order.
- Synthesize EF primary keys from provider key annotations when no explicit EF key exists.
- Infer resolved DynamoDB key roles from explicit EF primary key configuration.
- Validate conflicts when both configuration styles are present.
- Validate that alternate keys, EF indexes/unique indexes, shadow properties, and runtime-only
  properties do not become table key sources.
- Avoid silently rewriting explicit EF primary keys.

Runtime model likely affected:

- `src/EntityFrameworkCore.DynamoDb/Infrastructure/Internal/DynamoModelRuntimeInitializer.cs`
- `src/EntityFrameworkCore.DynamoDb/Metadata/Internal/DynamoRuntimeTableModel.cs`
- `src/EntityFrameworkCore.DynamoDb/Storage/Internal/DynamoTableDefinitionBuilder.cs`

Expected changes:

- Runtime model persists finalized partition/sort key roles instead of dynamically recomputing
  them from raw annotations or EF key order.
- Table definition builder still emits DynamoDB `HASH` and optional `RANGE` key schema.

Writes likely affected:

- `src/EntityFrameworkCore.DynamoDb/Storage/DynamoPartiqlStatementFactory.cs`
- `src/EntityFrameworkCore.DynamoDb/Storage/DynamoTransactionTargetIdentityFactory.cs`

Expected changes:

- Continue producing `WHERE partitionKey = ?` and optional `AND sortKey = ?`.
- Resolve key roles from finalized key metadata.
- Preserve original-value usage for updates/deletes.

Queries likely affected:

- `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoSqlTranslatingExpressionVisitor.cs`
- `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQueryTranslationPostprocessor.cs`
- `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoConstraintExtractionVisitor.cs`
- `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoAutoIndexSelectionAnalyzer.cs`
- `src/EntityFrameworkCore.DynamoDb/Query/Internal/DynamoQuerySqlGenerator.cs`

Expected changes:

- Effective partition key and sort key come from resolved key roles.
- Query classification rules remain conceptually unchanged:
  - partition key equality or `IN` enables keyed query path;
  - sort-key predicates become key conditions only when valid;
  - invalid key `OR` patterns remain scan-like or rejected according to current policy.

Tests likely affected:

- model-building tests for `HasKey`, `HasPartitionKey`, and `HasSortKey` interactions;
- validation tests for more than two key properties and mismatched hints;
- write statement tests verifying `WHERE` key predicates;
- query translation tests verifying key-condition recognition;
- shared-table validation tests;
- docs examples.

## Rationale

Option C best satisfies the decision drivers. It preserves the DynamoDB-specific API as the
recommended modeling surface while making normal EF key configuration work for users who expect it.

The preferred API remains explicit about DynamoDB concepts:

- partition key;
- optional sort key;
- key order and store behavior.

At the same time, users who configure models using normal EF APIs can do so naturally. A one-part
EF primary key maps to a DynamoDB partition key. A two-part EF primary key maps to partition key
plus sort key. More than two properties is invalid because DynamoDB cannot represent that key
shape.

The important design point is that the finalized model has one resolved key shape. Runtime code
should not have to guess between EF primary key and DynamoDB annotations. Conventions can
synthesize missing metadata, and validators can reject conflicting metadata.

This choice accepts more convention and validation complexity to avoid forcing either modeling
style on all users.

## Consequences

**Positive:**

- `HasPartitionKey(...)` and `HasSortKey(...)` remain preferred and sufficient.
- Regular EF `HasKey(...)` behaves like users expect.
- Composite EF keys map naturally to DynamoDB partition/sort key.
- Provider has one resolved key model for runtime behavior.
- Invalid key shapes are easy to explain: DynamoDB supports only one or two key properties.
- The provider aligns better with EF patterns while preserving DynamoDB vocabulary.

**Negative / Trade-offs:**

- Convention and validation logic becomes more nuanced.
- EF key property order becomes store-significant when using `HasKey(...)` alone.
- Existing tests and docs need broad updates.
- Shared-table and inheritance validation may become more subtle if different CLR types map to the
  same table.
- Users who configure conflicting key styles must fix their model instead of relying on provider
  annotations to silently win.

**Neutral / Follow-on work:**

- Decide the exact storage shape/name for persisted resolved key roles, such as runtime annotations
  versus extending `DynamoRuntimeTableModel`; runtime should not recompute roles dynamically.
- Define exact error messages for mismatched explicit role configuration/annotations.
- Update docs to present `HasPartitionKey(...)` / `HasSortKey(...)` first, with `HasKey(...)` as a
  supported EF-native alternative.
- Add migration guidance before publishing or before the behavior change ships.

## References

- [ADR-001: Discriminator strategy for shared tables](./ADR-001-discriminator-strategy-for-shared-tables.md)
- [ADR-002: Row limiting and paging semantics for DynamoDB queries](./ADR-002-pagination-model-options.md)
- EF Core Cosmos provider prior art in the EF Core repository:
  - `src/EFCore.Cosmos/Metadata/Conventions/CosmosKeyDiscoveryConvention.cs`
  - `src/EFCore.Cosmos/Metadata/Conventions/CosmosPartitionKeyInPrimaryKeyConvention.cs`
  - `src/EFCore.Cosmos/Metadata/Conventions/CosmosJsonIdConvention.cs`
