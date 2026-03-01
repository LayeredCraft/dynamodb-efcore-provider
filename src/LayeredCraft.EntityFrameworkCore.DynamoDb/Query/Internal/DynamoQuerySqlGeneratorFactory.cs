namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>Creates <see cref="DynamoQuerySqlGenerator" /> instances for individual query executions.</summary>
public class DynamoQuerySqlGeneratorFactory : IDynamoQuerySqlGeneratorFactory
{
    /// <inheritdoc />
    public virtual DynamoQuerySqlGenerator Create() => new();
}
