---
title: Querying
description: Overview of how LINQ queries are translated to PartiQL and executed against DynamoDB.
icon: lucide/search
---

# Querying

The DynamoDB EF Core provider translates LINQ expressions into PartiQL statements and executes them via the DynamoDB `ExecuteStatement` API.

This section covers how queries are executed, which LINQ operators are supported, and how DynamoDB-specific behaviors like pagination and index selection work.

- [How Queries Execute](how-queries-execute.md) — The LINQ-to-PartiQL translation pipeline and execution model.
- [Supported Operators](operators.md) — Reference table of supported LINQ operators and their PartiQL equivalents.
- [Filtering](filtering.md) — How `Where` clauses translate to PartiQL filter expressions.
- [Projection](projection.md) — How `Select` expressions are evaluated server-side vs client-side.
- [Ordering and Limiting](ordering-limiting.md) — How `OrderBy` and `Take` are handled.
- [Pagination](pagination.md) — Continuation tokens and the `ToPageAsync` pattern.
- [Index Selection](index-selection.md) — Targeting a specific GSI or LSI in a query.

## See also

- [Data Modeling](../modeling/index.md)
- [Saving Data](../saving/index.md)
- [Limitations](../limitations.md)
