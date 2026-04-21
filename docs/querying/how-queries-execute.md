---
title: How Queries Execute
description: How LINQ expressions are translated to PartiQL and executed via the DynamoDB ExecuteStatement API.
---

# How Queries Execute

_The provider compiles LINQ expressions into PartiQL `SELECT` statements and executes them using DynamoDB's `ExecuteStatement` API, with any unsupported operators evaluated client-side after results are returned._

!!! tip "Sample code"

    You can view and run the sample code for this article on GitHub.

## LINQ to PartiQL

## The ExecuteStatement Model

## Client-Side vs Server-Side Evaluation

## See also

- [Supported Operators](operators.md)
- [Filtering](filtering.md)
- [Limitations](../limitations.md)
