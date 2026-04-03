namespace EntityFrameworkCore.DynamoDb.Update.Internal;

/// <summary>Default factory that creates <see cref="DynamoWriteSqlGenerator" /> instances.</summary>
public sealed class DynamoWriteSqlGeneratorFactory : IDynamoWriteSqlGeneratorFactory
{
    /// <inheritdoc />
    public DynamoWriteSqlGenerator Create() => new();
}
