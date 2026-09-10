# INSERT

Use `INSERT` to add one new item to a DynamoDB table.

## Source

- https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.insert.html

## Syntax

```sql
INSERT INTO table VALUE item
```

## Parts explained

- `table`: destination table that already exists.
- `item`: one PartiQL tuple representing one DynamoDB item.

## Key DynamoDB behavior

- One item per statement.
- If an item with the same primary key exists, DynamoDB returns `DuplicateItemException`.
- Attribute names are case-sensitive.
- Use single quotes for string values.
- You can parameterize values with placeholders (`?`) when using API parameter lists.

## Common mistakes

- Trying to insert multiple items in one `INSERT` statement.
- Omitting required key attributes for the target table.
- Mixing quote styles for string literals.

## Limitations

- A single `INSERT` statement can only add one item.
- If the primary key already exists, DynamoDB returns `DuplicateItemException`.
- `INSERT` does not return item attributes.

## Minimal example

```sql
INSERT INTO "Music" VALUE {'Artist': 'Acme Band', 'SongTitle': 'PartiQL Rocks'}
```

## Related references

- [Data Types](data-types.md)
- [Transactions](transactions.md)
- [Batch Operations](batch-operations.md)
