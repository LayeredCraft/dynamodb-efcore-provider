---
title: Ordering and Limiting
description: How OrderBy and Take are handled in DynamoDB queries.
---

# Ordering and Limiting

_DynamoDB returns items in partition key order by default; `OrderBy` and `Take` have specific constraints due to how DynamoDB scans and paginates results._

!!! tip "Sample code"

    You can view and run the sample code for this article on GitHub.

## Ordering Results

## Limiting Results (Take)

## Constraints and Caveats

!!! warning

    DynamoDB's `Limit` parameter controls the number of items *evaluated*, not the number returned after filtering. Use `Take` carefully when combined with `Where`.

## See also

- [Pagination](pagination.md)
- [Supported Operators](operators.md)
- [Limitations](../limitations.md)
