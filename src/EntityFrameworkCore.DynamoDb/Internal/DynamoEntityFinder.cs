using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Internal;

/// <summary>Wraps EF Core's default entity finder while blocking synchronous find operations.</summary>
internal sealed class DynamoEntityFinder<TEntity> : IEntityFinder<TEntity> where TEntity : class
{
    private readonly EntityFinder<TEntity> _inner;

    /// <summary>Creates a finder for the specified entity type.</summary>
    public DynamoEntityFinder(
        IStateManager stateManager,
        IDbSetSource setSource,
        IDbSetCache setCache,
        IEntityType entityType)
        => _inner = new EntityFinder<TEntity>(stateManager, setSource, setCache, entityType);

    /// <summary>Throws because synchronous DynamoDB find is not supported.</summary>
    public TEntity? Find(object?[]? keyValues) => throw CreateSyncFindException();

    /// <summary>Throws because synchronous DynamoDB find is not supported.</summary>
    object? IEntityFinder.Find(object?[]? keyValues) => throw CreateSyncFindException();

    /// <summary>Finds a tracked entity entry for a single-property key.</summary>
    public InternalEntityEntry? FindEntry<TKey>(TKey keyValue) => _inner.FindEntry(keyValue);

    /// <summary>Finds a tracked entity entry by property value.</summary>
    public InternalEntityEntry? FindEntry<TProperty>(IProperty property, TProperty propertyValue)
        => _inner.FindEntry(property, propertyValue);

    /// <summary>Gets tracked entity entries by property value.</summary>
    public IEnumerable<InternalEntityEntry> GetEntries<TProperty>(
        IProperty property,
        TProperty propertyValue)
        => _inner.GetEntries(property, propertyValue);

    /// <summary>Finds a tracked entity entry by key values.</summary>
    public InternalEntityEntry? FindEntry(IEnumerable<object?> keyValues)
        => _inner.FindEntry(keyValues);

    /// <summary>Finds a tracked entity entry by property values.</summary>
    public InternalEntityEntry? FindEntry(
        IEnumerable<IProperty> properties,
        IEnumerable<object?> propertyValues)
        => _inner.FindEntry(properties, propertyValues);

    /// <summary>Gets tracked entity entries by property values.</summary>
    public IEnumerable<InternalEntityEntry> GetEntries(
        IEnumerable<IProperty> properties,
        IEnumerable<object?> propertyValues)
        => _inner.GetEntries(properties, propertyValues);

    /// <summary>Finds an entity asynchronously by primary key.</summary>
    public ValueTask<TEntity?> FindAsync(
        object?[]? keyValues,
        CancellationToken cancellationToken = default)
        => _inner.FindAsync(keyValues, cancellationToken);

    /// <summary>Finds an entity asynchronously by primary key.</summary>
    ValueTask<object?> IEntityFinder.FindAsync(
        object?[]? keyValues,
        CancellationToken cancellationToken)
        => ((IEntityFinder)_inner).FindAsync(keyValues, cancellationToken);

    /// <summary>Loads a navigation for a tracked entry.</summary>
    public void Load(INavigation navigation, InternalEntityEntry entry, LoadOptions options)
        => _inner.Load(navigation, entry, options);

    /// <summary>Loads a navigation for a tracked entry asynchronously.</summary>
    public Task LoadAsync(
        INavigation navigation,
        InternalEntityEntry entry,
        LoadOptions options,
        CancellationToken cancellationToken = default)
        => _inner.LoadAsync(navigation, entry, options, cancellationToken);

    /// <summary>Builds the query used to load a navigation.</summary>
    public IQueryable<TEntity> Query(INavigation navigation, InternalEntityEntry entry)
        => _inner.Query(navigation, entry);

    /// <summary>Builds the query used to load a navigation.</summary>
    IQueryable IEntityFinder.Query(INavigation navigation, InternalEntityEntry entry)
        => ((IEntityFinder)_inner).Query(navigation, entry);

    /// <summary>Gets current database values for an entry.</summary>
    public object[]? GetDatabaseValues(InternalEntityEntry entry)
        => _inner.GetDatabaseValues(entry);

    /// <summary>Gets current database values for an entry asynchronously.</summary>
    public Task<object[]?> GetDatabaseValuesAsync(
        InternalEntityEntry entry,
        CancellationToken cancellationToken = default)
        => _inner.GetDatabaseValuesAsync(entry, cancellationToken);

    private static InvalidOperationException CreateSyncFindException()
        => new(
            "Synchronous Find is not supported by the DynamoDB provider. Use FindAsync instead.");
}
