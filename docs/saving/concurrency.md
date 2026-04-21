---
title: Optimistic Concurrency
description: How optimistic concurrency tokens work in the DynamoDB EF Core provider.
---

# Optimistic Concurrency

_The provider implements optimistic concurrency by adding a condition expression to write operations that checks the expected token value before committing the change._

!!! tip "Sample code"

    You can view and run the sample code for this article on GitHub.

## Configuring a Concurrency Token

## How Conflicts Are Detected

## Handling DbUpdateConcurrencyException

## See also

- [Transactions](transactions.md)
- [Add, Update, and Delete](add-update-delete.md)
