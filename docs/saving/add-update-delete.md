---
title: Add, Update, and Delete
description: How SaveChangesAsync handles Added, Modified, and Deleted entities.
---

# Add, Update, and Delete

_`SaveChangesAsync` detects entity state changes tracked by EF Core and translates them into DynamoDB `PutItem`, `UpdateItem`, and `DeleteItem` operations._

!!! warning "Async only"

    `SaveChanges` is not supported. Always use `SaveChangesAsync`.

!!! tip "Sample code"

    You can view and run the sample code for this article on GitHub.

## Adding Entities

## Updating Entities

## Deleting Entities

## SaveChangesAsync vs SaveChanges

## See also

- [Transactions](transactions.md)
- [Optimistic Concurrency](concurrency.md)
- [Limitations](../limitations.md)
