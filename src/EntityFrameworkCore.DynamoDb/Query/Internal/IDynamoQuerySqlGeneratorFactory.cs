namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>Creates PartiQL SQL generators for individual query executions.</summary>
public interface IDynamoQuerySqlGeneratorFactory
{
    /// <summary>Creates a new PartiQL SQL generator instance.</summary>
    DynamoQuerySqlGenerator Create();
}
