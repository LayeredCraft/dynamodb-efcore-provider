---
title: Transactions
description: How write transactions are handled, including auto-transaction behavior and chunking.
---

# Transactions

_The provider wraps multiple write operations in a single DynamoDB `TransactWriteItems` call by default; when the item count exceeds DynamoDB's transaction limit, writes are chunked into multiple non-atomic batches._

!!! warning "Async only"

    `SaveChanges` is not supported. Always use `SaveChangesAsync`.

!!! tip "Sample code"

    You can view and run the sample code for this article on GitHub.

## Auto-Transaction Behavior

## Transaction Limits

!!! note

    DynamoDB `TransactWriteItems` supports a maximum of 100 items per transaction.

## Chunking and Overflow

## See also

- [Add, Update, and Delete](add-update-delete.md)
- [Optimistic Concurrency](concurrency.md)
- [Limitations](../limitations.md)
