namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>Creates <c>DynamoQuerySqlGenerator</c> instances for individual query executions.</summary>
public class DynamoQuerySqlGeneratorFactory : IDynamoQuerySqlGeneratorFactory
{
    /// <inheritdoc />
    public virtual DynamoQuerySqlGenerator Create() => new();
}
