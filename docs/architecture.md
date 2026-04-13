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

`SaveChangesAsync` uses a two-phase write pipeline:

1. **Plan**: validate supported states, build root entries, and compile one PartiQL write operation per root item.
1. **Execute**: pick execution mode from EF Core `Database.AutoTransactionBehavior`:
    - `WhenNeeded` (default): one root write executes directly; multiple root writes execute via DynamoDB `ExecuteTransaction`.
    - `Always`: behaves like `WhenNeeded` for a single root write, and requires `ExecuteTransaction` for multi-root writes.
    - `Never`: executes one root write directly, and executes multi-root writes through non-atomic DynamoDB `BatchExecuteStatement` in chunks.

During transactional execution, the provider enforces DynamoDB transaction constraints before sending any write:

- Maximum 100 write statements.
- No multiple operations targeting the same item in one transaction.

If constraints are violated, `SaveChangesAsync` throws a clear error unless overflow chunking is
explicitly enabled.

Overflow handling is provider-configurable:

- `TransactionOverflowBehavior.Throw` (default): throw when a transactional write unit exceeds the
    effective `MaxTransactionSize`.
- `TransactionOverflowBehavior.UseChunking`: split the write unit into multiple
    `ExecuteTransaction` calls of up to `MaxTransactionSize` operations each.

`AutoTransactionBehavior.Always` still requires a single atomic transactional unit; if the write
unit exceeds the effective max transaction size, SaveChanges throws.

Chunking semantics are explicit: each chunk is atomic, but the full SaveChanges unit is not
globally atomic across chunks.

Tracker semantics during chunking are also explicit: after each successful chunk commit, entries
represented by that chunk are accepted in the current context. If a later chunk fails, already
committed chunk entries remain accepted while failed/unrun chunk entries remain pending.

Non-atomic batch chunking for `AutoTransactionBehavior.Never` follows the same tracker-acceptance
model: successful statements are accepted immediately so retries do not replay committed writes.

Per-root write compilation remains:

1. **Added** — generates a PartiQL `INSERT INTO "Table" VALUE {...}` statement. The provider sets
    no provider-managed concurrency metadata. A `DuplicateItemException` from DynamoDB is mapped to
    `DbUpdateException` (duplicate primary key).
1. **Modified** — generates a single `UPDATE "Table" SET ... [REMOVE ...] WHERE pk = ?  [AND token = ?]` statement per root entity. The provider uses a hybrid strategy to
    minimise the write payload:
    - **Scalar root properties** — `SET "Prop" = ?` (existing behaviour, unchanged).
    - **Primitive collection properties** (lists, dictionaries, sets) — full attribute
        replacement: `SET "Tags" = ?` with the fully serialised SS/NS/BS/L/M value.
        EF Core tracks these as atomic property values with no element-level delta, so
        full replacement is correct.
    - **OwnsOne Modified** — per-property nested-path SET:
        `SET "Profile"."DisplayName" = ?`. Recurses through the full OwnsOne chain,
        so `SET "Profile"."Address"."City" = ?` is generated for three-level depth.
    - **OwnsOne Added** (null → ref) — full map replacement:
        `SET "Profile" = ?` with the entire M value, because a nested-path SET requires
        the parent attribute to already exist.
    - **OwnsOne Deleted** (ref → null) — `REMOVE "Profile"` removes the attribute
        entirely. SET and REMOVE are combined in one statement when both apply.
    - **OwnsMany** — full list replacement: `SET "Contacts" = ?` with all non-deleted
        elements serialised as L. EF Core has no element-level index delta, so
        `list_append` / `REMOVE [i]` are not used.
        For properties configured with `.IsConcurrencyToken()`, original values are added to
        the WHERE clause. A `ConditionalCheckFailedException` is mapped to
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
