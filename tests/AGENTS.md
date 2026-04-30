# Testing

## General

- Prefer `[Fact(Timeout = TestConfiguration.DefaultTimeout)]` over plain `[Fact]`.

## Project Split

- Use `tests/EntityFrameworkCore.DynamoDb.Tests/` for fast unit tests that validate translation,
  metadata, change tracking, materialization helpers, serializer behavior, and other provider
  internals without needing a live DynamoDB table.
- Use `tests/EntityFrameworkCore.DynamoDb.IntegrationTests/` for end-to-end behavior that depends on
  real table state, emitted PartiQL, `SaveChanges`, query execution, concurrency, or persisted
  DynamoDB wire shape.
- If a test asserts both emitted SQL/PartiQL and stored item contents after real execution, it
  belongs in integration tests.
- If a test only exercises an internal helper or factory and can run entirely in-memory with a local
  `DbContext`, it belongs in unit tests.

## Unit Test Placement

- Put translation and query-pipeline unit tests in
  `tests/EntityFrameworkCore.DynamoDb.Tests/Query/`.
- Put write-path, serializer, and storage helper unit tests in
  `tests/EntityFrameworkCore.DynamoDb.Tests/Storage/`.
- Put model-building, conventions, and annotations tests in
  `tests/EntityFrameworkCore.DynamoDb.Tests/Metadata/`.
- Put infrastructure and extension-method tests in the matching top-level folders under
  `tests/EntityFrameworkCore.DynamoDb.Tests/`.

## Integration Test Placement

- Group integration tests by table scenario under
  `tests/EntityFrameworkCore.DynamoDb.IntegrationTests/`.
- Put query behavior tests in the folder for the relevant model/table shape, such as `SimpleTable/`,
  `PkSkTable/`, `ComplexTypesTable/`, or `PrimitiveCollectionsTable/`.
- Put `SaveChanges` insert/update/delete/concurrency coverage in `SaveChangesTable/`.
- Put shared table fixtures, seed data, mappers, and table creation helpers under that scenario's
  `Infra/` folder.
- Add a new integration test scenario folder only when an existing table model cannot cover the
  behavior cleanly.
