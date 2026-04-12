---
icon: lucide/git-branch
---

# Architecture

## End-to-end flow

1. LINQ query is translated into provider expressions.
1. Provider builds a `SelectExpression` query model.
1. Query translation post-processor evaluates runtime index descriptors and applies the chosen
    secondary index to the `SelectExpression` `FROM` source before SQL generation (if applicable).
1. Provider generates PartiQL SQL text and positional parameters.
1. Provider executes PartiQL via DynamoDB `ExecuteStatement`.
1. Provider materializes rows from `Dictionary<string, AttributeValue>` into projection/entity results.

## Core semantics captured by this architecture

- Query execution is async-only.
- Paging uses DynamoDB continuation tokens via `ExecuteStatement` unless disabled.
- Result limit and page size are separate concepts.
- Materialization enforces strict required-property behavior for missing/null/wrong-typed data.
- Query planning relies on Dynamo-specific partition/sort key metadata (`HasPartitionKey(...)`,
    `HasSortKey(...)`, or supported Dynamo naming conventions), and the provider derives the EF primary
    key from that mapping rather than from user-configured `HasKey(...)`.
- Secondary-index metadata is configured at model build time and compiled into runtime annotations
    by `DynamoModelRuntimeInitializer` (an `IModelRuntimeInitializer`). No separate registry service
    is used; descriptors live on the EF Core runtime model.
- At query compile time, `DynamoQueryTranslationPostprocessor` reads those runtime annotations,
    invokes `IDynamoIndexSelectionAnalyzer`, and applies the chosen index to the `SelectExpression`
    before PartiQL generation. Explicit `.WithIndex()` hints take priority; conservative automatic
    selection runs next if enabled.

## Write pipeline (SaveChangesAsync)

`SaveChangesAsync` processes EF Core change-tracking entries in entity-state order:

1. **Added** — generates a PartiQL `INSERT INTO "Table" VALUE {...}` statement. The provider sets
    no provider-managed concurrency metadata. A `DuplicateItemException` from DynamoDB is mapped to
    `DbUpdateException` (duplicate primary key).
1. **Modified** — generates a `UPDATE "Table" SET ... WHERE pk = ? [AND token = ?]` statement.
    For properties configured with `.IsConcurrencyToken()`, original values are added to the WHERE
    clause. A `ConditionalCheckFailedException` is mapped to
    `DbUpdateConcurrencyException` (stale token).
1. **Deleted** — generates a `DELETE FROM "Table" WHERE pk = ? [AND token = ?]` statement using
    the same concurrency-token WHERE behavior as Modified.

See [Concurrency](concurrency.md) for optimistic concurrency behavior and exception handling.

## DynamoDB ExecuteStatement model

- SQL text is generated with positional `?` placeholders and a separate positional `AttributeValue`
    parameter list.
- Request `Limit` controls items evaluated per request, while provider result limits (`Take`,
    `First*`) control how many rows are returned to EF.
- DynamoDB can stop responses at request `Limit` or at the 1 MB processed-data cap, so continuation
    may be required for selective queries.

## External references

- AWS ExecuteStatement API: <https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_ExecuteStatement.html>
- AWS AttributeValue model: <https://docs.aws.amazon.com/amazondynamodb/latest/APIReference/API_AttributeValue.html>
