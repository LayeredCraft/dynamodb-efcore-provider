using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EntityFrameworkCore.DynamoDb.Metadata.Builders;

/// <summary>Provides DynamoDB-specific fluent configuration for a configured secondary index.</summary>
public class DynamoSecondaryIndexBuilder(IndexBuilder indexBuilder)
{
    /// <summary>Gets the underlying EF index builder used to configure this secondary index.</summary>
    public virtual IndexBuilder IndexBuilder { get; } = indexBuilder;

    /// <summary>Configures the DynamoDB secondary index name used for PartiQL index targeting.</summary>
    /// <param name="name">The DynamoDB secondary index name, or <see langword="null" /> to clear it.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public virtual DynamoSecondaryIndexBuilder HasSecondaryIndexName(string? name)
    {
        IndexBuilder.HasSecondaryIndexName(name);
        return this;
    }

    /// <summary>Configures whether this EF index maps to a global or local DynamoDB secondary index.</summary>
    /// <param name="kind">The secondary index kind, or <see langword="null" /> to clear it.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public virtual DynamoSecondaryIndexBuilder HasSecondaryIndexKind(DynamoSecondaryIndexKind? kind)
    {
        IndexBuilder.HasSecondaryIndexKind(kind);
        return this;
    }

    /// <summary>Configures the DynamoDB projection type for this secondary index.</summary>
    /// <param name="projectionType">The projection type, or <see langword="null" /> to clear it.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public virtual DynamoSecondaryIndexBuilder HasProjectionType(
        DynamoSecondaryIndexProjectionType? projectionType)
    {
        IndexBuilder.HasSecondaryIndexProjectionType(projectionType);
        return this;
    }
}

/// <summary>
///     Provides DynamoDB-specific fluent configuration for a configured secondary index on a typed
///     entity builder.
/// </summary>
public class DynamoSecondaryIndexBuilder<TEntity>(IndexBuilder<TEntity> indexBuilder)
    : DynamoSecondaryIndexBuilder(indexBuilder) where TEntity : class
{
    /// <summary>Gets the underlying typed EF index builder used to configure this secondary index.</summary>
    public new virtual IndexBuilder<TEntity> IndexBuilder { get; } = indexBuilder;

    /// <summary>Configures the DynamoDB secondary index name used for PartiQL index targeting.</summary>
    /// <param name="name">The DynamoDB secondary index name, or <see langword="null" /> to clear it.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public new virtual DynamoSecondaryIndexBuilder<TEntity> HasSecondaryIndexName(string? name)
        => (DynamoSecondaryIndexBuilder<TEntity>)base.HasSecondaryIndexName(name);

    /// <summary>Configures whether this EF index maps to a global or local DynamoDB secondary index.</summary>
    /// <param name="kind">The secondary index kind, or <see langword="null" /> to clear it.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public new virtual DynamoSecondaryIndexBuilder<TEntity> HasSecondaryIndexKind(
        DynamoSecondaryIndexKind? kind)
        => (DynamoSecondaryIndexBuilder<TEntity>)base.HasSecondaryIndexKind(kind);

    /// <summary>Configures the DynamoDB projection type for this secondary index.</summary>
    /// <param name="projectionType">The projection type, or <see langword="null" /> to clear it.</param>
    /// <returns>The same builder instance so that multiple calls can be chained.</returns>
    public new virtual DynamoSecondaryIndexBuilder<TEntity> HasProjectionType(
        DynamoSecondaryIndexProjectionType? projectionType)
        => (DynamoSecondaryIndexBuilder<TEntity>)base.HasProjectionType(projectionType);
}
