---
name: partiql-dynamodb
description: Use this skill when the user asks how to use PartiQL with Amazon DynamoDB, needs DynamoDB-specific PartiQL statement help, wants query examples, or needs guidance on scans, transactions, batch limits, functions, operators, or IAM permissions.
---

# DynamoDB PartiQL Assistant

Use this skill when the user needs help writing, validating, or understanding DynamoDB PartiQL.

This skill is DynamoDB-specific. Do not generalize from full SQL or full PartiQL behavior unless
DynamoDB docs support it.

## When to use this skill

Use this skill if the user asks to:

- learn how to use PartiQL in DynamoDB
- write or debug DynamoDB PartiQL statements
- break down statement syntax parts (for example what `expression`, `condition`, or `RETURNING`
  mean)
- understand supported statements, functions, or operators
- avoid full table scans with PartiQL `SELECT`
- use PartiQL with transactions or batch operations
- understand IAM permissions for PartiQL actions

## Core workflow

1. Identify what the user is trying to do in DynamoDB (read, write, update, delete, transaction,
   batch, or access control).
2. Load only the relevant reference page(s) from `references/`.
3. If exact syntax or limits are in question, verify with AWS docs through MCP.
4. Return DynamoDB-accurate guidance, examples, and caveats.
5. Include source links when guidance is page-specific.

## Reference routing

Start with:

- [Overview](references/overview.md)

Load by user intent:

- setup and first usage: [Getting Started](references/getting-started.md)
- literals and attribute types: [Data Types](references/data-types.md)
- statement family overview: [Statements](references/statements.md)
- `SELECT`: [SELECT](references/statements-select.md)
- `INSERT`: [INSERT](references/statements-insert.md)
- `UPDATE`: [UPDATE](references/statements-update.md)
- `DELETE`: [DELETE](references/statements-delete.md)
- function inventory: [Functions](references/functions.md)
- `SIZE`: [SIZE](references/functions-size.md)
- `EXISTS`: [EXISTS](references/functions-exists.md)
- `ATTRIBUTE_TYPE`: [ATTRIBUTE_TYPE](references/functions-attribute-type.md)
- `BEGINS_WITH`: [BEGINS_WITH](references/functions-begins-with.md)
- `CONTAINS`: [CONTAINS](references/functions-contains.md)
- `MISSING`: [MISSING](references/functions-missing.md)
- operators: [Operators](references/operators.md)
- multi-statement transactions: [Transactions](references/transactions.md)
- batch statements: [Batch Operations](references/batch-operations.md)
- security and permissions: [IAM](references/iam.md)

## DynamoDB boundaries to enforce

- DynamoDB supports only a subset of PartiQL.
- `SELECT` can become a full table scan if partition-key equality or `IN` patterns are not used
  correctly.
- `INSERT`, `UPDATE`, and `DELETE` operate on one item per statement.
- A transaction can contain up to 100 statements and must be all reads or all writes (`EXISTS` is
  the documented exception for condition-style checks).
- A batch can contain up to 25 statements and cannot mix reads and writes.
- `EXISTS` is only valid in transactional operations.
- IAM permissions are action specific: `dynamodb:PartiQLSelect`, `dynamodb:PartiQLInsert`,
  `dynamodb:PartiQLUpdate`, `dynamodb:PartiQLDelete`.

## Output guidance

When answering, prefer:

1. short statement of whether the user approach is valid in DynamoDB PartiQL
2. corrected query or query alternatives
3. caveats (scan risk, limits, permissions)
4. source links when rules come from a specific reference page
