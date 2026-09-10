# DELETE

Use `DELETE` to remove one existing item from a DynamoDB table.

## Source

- https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.delete.html

## Syntax

```sql
DELETE FROM table
WHERE condition [RETURNING returnvalues]
```

## Parts explained

- `table`: table containing the item.
- `condition`: key-based condition that resolves to one target item.
- `RETURNING`: optional response selector. For `DELETE`, supported form is `ALL OLD *`.

## Key DynamoDB behavior

- One item per statement.
- `WHERE` must resolve to a single primary key value.
- `RETURNING ALL OLD *` returns the deleted item content.
- If no matching item exists, operation succeeds with zero items deleted.
- Conditions can use placeholders (`?`) with typed parameters in API calls.

## Common mistakes

- Using non-key-only filters and expecting a single-item delete.
- Expecting deleted item attributes without adding `RETURNING ALL OLD *`.

## Limitations

- A single `DELETE` statement can only target one item.
- `WHERE` must resolve to a single primary key value.
- If no item exists for the key, operation succeeds with zero deleted items.
- If key exists but condition evaluates false, DynamoDB returns `ConditionalCheckFailedException`.

## Minimal example

```sql
DELETE FROM "Music"
WHERE "Artist" = 'Acme Band' AND "SongTitle" = 'PartiQL Rocks'
```

## Related references

- [Transactions](transactions.md)
- [Batch Operations](batch-operations.md)
