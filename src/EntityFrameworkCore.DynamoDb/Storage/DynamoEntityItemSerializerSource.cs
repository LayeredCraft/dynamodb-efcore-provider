using System.Collections;
using System.Collections.Concurrent;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Update;

#pragma warning disable EF1001 // Internal EF Core API usage

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>
/// Builds and caches typed write plans per <see cref="IEntityType"/> for SaveChanges.
/// Each plan holds one <see cref="Func{IUpdateEntry,AttributeValue}"/> per property,
/// produced at plan-build time by dispatching on the property's provider CLR type via
/// <see cref="DynamoWriteValueSerializer" />. Write-time execution is just delegate invocation — no
/// reflection, no expression compilation, and no boxing on the hot path for well-known types.
/// </summary>
public sealed class DynamoEntityItemSerializerSource
{
    private readonly ConcurrentDictionary<IEntityType, EntityWritePlan> _cache = new();
    private readonly DynamoEntityWritePlanFactory _planFactory = new();

    // Separate cache for original-value (WHERE clause) serializers, keyed by property.
    // These read the original-or-current value via IUpdateEntry.GetOriginalValue<T> /
    // GetCurrentValue<T> rather than GetCurrentValue<T> used by the INSERT/UPDATE SET path.
    private readonly ConcurrentDictionary<IProperty, Func<IUpdateEntry, AttributeValue>>
        _originalValueCache = new();

    /// <summary>
    /// Returns the fully assembled DynamoDB item dictionary for a root <see cref="IUpdateEntry"/>.
    /// Owned sub-entries are resolved on-demand via the EF state manager, scoped to what is
    /// reachable from <paramref name="rootEntry"/> — no global owned-entries dictionary is needed.
    /// </summary>
    public Dictionary<string, AttributeValue> BuildItem(IUpdateEntry rootEntry)
        => GetOrBuildPlan(rootEntry.EntityType).Serialize(rootEntry);

    /// <summary>
    ///     Serializes the current value of <paramref name="property" /> on <paramref name="entry" />
    ///     to an <see cref="AttributeValue" /> using the pre-compiled per-type delegate. Used by the
    ///     update path to serialize scalar and collection-typed properties for UPDATE SET clauses.
    /// </summary>
    internal AttributeValue SerializeProperty(IUpdateEntry entry, IProperty property)
        => GetOrBuildPlan(entry.EntityType).SerializeProperty(entry, property);

    /// <summary>
    ///     Returns a cached delegate that reads the original (or current, if no original is tracked)
    ///     value of <paramref name="property" /> from an <see cref="IUpdateEntry" /> and serializes it to
    ///     an <see cref="AttributeValue" /> without boxing.
    /// </summary>
    /// <remarks>
    ///     Used for WHERE clause key and concurrency token parameters in UPDATE and DELETE
    ///     statements. Original values are required so the correct DynamoDB item is targeted even if the
    ///     in-memory value was touched before the write was requested. The delegate is compiled once per
    ///     property and cached.
    /// </remarks>
    internal Func<IUpdateEntry, AttributeValue>
        GetOrBuildOriginalValueSerializer(IProperty property)
        => _originalValueCache.GetOrAdd(
            property,
            DynamoWriteValueSerializer.CreateOriginalValueSerializer);

    private EntityWritePlan GetOrBuildPlan(IEntityType entityType)
        => _cache.GetOrAdd(
            entityType,
            static (type, source) => source._planFactory.BuildPlan(
                type,
                DynamoWriteValueSerializer.CreateCurrentValueSerializer),
            this);
}
