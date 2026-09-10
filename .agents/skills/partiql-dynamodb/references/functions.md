# Functions

Use this page to identify built-in DynamoDB PartiQL functions and open the right function reference.

## Source

- https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-functions.html

## Supported functions

- `SIZE`
- `EXISTS`
- `ATTRIBUTE_TYPE`
- `BEGINS_WITH`
- `CONTAINS`
- `MISSING`

## Choose by intent

- size-based checks -> use [SIZE](functions-size.md)
- transactional existence check -> use [EXISTS](functions-exists.md)
- validate attribute type -> use [ATTRIBUTE_TYPE](functions-attribute-type.md)
- string prefix matching -> use [BEGINS_WITH](functions-begins-with.md)
- substring, set membership, or list membership check -> use [CONTAINS](functions-contains.md)
- attribute presence check -> use [MISSING](functions-missing.md)

## Limitations

- Only functions listed in DynamoDB PartiQL docs are supported.
- `EXISTS` is restricted to transactional operations.

## Related references

- [SIZE](functions-size.md)
- [EXISTS](functions-exists.md)
- [ATTRIBUTE_TYPE](functions-attribute-type.md)
- [BEGINS_WITH](functions-begins-with.md)
- [CONTAINS](functions-contains.md)
- [MISSING](functions-missing.md)
