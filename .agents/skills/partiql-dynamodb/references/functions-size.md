# SIZE

Returns a numeric size value for an attribute using DynamoDB `size` semantics.

## Source

- https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-functions.size.html
- https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/Expressions.OperatorsAndFunctions.html#Expressions.OperatorsAndFunctions.Functions

## Syntax

```sql
size(path)
```

## Arguments

- `path`: attribute name or document path.

## Return type

- `int`

## Where to use it

- Typically used in `WHERE` conditions to filter by attribute size.

## Limitations

- Valid only for DynamoDB-supported `size` operand types.
- `size` behavior is type dependent: string length for `String`, byte count for `Binary`, and
  element count for `Set`, `List`, or `Map`.
- Returns an integer size value; use comparisons in conditions for filtering.

## Example

```sql
SELECT * FROM "Orders" WHERE "OrderID" = 1 AND size("Image") > 300
```

## Related references

- [SELECT](statements-select.md)
