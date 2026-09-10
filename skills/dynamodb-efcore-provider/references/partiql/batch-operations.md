# Batch Operations

Use this page for batch PartiQL execution in DynamoDB.

## Source

- https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.multiplestatements.batching.html

## Core limits and rules

- A batch must be all reads or all writes.
- Maximum 25 statements per batch.

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

- Batch responses are returned per statement entry.
- Batch execution is not an all-or-nothing transaction boundary.

## Operational caveats

- Keep batches small and predictable for retry safety.
- Batch behavior is still bounded by DynamoDB throughput and request limits.
- Keep read batches and write batches separate; mixed batches are rejected.

## Limitations

- Maximum 25 statements per batch request.
- You cannot mix read and write statements in one batch.
- Batch execution is not a transactional all-or-nothing boundary.

## Related references

- [Statements](statements.md)
- [Transactions](transactions.md)
