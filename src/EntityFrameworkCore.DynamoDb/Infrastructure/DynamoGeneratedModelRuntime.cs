using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using EntityFrameworkCore.DynamoDb.Storage;
using EntityFrameworkCore.DynamoDb.Storage.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.DynamoDb.Infrastructure;

/// <summary>
///     Runtime support used by EF Core compiled-model generated code for the DynamoDB provider.
/// </summary>
/// <remarks>
///     <para>
///         Compiled-model generated code calls these methods to rebuild DynamoDB type mappings
///         with statically-constructed collection codecs. This keeps NativeAOT applications from
///         building codecs through <c>MakeGenericMethod</c>, whose generic instantiations cannot
///         be preserved by the linker.
///     </para>
///     <para>
///         This is generated-code-only infrastructure and is not intended as a hand-authored
///         application API.
///     </para>
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class DynamoGeneratedModelRuntime
{
    /// <summary>Primes a compiled-model list mapping with a statically-constructed codec.</summary>
    /// <typeparam name="TCollection">The mapped collection CLR type.</typeparam>
    /// <typeparam name="TElement">The collection element CLR type.</typeparam>
    /// <param name="mapping">The cloned compiled-model mapping to prime.</param>
    /// <returns>The mapping with the collection codec injected.</returns>
    public static DynamoTypeMapping
        PrimeListMapping<TCollection, TElement>(DynamoTypeMapping mapping) where TCollection : class
        => Prime(
            mapping,
            static elementReaderWriter
                => new ListDynamoValueReaderWriter<TCollection, TElement>(
                    DynamoValueReaderWriterFactory.CoerceReaderWriter<TElement>(
                        elementReaderWriter)));

    /// <summary>Primes a compiled-model dictionary mapping with a statically-constructed codec.</summary>
    /// <typeparam name="TCollection">The mapped dictionary CLR type.</typeparam>
    /// <typeparam name="TValue">The dictionary value CLR type.</typeparam>
    /// <param name="mapping">The cloned compiled-model mapping to prime.</param>
    /// <returns>The mapping with the dictionary codec injected.</returns>
    public static DynamoTypeMapping PrimeDictionaryMapping<TCollection, TValue>(
        DynamoTypeMapping mapping) where TCollection : class
        => Prime(
            mapping,
            static elementReaderWriter =>
            {
                var readOnly =
                    DynamoTypeMappingSource.TryGetDictionaryValueType(
                        typeof(TCollection),
                        out _,
                        out var readOnlyDictionary)
                    && readOnlyDictionary;
                return new DictionaryDynamoValueReaderWriter<TCollection, TValue>(
                    DynamoValueReaderWriterFactory.CoerceReaderWriter<TValue>(elementReaderWriter),
                    readOnly);
            });

    /// <summary>Primes a compiled-model set mapping with a statically-constructed codec.</summary>
    /// <typeparam name="TCollection">The mapped set CLR type.</typeparam>
    /// <typeparam name="TElement">The set element CLR type.</typeparam>
    /// <param name="mapping">The cloned compiled-model mapping to prime.</param>
    /// <returns>The mapping with the set codec injected.</returns>
    public static DynamoTypeMapping
        PrimeSetMapping<TCollection, TElement>(DynamoTypeMapping mapping) where TCollection : class
        => Prime(
            mapping,
            static elementReaderWriter
                => new SetDynamoValueReaderWriter<TCollection, TElement>(
                    DynamoValueReaderWriterFactory.CoerceReaderWriter<TElement>(
                        elementReaderWriter)));

    private static DynamoTypeMapping Prime(
        DynamoTypeMapping mapping,
        Func<DynamoValueReaderWriter, DynamoValueReaderWriter> createCodec)
    {
        if (mapping.Converter is not null)
            throw new NotSupportedException(
                "Compiled-model DynamoDB collection mappings composed with a property-level value "
                + "converter are not supported under NativeAOT. Remove the converter or avoid "
                + "NativeAOT for this model.");

        var elementMapping = mapping.ElementTypeMapping as DynamoTypeMapping
            ?? throw new InvalidOperationException(
                $"Collection mapping for '{mapping.ClrType.Name}' does not have a DynamoDB element mapping.");

        var elementReaderWriter = elementMapping.ReaderWriter
            ?? throw new InvalidOperationException(
                $"Element mapping for '{elementMapping.ClrType.Name}' has no DynamoDB value reader.");

        var codec = createCodec(elementReaderWriter);

        return mapping.WithReaderWriter(codec);
    }
}
