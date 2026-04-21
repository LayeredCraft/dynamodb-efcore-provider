---
title: DynamoDB Concepts for EF Developers
description: Key DynamoDB concepts that affect how you model and query data with the EF Core provider.
icon: lucide/book-open
---

# DynamoDB Concepts for EF Developers

_DynamoDB is a key-value and document database with a fundamentally different access model from relational databases — understanding its core concepts is essential before mapping your EF Core model._

## Partition Keys and Sort Keys

## Single-Table Design

## Items and Attributes

## No Joins

## Async-Only API

!!! warning

    The DynamoDB SDK does not support synchronous I/O. All operations in this provider are async-only.

## How This Affects Your EF Core Model

## See also

- [Getting Started](getting-started.md)
- [Entities and Keys](modeling/entities-keys.md)
- [How Queries Execute](querying/how-queries-execute.md)
