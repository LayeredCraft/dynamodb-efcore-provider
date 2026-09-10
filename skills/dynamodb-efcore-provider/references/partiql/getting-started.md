# Getting Started

Use this page for first-run DynamoDB PartiQL workflows in console, NoSQL Workbench, CLI, and API
code paths.

## Source

- https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ql-gettingstarted.html

## Main usage paths

- Console: use the DynamoDB PartiQL editor and generated query flow.
- NoSQL Workbench: build statements and supply typed parameters.
- AWS CLI: run `aws dynamodb execute-statement --statement "..."`.
- SDK/API: run `ExecuteStatement` style operations with parameterized values.

## Prerequisites

- Have a DynamoDB table with known partition key (and sort key if applicable).
- For AWS examples that use `Music`, adapt key names and values to your table schema.

## Useful starter sequence

- Insert one item with `INSERT`.
- Retrieve with `SELECT`.
- Modify with `UPDATE`.
- Remove with `DELETE`.

## Parameter placeholders

- In API and CLI workflows, use `?` placeholders in statements and pass typed `Parameters`
  separately.
- This keeps statements reusable and avoids embedding dynamic values directly in query text.

## Limitations

- AWS starter flows use example tables and keys (such as `Music`); you must adapt statements to your
  own schema.
- CLI/API placeholder usage requires separate typed `Parameters`; placeholders alone are not
  sufficient.
- Getting-started examples are usage patterns, not guarantees against scan-heavy statements unless
  key conditions are correct.

## Related references

- [Overview](overview.md)
- [SELECT](statements-select.md)
- [INSERT](statements-insert.md)
- [UPDATE](statements-update.md)
- [DELETE](statements-delete.md)
