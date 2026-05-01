---
title: Index Selection
description: How to target a specific GSI or LSI in a query, and how automatic index selection works.
---

# Index Selection

_Queries can target a Global or Local Secondary Index explicitly using index hints, or the provider can select an appropriate index automatically based on the query shape._

!!! tip "Sample code"

    You can view and run the sample code for this article on GitHub.

## Explicit Index Hints

Use `.WithIndex("name")` to target a specific index:

```csharp
var pending = await context.Orders
    .WithIndex("ByStatus")
    .Where(x => x.Status == "PENDING")
    .ToListAsync();
```

The provider emits `FROM "Table"."Index"` in generated PartiQL when an index is selected.

Rules and behavior:

- `.WithIndex(...)` requires a non-empty index name.
- The named index must exist on the mapped table; otherwise translation throws `InvalidOperationException`.
- Explicit selection works regardless of automatic selection mode (`Off`, `SuggestOnly`, `On`).
- Explicit selection can target `All`, `KeysOnly`, or `Include` projection indexes.
- For explicit `.WithIndex(...)`, the provider does not pre-validate projection coverage.
- On non-`All` indexes, missing required attributes throw during materialization; missing optional
    attributes can materialize as `null` / CLR default.

## Automatic Index Selection

Automatic selection is `On` by default. Configure it via `UseAutomaticIndexSelection(...)`:

- `Off`: no automatic routing.
- `SuggestOnly`: analyze and emit diagnostics, but keep base-table execution.
- `On` (default): auto-route only when exactly one safe candidate is found.

When automatic selection is `On`, an index candidate is eligible only if all gates pass:

1. The `WHERE` clause includes equality or `IN` on the index partition key.
1. The predicate does not contain an unsafe `OR` shape.
1. The index projection type is `All`.

If multiple candidates tie, the provider falls back to the base table and emits a tie diagnostic.

Scoring used to break non-tied candidates:

- `+1` when the index sort key is constrained as a key condition.
- `+1` when query ordering aligns with the index sort key.

Diagnostics emitted during analysis:

- `DYNAMO_IDX001`: no compatible index found.
- `DYNAMO_IDX002`: multiple compatible indexes tied.
- `DYNAMO_IDX003`: index selected (or would be auto-selected in `SuggestOnly`).
- `DYNAMO_IDX004`: index explicitly selected via `.WithIndex(...)`.
- `DYNAMO_IDX005`: candidate rejected with reason.

## When No Index Is Used

Use `.WithoutIndex()` to force base-table execution and suppress both explicit and automatic
selection:

```csharp
var orders = await context.Orders
    .WithoutIndex()
    .Where(x => x.CustomerId == customerId)
    .ToListAsync();
```

Behavior:

- Emits `DYNAMO_IDX006` to record that selection was explicitly suppressed.
- Combining `.WithIndex(...)` and `.WithoutIndex()` on the same query throws `InvalidOperationException`.
- When no index is selected (or selection is suppressed), generated PartiQL reads from the base table.

## See also

- [Secondary Indexes](../modeling/secondary-indexes.md)
- [Filtering](filtering.md)
