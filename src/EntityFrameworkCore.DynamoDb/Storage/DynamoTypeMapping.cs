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
public class DynamoTypeMapping : CoreTypeMapping
{
    internal DynamoValueReaderWriter? ReaderWriter { get; }

    /// <summary>Creates a mapping for the given CLR type.</summary>
    public DynamoTypeMapping(
        Type clrType,
        ValueComparer? comparer = null,
        ValueComparer? keyComparer = null) : base(
        new CoreTypeMappingParameters(clrType, null, comparer, keyComparer))
        => ReaderWriter = CreateReaderWriter(Parameters);

    /// <summary>Creates a mapping from a fully-specified EF Core mapping parameter set.</summary>
    protected DynamoTypeMapping(CoreTypeMappingParameters parameters) : base(parameters)
        => ReaderWriter = CreateReaderWriter(parameters);

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

    internal virtual bool CanSerialize => ReaderWriter != null;

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
    ///     The mapping remains model-value-facing; any EF Core value converter is already composed
    ///     into the underlying reader/writer.
    /// </remarks>
    internal virtual AttributeValue CreateAttributeValue(object? value)
    {
        if (value == null)
            return new AttributeValue { NULL = true };

        return ReaderWriter?.WriteAsObject(value)
            ?? throw new NotSupportedException(
                $"CLR type '{ClrType.Name}' is not supported for DynamoDB AttributeValue serialization.");
    }

    /// <summary>
    /// Generates a SQL literal for a constant value in PartiQL.
    /// </summary>
    public virtual string GenerateConstant(object? value)
    {
        if (value == null)
            return "NULL";

        return ReaderWriter?.ToPartiQlLiteralAsObject(value)
            ?? throw new NotSupportedException(
                $"CLR type '{ClrType.Name}' is not supported for PartiQL constant generation.");
    }

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
