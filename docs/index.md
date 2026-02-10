# LayeredCraft.EntityFrameworkCore.DynamoDb

Entity Framework Core provider for AWS DynamoDB.

This provider translates LINQ queries to PartiQL and executes them with the AWS SDK.

## Documentation

- [Configuration](configuration.md)
- [Operators](operators.md)
- [Pagination](pagination.md)
- [Projections](projections.md)
- [Diagnostics](diagnostics.md)
- [Limitations](limitations.md)
- [Repository README](../README.md)

## Status

- Current scope is query execution (`ExecuteStatement`) with async query APIs.
- `SaveChanges` is not implemented yet.
