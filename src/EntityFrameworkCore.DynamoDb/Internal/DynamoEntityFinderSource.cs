using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Internal;

/// <summary>Creates DynamoDB entity finders for EF Core find operations.</summary>
internal sealed class DynamoEntityFinderSource : IEntityFinderSource
{
    private readonly ConcurrentDictionary<
        Type, Func<IStateManager, IDbSetSource, IDbSetCache, IEntityType, IEntityFinder>> _cache =
        new();

    /// <summary>Creates a finder for the given entity type.</summary>
    public IEntityFinder Create(
        IStateManager stateManager,
        IDbSetSource setSource,
        IDbSetCache setCache,
        IEntityType type)
        => _cache.GetOrAdd(
            type.ClrType,
            static clrType
                => (Func<IStateManager, IDbSetSource, IDbSetCache, IEntityType, IEntityFinder>)
                typeof(DynamoEntityFinderSource).GetMethod(
                        nameof(CreateConstructor),
                        BindingFlags.NonPublic | BindingFlags.Static)!
                    .MakeGenericMethod(clrType)
                    .Invoke(null, null)!)(stateManager, setSource, setCache, type);

    private static Func<IStateManager, IDbSetSource, IDbSetCache, IEntityType, IEntityFinder>
        CreateConstructor<TEntity>() where TEntity : class
        => static (stateManager, setSource, setCache, entityType)
            => new DynamoEntityFinder<TEntity>(stateManager, setSource, setCache, entityType);
}
