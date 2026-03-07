using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Builders;

/// <summary>Provides DynamoDB-specific fluent configuration for a configured secondary index.</summary>
public class DynamoSecondaryIndexBuilder(IndexBuilder indexBuilder)
{
    /// <summary>Gets the underlying EF index builder used to configure this secondary index.</summary>
    public IndexBuilder IndexBuilder { get; } = indexBuilder;

    /// <summary>
    ///     Marks this secondary index as projecting all attributes needed for full-entity
    ///     materialization.
    /// </summary>
    /// <returns>The same builder instance so additional configuration can be chained.</returns>
    public virtual DynamoSecondaryIndexBuilder ProjectsAll()
    {
        IndexBuilder.Metadata.SetSecondaryIndexProjectionType(DynamoSecondaryIndexProjectionType.All);
        return this;
    }
}

/// <summary>
///     Provides DynamoDB-specific fluent configuration for a configured secondary index on a typed
///     entity builder.
/// </summary>
/// <typeparam name="TEntity">The entity type that owns the configured secondary index.</typeparam>
public class DynamoSecondaryIndexBuilder<TEntity>(IndexBuilder<TEntity> indexBuilder)
    : DynamoSecondaryIndexBuilder(indexBuilder)
    where TEntity : class
{
    /// <summary>Gets the underlying typed EF index builder used to configure this secondary index.</summary>
    public new IndexBuilder<TEntity> IndexBuilder { get; } = indexBuilder;

    /// <summary>
    ///     Marks this secondary index as projecting all attributes needed for full-entity
    ///     materialization.
    /// </summary>
    /// <returns>The same builder instance so additional configuration can be chained.</returns>
    public override DynamoSecondaryIndexBuilder<TEntity> ProjectsAll()
    {
        base.ProjectsAll();
        return this;
    }
}
