namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>Creates <c>DynamoQuerySqlGenerator</c> instances for individual query executions.</summary>
public sealed class DynamoQuerySqlGeneratorFactory : IDynamoQuerySqlGeneratorFactory
{
    /// <inheritdoc />
    public DynamoQuerySqlGenerator Create() => new();
}
