# MISSING

Checks whether an item does not include a specified attribute.

## Source

- https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-functions.missing.html

## Syntax

```sql
attributename IS MISSING
attributename IS NOT MISSING
```

## Key DynamoDB behavior

- Only `IS MISSING` and `IS NOT MISSING` predicate forms are supported.

## Limitations

- `MISSING` checks are limited to `IS MISSING` and `IS NOT MISSING` style predicates.
- This is an attribute-presence test, not a null-value test.

## Where to use it

- Typically used in `WHERE` conditions to check attribute presence or absence.

## Return type

- `bool`

## Example

```sql
SELECT * FROM Music WHERE "Awards" IS MISSING
```

## Related references

- [SELECT](statements-select.md)
