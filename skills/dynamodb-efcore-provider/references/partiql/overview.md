# Overview

PartiQL in DynamoDB provides SQL-compatible statements for reading and modifying items, while
keeping DynamoDB data-plane behavior.

## Source

- https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.html

## What DynamoDB PartiQL is

- Supports SQL-like `SELECT`, `INSERT`, `UPDATE`, and `DELETE` against DynamoDB tables.
- Works through DynamoDB console, NoSQL Workbench, AWS CLI, and DynamoDB APIs.
- Uses DynamoDB service semantics for performance and availability.

## How to use this reference set

- Start with [Getting Started](getting-started.md) for first execution patterns.
- Use [Statements](statements.md) when deciding between `SELECT`, `INSERT`, `UPDATE`, and `DELETE`.
- Use [Data Types](data-types.md) when building literals or nested values.
- Use [Functions](functions.md) and [Operators](operators.md) for condition logic.
- Use [Transactions](transactions.md), [Batch Operations](batch-operations.md), and [IAM](iam.md)
  for production constraints.

## Limitations

- DynamoDB supports only a subset of PartiQL, not the full language.
- Amazon Ion data format and Ion literals are not supported in DynamoDB PartiQL.

## Related references

- [Getting Started](getting-started.md)
- [Statements](statements.md)
- [Functions](functions.md)
- [Operators](operators.md)
- [Transactions](transactions.md)
- [Batch Operations](batch-operations.md)
- [IAM](iam.md)
