using System.Collections.ObjectModel;
using LayeredCraft.EntityFrameworkCore.DynamoDb.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;

/// <summary>
/// Resolves EF Core type mappings for DynamoDB provider and collection shapes.
/// Primitive provider types map directly; other CLR types rely on EF Core value converter composition.
/// </summary>
/// <param name="dependencies">Core services required by <see cref="TypeMappingSource"/>.</param>
public class DynamoTypeMappingSource(TypeMappingSourceDependencies dependencies)
    : TypeMappingSource(dependencies)
{
    private static readonly ValueConverter<ReadOnlyMemory<byte>, byte[]>
        ReadOnlyMemoryByteConverter =
            new(value => value.ToArray(), value => new ReadOnlyMemory<byte>(value));

    private static readonly ValueComparer<ReadOnlyMemory<byte>> ReadOnlyMemoryByteComparer =
        new ReadOnlyMemoryByteValueComparer();

    /// <summary>
    ///     Resolves mapping for a property and propagates element mapping metadata for collection
    ///     properties.
    /// </summary>
    /// <param name="property">The model property to map.</param>
    /// <returns>The resolved mapping, or <see langword="null" /> if no mapping is available.</returns>
    public override CoreTypeMapping? FindMapping(IProperty property)
    {
        var mapping = base.FindMapping(property);

        if (mapping?.ElementTypeMapping != null
            && property.GetElementType() is IMutableElementType mutableElementType)
            mutableElementType.SetTypeMapping(mapping.ElementTypeMapping);

        return mapping;
    }

    /// <summary>Resolves mapping for an element type and composes any configured value converter.</summary>
    /// <param name="elementType">The element type metadata to map.</param>
    /// <returns>The resolved mapping, or <see langword="null" /> if no mapping is available.</returns>
    public override CoreTypeMapping? FindMapping(IElementType elementType)
    {
        var mapping = FindMapping(new TypeMappingInfo(elementType.ClrType));
        if (mapping == null)
            return null;

        var valueConverter = elementType.GetValueConverter();
        if (valueConverter == null)
            return mapping;

        return mapping.WithComposedConverter(valueConverter);
    }

    /// <summary>
    ///     Resolves mappings for DynamoDB primitives and supported collection types. Returns
    ///     <see langword="null" /> for unsupported shapes so EF Core can compose converter-based mappings.
    /// </summary>
    /// <param name="mappingInfo">The type mapping lookup information.</param>
    /// <returns>The resolved mapping, or <see langword="null" /> when no mapping can be created.</returns>
    protected override CoreTypeMapping? FindMapping(in TypeMappingInfo mappingInfo)
    {
        var clrType = mappingInfo.ClrType;
        if (clrType == null)
            return null;

        var nonNullableType = Nullable.GetUnderlyingType(clrType) ?? clrType;

        if (clrType == typeof(ReadOnlyMemory<byte>))
            return new DynamoTypeMapping(clrType, ReadOnlyMemoryByteComparer).WithComposedConverter(
                ReadOnlyMemoryByteConverter,
                ReadOnlyMemoryByteComparer);

        // Map ONLY wire primitives that AttributeValue directly supports
        // EF Core will automatically compose converters for non-primitive types:
        // - int, short, byte → long (via EF Core converter)
        // - float → double (via EF Core converter)
        // - DateTime, DateTimeOffset → string (via EF Core converter)
        // - Guid → string (via EF Core converter)
        // - Enums → int/long/string (via EF Core converter)
        if (IsPrimitiveType(nonNullableType))
            return new DynamoTypeMapping(clrType);

        if (TryCreateCollectionMapping(mappingInfo, clrType, out var collectionMapping))
            return collectionMapping;

        // Return null for all other types - EF Core will compose converters
        return null;
    }

    /// <summary>Resolves type mapping for a collection element type.</summary>
    /// <param name="elementType">The CLR element type.</param>
    /// <returns>The element mapping, or <see langword="null" /> when unavailable.</returns>
    private CoreTypeMapping? FindElementMapping(Type elementType)
    {
        var typeMappingInfo = new TypeMappingInfo(elementType);
        return base.FindMapping(typeMappingInfo) ?? FindMapping(typeMappingInfo);
    }

    /// <summary>Attempts to build a mapping for supported collection shapes.</summary>
    /// <param name="mappingInfo">The lookup context for mapping resolution.</param>
    /// <param name="clrType">The CLR collection type to map.</param>
    /// <param name="mapping">The resolved collection mapping when successful.</param>
    /// <returns><see langword="true" /> when a mapping is created; otherwise <see langword="false" />.</returns>
    private bool TryCreateCollectionMapping(
        in TypeMappingInfo mappingInfo,
        Type clrType,
        out CoreTypeMapping? mapping)
    {
        mapping = null;

        if (TryGetDictionaryValueType(
            clrType,
            out var dictionaryValueType,
            out var isReadOnlyDictionary))
        {
            var valueMapping =
                mappingInfo.ElementTypeMapping ?? FindElementMapping(dictionaryValueType);
            if (valueMapping == null)
                return false;

            var comparer = CreateDictionaryComparer(
                clrType,
                dictionaryValueType,
                valueMapping.Comparer,
                isReadOnlyDictionary);
            mapping = new DynamoTypeMapping(clrType, comparer).WithComposedConverter(
                null,
                comparer,
                elementMapping: valueMapping);
            return true;
        }

        if (TryGetSetElementType(clrType, out var setElementType))
        {
            var elementMapping =
                mappingInfo.ElementTypeMapping ?? FindElementMapping(setElementType);
            if (elementMapping == null)
                return false;

            if (!IsSupportedSetElementProviderType(elementMapping))
                throw new InvalidOperationException(
                    $"DynamoDB set mapping for '{clrType}' requires element provider type string, numeric, or byte[].");

            var comparer = CreateSetComparer(clrType, setElementType, elementMapping.Comparer);
            mapping = new DynamoTypeMapping(clrType, comparer).WithComposedConverter(
                null,
                comparer,
                elementMapping: elementMapping);
            return true;
        }

        if (TryGetListElementType(clrType, out var listElementType))
        {
            var elementMapping =
                mappingInfo.ElementTypeMapping ?? FindElementMapping(listElementType);
            if (elementMapping == null)
                return false;

            var comparer = CreateListComparer(clrType, listElementType, elementMapping.Comparer);
            mapping = new DynamoTypeMapping(clrType, comparer).WithComposedConverter(
                null,
                comparer,
                elementMapping: elementMapping);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Determines if a type is a wire primitive that DynamoDB's AttributeValue natively supports.
    /// </summary>
    private static bool IsPrimitiveType(Type type)
        => type == typeof(string) // AttributeValue.S
            || type == typeof(bool) // AttributeValue.BOOL
            || type == typeof(int) // AttributeValue.N
            || type == typeof(short) // AttributeValue.N
            || type == typeof(byte) // AttributeValue.N
            || type == typeof(long) // AttributeValue.N (primary integer wire type)
            || type == typeof(float) // AttributeValue.N
            || type == typeof(double) // AttributeValue.N (floating point wire type)
            || type == typeof(decimal) // AttributeValue.N (high precision wire type)
            || type == typeof(byte[]); // AttributeValue.B (binary wire type)

    /// <summary>Detects list-like CLR types and returns their element type.</summary>
    /// <param name="clrType">The CLR type to inspect.</param>
    /// <param name="elementType">The discovered element type when successful.</param>
    /// <returns><see langword="true" /> when the type is list-like; otherwise <see langword="false" />.</returns>
    private static bool TryGetListElementType(Type clrType, out Type elementType)
    {
        elementType = null!;
        if (clrType == typeof(byte[]))
            return false;

        if (clrType.IsArray)
        {
            var arrayElementType = clrType.GetElementType();
            if (arrayElementType == null)
                return false;

            elementType = arrayElementType;
            return true;
        }

        if (!clrType.IsGenericType)
            return false;

        var genericTypeDefinition = clrType.GetGenericTypeDefinition();
        if (genericTypeDefinition == typeof(List<>)
            || genericTypeDefinition == typeof(IList<>)
            || genericTypeDefinition == typeof(IReadOnlyList<>))
        {
            elementType = clrType.GetGenericArguments()[0];
            return true;
        }

        return false;
    }

    /// <summary>Detects dictionary CLR types with string keys and returns their value type.</summary>
    /// <param name="clrType">The CLR type to inspect.</param>
    /// <param name="valueType">The dictionary value type when successful.</param>
    /// <returns>
    ///     <see langword="true" /> when the type is a supported dictionary; otherwise
    ///     <see langword="false" />.
    /// </returns>
    private static bool TryGetDictionaryValueType(
        Type clrType,
        out Type valueType,
        out bool isReadOnlyDictionary)
    {
        valueType = null!;
        isReadOnlyDictionary = false;
        if (!clrType.IsGenericType)
            return false;

        var genericTypeDefinition = clrType.GetGenericTypeDefinition();
        if (genericTypeDefinition != typeof(Dictionary<,>)
            && genericTypeDefinition != typeof(IDictionary<,>)
            && genericTypeDefinition != typeof(IReadOnlyDictionary<,>)
            && genericTypeDefinition != typeof(ReadOnlyDictionary<,>))
            return false;

        var genericArguments = clrType.GetGenericArguments();
        if (genericArguments[0] != typeof(string))
            throw new InvalidOperationException(
                $"DynamoDB dictionary mapping requires string keys, but '{clrType}' uses '{genericArguments[0]}'.");

        valueType = genericArguments[1];
        isReadOnlyDictionary = genericTypeDefinition == typeof(ReadOnlyDictionary<,>);
        return true;
    }

    /// <summary>Detects set CLR types and returns their element type.</summary>
    /// <param name="clrType">The CLR type to inspect.</param>
    /// <param name="elementType">The set element type when successful.</param>
    /// <returns>
    ///     <see langword="true" /> when the type is a supported set; otherwise
    ///     <see langword="false" />.
    /// </returns>
    private static bool TryGetSetElementType(Type clrType, out Type elementType)
    {
        elementType = null!;
        if (!clrType.IsGenericType)
            return false;

        var genericTypeDefinition = clrType.GetGenericTypeDefinition();
        if (genericTypeDefinition != typeof(HashSet<>) && genericTypeDefinition != typeof(ISet<>))
            return false;

        elementType = clrType.GetGenericArguments()[0];
        return true;
    }

    /// <summary>Validates that a set element maps to a DynamoDB-supported provider type.</summary>
    /// <param name="elementMapping">The element type mapping to validate.</param>
    /// <returns><see langword="true" /> when the provider type is supported for sets.</returns>
    private static bool IsSupportedSetElementProviderType(CoreTypeMapping elementMapping)
    {
        var providerClrType = elementMapping.Converter?.ProviderClrType ?? elementMapping.ClrType;
        var nonNullableProviderType =
            Nullable.GetUnderlyingType(providerClrType) ?? providerClrType;

        return nonNullableProviderType == typeof(string)
            || nonNullableProviderType == typeof(byte[])
            || IsNumericType(nonNullableProviderType);
    }

    /// <summary>Determines whether a CLR type is numeric.</summary>
    /// <param name="type">The CLR type to inspect.</param>
    /// <returns><see langword="true" /> when the type is numeric; otherwise <see langword="false" />.</returns>
    private static bool IsNumericType(Type type)
        => type == typeof(byte)
            || type == typeof(short)
            || type == typeof(int)
            || type == typeof(long)
            || type == typeof(float)
            || type == typeof(double)
            || type == typeof(decimal);

    /// <summary>Creates a value comparer for list-like collection types.</summary>
    /// <param name="collectionType">The CLR list type.</param>
    /// <param name="elementType">The CLR list element type.</param>
    /// <param name="elementComparer">The comparer for list elements.</param>
    /// <returns>A comparer for the list type.</returns>
    private static ValueComparer CreateListComparer(
        Type collectionType,
        Type elementType,
        ValueComparer elementComparer)
    {
        var typeToInstantiate = FindCollectionTypeToInstantiate(collectionType, elementType);
        var nullableUnderlyingType = Nullable.GetUnderlyingType(elementType);
        var isNullableValueType = nullableUnderlyingType != null;
        var comparerType =
            isNullableValueType
                ?
                typeof(ListOfNullableValueTypesComparer<,>).MakeGenericType(
                    typeToInstantiate,
                    nullableUnderlyingType!)
                : elementType.IsValueType
                    ? typeof(ListOfValueTypesComparer<,>).MakeGenericType(
                        typeToInstantiate,
                        elementType)
                    : typeof(ListOfReferenceTypesComparer<,>).MakeGenericType(
                        typeToInstantiate,
                        elementType);

        return (ValueComparer)Activator.CreateInstance(comparerType, elementComparer)!;
    }

    private static Type FindCollectionTypeToInstantiate(Type collectionType, Type elementType)
    {
        if (collectionType.IsArray)
            return collectionType;

        var listOfT = typeof(List<>).MakeGenericType(elementType);
        if (!collectionType.IsAssignableFrom(listOfT))
            return collectionType;

        if (collectionType.IsAbstract)
            return listOfT;

        var constructor = collectionType.GetConstructor(Type.EmptyTypes);
        return constructor?.IsPublic == true ? collectionType : listOfT;
    }

    /// <summary>Creates a value comparer for string-keyed dictionary types.</summary>
    /// <param name="dictionaryType">The CLR dictionary type.</param>
    /// <param name="valueType">The CLR dictionary value type.</param>
    /// <param name="valueComparer">The comparer for dictionary values.</param>
    /// <returns>A comparer for the dictionary type.</returns>
    private static ValueComparer CreateDictionaryComparer(
        Type dictionaryType,
        Type valueType,
        ValueComparer valueComparer,
        bool readOnly)
        => (ValueComparer)Activator.CreateInstance(
            typeof(StringDictionaryValueComparer<,>).MakeGenericType(dictionaryType, valueType),
            valueComparer,
            readOnly)!;

    /// <summary>Creates a value comparer for set types.</summary>
    /// <param name="setType">The CLR set type.</param>
    /// <param name="elementType">The CLR set element type.</param>
    /// <param name="elementComparer">The comparer for set elements.</param>
    /// <returns>A comparer for the set type.</returns>
    private static ValueComparer CreateSetComparer(
        Type setType,
        Type elementType,
        ValueComparer elementComparer)
        => (ValueComparer)Activator.CreateInstance(
            typeof(SetValueComparer<,>).MakeGenericType(setType, elementType),
            elementComparer)!;

    private sealed class ReadOnlyMemoryByteValueComparer() : ValueComparer<ReadOnlyMemory<byte>>(
        (left, right) => Compare(left, right),
        value => ComputeHash(value),
        value => SnapshotMemory(value))
    {
        private static bool Compare(ReadOnlyMemory<byte> left, ReadOnlyMemory<byte> right)
            => left.Span.SequenceEqual(right.Span);

        private static int ComputeHash(ReadOnlyMemory<byte> value)
        {
            var hashCode = new HashCode();
            foreach (var b in value.Span)
                hashCode.Add(b);

            return hashCode.ToHashCode();
        }

        private static ReadOnlyMemory<byte> SnapshotMemory(ReadOnlyMemory<byte> value)
            => new(value.ToArray());
    }
}
