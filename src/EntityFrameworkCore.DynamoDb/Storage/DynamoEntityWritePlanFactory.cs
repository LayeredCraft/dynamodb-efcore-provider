using System.Collections;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Update;

#pragma warning disable EF1001 // Internal EF Core API usage

namespace EntityFrameworkCore.DynamoDb.Storage;

internal sealed class DynamoEntityWritePlanFactory
{
    public EntityWritePlan BuildPlan(
        IEntityType entityType,
        Func<IProperty, Func<IUpdateEntry, AttributeValue>> createPropertySerializer)
    {
        var properties = entityType
            .GetProperties()
            .Where(static p => !(p.IsShadowProperty() && p.IsKey()))
            .ToList();

        var propertyWriters = new List<PropertyWriteAction>(properties.Count);
        var propertySerializers =
            new Dictionary<IProperty, Func<IUpdateEntry, AttributeValue>>(properties.Count);

        foreach (var p in properties)
        {
            var serializer = createPropertySerializer(p);
            propertyWriters.Add(new PropertyWriteAction(p.GetAttributeName(), serializer));
            propertySerializers[p] = serializer;
        }

        var ownedNavigations = entityType
            .GetNavigations()
            .Where(static n => !n.IsOnDependent && n.TargetEntityType.IsOwned())
            .ToList();

        return new EntityWritePlan(propertyWriters, propertySerializers, ownedNavigations);
    }
}

internal readonly record struct PropertyWriteAction(
    string AttributeName,
    Func<IUpdateEntry, AttributeValue> Serialize);

internal sealed class EntityWritePlan(
    List<PropertyWriteAction> propertyWriters,
    Dictionary<IProperty, Func<IUpdateEntry, AttributeValue>> propertySerializers,
    List<INavigation> ownedNavigations)
{
    internal AttributeValue SerializeProperty(IUpdateEntry entry, IProperty property)
    {
        if (!propertySerializers.TryGetValue(property, out var serializer))
            throw new InvalidOperationException(
                $"No serializer was built for property "
                + $"'{property.DeclaringType?.DisplayName()}.{property.Name}'. "
                + "Shadow key properties are not serialized.");

        return serializer(entry);
    }

    public Dictionary<string, AttributeValue> Serialize(
        IUpdateEntry entry,
        DynamoEntityItemSerializerSource source)
    {
        var result = new Dictionary<string, AttributeValue>(
            propertyWriters.Count + ownedNavigations.Count,
            StringComparer.Ordinal);

        foreach (var writer in propertyWriters)
            result[writer.AttributeName] = writer.Serialize(entry);

        if (ownedNavigations.Count == 0)
            return result;

        var stateManager = ((InternalEntityEntry)entry).StateManager;

        foreach (var nav in ownedNavigations)
        {
            var navValue = entry.GetCurrentValue(nav);
            if (navValue is null)
                continue;

            var attributeName = nav.TargetEntityType.GetContainingAttributeName() ?? nav.Name;

            if (nav.IsCollection)
            {
                var elements = new List<AttributeValue>();
                if (navValue is IEnumerable collection)
                    foreach (var element in collection)
                    {
                        if (element is null)
                            continue;
                        var ownedEntry = stateManager.TryGetEntry(element, nav.TargetEntityType);
                        if (ownedEntry is not null)
                            elements.Add(
                                new AttributeValue
                                {
                                    M = source.BuildItemFromOwnedEntry(ownedEntry),
                                });
                    }

                result[attributeName] = new AttributeValue { L = elements };
            }
            else
            {
                var ownedEntry = stateManager.TryGetEntry(navValue, nav.TargetEntityType);
                if (ownedEntry is not null)
                    result[attributeName] = new AttributeValue
                    {
                        M = source.BuildItemFromOwnedEntry(ownedEntry),
                    };
            }
        }

        return result;
    }
}
