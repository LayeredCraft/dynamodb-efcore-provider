using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using LayeredCraft.EntityFrameworkCore.DynamoDb.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;

public class DynamoTypeMappingSource(TypeMappingSourceDependencies dependencies)
    : TypeMappingSource(dependencies)
{
    private static readonly
        ConcurrentDictionary<(Type CollectionType, Type ElementType), ValueComparer>
        ListComparerCache = new();

    private static readonly
        ConcurrentDictionary<(Type CollectionType, Type ValueType, bool ReadOnly), ValueComparer>
        DictionaryComparerCache = new();

    private static readonly
        ConcurrentDictionary<(Type CollectionType, Type ElementType), ValueComparer>
        SetComparerCache = new();

    /// <summary>Resolves mapping for a property and propagates element mappings for primitive collections.</summary>
    public override CoreTypeMapping? FindMapping(IProperty property)
    {
        var mapping = base.FindMapping(property);

        if (mapping?.ElementTypeMapping != null
            && property.GetElementType() is IMutableElementType mutableElementType)
            mutableElementType.SetTypeMapping(mapping.ElementTypeMapping);

        return mapping;
    }

    /// <summary>Resolves mapping for primitive collection element metadata, including value converters.</summary>
    public override CoreTypeMapping? FindMapping(IElementType elementType)
    {
        var mapping = FindMapping(new TypeMappingInfo(elementType.ClrType));
        if (mapping == null)
            return null;

        var valueConverter = elementType.GetValueConverter();
        return valueConverter == null ? mapping : mapping.WithComposedConverter(valueConverter);
    }

    /// <summary>Resolves provider mapping for primitives and strict primitive collection shapes.</summary>
    protected override CoreTypeMapping? FindMapping(in TypeMappingInfo mappingInfo)
    {
        var clrType = mappingInfo.ClrType;
        if (clrType == null)
            return null;

        var nonNullableType = Nullable.GetUnderlyingType(clrType) ?? clrType;
        if (IsPrimitiveType(nonNullableType))
            return new DynamoTypeMapping(clrType);

        if (TryGetDictionaryValueType(
            clrType,
            out var dictionaryValueType,
            out var readOnlyDictionary))
            return FindDictionaryTypeMapping(clrType, dictionaryValueType, readOnlyDictionary);

        if (TryGetSetElementType(clrType, out var setElementType))
            return FindSetTypeMapping(clrType, setElementType);

        if (TryGetListElementType(clrType, out var listElementType))
            return FindListTypeMapping(clrType, listElementType);

        return null;
    }

    /// <summary>Builds a dictionary mapping with composed element mapping and dictionary comparer.</summary>
    private CoreTypeMapping? FindDictionaryTypeMapping(
        Type clrType,
        Type valueType,
        bool readOnlyDictionary)
    {
        var valueMapping = FindMapping(new TypeMappingInfo(valueType));
        if (valueMapping == null)
            return null;

        var valueComparer = valueMapping.Comparer ?? ValueComparer.CreateDefault(valueType, false);

        var comparer = DictionaryComparerCache.GetOrAdd(
            (clrType, valueType, readOnlyDictionary),
            key => CreateDictionaryComparer(
                key.CollectionType,
                key.ValueType,
                key.ReadOnly,
                valueComparer));

        return new DynamoTypeMapping(clrType, comparer).WithComposedConverter(
            null,
            comparer,
            elementMapping: valueMapping);
    }

    /// <summary>Builds a set mapping with composed element mapping and set comparer.</summary>
    private CoreTypeMapping? FindSetTypeMapping(Type clrType, Type elementType)
    {
        var elementMapping = FindMapping(new TypeMappingInfo(elementType));
        if (elementMapping == null)
            return null;

        var providerType = elementMapping.Converter?.ProviderClrType ?? elementType;
        if (!IsSupportedSetElementProviderType(providerType))
            throw new InvalidOperationException(
                $"DynamoDB set collection '{clrType.Name}' has element type '{elementType.Name}', "
                + "which does not map to a supported DynamoDB set wire type. Supported provider "
                + "types are string, numeric, and byte[].");

        var elementComparer = elementMapping.Comparer
            ?? ValueComparer.CreateDefault(elementType, false);

        var comparer = SetComparerCache.GetOrAdd(
            (clrType, elementType),
            key => CreateSetComparer(key.CollectionType, key.ElementType, elementComparer));

        return new DynamoTypeMapping(clrType, comparer).WithComposedConverter(
            null,
            comparer,
            elementMapping: elementMapping);
    }

    /// <summary>Builds a list mapping with composed element mapping and list comparer.</summary>
    private CoreTypeMapping? FindListTypeMapping(Type clrType, Type elementType)
    {
        var elementMapping = FindMapping(new TypeMappingInfo(elementType));
        if (elementMapping == null)
            return null;

        var elementComparer = elementMapping.Comparer
            ?? ValueComparer.CreateDefault(elementType, false);

        var comparer = ListComparerCache.GetOrAdd(
            (clrType, elementType),
            key => CreateListComparer(key.CollectionType, key.ElementType, elementComparer));

        return new DynamoTypeMapping(clrType, comparer).WithComposedConverter(
            null,
            comparer,
            elementMapping: elementMapping);
    }

    /// <summary>
    /// Creates the dictionary comparer implementation for a mapped dictionary shape.
    /// </summary>
    private static ValueComparer CreateDictionaryComparer(
        Type collectionType,
        Type valueType,
        bool readOnly,
        ValueComparer elementComparer)
    {
        var nullableUnderlyingType = Nullable.GetUnderlyingType(valueType);
        var comparerType = nullableUnderlyingType != null
            ? typeof(NullableStringDictionaryValueComparer<,>).MakeGenericType(
                collectionType,
                nullableUnderlyingType ?? valueType)
            : typeof(StringDictionaryValueComparer<,>).MakeGenericType(collectionType, valueType);

        return (ValueComparer)Activator.CreateInstance(comparerType, elementComparer, readOnly)!;
    }

    /// <summary>Creates the set comparer implementation for a mapped set shape.</summary>
    private static ValueComparer CreateSetComparer(
        Type collectionType,
        Type elementType,
        ValueComparer elementComparer)
        => (ValueComparer)Activator.CreateInstance(
            typeof(SetValueComparer<,>).MakeGenericType(collectionType, elementType),
            elementComparer)!;

    /// <summary>Creates the list comparer implementation for a mapped list shape.</summary>
    private static ValueComparer CreateListComparer(
        Type collectionType,
        Type elementType,
        ValueComparer elementComparer)
        => (ValueComparer)Activator.CreateInstance(
            typeof(ListValueComparer<,>).MakeGenericType(collectionType, elementType),
            elementComparer)!;

    /// <summary>Returns the element type when the CLR type is a supported list shape.</summary>
    internal static bool TryGetListElementType(Type clrType, out Type elementType)
    {
        if (clrType.IsArray)
        {
            elementType = clrType.GetElementType()!;
            return true;
        }

        if (!clrType.IsGenericType)
        {
            elementType = null!;
            return false;
        }

        var genericDefinition = clrType.GetGenericTypeDefinition();
        if (genericDefinition == typeof(List<>)
            || genericDefinition == typeof(IList<>)
            || genericDefinition == typeof(IReadOnlyList<>))
        {
            elementType = clrType.GetGenericArguments()[0];
            return true;
        }

        elementType = null!;
        return false;
    }

    /// <summary>Returns dictionary value type and read-only flag for supported dictionary shapes.</summary>
    internal static bool TryGetDictionaryValueType(
        Type clrType,
        out Type valueType,
        out bool readOnlyDictionary)
    {
        valueType = null!;
        readOnlyDictionary = false;

        if (!clrType.IsGenericType)
            return false;

        var genericDefinition = clrType.GetGenericTypeDefinition();
        if (genericDefinition != typeof(Dictionary<,>)
            && genericDefinition != typeof(IDictionary<,>)
            && genericDefinition != typeof(IReadOnlyDictionary<,>)
            && genericDefinition != typeof(ReadOnlyDictionary<,>))
            return false;

        var genericArguments = clrType.GetGenericArguments();
        if (genericArguments[0] != typeof(string))
            throw new InvalidOperationException(
                $"DynamoDB dictionary collection '{clrType.Name}' must use string keys.");

        valueType = genericArguments[1];
        readOnlyDictionary = genericDefinition == typeof(ReadOnlyDictionary<,>);
        return true;
    }

    /// <summary>Returns the element type when the CLR type is a supported set shape.</summary>
    internal static bool TryGetSetElementType(Type clrType, out Type elementType)
    {
        if (clrType.IsGenericType)
        {
            var genericDefinition = clrType.GetGenericTypeDefinition();
            if (genericDefinition == typeof(HashSet<>)
                || genericDefinition == typeof(ISet<>)
                || genericDefinition == typeof(IReadOnlySet<>))
            {
                elementType = clrType.GetGenericArguments()[0];
                return true;
            }
        }

        elementType = null!;
        return false;
    }

    /// <summary>Checks whether a CLR type matches any supported primitive collection shape.</summary>
    internal static bool IsSupportedPrimitiveCollectionShape(Type clrType)
        => TryGetListElementType(clrType, out _)
            || TryGetSetElementType(clrType, out _)
            || TryGetDictionaryValueType(clrType, out _, out _);

    /// <summary>
    /// Checks whether the type is a provider-level primitive wire mapping candidate.
    /// </summary>
    private static bool IsPrimitiveType(Type type)
        => type == typeof(string)
            || type == typeof(bool)
            || type == typeof(byte)
            || type == typeof(sbyte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal)
            || type == typeof(byte[]);

    /// <summary>
    ///     Checks whether a set element provider type can be represented by DynamoDB set wire
    ///     members.
    /// </summary>
    private static bool IsSupportedSetElementProviderType(Type providerType)
    {
        var nonNullableProviderType = Nullable.GetUnderlyingType(providerType) ?? providerType;
        if (nonNullableProviderType == typeof(string) || nonNullableProviderType == typeof(byte[]))
            return true;

        return nonNullableProviderType == typeof(byte)
            || nonNullableProviderType == typeof(sbyte)
            || nonNullableProviderType == typeof(short)
            || nonNullableProviderType == typeof(ushort)
            || nonNullableProviderType == typeof(int)
            || nonNullableProviderType == typeof(uint)
            || nonNullableProviderType == typeof(long)
            || nonNullableProviderType == typeof(ulong)
            || nonNullableProviderType == typeof(float)
            || nonNullableProviderType == typeof(double)
            || nonNullableProviderType == typeof(decimal);
    }
}
