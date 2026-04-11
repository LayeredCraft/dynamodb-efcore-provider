# BEGINS_WITH

Checks whether a string attribute or path value starts with a substring.

## Source

- https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-functions.beginswith.html

## Syntax

```sql
begins_with(path, value)
```

## Arguments

- `path`: attribute name or document path.
- `value`: prefix to match.

## Return type

- `bool`

## Where to use it

- Typically used in `WHERE` conditions for prefix matching.

## Limitations

- `begins_with` is for prefix matching and is typically applied to string values.
- Its behavior is condition-focused; use it in predicates rather than as a projection transform.

## Example

```sql
SELECT * FROM "Orders" WHERE "OrderID" = 1 AND begins_with("Address", '7834 24th')
```

## Related references

- [SELECT](statements-select.md)
