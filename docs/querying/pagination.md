---
title: Pagination
description: How pagination works using continuation tokens and the ToPageAsync pattern.
---

# Pagination

_The provider exposes DynamoDB's continuation token model through `ToPageAsync` and `WithNextToken`, allowing you to fetch pages of results without loading the full result set._

!!! tip "Sample code"

    You can view and run the sample code for this article on GitHub.

## Continuation Tokens

## ToPageAsync

## WithNextToken

## Page Size vs Result Limit

!!! note

    DynamoDB evaluates up to `Limit` items before applying filter expressions. The number of items returned in a page may be less than the page size when filters are in use.

## See also

- [Ordering and Limiting](ordering-limiting.md)
- [How Queries Execute](how-queries-execute.md)
