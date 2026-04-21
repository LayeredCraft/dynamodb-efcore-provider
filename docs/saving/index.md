---
title: Saving Data
description: Overview of how SaveChangesAsync works and what write operations are supported.
icon: lucide/save
---

# Saving Data

The DynamoDB EF Core provider implements `SaveChangesAsync` to persist Added, Modified, and Deleted entities using DynamoDB's write APIs.

!!! warning "Async only"

    The DynamoDB SDK does not support synchronous I/O. `SaveChanges` is not supported; use `SaveChangesAsync` instead.

This section covers how write operations work, including transaction behavior, concurrency control, and chunking for large batches.

- [Add, Update, and Delete](add-update-delete.md) — How `SaveChangesAsync` handles each entity state.
- [Transactions](transactions.md) — Auto-transaction behavior, limits, and chunking.
- [Optimistic Concurrency](concurrency.md) — Configuring and handling concurrency tokens.

## See also

- [Querying](../querying/index.md)
- [Limitations](../limitations.md)
