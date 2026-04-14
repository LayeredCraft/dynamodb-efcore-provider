# Statements

Use this page as the entry point for supported DynamoDB PartiQL statement families.

## Source

- https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.statements.html

## Supported statement families

- `SELECT`
- `UPDATE`
- `INSERT`
- `DELETE`

DynamoDB also supports multi-statement execution patterns through:

- transactions
- batch operations

## Limitations

- DynamoDB does not support every PartiQL statement in the broader PartiQL spec.
- `INSERT`, `UPDATE`, and `DELETE` operate on one item per statement.

## Statement anatomy quick map

- `SELECT expression FROM table[.index] [WHERE condition] [ORDER BY ...]`
- `INSERT INTO table VALUE item`
- `UPDATE table [SET|REMOVE ...] WHERE condition [RETURNING ...]`
- `DELETE FROM table WHERE condition [RETURNING ...]`

## How to choose the right statement

- Read one or more items -> use `SELECT`.
- Create one item -> use `INSERT`.
- Change attributes on one item -> use `UPDATE`.
- Remove one item -> use `DELETE`.
- Apply multiple statements atomically -> use [Transactions](transactions.md).
- Apply multiple statements in a single batch request ->
  use [Batch Operations](batch-operations.md).

## Related references

- [SELECT](statements-select.md)
- [INSERT](statements-insert.md)
- [UPDATE](statements-update.md)
- [DELETE](statements-delete.md)
- [Transactions](transactions.md)
- [Batch Operations](batch-operations.md)
