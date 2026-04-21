---
title: Filtering
description: How Where clauses are translated to PartiQL filter expressions.
---

# Filtering

_`Where` clauses are translated to PartiQL `WHERE` conditions and evaluated by DynamoDB; the provider validates that the predicate shape is supported before executing the query._

!!! tip "Sample code"

    You can view and run the sample code for this article on GitHub.

## Basic Where Clauses

## Supported Predicates

## Key Conditions vs Filter Expressions

!!! note

    DynamoDB distinguishes between key conditions (on partition/sort key) and filter expressions (on other attributes). Filter expressions are applied after items are read and do not reduce read capacity consumption.

## See also

- [Supported Operators](operators.md)
- [Index Selection](index-selection.md)
- [Limitations](../limitations.md)
