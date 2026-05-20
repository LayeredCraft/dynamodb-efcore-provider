using System.Collections.Concurrent;
using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Storage.Internal;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>
///     Represents a DynamoDB type mapping that owns both EF Core conversion metadata and the
///     provider's AttributeValue/PartiQL serialization rules.
/// </summary>
/// <remarks>
///     <para>
///         Serialization is handled by <see cref="DynamoValueReaderWriter" /> instances owned
///         by each mapping. This is a DynamoDB-specific codec system — <b>not</b> EF Core's
///         <c>JsonValueReaderWriter</c> infrastructure. The wire format is DynamoDB
///         <see cref="Amazon.DynamoDBv2.Model.AttributeValue" />, not JSON, so the JSON
///         reader/writer slot in <c>CoreTypeMappingParameters</c>
///         is intentionally left unpopulated. Any <c>JsonValueReaderWriter</c> configured on a
///         property via model builder is ignored by this provider.
///     </para>
///     <para>
///         Literal generation uses <see cref="GenerateConstant(object?)" /> rather than
///         <c>RelationalTypeMapping.GenerateSqlLiteral</c>. The latter is relational-only and
///         is neither inherited by <see cref="Microsoft.EntityFrameworkCore.Storage.CoreTypeMapping" />
///         subclasses nor auto-invoked by EF Core infrastructure for non-relational providers.
///         This mirrors the pattern used by the EF Core Cosmos provider.
///     </para>
/// </remarks>
public class DynamoTypeMapping : CoreTypeMapping
{
    internal DynamoValueReaderWriter? ReaderWriter { get; }

    private readonly ConcurrentDictionary<Type, Func<object?, AttributeValue>>
        _attributeValueSerializers = new();

    private readonly ConcurrentDictionary<Type, Func<object?, string>> _literalSerializers = new();

    /// <summary>Creates a mapping for the given CLR type.</summary>
    public DynamoTypeMapping(
        Type clrType,
        ValueComparer? comparer = null,
        ValueComparer? keyComparer = null) : base(
        new CoreTypeMappingParameters(clrType, null, comparer, keyComparer))
    {
        ReaderWriter = CreateReaderWriter(Parameters);
    }

    /// <summary>Creates a mapping from a fully-specified EF Core mapping parameter set.</summary>
    protected DynamoTypeMapping(CoreTypeMappingParameters parameters) : base(parameters)
    {
        ReaderWriter = CreateReaderWriter(parameters);
    }

    /// <summary>Clones the mapping with updated parameters.</summary>
    protected override CoreTypeMapping Clone(CoreTypeMappingParameters parameters)
        => new DynamoTypeMapping(parameters);

    /// <summary>Returns a new mapping that composes the provided converter and element mapping.</summary>
    public override CoreTypeMapping WithComposedConverter(
        ValueConverter? converter,
        ValueComparer? comparer = null,
        ValueComparer? keyComparer = null,
        CoreTypeMapping? elementMapping = null,
        JsonValueReaderWriter? jsonValueReaderWriter = null)
        => new DynamoTypeMapping(
            Parameters.WithComposedConverter(
                converter,
                comparer,
                keyComparer,
                elementMapping,
                jsonValueReaderWriter));

    internal virtual bool CanWriteToAttributeValue => ReaderWriter != null;

    internal virtual bool RequiresParameterForPartiQlLiteral
        => ReaderWriter?.RequiresParameterForPartiQlLiteral == true;

    /// <summary>Creates the expression-tree fragment used to materialize a single DynamoDB value.</summary>
    internal virtual Expression CreateReadExpression(
        Expression attributeValueExpression,
        string propertyPath,
        bool required,
        IProperty? property)
        => ReaderWriter?.CreateReadExpression(
                attributeValueExpression,
                propertyPath,
                required,
                property)
            ?? throw new InvalidOperationException(
                $"No DynamoDB value reader/writer is configured for CLR type '{ClrType.Name}'.");

    /// <summary>Serializes a model CLR value to an <see cref="AttributeValue" />.</summary>
    /// <remarks>
    ///     EF exposes runtime query values to mappings as <see cref="object" />. The runtime value
    ///     serializer is the narrow adapter back into typed expression-based conversion.
    /// </remarks>
    internal virtual AttributeValue CreateAttributeValue(object? value)
        => DynamoQueryValueSerializer.CreateAttributeValue(this, _attributeValueSerializers, value);

    /// <summary>Serializes a value whose runtime/source CLR type is already known.</summary>
    internal virtual AttributeValue CreateAttributeValue(object? value, Type sourceType)
        => DynamoQueryValueSerializer.CreateAttributeValue(
            this,
            _attributeValueSerializers,
            value,
            sourceType);

    /// <summary>Builds the typed expression used by cached query/runtime serializers.</summary>
    internal virtual Expression CreateAttributeValueExpression(Expression valueExpression)
        => ReaderWriter?.CreateWriteExpression(valueExpression)
            ?? throw new NotSupportedException(
                $"CLR type '{ClrType.Name}' is not supported for DynamoDB AttributeValue serialization.");

    /// <summary>Generates a PartiQL literal for a constant value.</summary>
    /// <remarks>
    ///     EF exposes constants as boxed values. This public boundary infers the runtime source type
    ///     once, then dispatches through a cached typed expression-tree serializer.
    /// </remarks>
    public virtual string GenerateConstant(object? value)
        => value is null ? "NULL" : GenerateNonNullConstant(value);

    /// <summary>Generates a PartiQL literal for a value whose runtime/source CLR type is known.</summary>
    internal virtual string GenerateConstant(object? value, Type sourceType)
        => DynamoQueryValueSerializer.GenerateLiteral(this, _literalSerializers, value, sourceType);

    /// <summary>Builds the typed expression used by cached PartiQL literal serializers.</summary>
    internal virtual Expression CreatePartiQlLiteralExpression(Expression valueExpression)
        => ReaderWriter?.CreatePartiQlLiteralExpression(valueExpression)
            ?? throw new NotSupportedException(
                $"CLR type '{ClrType.Name}' is not supported for PartiQL constant generation.");

    /// <summary>Generates a PartiQL literal for a known non-null value.</summary>
    /// <remarks>
    ///     This override point exists for compatibility with the previous template-method shape.
    ///     The base implementation immediately resumes the typed expression-tree serializer.
    /// </remarks>
    protected virtual string GenerateNonNullConstant(object value)
        => DynamoQueryValueSerializer.GenerateLiteral(
            this,
            _literalSerializers,
            value,
            value.GetType());

    private static DynamoValueReaderWriter? CreateReaderWriter(CoreTypeMappingParameters parameters)
    {
        DynamoValueReaderWriter? elementReaderWriter = null;
        var elementMapping = parameters.ElementTypeMapping as DynamoTypeMapping;
        if (elementMapping != null)
            elementReaderWriter = elementMapping.ReaderWriter;

        // Read-only dictionary shape is a CLR-collection concern that the collection reader/writer
        // needs when materializing results back into the requested collection type.
        var readOnlyDictionary =
            DynamoTypeMappingSource.TryGetDictionaryValueType(
                parameters.ClrType,
                out _,
                out var readOnly)
            && readOnly;

        var readerWriter = DynamoValueReaderWriterFactory.Create(
            parameters.ClrType,
            elementReaderWriter,
            readOnlyDictionary);

        // Apply the converter once after the provider-level reader/writer is known so both read and
        // write paths share the same composed model <-> provider conversion behavior.
        return DynamoValueReaderWriterFactory.Compose(parameters.Converter, readerWriter);
    }
}
