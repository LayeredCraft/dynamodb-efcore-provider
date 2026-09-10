# EXISTS

Use `EXISTS` for transactional existence checks similar to `ConditionCheck` behavior.

## Source

- https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-functions.exists.html

## Syntax

```sql
EXISTS(statement)
```

## Arguments

- `statement`: required `SELECT` statement to evaluate.

## Key DynamoDB behavior

- Only valid in transactional operations.
- The `SELECT` inside `EXISTS` must specify the full primary key and one additional condition.

## Limitations

- `EXISTS` can only be used in transactional operations.
- The inner `SELECT` must include full primary key criteria plus one additional condition.

## Return type

- `bool`

## Example

```sql
EXISTS(
  SELECT * FROM "Music"
  WHERE "Artist" = 'Acme Band'
    AND "SongTitle" = 'PartiQL Rocks'
    AND "Awards" IS MISSING
)
```

## Related references

- [Transactions](transactions.md)
