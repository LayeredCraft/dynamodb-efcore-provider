# Transactions

Use this page for multi-statement transactional PartiQL execution in DynamoDB.

## Source

- https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.multiplestatements.transactions.html

## Core limits and rules

- Up to 100 total statements per transaction.
- A transaction must be all reads or all writes.
- `EXISTS` is the documented exception used for condition-style checks in transaction workflows.

## Statement shape

```json
[
  {
    "Statement": "statement",
    "Parameters": [
      { "S": "value" }
    ]
  }
]
```

## Parts explained

- `Statement`: required PartiQL statement string.
- `Parameters`: optional positional values for `?` placeholders.
- Each entry in `Parameters` is a DynamoDB `AttributeValue` object (for example `{ "S": "value" }`,
  `{ "N": "42" }`, `{ "BOOL": true }`).

## Return behavior

- Write transactions do not return item content.
- Read transactions return items based on the `SELECT` statements and conditions.

## Operational caveats

- On per-statement failures, transaction cancellation behavior is surfaced as
  `TransactionCanceledException`.
- If any statement fails, DynamoDB cancels the transaction.
- Prefer idempotent retry strategies in client code for transient failures.

## Limitations

- Maximum 100 statements per transaction.
- You cannot mix read and write statements in the same transaction.
- `EXISTS` is the documented exception used for transaction-side conditional checks.

## Related references

- [EXISTS](functions-exists.md)
- [Statements](statements.md)
- [IAM](iam.md)
