# Writes, Transactions, and Lifecycle

## Writes

Use `Add`, normal tracked updates, `Remove`, and `SaveChangesAsync`. The provider compiles writes
to DynamoDB PartiQL requests. Keep each item and generated statement within DynamoDB limits.

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
