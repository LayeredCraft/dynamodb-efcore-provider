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
    /// <summary>Builds a write plan for the given entity type.</summary>
    public EntityWritePlan BuildPlan(
        IEntityType entityType,
        Func<IProperty, Func<IUpdateEntry, AttributeValue>> createPropertySerializer)
    {
        var properties = entityType
            .GetProperties()
            .Where(static p => !(p.IsShadowProperty() && p.IsKey()))
            // Runtime-only properties are populated by query/runtime pipelines and must never be
            // serialized to DynamoDB.
            .Where(static p => !p.IsRuntimeOnly())
            .ToList();

        var propertyWriters = new List<PropertyWriteAction>(properties.Count);
        var propertySerializers =
            new Dictionary<IProperty, Func<IUpdateEntry, AttributeValue>>(
                properties.Count,
                ReferenceEqualityComparer.Instance);

        foreach (var p in properties)
        {
            var serializer = createPropertySerializer(p);
            propertyWriters.Add(new PropertyWriteAction(p.GetAttributeName(), serializer));
            propertySerializers[p] = serializer;
        }

        var complexProperties = entityType.GetComplexProperties().ToList();

        return new EntityWritePlan(propertyWriters, propertySerializers, complexProperties);
    }
}

internal readonly record struct PropertyWriteAction(
    string AttributeName,
    Func<IUpdateEntry, AttributeValue> Serialize);

internal sealed class EntityWritePlan(
    List<PropertyWriteAction> propertyWriters,
    Dictionary<IProperty, Func<IUpdateEntry, AttributeValue>> propertySerializers,
    List<IComplexProperty> complexProperties)
{
    /// <summary>Serializes the current value of a single tracked property for the update path.</summary>
    internal AttributeValue SerializeProperty(IUpdateEntry entry, IProperty property)
    {
        if (!propertySerializers.TryGetValue(property, out var serializer))
            throw new InvalidOperationException(
                $"No serializer was built for property "
                + $"'{property.DeclaringType?.DisplayName()}.{property.Name}'. "
                + "Shadow key properties are not serialized.");

        return serializer(entry);
    }

    /// <summary>Serializes the full entity entry into a DynamoDB item dictionary.</summary>
    public Dictionary<string, AttributeValue> Serialize(IUpdateEntry entry)
    {
        var result = new Dictionary<string, AttributeValue>(
            propertyWriters.Count + complexProperties.Count,
            StringComparer.Ordinal);

        foreach (var writer in propertyWriters)
            result[writer.AttributeName] = writer.Serialize(entry);

        if (complexProperties.Count == 0)
            return result;

        // Complex type values are embedded in the owning entity — read via the CLR getter
        // rather than the change tracker, which does not track complex types separately.
        var entity = ((InternalEntityEntry)entry).Entity;

        foreach (var cp in complexProperties)
        {
            var value = cp.GetGetter().GetClrValue(entity);
            result[((IReadOnlyComplexProperty)cp).GetAttributeName()] =
                SerializeComplexProperty(value, cp);
        }

        return result;
    }

    /// <summary>
    ///     Serializes a complex property value to an <see cref="AttributeValue" /> (M map or L list).
    ///     Also used by the PartiQL UPDATE path when building SET clause parameters.
    /// </summary>
    internal static AttributeValue SerializeComplexProperty(object? value, IComplexProperty cp)
    {
        if (value is null)
            return new AttributeValue { NULL = true };

        if (cp.IsCollection)
        {
            var elements = new List<AttributeValue>();
            foreach (var element in (IEnumerable)value)
            {
                if (element is null)
                {
                    elements.Add(new AttributeValue { NULL = true });
                    continue;
                }

                var map = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
                SerializeComplexTypeIntoMap(element, cp.ComplexType, map);
                elements.Add(new AttributeValue { M = map });
            }

            return new AttributeValue { L = elements };
        }
        else
        {
            var map = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
            SerializeComplexTypeIntoMap(value, cp.ComplexType, map);
            return new AttributeValue { M = map };
        }
    }

    /// <summary>Recursively serializes a complex type instance into a DynamoDB attribute map.</summary>
    private static void SerializeComplexTypeIntoMap(
        object instance,
        IComplexType complexType,
        Dictionary<string, AttributeValue> map)
    {
        foreach (var property in complexType.GetProperties())
        {
            if (property.IsRuntimeOnly())
                continue;

            var rawValue = property.GetGetter().GetClrValue(instance);
            map[property.GetAttributeName()] = SerializeScalarPropertyValue(rawValue, property);
        }

        foreach (var nestedCp in complexType.GetComplexProperties())
        {
            var nestedValue = nestedCp.GetGetter().GetClrValue(instance);
            map[((IReadOnlyComplexProperty)nestedCp).GetAttributeName()] =
                SerializeComplexProperty(nestedValue, nestedCp);
        }
    }

    /// <summary>
    ///     Serializes a single scalar property value using boxing + the property's type mapping.
    ///     Used only on the complex-type path; root entity scalar properties use the typed delegates
    ///     compiled at plan-build time.
    /// </summary>
    private static AttributeValue SerializeScalarPropertyValue(object? value, IProperty property)
    {
        if (value is null)
            return new AttributeValue { NULL = true };

        var converter = property.GetTypeMapping().Converter;
        var providerValue = converter != null ? converter.ConvertToProvider(value) : value;

        if (providerValue is null)
            return new AttributeValue { NULL = true };

        return DynamoWireValueConversion
            .ConvertProviderValueToAttributeValue<object>(providerValue);
    }
}
