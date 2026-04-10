namespace EntityFrameworkCore.DynamoDb.Update.Internal;

/// <summary>Creates <see cref="DynamoWriteSqlGenerator" /> instances.</summary>
public interface IDynamoWriteSqlGeneratorFactory
{
    /// <summary>Creates a new <see cref="DynamoWriteSqlGenerator" />.</summary>
    /// <returns>A fresh generator instance ready to produce a single statement.</returns>
    DynamoWriteSqlGenerator Create();
}
