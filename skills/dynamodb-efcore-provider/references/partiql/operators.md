# Operators

Use this page for supported arithmetic, comparison, and logical operators in DynamoDB PartiQL.

## Source

- https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-operators.html

## Arithmetic operators

- `+` add numeric values
- `-` subtract numeric values

## Comparison operators

- `=` equal to
- `<>` not equal to
- `!=` not equal to
- `>` greater than
- `<` less than
- `>=` greater than or equal to
- `<=` less than or equal to

## Logical operators

- `AND` all conditions must be true
- `BETWEEN` inclusive range check
- `IN` membership check in a list of values
- `IS` type/null/missing style checks
- `NOT` negates a condition
- `OR` at least one condition must be true

## Limitations

- Only operators listed in DynamoDB PartiQL docs are supported.
- `IN` has documented limits: up to 50 partition-key values (AWS docs use "hash attribute") or up to
  100 non-key attribute values.
- `IN` results are paged (up to 10 items per page) and may require `NextToken` retrieval.

## Related references

- [SELECT](statements-select.md)
- [Functions](functions.md)
