# IAM

Use this page for permission requirements and policy patterns for DynamoDB PartiQL.

## Source

- https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-iam.html

## Required actions by statement type

- `dynamodb:PartiQLSelect`
- `dynamodb:PartiQLInsert`
- `dynamodb:PartiQLUpdate`
- `dynamodb:PartiQLDelete`

## Statement to action mapping

- `SELECT` -> `dynamodb:PartiQLSelect`
- `INSERT` -> `dynamodb:PartiQLInsert`
- `UPDATE` -> `dynamodb:PartiQLUpdate`
- `DELETE` -> `dynamodb:PartiQLDelete`

## Practical policy patterns

- allow all PartiQL actions on a table
- allow only `PartiQLSelect`
- allow action scope on a table index ARN
- allow only transactional PartiQL via `dynamodb:EnclosingOperation`
- deny transactional PartiQL while allowing non-transactional usage
- deny full-table-scan style `SELECT` using `dynamodb:FullTableScan`

## Limitations

- PartiQL permissions are action-specific (`PartiQLSelect`, `PartiQLInsert`, `PartiQLUpdate`,
  `PartiQLDelete`); missing one blocks that statement type.
- Resource scoping must match table/index ARNs used by statements.
- Full table scan blocking for `SELECT` requires explicit deny conditions such as
  `dynamodb:FullTableScan`.

## Related references

- [SELECT](statements-select.md)
- [Transactions](transactions.md)
- [Batch Operations](batch-operations.md)
