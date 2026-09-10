# Data Types

Use this page to map DynamoDB types to PartiQL representations and write valid literals.

## Source

- https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-reference.data-types.html

## Core mappings

- `Binary` -> `N/A` (inline literal not supported; use typed parameters in code/API)
- `String` -> `'value'`
- `Number` -> `1`, `1.0`, `1e0`
- `Boolean` -> `TRUE` / `FALSE`
- `Null` -> `NULL`
- `List` -> `[value1, value2]`
- `Map` -> `{'name': value}`
- `Number Set` -> `<<1,2,3>>`
- `String Set` -> `<<'a','b'>>`

## Type behavior notes

- `List` values can mix types (for example numbers and strings in one list).
- `Map` values can mix types across keys.
- `Number Set` members must all be numbers.
- `String Set` members must all be strings.
- `Binary` values must be passed through typed `Parameters` (`AttributeValue`) rather than inline
  literals.

## Limitations

- String literals must use single quotes.
- Number precision follows DynamoDB numeric limits (up to 38 digits).
- Binary values are supported only via typed parameters (no inline PartiQL literal form).
- Set values are type-constrained (`Number Set` must be numeric, `String Set` must be string).
- Attribute names are case-sensitive.

## Minimal examples

```sql
INSERT INTO TypesTable VALUE {
  'primarykey':'1',
  'NumberType':1,
  'MapType': {'entryname1': 'value', 'entryname2': 4},
  'ListType': [1, 'stringval'],
  'NumberSetType': <<1,34,32,4.5>>,
  'StringSetType': <<'stringval','stringval2'>>
}
```

## Related references

- [UPDATE](statements-update.md)
- [Functions](functions.md)
