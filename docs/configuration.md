# Configuration

## DbContext setup
- Configure the provider with `UseDynamo(...)`.
- Set endpoint/credentials settings appropriate to your environment.

## Options
- `ServiceUrl`: target DynamoDB Local or a custom endpoint.
- `AuthenticationRegion`: AWS region used by the SDK client.
- `DefaultPageSize`: default request page size (`ExecuteStatementRequest.Limit`) for queries.

## Table mapping
- Use `ToTable("TableName")` to map an entity to a DynamoDB table.
- If omitted, the provider falls back to the entity CLR type name.
