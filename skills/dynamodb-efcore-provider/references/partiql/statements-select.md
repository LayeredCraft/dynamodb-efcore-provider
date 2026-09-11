# SELECT

Use `SELECT` to retrieve items from DynamoDB tables or indexes through PartiQL.

## Source

- https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.select.html

## Syntax

```sql
SELECT expression [, ...]
FROM table[.index]
[WHERE condition]
[ORDER BY key [DESC|ASC], ...]
```

## Parts explained

- `expression`: projected attributes, `*`, document paths, or supported function expressions.
- `table`: DynamoDB table name.
- `index`: optional index name. When selecting from an index, quote table and index names.
- `condition`: optional filter criteria in `WHERE`.
- `key`: partition key or sort key used in `ORDER BY`.

## Key DynamoDB behavior

- `WHERE` design determines whether the operation is query-like or scan-like.
- To avoid full table scans, include partition-key equality or `IN` conditions.
- Missing or non-key-only filters can force full scans.
- Omitting `WHERE` retrieves all items from the table.

## Practical guardrails

- Prefer `WHERE PartitionKey = value` or `WHERE PartitionKey IN [...]`.
- `OR` conditions can still be query-like if every branch is partition-key equality.
- Be careful with `OR` combinations that include non-key predicates.
- Use IAM policy controls when you must block scan-causing PartiQL patterns.

## Limitations

- Without partition-key equality or `IN` conditions in `WHERE`, `SELECT` can become a full table
  scan.
- Omitting `WHERE` reads all items in the table.
- `IN` usage has DynamoDB-specific limits documented under operators (max 50 partition-key values or
  max 100 non-key values, paged responses).

## Safe vs risky WHERE patterns

- Generally safe: `WHERE PartitionKey = ...`.
- Generally safe: `WHERE PartitionKey = ... OR PartitionKey = ...`.
- Generally safe: `WHERE PartitionKey IN [...]`.
- Often risky: predicates only on non-key attributes.
- Often risky: inequality (`>`, `<`, `BETWEEN`) on non-key attributes.
- Easy to misuse: `OR` expressions that include any non-key-only branch.

## Minimal examples

```sql
SELECT OrderID, Total
FROM "Orders"
WHERE OrderID = 1
```

```sql
SELECT OrderID, Total
FROM "Orders"
WHERE OrderID IN [1, 2, 3] ORDER BY OrderID DESC
```

```sql
SELECT *
FROM "TableName"."IndexName"
WHERE PartitionKey = 'pk-value'
```

## Related references

- [Operators](operators.md)
- [Functions](functions.md)
- [IAM](iam.md)
- [Transactions](transactions.md)
