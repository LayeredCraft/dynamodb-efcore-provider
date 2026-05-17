using System.Collections;
using System.Collections.Concurrent;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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

        var complexProperties = entityType
            .GetComplexProperties()
            .Select(static cp => new ComplexPropertyWriteAction(
                ((IReadOnlyComplexProperty)cp).GetAttributeName(),
                cp,
                ComplexTypeWritePlan.GetOrBuild(cp.ComplexType)))
            .ToList();

        return new EntityWritePlan(propertyWriters, propertySerializers, complexProperties);
    }
}

internal readonly record struct PropertyWriteAction(
    string AttributeName,
    Func<IUpdateEntry, AttributeValue> Serialize);

internal readonly record struct ComplexPropertyWriteAction(
    string AttributeName,
    IComplexProperty Property,
    ComplexTypeWritePlan Plan)
{
    /// <summary>Serializes this complex property value.</summary>
    public AttributeValue Serialize(object? value) => Plan.SerializeProperty(value, Property);
}

internal sealed class ComplexTypeWritePlan(
    List<ComplexScalarWriteAction> scalarProperties,
    List<ComplexPropertyWriteAction> complexProperties)
{
    private static readonly ConcurrentDictionary<IComplexType, ComplexTypeWritePlan> Cache = new(
        ReferenceEqualityComparer.Instance);

    /// <summary>Gets or builds a cached complex type write plan.</summary>
    public static ComplexTypeWritePlan GetOrBuild(IComplexType complexType)
        => Cache.GetOrAdd(complexType, static type => Build(type));

    /// <summary>Builds a complex type write plan.</summary>
    private static ComplexTypeWritePlan Build(IComplexType complexType)
    {
        var scalarProperties = complexType
            .GetProperties()
            .Where(static p => !p.IsRuntimeOnly())
            .Select(static p => new ComplexScalarWriteAction(
                p.GetAttributeName(),
                DynamoWriteValueSerializer.CreateComplexValueSerializer(p)))
            .ToList();

        var complexProperties = complexType
            .GetComplexProperties()
            .Select(static cp => new ComplexPropertyWriteAction(
                ((IReadOnlyComplexProperty)cp).GetAttributeName(),
                cp,
                GetOrBuild(cp.ComplexType)))
            .ToList();

        return new ComplexTypeWritePlan(scalarProperties, complexProperties);
    }

    /// <summary>Serializes a complex property value to a DynamoDB attribute.</summary>
    public AttributeValue SerializeProperty(object? value, IComplexProperty property)
    {
        if (value is null)
            return new AttributeValue { NULL = true };

        if (!property.IsCollection)
            return new AttributeValue { M = SerializeElement(value) };

        var elements = new List<AttributeValue>();
        foreach (var element in (IEnumerable)value)
        {
            if (element is null)
                throw new InvalidOperationException(
                    $"Complex collection '{property.DeclaringType.DisplayName()}.{property.Name}' "
                    + "contains null element. Elements must be non-null complex objects.");

            elements.Add(new AttributeValue { M = SerializeElement(element) });
        }

        return new AttributeValue { L = elements };
    }

    /// <summary>Serializes a single complex object instance to a DynamoDB map.</summary>
    private Dictionary<string, AttributeValue> SerializeElement(object instance)
    {
        var map = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);

        foreach (var property in scalarProperties)
            map[property.AttributeName] = property.Serialize(instance);

        foreach (var complexProperty in complexProperties)
        {
            var nestedValue = complexProperty.Property.GetGetter().GetClrValue(instance);
            map[complexProperty.AttributeName] = complexProperty.Serialize(nestedValue);
        }

        return map;
    }
}

internal readonly record struct ComplexScalarWriteAction(
    string AttributeName,
    Func<object, AttributeValue> Serialize);

internal sealed class EntityWritePlan(
    List<PropertyWriteAction> propertyWriters,
    Dictionary<IProperty, Func<IUpdateEntry, AttributeValue>> propertySerializers,
    List<ComplexPropertyWriteAction> complexProperties)
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

        foreach (var complexProperty in complexProperties)
        {
            var value = complexProperty.Property.GetGetter().GetClrValue(entity);
            if (value is null && !complexProperty.Property.IsCollection)
                continue;

            result[complexProperty.AttributeName] = complexProperty.Serialize(value);
        }

        return result;
    }

    /// <summary>
    ///     Serializes a complex property value to an <see cref="AttributeValue" /> (M map or L list).
    ///     Also used by the PartiQL UPDATE path when building SET clause parameters.
    /// </summary>
    internal static AttributeValue SerializeComplexProperty(object? value, IComplexProperty cp)
        => ComplexTypeWritePlan.GetOrBuild(cp.ComplexType).SerializeProperty(value, cp);

    /// <summary>
    ///     Serializes a single scalar property value using the property's type mapping. Used only on
    ///     the complex-type path; root entity scalar properties use typed delegates compiled at
    ///     plan-build time.
    /// </summary>
    internal static AttributeValue SerializeScalarPropertyValue(object? value, IProperty property)
    {
        if (value is null)
            return new AttributeValue { NULL = true };

        var typeMapping = property.GetTypeMapping();
        var converter = typeMapping.Converter;

        // Property-level converters define the store shape for the entire property, even when the
        // model CLR type itself looks like a collection.
        if (converter is not null)
        {
            var providerValue = converter.ConvertToProvider(value);
            return DynamoWriteValueSerializer.ConvertProviderShapeToAttributeValue(providerValue);
        }

        var clrType = property.ClrType;
        var nonNullableType = Nullable.GetUnderlyingType(clrType) ?? clrType;
        if (nonNullableType == typeof(byte[]))
            return DynamoWireValueConversion.ConvertProviderValueToAttributeValue((byte[])value);

        if (DynamoTypeMappingSource.TryGetDictionaryValueType(
            clrType,
            out var dictionaryValueType,
            out _))
        {
            var valueConverter = typeMapping.ElementTypeMapping?.Converter;
            return valueConverter is null
                ? DynamoWriteValueSerializer.SerializeDirectScalarDictionary(
                    value,
                    dictionaryValueType)
                : DynamoWriteValueSerializer.SerializeConvertedScalarDictionary(
                    value,
                    valueConverter);
        }

        if (DynamoTypeMappingSource.TryGetSetElementType(clrType, out var setElementType))
        {
            var elementConverter = typeMapping.ElementTypeMapping?.Converter;
            return elementConverter is null
                ? DynamoWriteValueSerializer.SerializeDirectScalarSet(value, setElementType)
                : DynamoWriteValueSerializer.SerializeConvertedScalarSet(value, elementConverter);
        }

        if (DynamoTypeMappingSource.TryGetListElementType(clrType, out var listElementType))
        {
            var elementConverter = typeMapping.ElementTypeMapping?.Converter;
            return elementConverter is null
                ? DynamoWriteValueSerializer.SerializeDirectScalarList(value, listElementType)
                : DynamoWriteValueSerializer.SerializeConvertedScalarList(value, elementConverter);
        }

        return DynamoWireValueConversion.ConvertProviderValueToAttributeValue(value);
    }
}
