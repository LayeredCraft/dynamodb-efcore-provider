using System.Collections;
using System.Runtime.CompilerServices;
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
                + "Runtime-only properties are not serialized.");

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
            if (value is null && !cp.IsCollection)
                continue;

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
                    throw new InvalidOperationException(
                        $"Complex collection '{cp.DeclaringType.DisplayName()}.{cp.Name}' "
                        + "contains null element. Elements must be non-null complex objects.");

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

    private static readonly ConditionalWeakTable<IComplexType, ComplexTypeWritePlan>
        ComplexTypePlanCache = new();

    /// <summary>Serializes a whole complex type value to a DynamoDB map attribute.</summary>
    internal static AttributeValue SerializeComplexTypeValue(
        object? value,
        IComplexType complexType)
    {
        if (value is null)
            return new AttributeValue { NULL = true };

        var map = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);
        SerializeComplexTypeIntoMap(value, complexType, map);
        return new AttributeValue { M = map };
    }

    /// <summary>Recursively serializes a complex type instance into a DynamoDB attribute map.</summary>
    private static void SerializeComplexTypeIntoMap(
        object instance,
        IComplexType complexType,
        Dictionary<string, AttributeValue> map)
        => ComplexTypePlanCache
            .GetValue(complexType, static type => ComplexTypeWritePlan.Create(type))
            .Serialize(instance, map);

    /// <summary>Builds and executes serializers for members of a complex type.</summary>
    private sealed class ComplexTypeWritePlan(
        List<ComplexScalarWriteAction> scalarWriters,
        List<ComplexPropertyWriteAction> complexWriters)
    {
        /// <summary>Creates a write plan for the given complex type.</summary>
        public static ComplexTypeWritePlan Create(IComplexType complexType)
        {
            var scalarWriters = new List<ComplexScalarWriteAction>();
            foreach (var property in complexType.GetProperties())
            {
                if (property.IsRuntimeOnly())
                    continue;

                scalarWriters.Add(
                    new ComplexScalarWriteAction(
                        property.GetAttributeName(),
                        property.GetGetter(),
                        DynamoWriteValueSerializerSource
                            .GetOrCreateScalarValueSerializer(property)));
            }

            var complexWriters = new List<ComplexPropertyWriteAction>();
            foreach (var complexProperty in complexType.GetComplexProperties())
                complexWriters.Add(
                    new ComplexPropertyWriteAction(
                        complexProperty.GetAttributeName(),
                        complexProperty,
                        complexProperty.GetGetter()));

            return new ComplexTypeWritePlan(scalarWriters, complexWriters);
        }

        /// <summary>Serializes the complex instance into the target map.</summary>
        public void Serialize(object instance, Dictionary<string, AttributeValue> map)
        {
            foreach (var writer in scalarWriters)
                map[writer.AttributeName] = writer.Serialize(writer.Getter.GetClrValue(instance));

            foreach (var writer in complexWriters)
                map[writer.AttributeName] = SerializeComplexProperty(
                    writer.Getter.GetClrValue(instance),
                    writer.ComplexProperty);
        }
    }

    private readonly record struct ComplexScalarWriteAction(
        string AttributeName,
        IClrPropertyGetter Getter,
        Func<object?, AttributeValue> Serialize);

    private readonly record struct ComplexPropertyWriteAction(
        string AttributeName,
        IComplexProperty ComplexProperty,
        IClrPropertyGetter Getter);

    /// <summary>Serializes a scalar property value from an untyped EF edge.</summary>
    internal static AttributeValue SerializeScalarPropertyValue(object? value, IProperty property)
        => DynamoWriteValueSerializerSource.SerializeScalarPropertyValue(value, property);
}
