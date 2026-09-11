# Writes, Transactions, and Lifecycle

## Writes

Use `Add`, normal tracked updates, `Remove`, and `SaveChangesAsync`. The provider compiles writes
to DynamoDB PartiQL requests. Keep each item and generated statement within DynamoDB limits.

# Writes, Transactions, and Lifecycle

## Writes

Use `Add`, normal tracked updates, `Remove`, and `SaveChangesAsync`. The provider compiles writes
to DynamoDB PartiQL requests. Keep each item and generated statement within DynamoDB limits.

## ExecuteUpdateAsync

`ExecuteUpdateAsync` applies a single-item update directly against the table. Its contract is
narrow — check it before promising bulk-update behavior:

- The WHERE clause must equality-constrain the full primary key (partition key, plus sort key when
  present). Extra non-key filters are allowed; `IN`, key ranges, and OR over keys are not.
- The result is `0` (item not found) or `1`. There is no multi-row update or row count.
- Execution is immediate and does not touch the change tracker; tracked entities must be re-read
  to observe new values.
- Numeric self-reference supports `+` and `-` only (`Count + 1`). Multiplication, division, and
  string concatenation are rejected — DynamoDB PartiQL SET supports numeric add/subtract only.
- `SetProperty` targets mapped scalar properties or leaf scalars of nested complex-property paths.
  Navigations, whole complex properties, and key properties cannot be assigned.
- Synchronous `ExecuteUpdate` throws. `ExecuteDeleteAsync` is not implemented; delete through the
  change tracker (`Remove` + `SaveChangesAsync`).

## Concurrency and transactions

- Configure a concurrency token when competing writers must be detected. Application code is
  responsible for updating manual token values as required by its model.
- Transactions have DynamoDB item and statement limits. A large `SaveChangesAsync` call can exceed
  them; batch work deliberately when application semantics allow it.
- Do not expect relational foreign keys, cascading behavior, or arbitrary multi-table SQL.

## Table lifecycle

`SaveChangesAsync` does not create tables. Use asynchronous lifecycle APIs for development or test
setup, or provision tables through infrastructure for production. Existing-table validation checks
only supported schema details; it is not a full migration system.

Table creation and deletion, connectivity checks, seeding, and health checks must use their async
APIs. Health checks confirm the configured context can reach DynamoDB; they do not validate the
application's complete schema or access patterns.
