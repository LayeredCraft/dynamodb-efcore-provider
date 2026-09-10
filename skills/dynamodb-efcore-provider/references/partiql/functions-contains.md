# CONTAINS

Checks whether a string contains a substring, a set includes a member, or a list includes an
element.

## Source

- https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-functions.contains.html
- https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/Expressions.OperatorsAndFunctions.html#Expressions.OperatorsAndFunctions.Functions

## Syntax

```sql
contains(path, substring)
```

## Arguments

- `path`: attribute name or document path.
- `substring`: target substring, set member, or list element.

## Return type

- `bool`

## Where to use it

- Typically used in `WHERE` conditions for substring, set-member, or list-member checks.

## Limitations

- Input/value compatibility depends on the underlying attribute type.
- `path` and comparison value must be distinct; `contains(a, a)` is invalid.

## Example

```sql
SELECT * FROM "Orders" WHERE "OrderID" = 1 AND contains("Address", 'Kirkland')
```

## Related references

- [SELECT](statements-select.md)
- [Data Types](data-types.md)
