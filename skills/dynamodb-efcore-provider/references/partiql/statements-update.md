# UPDATE

Use `UPDATE` to modify attributes in a single DynamoDB item.

## Source

- https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.update.html

## Syntax

```sql
UPDATE table
[SET | REMOVE] path [= data] [...]
WHERE condition [RETURNING returnvalues]
```

## Parts explained

- `table`: table containing the item to modify.
- `SET`: adds/replaces attribute values.
- `REMOVE`: removes attribute paths (for example list members or map entries).
- `path`: attribute name or document path to modify.
- `data`: literal value or supported operation result.
- `condition`: must resolve to a single item key.
- `RETURNING`: optional output selector (`ALL OLD *`, `MODIFIED OLD *`, `ALL NEW *`,
  `MODIFIED NEW *`).

## Key DynamoDB behavior

- One item per statement.
- `WHERE` must resolve to a single primary key value.
- Supports `LIST_APPEND`, `SET_ADD`, and `SET_DELETE` in `SET` operations.
- Optional `RETURNING` controls whether old/new attributes are returned.
- Values can be parameterized with placeholders (`?`) and supplied separately in API calls.

## Supported set operations

- `LIST_APPEND(list, values)`
- `SET_ADD(set, values)`
- `SET_DELETE(set, values)`

## Common mistakes

- Using a `WHERE` clause that can match multiple partition keys.
- Expecting updates without `RETURNING` to include item data in the response.
- Using set operations on non-set attributes.

## Limitations

- A single `UPDATE` statement can only target one item.
- `WHERE` must resolve to a single primary key value.
- If no item matches a true condition, DynamoDB can return `ConditionalCheckFailedException`.

## Minimal examples

```sql
UPDATE "Music"
SET AwardsWon = 1
SET AwardDetail = {'Grammys': [2020, 2018]}
WHERE Artist = 'Acme Band' AND SongTitle = 'PartiQL Rocks'
```

```sql
UPDATE "Music"
SET BandMembers = set_add(BandMembers, <<'newbandmember'>>)
WHERE Artist = 'Acme Band' AND SongTitle = 'PartiQL Rocks'
```

## Related references

- [Data Types](data-types.md)
- [Functions](functions.md)
- [Transactions](transactions.md)
- [Batch Operations](batch-operations.md)
