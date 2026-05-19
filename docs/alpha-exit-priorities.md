---
title: Alpha Exit Priorities
description: Recommended priorities and release rubric for taking the DynamoDB EF Core provider out of alpha.
---

# Alpha Exit Priorities

## Product stance

This provider should feel **DynamoDB-native**, not like a relational provider awkwardly pointed at DynamoDB.

That implies:

- embedded data should be modeled as **document/value structure**
- cross-root relationships should **not** imply joins or relational loading semantics
- DynamoDB-specific safety and operational concerns should be first-class:
  - accidental scans
  - consistency configuration
  - diagnosability
  - correctness of write/concurrency behavior

## Recommended v1 modeling policy

### Embedded data: Complex Types = yes

Use complex types for inline document structure:

- `ComplexProperty(...)`
- `ComplexCollection(...)` or equivalent complex collection support when feasible

These are the best conceptual fit for DynamoDB documents.

### Embedded data: OwnsOne / OwnsMany = no for the long-term surface

If the provider chooses the complex-type direction, then `OwnsOne` / `OwnsMany` should not remain the preferred embedded modeling surface.

Recommended options:

- remove support before 1.0, or
- keep support temporarily but mark it transitional and de-emphasized

Why this matters:

- the current docs present owned types as the nested document model
- if that is the wrong abstraction, it should be corrected while still in alpha
- waiting increases the cost of changing docs, examples, tests, and user expectations

### Root relationships: HasOne / HasMany = maybe, but limited

`HasOne` / `HasMany` can still make sense for separate root entities, but not as a promise of relational behavior.

Supported meaning:

- model metadata for separate root entities
- possible future helper patterns for manual relationship handling

Not implied:

- joins
- relational foreign key loading
- cross-root `.Include()`

## Clean v1 modeling policy

- **Complex types:** yes, for embedded document/value data
- **Owned types:** no, or transitional only
- **HasOne/HasMany:** allowed only if clearly framed as non-relational root relationships
- **Include across roots:** no

---

# Priority List

## P0 - Must tackle before taking out of alpha

### #168 - Evaluate switching embedded document support from owned entities to complex types

**Why:** This is a product-identity and architecture decision, not just a feature request. It affects modeling, materialization, writes, documentation, and examples.

**Priority rationale:** If embedded modeling is going to be complex-type-centric, that needs to be locked in while the provider is still alpha.

**Issue:** #168

### #170 - ConditionalCheckFailedException incorrectly maps to DbUpdateConcurrencyException when item does not exist

**Why:** This is a correctness bug. False concurrency exceptions are not acceptable for an alpha-exit bar.

**Priority rationale:** A provider leaving alpha must get fundamental error semantics right.

**Issue:** #170

### #174 - Add EF Core specification test suite (FunctionalTests project)

**Why:** The provider needs much stronger regression protection before leaving alpha.

**Priority rationale:** Handwritten tests alone are not enough for confidence in a provider surface that tracks EF Core behavior over time.

**Issue:** #174

### #51 - Scan behavior configuration

**Why:** DynamoDB's biggest footgun is accidental scans. Users need explicit policy control.

**Priority rationale:** Scan safety is a DynamoDB-native concern and should be first-class before alpha exit.

**Issue:** #51

### #71 - Query vs scan detection

**Why:** Scan policy is only credible if the provider can actually detect scan-like query shapes.

**Priority rationale:** This is core safety functionality for a DynamoDB provider.

**Issue:** #71

### #179 - Production-grade diagnostics and observability for the DynamoDB EF Core provider

**Why:** Not every observability feature must ship before alpha exit, but the provider needs a minimum supportability floor.

**Minimum expected slice before alpha exit:**

- enough diagnostics to understand query behavior
- enough diagnostics to understand write behavior
- enough inspection support to troubleshoot generated operations
- enough warning/error surface to make runtime behavior explainable

**Priority rationale:** A provider cannot credibly leave alpha if common failures are hard to diagnose.

**Issue:** #179

## P1 - Very important, should come soon after alpha exit blockers

### #178 - Integrate provider warnings with ConfigureWarnings

**Why:** Important operational polish; makes the provider behave more like a mature EF provider.

**Issue:** #178

### #171 - Consistent read preference

**Why:** This is a real DynamoDB-native capability and currently a documented gap.

**Issue:** #171

### #130 - Add Count/LongCount support via low-level DynamoDB Query/Scan

**Why:** This is a meaningful query capability gap for everyday usage.

**Issue:** #130

### #136 - Add configurable concurrency token auto-update hooks

**Why:** Important for a polished concurrency story in document workloads.

**Issue:** #136

### #175 - Support FindAsync for single-item retrieval by primary key

**Why:** Good EF ergonomics and a very natural DynamoDB access path.

**Issue:** #175

### #129 - Design explicit key-scoped discriminator safety for First* on shared-table queries

**Why:** Important for single-table correctness and usability, though current conservative behavior is safer than incorrect behavior.

**Issue:** #129

### #77 - Timeout configuration

**Why:** Important for operational hardening and guidance, even if it remains largely documentation/audit focused.

**Issue:** #77

## P2 - Nice to have

### #88 - C# Records and Init-Only Properties

**Issue:** #88

### #173 - Emit LIST_APPEND / SET_ADD / SET_DELETE for primitive list and set properties on update

**Issue:** #173

### #183 - CLR attribute-based model configuration for table and key mapping

**Issue:** #183

### #159 - Investigate PK/SK key identification with naming conventions

**Issue:** #159

### #64 - Define DynamoDB event IDs, logger category, and EventData classes

**Issue:** #64

### #65 - Structured logging definitions

**Issue:** #65

### #66 - Interceptors

**Issue:** #66

### #67 - OpenTelemetry tracing

**Issue:** #67

### #68 - Metrics

**Issue:** #68

### #69 - Health checks

**Issue:** #69

### #90 - Provider options diagnostics (LogFragment)

**Issue:** #90

### #58 - Sort-Key Prefix Discriminator

**Why:** Very DynamoDB-native, but specialized enough to defer.

**Issue:** #58

### #57 - PK/SK template key generation

**Why:** Useful single-table DX, but not required for a credible alpha exit.

**Issue:** #57

### #48 - Dynamic attributes (Dictionary<string, object>)

**Issue:** #48

### #25 - Implement SIZE/ATTRIBUTE_TYPE support

**Issue:** #25

## P3 - Low priority / does not matter much for alpha exit

### #182 - Rename DynamoAutomaticIndexSelectionMode.Conservative and default auto-selection to On

**Issue:** #182

### #177 - Low-level DynamoDB API access from DbContext

**Issue:** #177

### #176 - FromPartiQL escape hatch for raw PartiQL SELECT queries

**Issue:** #176

### #166 - Zero-copy binary reads

**Issue:** #166

### #148 - ExecuteDelete support

**Issue:** #148

### #147 - ExecuteUpdate support

**Issue:** #147

### #74 - PartiQL command template caching

**Issue:** #74

---

# Release Rubric

## Must before beta

These are the items that should be addressed before the provider is taken out of alpha / treated as beta-quality.

### Required

- #168 - embedded modeling direction is explicitly decided and documented
- #170 - concurrency exception mapping correctness bug is fixed
- #174 - specification/regression test foundation is in place
- #51 - scan behavior policy exists
- #71 - scan-like query detection exists
- core slice of #179 - minimum viable diagnostics/supportability exists

### Beta bar summary

Before beta, the provider should:

- have a clear DynamoDB-native modeling story
- prevent or clearly surface dangerous query shapes
- have correct core write/concurrency semantics
- be diagnosable in normal development and troubleshooting workflows
- have a stronger regression safety net than ad hoc tests alone

## Must before 1.0

These should be strongly considered part of the 1.0 quality bar, even if not all are required for alpha exit.

- #178 - `ConfigureWarnings` integration
- #171 - consistent read preference
- #130 - `Count` / `LongCount`
- #136 - concurrency token auto-update hooks
- #175 - `FindAsync`
- #129 - clearer safe-path rules for shared-table `First*`
- #77 - timeout/cancellation hardening and docs

### 1.0 bar summary

Before 1.0, the provider should:

- feel operationally mature
- cover core day-to-day EF usage patterns
- expose important DynamoDB capabilities intentionally
- reduce major paper-cut gaps in query and concurrency workflows

## Can stay backlog indefinitely

These items may be worth doing, but they should not block the provider from becoming stable if the rest of the platform is solid.

- #182
- #177
- #176
- #166
- #148
- #147
- #74

Potentially also backlog-able depending on adoption pressure:

- #183
- #159
- #88
- #173
- #58
- #57
- #48
- #25

---

# Recommended Alpha Exit Gate

If setting a simple decision rule, use this:

## Required for alpha exit

- #168
- #170
- #174
- #51
- #71
- core slice of #179

## Strongly recommended immediately after

- #178
- #171
- #130
- #136

---

# Final Recommendation

Do not leave alpha with the embedded-modeling direction unresolved.

If the real product vision is:

- **complex types for embedded documents**
- **non-relational root relationship semantics**
- **DynamoDB-native query safety and diagnostics**

then the provider should say that explicitly and align the implementation/documentation around it before calling itself more than alpha.
