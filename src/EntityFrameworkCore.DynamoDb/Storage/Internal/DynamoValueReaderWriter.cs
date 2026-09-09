using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EntityFrameworkCore.DynamoDb.Storage.Internal;

/// <summary>Converts a single mapped CLR value to and from DynamoDB wire representations.</summary>
/// <remarks>
///     Mappings own these instances so query materialization, query parameter generation, and
///     SaveChanges writes all share the same conversion rules.
/// </remarks>
internal abstract class DynamoValueReaderWriter
{
    public abstract Type ValueType { get; }

    /// <summary>
    ///     Recreates this codec inside generated query expressions without forcing query compilation
    ///     to close over a specific runtime instance.
    /// </summary>
    internal abstract Expression ConstructorExpression { get; }

    internal abstract string WireMemberName { get; }

    internal virtual bool RequiresParameterForPartiQlLiteral => false;

    internal abstract bool HasValue(AttributeValue attributeValue);

    /// <summary>
    ///     Serializes a boxed runtime value through this codec without expression-tree
    ///     construction, keeping precompiled-query execution NativeAOT-safe.
    /// </summary>
    internal abstract AttributeValue WriteBoxed(object? value);

    /// <summary>
    ///     Formats a boxed runtime value as a PartiQL literal without expression-tree
    ///     construction, keeping precompiled-query execution NativeAOT-safe.
    /// </summary>
    internal abstract string ToPartiQlLiteralBoxed(object? value);

    internal abstract object? ReadObject(
        AttributeValue attributeValue,
        string propertyPath,
        bool required,
        IProperty? property);

    internal abstract Expression CreateReadExpression(
        Expression attributeValueExpression,
        string propertyPath,
        bool required,
        IProperty? property);

    internal abstract Expression CreateWriteExpression(Expression typedValueExpression);

    internal abstract Expression CreatePartiQlLiteralExpression(Expression typedValueExpression);

    protected static Expression CreatePropertyExpression(
        Expression attributeValueExpression,
        string propertyPath,
        bool required,
        IProperty? property,
        MethodInfo methodInfo,
        Expression instanceExpression)
        => Expression.Call(
            instanceExpression,
            methodInfo,
            attributeValueExpression,
            Expression.Constant(propertyPath),
            Expression.Constant(required),
            Expression.Constant(property, typeof(IProperty)));

    protected string CreateMissingValueMessage(string propertyPath)
        => $"Required property '{propertyPath}' did not contain a value for expected DynamoDB wire member '{WireMemberName}'.";
}

/// <summary>Strongly-typed base implementation for a DynamoDB value reader/writer.</summary>
internal abstract class DynamoValueReaderWriter<TValue> : DynamoValueReaderWriter
{
    private static readonly MethodInfo ReadMethod =
        typeof(DynamoValueReaderWriter<TValue>).GetMethod(
            nameof(Read),
            [typeof(AttributeValue), typeof(string), typeof(bool), typeof(IProperty)])!;

    private static readonly MethodInfo WriteMethod =
        typeof(DynamoValueReaderWriter<TValue>).GetMethod(nameof(Write), [typeof(TValue)])!;

    private static readonly MethodInfo ToPartiQlLiteralMethod =
        typeof(DynamoValueReaderWriter<TValue>).GetMethod(
            nameof(ToPartiQlLiteral),
            [typeof(TValue)])!;

    public sealed override Type ValueType => typeof(TValue);

    internal sealed override Expression ConstructorExpression => CreateConstructorExpression();

    internal override Expression CreateReadExpression(
            Expression attributeValueExpression,
            string propertyPath,
            bool required,
            IProperty? property)
        // Recreate the codec in the expression tree so query compilation does not close over a
        // specific runtime instance while still returning the final typed CLR value directly.
        => CreatePropertyExpression(
            attributeValueExpression,
            propertyPath,
            required,
            property,
            ReadMethod,
            CreateConstructorExpression());

    public TValue Read(
        AttributeValue attributeValue,
        string propertyPath,
        bool required,
        IProperty? property)
    {
        if (!HasValue(attributeValue))
        {
            if (required)
                throw new InvalidOperationException(CreateMissingValueMessage(propertyPath));

            return default!;
        }

        return ReadValue(attributeValue, propertyPath, property);
    }

    internal sealed override object? ReadObject(
        AttributeValue attributeValue,
        string propertyPath,
        bool required,
        IProperty? property)
        => Read(attributeValue, propertyPath, required, property);

    protected abstract TValue ReadValue(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property);

    public abstract AttributeValue Write(TValue value);

    public abstract string ToPartiQlLiteral(TValue value);

    internal sealed override AttributeValue WriteBoxed(object? value)
        // Null values are filtered by the boxed serializer before dispatch unless the converter
        // handles nulls; the default write keeps nullable codecs (for example List<int?>) safe.
        => value is null ? Write(default!) : Write((TValue)value);

    internal sealed override string ToPartiQlLiteralBoxed(object? value)
        => value is null ? ToPartiQlLiteral(default!) : ToPartiQlLiteral((TValue)value);

    protected virtual Expression CreateConstructorExpression() => Expression.New(GetType());

    internal override Expression CreateWriteExpression(Expression typedValueExpression)
        => Expression.Call(Expression.Constant(this, GetType()), WriteMethod, typedValueExpression);

    internal override Expression CreatePartiQlLiteralExpression(Expression typedValueExpression)
        => Expression.Call(
            Expression.Constant(this, GetType()),
            ToPartiQlLiteralMethod,
            typedValueExpression);
}

/// <summary>Exposes the provider-level reader/writer under a composed wrapper.</summary>
/// <remarks>
///     This allows mapping construction to peel back previously composed wrappers and apply the
///     current converter exactly once, mirroring EF Core's converted reader/writer patterns.
/// </remarks>
internal interface IDynamoConvertedValueReaderWriter
{
    DynamoValueReaderWriter InnerReaderWriter { get; }

    bool ConvertsNulls { get; }
}

/// <summary>Wraps a provider-level reader/writer with an EF Core <see cref="ValueConverter" />.</summary>
internal sealed class DynamoConvertedValueReaderWriter<TModel, TProvider>(
    DynamoValueReaderWriter<TProvider> innerReaderWriter,
    ValueConverter converter) : DynamoValueReaderWriter<TModel>, IDynamoConvertedValueReaderWriter
{
    private static readonly ConstructorInfo Constructor =
        typeof(DynamoConvertedValueReaderWriter<TModel, TProvider>).GetConstructor(
            [typeof(DynamoValueReaderWriter<TProvider>), typeof(ValueConverter)])!;

    private static readonly MethodInfo ReadMethod =
        typeof(DynamoValueReaderWriter<TModel>).GetMethod(
            nameof(Read),
            [typeof(AttributeValue), typeof(string), typeof(bool), typeof(IProperty)])!;

    // These reflection lookups are tied to the abstract DynamoValueReaderWriter<TValue> contract.
    // If those signatures change, class initialization should fail rather than build invalid trees.
    private static readonly MethodInfo WriteMethod =
        typeof(DynamoValueReaderWriter<TModel>).GetMethod(nameof(Write), [typeof(TModel)])!;

    private static readonly MethodInfo ToPartiQlLiteralMethod =
        typeof(DynamoValueReaderWriter<TModel>).GetMethod(
            nameof(ToPartiQlLiteral),
            [typeof(TModel)])!;

    internal override string WireMemberName => innerReaderWriter.WireMemberName;

    internal override bool RequiresParameterForPartiQlLiteral
        => innerReaderWriter.RequiresParameterForPartiQlLiteral;

    DynamoValueReaderWriter IDynamoConvertedValueReaderWriter.InnerReaderWriter
        => innerReaderWriter;

    bool IDynamoConvertedValueReaderWriter.ConvertsNulls => converter.ConvertsNulls;

    internal override bool HasValue(AttributeValue attributeValue)
    {
        if (attributeValue is null)
            return converter.ConvertsNulls;

        return innerReaderWriter.HasValue(attributeValue)
            || (converter.ConvertsNulls && attributeValue.NULL == true);
    }

    internal override Expression CreateReadExpression(
        Expression attributeValueExpression,
        string propertyPath,
        bool required,
        IProperty? property)
        => CreatePropertyExpression(
            attributeValueExpression,
            propertyPath,
            required,
            property,
            ReadMethod,
            CreateConstructorExpression());

    // ── Object-based (boxed) fallback methods ──────────────────────────────────────
    //
    // These methods use the untyped ValueConverter API and box value types. Query read/value
    // binding and SaveChanges write paths use expression-tree APIs below instead.

    protected override TModel ReadValue(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property)
    {
        var providerValue =
            attributeValue is not null
            && attributeValue.NULL != true
            && innerReaderWriter.HasValue(attributeValue)
                ? innerReaderWriter.Read(attributeValue, propertyPath, true, property)
                : default;

        return (TModel)converter.ConvertFromProvider(providerValue)!;
    }

    public override AttributeValue Write(TModel value)
        => innerReaderWriter.Write((TProvider)converter.ConvertToProvider(value)!);

    public override string ToPartiQlLiteral(TModel value)
        => innerReaderWriter.ToPartiQlLiteral((TProvider)converter.ConvertToProvider(value)!);

    protected override Expression CreateConstructorExpression()
        // ValueConverter instances may carry state; we capture the exact converter the mapping
        // was constructed with rather than quoting it (quoting would create a new instance on
        // every expression-tree evaluation, forcing recompilation and degrading performance).
        // This is safe because ValueConverter instances are model-lifetime singletons — the
        // same lifetime as compiled queries (both are tied to the service provider).
        // NativeAOT uses DynamoAotConvertedValueReaderWriter instead; this path is only used when
        // dynamic code is available. EF Core uses the same captured-converter approach in
        // JsonConvertedValueReaderWriter (dotnet/efcore #36856).
        => Expression.New(
            Constructor,
            innerReaderWriter.ConstructorExpression,
            Expression.Constant(converter, typeof(ValueConverter)));

    internal override Expression CreateWriteExpression(Expression typedValueExpression)
        => RequiresBoxedConverterFallback(typedValueExpression)
            ? Expression.Call(
                Expression.Constant(this, GetType()),
                WriteMethod,
                ConvertToModelValueExpression(typedValueExpression))
            : innerReaderWriter.CreateWriteExpression(
                CreateProviderValueExpression(typedValueExpression));

    internal override Expression CreatePartiQlLiteralExpression(Expression typedValueExpression)
        => RequiresBoxedConverterFallback(typedValueExpression)
            ? Expression.Call(
                Expression.Constant(this, GetType()),
                ToPartiQlLiteralMethod,
                ConvertToModelValueExpression(typedValueExpression))
            : innerReaderWriter.CreatePartiQlLiteralExpression(
                CreateProviderValueExpression(typedValueExpression));

    private static Expression ConvertToModelValueExpression(Expression typedValueExpression)
        => typedValueExpression.Type == typeof(TModel)
            ? typedValueExpression
            : Expression.Convert(typedValueExpression, typeof(TModel));

    private bool RequiresBoxedConverterFallback(Expression modelValueExpression)
        => Nullable.GetUnderlyingType(
            converter.ConvertToProviderExpression.Parameters.Single().Type) is not null;

    private Expression CreateProviderValueExpression(Expression modelValueExpression)
    {
        var providerValueExpression = ReplacingExpressionVisitor.Replace(
            converter.ConvertToProviderExpression.Parameters.Single(),
            modelValueExpression,
            converter.ConvertToProviderExpression.Body);

        return providerValueExpression.Type != typeof(TProvider)
            ? Expression.Convert(providerValueExpression, typeof(TProvider))
            : providerValueExpression;
    }
}

/// <summary>
///     Composes a converter without constructing a closed generic wrapper through reflection.
/// </summary>
internal sealed class DynamoAotConvertedValueReaderWriter(
    DynamoValueReaderWriter innerReaderWriter,
    ValueConverter converter) : DynamoValueReaderWriter, IDynamoConvertedValueReaderWriter
{
    private static readonly MethodInfo ReadObjectMethod =
        typeof(DynamoValueReaderWriter).GetMethod(
            nameof(ReadObject),
            BindingFlags.Instance | BindingFlags.NonPublic)!;

    public override Type ValueType => converter.ModelClrType;

    internal override Expression ConstructorExpression
        => throw new NotSupportedException(
            "A NativeAOT converter reader is created by the compiled model.");

    internal override string WireMemberName => innerReaderWriter.WireMemberName;

    internal override bool RequiresParameterForPartiQlLiteral
        => innerReaderWriter.RequiresParameterForPartiQlLiteral;

    DynamoValueReaderWriter IDynamoConvertedValueReaderWriter.InnerReaderWriter
        => innerReaderWriter;

    bool IDynamoConvertedValueReaderWriter.ConvertsNulls => converter.ConvertsNulls;

    internal override bool HasValue(AttributeValue attributeValue)
        // Mirrors DynamoConvertedValueReaderWriter<TModel, TProvider>.HasValue so converters that
        // intentionally handle nulls behave identically on the NativeAOT path.
        => attributeValue is null
            ? converter.ConvertsNulls
            : innerReaderWriter.HasValue(attributeValue)
            || (converter.ConvertsNulls && attributeValue.NULL == true);

    internal override AttributeValue WriteBoxed(object? value)
        => innerReaderWriter.WriteBoxed(converter.ConvertToProvider(value));

    internal override string ToPartiQlLiteralBoxed(object? value)
        => innerReaderWriter.ToPartiQlLiteralBoxed(converter.ConvertToProvider(value));

    internal override object? ReadObject(
        AttributeValue attributeValue,
        string propertyPath,
        bool required,
        IProperty? property)
    {
        if (!HasValue(attributeValue))
        {
            if (required)
                throw new InvalidOperationException(CreateMissingValueMessage(propertyPath));

            return null;
        }

        var providerValue =
            attributeValue is null || attributeValue.NULL == true
                ? null
                : innerReaderWriter.ReadObject(attributeValue, propertyPath, true, property);
        return converter.ConvertFromProvider(providerValue);
    }

    internal override Expression CreateReadExpression(
        Expression attributeValueExpression,
        string propertyPath,
        bool required,
        IProperty? property)
        => Expression.Convert(
            Expression.Call(
                Expression.Constant(this),
                ReadObjectMethod,
                attributeValueExpression,
                Expression.Constant(propertyPath),
                Expression.Constant(required),
                Expression.Constant(property, typeof(IProperty))),
            ValueType);

    internal override Expression CreateWriteExpression(Expression typedValueExpression)
        => innerReaderWriter.CreateWriteExpression(ConvertToProvider(typedValueExpression));

    internal override Expression CreatePartiQlLiteralExpression(Expression typedValueExpression)
        => innerReaderWriter.CreatePartiQlLiteralExpression(
            ConvertToProvider(typedValueExpression));

    private Expression ConvertToProvider(Expression modelValueExpression)
        => ReplacingExpressionVisitor.Replace(
            converter.ConvertToProviderExpression.Parameters.Single(),
            modelValueExpression,
            converter.ConvertToProviderExpression.Body);
}

/// <summary>
///     Exposes a non-generic converted wrapper (the NativeAOT composition) as a strongly-typed
///     codec for primitive-collection element mappings.
/// </summary>
/// <remarks>
///     Collection codecs require <see cref="DynamoValueReaderWriter{TValue}" /> for their element
///     type. Under NativeAOT the composed wrapper cannot be closed over the model CLR type without
///     <c>MakeGenericType</c>, so this adapter delegates to the boxed APIs of the wrapped codec
///     instead.
/// </remarks>
internal sealed class ConvertedDynamoValueReaderWriter<TValue>(
    IDynamoConvertedValueReaderWriter convertedReaderWriter) : DynamoValueReaderWriter<TValue>
{
    // The converted wrapper itself performs the model<->provider conversion; its InnerReaderWriter
    // is the provider-level codec underneath the converter and must not receive model values.
    private readonly DynamoValueReaderWriter _convertedReaderWriter =
        (DynamoValueReaderWriter)convertedReaderWriter;

    private readonly bool _convertsNulls = convertedReaderWriter.ConvertsNulls;

    internal override string WireMemberName => _convertedReaderWriter.WireMemberName;

    internal override bool RequiresParameterForPartiQlLiteral
        => _convertedReaderWriter.RequiresParameterForPartiQlLiteral;

    // Collection codecs are composed at runtime, not from compiled-model expression trees; the
    // AOT wrapper rejects expression-tree construction, and this adapter delegates to it.
    protected override Expression CreateConstructorExpression()
        => throw new NotSupportedException(
            "A NativeAOT converted collection codec is created through CoerceReaderWriter, not expression trees.");

    internal override bool HasValue(AttributeValue attributeValue)
        => _convertedReaderWriter.HasValue(attributeValue);

    protected override TValue ReadValue(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property)
        => (TValue)_convertedReaderWriter.ReadObject(attributeValue, propertyPath, true, property)!;

    public override AttributeValue Write(TValue value)
        => value is null && !_convertsNulls
            ? new AttributeValue { NULL = true }
            : _convertedReaderWriter.WriteBoxed(value!);

    public override string ToPartiQlLiteral(TValue value)
        => value is null && !_convertsNulls
            ? "NULL"
            : _convertedReaderWriter.ToPartiQlLiteralBoxed(value!);
}

/// <summary>Adapts a non-nullable provider reader/writer for nullable value-type mappings.</summary>
/// <remarks>
///     This is used both for scalar properties and for primitive collection element mappings such
///     as <c>List&lt;int?&gt;</c>, where DynamoDB NULL must round-trip as a nullable CLR value.
/// </remarks>
internal sealed class NullableDynamoValueReaderWriter<TValue>(
    DynamoValueReaderWriter<TValue> innerReaderWriter) : DynamoValueReaderWriter<TValue?>
    where TValue : struct
{
    private static readonly ConstructorInfo Constructor =
        typeof(NullableDynamoValueReaderWriter<TValue>).GetConstructor(
            [typeof(DynamoValueReaderWriter<TValue>)])!;

    internal override string WireMemberName => innerReaderWriter.WireMemberName;

    internal override bool RequiresParameterForPartiQlLiteral
        => innerReaderWriter.RequiresParameterForPartiQlLiteral;

    protected override Expression CreateConstructorExpression()
        => Expression.New(Constructor, innerReaderWriter.ConstructorExpression);

    internal override bool HasValue(AttributeValue attributeValue)
        // DynamoDB NULL is treated as "has a value (null)" rather than "attribute absent".
        // Returning true here lets the base Read() call through to ReadValue, which returns null.
        // This is intentional: NULL = true round-trips as CLR null, not as a missing attribute.
        => attributeValue.NULL == true || innerReaderWriter.HasValue(attributeValue);

    protected override TValue? ReadValue(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property)
        => attributeValue.NULL == true
            ? null
            : innerReaderWriter.Read(attributeValue, propertyPath, true, property);

    public override AttributeValue Write(TValue? value)
        => value.HasValue
            ? innerReaderWriter.Write(value.Value)
            : new AttributeValue { NULL = true };

    public override string ToPartiQlLiteral(TValue? value)
        => value.HasValue ? innerReaderWriter.ToPartiQlLiteral(value.Value) : "NULL";
}

internal sealed class StringDynamoValueReaderWriter : DynamoValueReaderWriter<string>
{
    internal override string WireMemberName => nameof(AttributeValue.S);

    internal override bool HasValue(AttributeValue attributeValue) => attributeValue.S != null;

    protected override string ReadValue(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property)
        => attributeValue.S;

    public override AttributeValue Write(string value) => new() { S = value };

    public override string ToPartiQlLiteral(string value)
        => DynamoValueReaderWriterHelpers.FormatStringLiteral(value);
}

internal sealed class BoolDynamoValueReaderWriter : DynamoValueReaderWriter<bool>
{
    internal override string WireMemberName => nameof(AttributeValue.BOOL);

    internal override bool HasValue(AttributeValue attributeValue) => attributeValue.BOOL != null;

    protected override bool ReadValue(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property)
        => attributeValue.BOOL!.Value;

    public override AttributeValue Write(bool value) => new() { BOOL = value };

    public override string ToPartiQlLiteral(bool value) => value ? "TRUE" : "FALSE";
}

internal sealed class BinaryDynamoValueReaderWriter : DynamoValueReaderWriter<byte[]>
{
    internal override string WireMemberName => nameof(AttributeValue.B);

    internal override bool RequiresParameterForPartiQlLiteral => true;

    internal override bool HasValue(AttributeValue attributeValue) => attributeValue.B != null;

    protected override byte[] ReadValue(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property)
        => attributeValue.B!.ToArray();

    public override AttributeValue Write(byte[] value)
        => DynamoWireValueConversion.CreateBinaryAttributeValue(value);

    public override string ToPartiQlLiteral(byte[] value)
        => throw new NotSupportedException(
            "Binary values are not supported for inline PartiQL constant generation.");
}

internal sealed class NumericDynamoValueReaderWriter<TValue>(
    Func<string, TValue> parse,
    Func<TValue, string> format) : DynamoValueReaderWriter<TValue>
{
    private static readonly ConstructorInfo Constructor =
        typeof(NumericDynamoValueReaderWriter<TValue>).GetConstructor(
            [typeof(Func<string, TValue>), typeof(Func<TValue, string>)])!;

    internal override string WireMemberName => nameof(AttributeValue.N);

    protected override Expression CreateConstructorExpression()
        // The numeric codec is defined by its parse/format delegates, so the constructor expression
        // needs to carry those delegates forward when the codec is recreated in query trees.
        => Expression.New(Constructor, Expression.Constant(parse), Expression.Constant(format));

    internal override bool HasValue(AttributeValue attributeValue) => attributeValue.N != null;

    protected override TValue ReadValue(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property)
        => parse(attributeValue.N);

    public override AttributeValue Write(TValue value) => new() { N = format(value) };

    public override string ToPartiQlLiteral(TValue value) => format(value);
}

internal sealed class ListDynamoValueReaderWriter<TCollection, TElement>(
    DynamoValueReaderWriter<TElement> elementReaderWriter) : DynamoValueReaderWriter<TCollection>
{
    private static readonly ConstructorInfo Constructor =
        typeof(ListDynamoValueReaderWriter<TCollection, TElement>).GetConstructor(
            [typeof(DynamoValueReaderWriter<TElement>)])!;

    internal override string WireMemberName => nameof(AttributeValue.L);

    internal override bool RequiresParameterForPartiQlLiteral
        => elementReaderWriter.RequiresParameterForPartiQlLiteral;

    protected override Expression CreateConstructorExpression()
        => Expression.New(Constructor, elementReaderWriter.ConstructorExpression);

    internal override bool HasValue(AttributeValue attributeValue) => attributeValue.L != null;

    protected override TCollection ReadValue(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property)
        => DynamoValueReaderWriterHelpers.ReadList<TCollection, TElement>(
            attributeValue,
            propertyPath,
            property,
            elementReaderWriter);

    public override AttributeValue Write(TCollection value)
        => DynamoValueReaderWriterHelpers.WriteList(
            DynamoValueReaderWriterHelpers.Enumerate<TCollection, TElement>(value),
            elementReaderWriter);

    public override string ToPartiQlLiteral(TCollection value)
        => DynamoValueReaderWriterHelpers.FormatListLiteral(
            DynamoValueReaderWriterHelpers.Enumerate<TCollection, TElement>(value),
            elementReaderWriter);
}

internal sealed class DictionaryDynamoValueReaderWriter<TCollection, TValue>(
    DynamoValueReaderWriter<TValue> valueReaderWriter,
    bool readOnly) : DynamoValueReaderWriter<TCollection>
{
    private static readonly ConstructorInfo Constructor =
        typeof(DictionaryDynamoValueReaderWriter<TCollection, TValue>).GetConstructor(
            [typeof(DynamoValueReaderWriter<TValue>), typeof(bool)])!;

    internal override string WireMemberName => nameof(AttributeValue.M);

    internal override bool RequiresParameterForPartiQlLiteral
        => valueReaderWriter.RequiresParameterForPartiQlLiteral;

    protected override Expression CreateConstructorExpression()
        => Expression.New(
            Constructor,
            valueReaderWriter.ConstructorExpression,
            Expression.Constant(readOnly));

    internal override bool HasValue(AttributeValue attributeValue) => attributeValue.M != null;

    protected override TCollection ReadValue(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property)
        => DynamoValueReaderWriterHelpers.ReadDictionary<TCollection, TValue>(
            attributeValue,
            propertyPath,
            property,
            valueReaderWriter,
            readOnly);

    public override AttributeValue Write(TCollection value)
        => DynamoValueReaderWriterHelpers.WriteDictionary(
            DynamoValueReaderWriterHelpers.Enumerate<TCollection, KeyValuePair<string, TValue>>(
                value),
            valueReaderWriter);

    public override string ToPartiQlLiteral(TCollection value)
        => DynamoValueReaderWriterHelpers.FormatDictionaryLiteral(
            DynamoValueReaderWriterHelpers.Enumerate<TCollection, KeyValuePair<string, TValue>>(
                value),
            valueReaderWriter);
}

internal sealed class SetDynamoValueReaderWriter<TCollection, TElement>(
    DynamoValueReaderWriter<TElement> elementReaderWriter) : DynamoValueReaderWriter<TCollection>
{
    private static readonly ConstructorInfo Constructor =
        typeof(SetDynamoValueReaderWriter<TCollection, TElement>).GetConstructor(
            [typeof(DynamoValueReaderWriter<TElement>)])!;

    internal override string WireMemberName { get; } =
        DynamoValueReaderWriterHelpers.GetSetWireMemberName(elementReaderWriter.WireMemberName);

    internal override bool RequiresParameterForPartiQlLiteral
        => elementReaderWriter.RequiresParameterForPartiQlLiteral;

    protected override Expression CreateConstructorExpression()
        => Expression.New(Constructor, elementReaderWriter.ConstructorExpression);

    internal override bool HasValue(AttributeValue attributeValue)
        => DynamoValueReaderWriterHelpers.HasSetValue(attributeValue, WireMemberName);

    protected override TCollection ReadValue(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property)
        => DynamoValueReaderWriterHelpers.ReadSet<TCollection, TElement>(
            attributeValue,
            propertyPath,
            property,
            elementReaderWriter,
            WireMemberName);

    public override AttributeValue Write(TCollection value)
        => DynamoValueReaderWriterHelpers.WriteSet(
            DynamoValueReaderWriterHelpers.Enumerate<TCollection, TElement>(value),
            elementReaderWriter,
            WireMemberName);

    public override string ToPartiQlLiteral(TCollection value)
        => DynamoValueReaderWriterHelpers.FormatSetLiteral(
            DynamoValueReaderWriterHelpers.Enumerate<TCollection, TElement>(value),
            elementReaderWriter);
}

/// <summary>Builds reader/writers for mapping CLR types and composes them with EF Core converters.</summary>
internal static class DynamoValueReaderWriterFactory
{
    private static readonly DynamoValueReaderWriter StringReaderWriter =
        new StringDynamoValueReaderWriter();

    private static readonly DynamoValueReaderWriter BoolReaderWriter =
        new BoolDynamoValueReaderWriter();

    private static readonly DynamoValueReaderWriter BinaryReaderWriter =
        new BinaryDynamoValueReaderWriter();

    private static readonly MethodInfo CreateListReaderWriterMethod =
        typeof(DynamoValueReaderWriterFactory).GetMethod(
            nameof(CreateListReaderWriter),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo CreateDictionaryReaderWriterMethod =
        typeof(DynamoValueReaderWriterFactory).GetMethod(
            nameof(CreateDictionaryReaderWriter),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo CreateSetReaderWriterMethod =
        typeof(DynamoValueReaderWriterFactory).GetMethod(
            nameof(CreateSetReaderWriter),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo CreateNullableReaderWriterMethod =
        typeof(DynamoValueReaderWriterFactory).GetMethod(
            nameof(CreateNullableReaderWriterGeneric),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    /// <summary>Creates a DynamoDB value reader/writer for the specified CLR type.</summary>
    /// <remarks>
    ///     Resolution order is scalar primitives (string/bool/binary), numeric primitives (including
    ///     nullable wrappers), then collection shapes (list/dictionary/set) when an element/value
    ///     reader/writer is supplied.
    /// </remarks>
    public static DynamoValueReaderWriter? Create(
        Type clrType,
        DynamoValueReaderWriter? elementReaderWriter = null,
        bool readOnlyDictionary = false,
        bool allowReflectionFallback = true)
    {
        var nonNullableType = Nullable.GetUnderlyingType(clrType) ?? clrType;
        var isNullableValueType = clrType != nonNullableType && nonNullableType.IsValueType;

        if (nonNullableType == typeof(string))
            return StringReaderWriter;

        if (nonNullableType == typeof(bool))
            return isNullableValueType
                ? CreateNullableReaderWriter(nonNullableType, BoolReaderWriter)
                : BoolReaderWriter;

        if (nonNullableType == typeof(byte[]))
            return BinaryReaderWriter;

        if (TryCreateNumeric(nonNullableType, out var numericReaderWriter))
            return isNullableValueType
                ? CreateNullableReaderWriter(nonNullableType, numericReaderWriter)
                : numericReaderWriter;

        // Collection mappings are built from the element/value mapping that EF resolved earlier,
        // so rich shapes inherit the same converter and wire-format behavior as their elements.
        Type listElementType = null!, dictionaryValueType = null!, setElementType = null!;
        var hasListShape = elementReaderWriter != null
            && DynamoTypeMappingSource.TryGetListElementType(clrType, out listElementType);
        var hasDictionaryShape = !hasListShape
            && elementReaderWriter != null
            && DynamoTypeMappingSource.TryGetDictionaryValueType(
                clrType,
                out dictionaryValueType,
                out _);
        var hasSetShape =
            !hasListShape
            && !hasDictionaryShape
            && elementReaderWriter != null
            && DynamoTypeMappingSource.TryGetSetElementType(clrType, out setElementType);

        if (hasListShape || hasDictionaryShape || hasSetShape)
        {
            if (!allowReflectionFallback)
                throw new InvalidOperationException(
                    $"The DynamoDB collection mapping for '{FormatFriendlyTypeName(clrType)}' was "
                    + "not primed by a compiled model. Materializing unprimed collection mappings "
                    + "requires runtime generic instantiation, which fails under NativeAOT. "
                    + "Generate the compiled model (`dotnet ef dbcontext optimize`) and configure "
                    + "the context with UseModel so collection codecs are primed statically.");

            if (hasListShape)
                return (DynamoValueReaderWriter)CreateListReaderWriterMethod
                    .MakeGenericMethod(clrType, listElementType)
                    .Invoke(null, [elementReaderWriter])!;

            if (hasDictionaryShape)
                return (DynamoValueReaderWriter)CreateDictionaryReaderWriterMethod
                    .MakeGenericMethod(clrType, dictionaryValueType)
                    .Invoke(null, [elementReaderWriter, readOnlyDictionary])!;

            return (DynamoValueReaderWriter)CreateSetReaderWriterMethod
                .MakeGenericMethod(clrType, setElementType)
                .Invoke(null, [elementReaderWriter])!;
        }

        return null;
    }

    /// <summary>Composes an EF Core value converter over an existing DynamoDB reader/writer.</summary>
    /// <remarks>
    ///     If <paramref name="readerWriter" /> is already a converted wrapper, this method unwraps it
    ///     to avoid nesting converter adapters, then creates one composed reader/writer at
    ///     mapping-construction time.
    /// </remarks>
    public static DynamoValueReaderWriter? Compose(
        ValueConverter? converter,
        DynamoValueReaderWriter? readerWriter)
    {
        if (readerWriter is IDynamoConvertedValueReaderWriter converted)
            readerWriter = converted.InnerReaderWriter;

        if (converter == null || readerWriter == null)
            return readerWriter;

        var providerType = converter.ProviderClrType;
        if (readerWriter.ValueType != providerType)
        {
            var underlyingProviderType = Nullable.GetUnderlyingType(providerType);
            if (underlyingProviderType != null && readerWriter.ValueType == underlyingProviderType)
                readerWriter = CreateNullableReaderWriter(underlyingProviderType, readerWriter);
            else
                throw new InvalidOperationException(
                    $"Converter provider type '{providerType.Name}' does not match DynamoDB reader/writer "
                    + $"type '{readerWriter.ValueType.Name}'.");
        }

        if (!RuntimeFeature.IsDynamicCodeSupported)
            return new DynamoAotConvertedValueReaderWriter(readerWriter, converter);

        // Compose the converter once at mapping-construction time so callers can keep working with
        // model CLR values while the inner reader/writer stays focused on the provider CLR type.
        // Activator is acceptable here because this runs when the mapping is built, not in the
        // per-row/per-parameter hot path.
        return (DynamoValueReaderWriter)Activator.CreateInstance(
            typeof(DynamoConvertedValueReaderWriter<,>).MakeGenericType(
                converter.ModelClrType,
                providerType),
            readerWriter,
            converter)!;
    }

    private static ListDynamoValueReaderWriter<TCollection, TElement>
        CreateListReaderWriter<TCollection, TElement>(DynamoValueReaderWriter elementReaderWriter)
        => new(CoerceReaderWriter<TElement>(elementReaderWriter));

    private static string FormatFriendlyTypeName(Type type)
        => type.IsGenericType
            ? $"{type.Name[..type.Name.IndexOf('`')]}<"
            + string.Join(", ", type.GetGenericArguments().Select(FormatFriendlyTypeName))
            + ">"
            : type.Name;

    private static DictionaryDynamoValueReaderWriter<TCollection, TValue>
        CreateDictionaryReaderWriter<TCollection, TValue>(
            DynamoValueReaderWriter valueReaderWriter,
            bool readOnly)
        => new(CoerceReaderWriter<TValue>(valueReaderWriter), readOnly);

    private static SetDynamoValueReaderWriter<TCollection, TElement>
        CreateSetReaderWriter<TCollection, TElement>(DynamoValueReaderWriter elementReaderWriter)
        => new(CoerceReaderWriter<TElement>(elementReaderWriter));

    private static DynamoValueReaderWriter CreateNullableReaderWriter(
            Type valueType,
            DynamoValueReaderWriter readerWriter)
        // The common scalar cases dispatch to statically-instantiated generic helpers so
        // NativeAOT never has to compile missing generic instantiations at runtime.
        => valueType == typeof(bool) ? NullableWrap<bool>(readerWriter) :
            valueType == typeof(byte) ? NullableWrap<byte>(readerWriter) :
            valueType == typeof(sbyte) ? NullableWrap<sbyte>(readerWriter) :
            valueType == typeof(short) ? NullableWrap<short>(readerWriter) :
            valueType == typeof(ushort) ? NullableWrap<ushort>(readerWriter) :
            valueType == typeof(int) ? NullableWrap<int>(readerWriter) :
            valueType == typeof(uint) ? NullableWrap<uint>(readerWriter) :
            valueType == typeof(long) ? NullableWrap<long>(readerWriter) :
            valueType == typeof(ulong) ? NullableWrap<ulong>(readerWriter) :
            valueType == typeof(float) ? NullableWrap<float>(readerWriter) :
            valueType == typeof(double) ? NullableWrap<double>(readerWriter) :
            valueType == typeof(decimal) ? NullableWrap<decimal>(readerWriter) :
            !RuntimeFeature.IsDynamicCodeSupported ? throw new NotSupportedException(
                $"Nullable values of type '{valueType.Name}' cannot be materialized under "
                + "NativeAOT because their reader/writer requires runtime generic "
                + "instantiation. Prime the mapping through the compiled model or map the "
                + "element type explicitly.") :
            (DynamoValueReaderWriter)CreateNullableReaderWriterMethod
                .MakeGenericMethod(valueType)
                .Invoke(null, [readerWriter])!;

    private static NullableDynamoValueReaderWriter<TValue> NullableWrap<TValue>(
        DynamoValueReaderWriter readerWriter) where TValue : struct
        => new((DynamoValueReaderWriter<TValue>)readerWriter);

    private static NullableDynamoValueReaderWriter<TValue>
        CreateNullableReaderWriterGeneric<TValue>(DynamoValueReaderWriter readerWriter)
        where TValue : struct
        => new((DynamoValueReaderWriter<TValue>)readerWriter);

    internal static DynamoValueReaderWriter<TValue> CoerceReaderWriter<TValue>(
        DynamoValueReaderWriter readerWriter)
    {
        if (readerWriter is DynamoValueReaderWriter<TValue> typedReaderWriter)
            return typedReaderWriter;

        // Under NativeAOT, converter compositions arrive as the non-generic converted wrapper.
        // Re-wrap it in a typed adapter so collection codecs can close over the element CLR type
        // without MakeGenericType, which is not trimming- or NativeAOT-safe.
        if (readerWriter is IDynamoConvertedValueReaderWriter
            && (readerWriter.ValueType == typeof(TValue)
                || readerWriter.ValueType == Nullable.GetUnderlyingType(typeof(TValue))))
            return new ConvertedDynamoValueReaderWriter<TValue>(
                (IDynamoConvertedValueReaderWriter)readerWriter);

        var underlyingType = Nullable.GetUnderlyingType(typeof(TValue));
        if (underlyingType != null && readerWriter.ValueType == underlyingType)
            // Primitive collection metadata often flows the non-nullable element mapping even when
            // the collection CLR element is nullable. Wrap it here so list/set/dictionary readers
            // can still materialize and write NULL collection elements consistently.
            return (DynamoValueReaderWriter<TValue>)CreateNullableReaderWriter(
                underlyingType,
                readerWriter);

        throw new InvalidCastException(
            $"Unable to use DynamoDB reader/writer for '{readerWriter.ValueType.Name}' as '{typeof(TValue).Name}'.");
    }

    private static bool TryCreateNumeric(
        Type clrType,
        [NotNullWhen(true)] out DynamoValueReaderWriter? readerWriter)
    {
        readerWriter = clrType switch
        {
            _ when clrType == typeof(byte) => new NumericDynamoValueReaderWriter<byte>(
                static value
                    => byte.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                static value => value.ToString(CultureInfo.InvariantCulture)),

            _ when clrType == typeof(sbyte) => new NumericDynamoValueReaderWriter<sbyte>(
                static value
                    => sbyte.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                static value => value.ToString(CultureInfo.InvariantCulture)),

            _ when clrType == typeof(short) => new NumericDynamoValueReaderWriter<short>(
                static value
                    => short.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                static value => value.ToString(CultureInfo.InvariantCulture)),

            _ when clrType == typeof(ushort) => new NumericDynamoValueReaderWriter<ushort>(
                static value
                    => ushort.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                static value => value.ToString(CultureInfo.InvariantCulture)),

            _ when clrType == typeof(int) => new NumericDynamoValueReaderWriter<int>(
                static value
                    => int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                static value => value.ToString(CultureInfo.InvariantCulture)),

            _ when clrType == typeof(uint) => new NumericDynamoValueReaderWriter<uint>(
                static value
                    => uint.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                static value => value.ToString(CultureInfo.InvariantCulture)),

            _ when clrType == typeof(long) => new NumericDynamoValueReaderWriter<long>(
                static value
                    => long.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                static value => value.ToString(CultureInfo.InvariantCulture)),

            _ when clrType == typeof(ulong) => new NumericDynamoValueReaderWriter<ulong>(
                static value
                    => ulong.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture),
                static value => value.ToString(CultureInfo.InvariantCulture)),

            _ when clrType == typeof(float) => new NumericDynamoValueReaderWriter<float>(
                static value
                    => float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture),
                static value => value.ToString("R", CultureInfo.InvariantCulture)),

            _ when clrType == typeof(double) => new NumericDynamoValueReaderWriter<double>(
                static value
                    => double.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture),
                static value => value.ToString("R", CultureInfo.InvariantCulture)),

            _ when clrType == typeof(decimal) => new NumericDynamoValueReaderWriter<decimal>(
                static value
                    => decimal.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture),
                static value => value.ToString(CultureInfo.InvariantCulture)),

            _ => null
        };

        return readerWriter != null;
    }
}

/// <summary>Shared helpers for collection materialization and PartiQL literal formatting.</summary>
internal static class DynamoValueReaderWriterHelpers
{
    public static string FormatStringLiteral(string value)
        => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";

    public static TCollection ReadList<TCollection, TElement>(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property,
        DynamoValueReaderWriter<TElement> elementReaderWriter)
    {
        // Collection element nullability comes from element metadata, not the collection property
        // itself. Required value-type elements should fail when a wire value is missing.
        var elementRequired = IsRequiredCollectionElement(property, typeof(TElement));
        var result = new List<TElement>(attributeValue.L.Count);

        foreach (var element in attributeValue.L)
            result.Add(elementReaderWriter.Read(element, propertyPath, elementRequired, null));

        return MaterializeList<TCollection, TElement>(result);
    }

    public static AttributeValue WriteList<TElement>(
        IEnumerable<TElement> value,
        DynamoValueReaderWriter<TElement> elementReaderWriter)
        => new() { L = value.Select(elementReaderWriter.Write).ToList() };

    public static string FormatListLiteral<TElement>(
        IEnumerable<TElement> value,
        DynamoValueReaderWriter<TElement> elementReaderWriter)
        => $"[{string.Join(", ", value.Select(elementReaderWriter.ToPartiQlLiteral))}]";

    public static TCollection ReadDictionary<TCollection, TValue>(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property,
        DynamoValueReaderWriter<TValue> valueReaderWriter,
        bool readOnly)
    {
        // Dictionary element metadata describes KeyValuePair<K, V>, which is never nullable, so
        // it cannot express value nullability. The write path emits NULL wire entries for
        // nullable and reference-type values, so the read path must accept them; only
        // non-nullable value-type values are required.
        var valueRequired =
            typeof(TValue).IsValueType && Nullable.GetUnderlyingType(typeof(TValue)) == null;
        var result = new Dictionary<string, TValue>(attributeValue.M.Count, StringComparer.Ordinal);

        foreach (var pair in attributeValue.M)
            result.Add(
                pair.Key,
                valueReaderWriter.Read(pair.Value, propertyPath, valueRequired, null));

        return MaterializeDictionary<TCollection, TValue>(result, readOnly);
    }

    public static AttributeValue WriteDictionary<TValue>(
        IEnumerable<KeyValuePair<string, TValue>> value,
        DynamoValueReaderWriter<TValue> valueReaderWriter)
    {
        var map = new Dictionary<string, AttributeValue>(StringComparer.Ordinal);

        foreach (var pair in value)
            map[pair.Key] = valueReaderWriter.Write(pair.Value);

        return new AttributeValue { M = map };
    }

    public static string FormatDictionaryLiteral<TValue>(
        IEnumerable<KeyValuePair<string, TValue>> value,
        DynamoValueReaderWriter<TValue> valueReaderWriter)
        => $"{{{string.Join(", ", value.Select(
            pair => $"{FormatStringLiteral(pair.Key)}: {valueReaderWriter.ToPartiQlLiteral(pair.Value)}"))}}}";

    public static bool HasSetValue(AttributeValue attributeValue, string wireMemberName)
        => wireMemberName == nameof(AttributeValue.SS) ? attributeValue.SS != null :
            wireMemberName == nameof(AttributeValue.NS) ? attributeValue.NS != null :
            attributeValue.BS != null;

    public static string GetSetWireMemberName(string elementWireMemberName)
        => elementWireMemberName == nameof(AttributeValue.S) ? nameof(AttributeValue.SS) :
            elementWireMemberName == nameof(AttributeValue.B) ? nameof(AttributeValue.BS) :
            nameof(AttributeValue.NS);

    public static TCollection ReadSet<TCollection, TElement>(
        AttributeValue attributeValue,
        string propertyPath,
        IProperty? property,
        DynamoValueReaderWriter<TElement> elementReaderWriter,
        string setWireMemberName)
    {
        var elementRequired = IsRequiredCollectionElement(property, typeof(TElement));

        // Honor a model-configured element comparer so app-level Contains/except semantics on
        // the materialized set match the configured model instead of default reference/equality.
        var elementComparer =
            property?.GetElementType()?.GetValueComparer() as IEqualityComparer<TElement>;
        var result = elementComparer is not null ? new HashSet<TElement>(elementComparer) : [];

        if (setWireMemberName == nameof(AttributeValue.SS))
            foreach (var value in attributeValue.SS)
                result.Add(
                    elementReaderWriter.Read(
                        new AttributeValue { S = value },
                        propertyPath,
                        elementRequired,
                        null));
        else if (setWireMemberName == nameof(AttributeValue.NS))
            foreach (var value in attributeValue.NS)
                result.Add(
                    elementReaderWriter.Read(
                        new AttributeValue { N = value },
                        propertyPath,
                        elementRequired,
                        null));
        else
            foreach (var value in attributeValue.BS)
                result.Add(
                    elementReaderWriter.Read(
                        new AttributeValue { B = value },
                        propertyPath,
                        elementRequired,
                        null));

        return MaterializeSet<TCollection, TElement>(result);
    }

    public static AttributeValue WriteSet<TElement>(
        IEnumerable<TElement> value,
        DynamoValueReaderWriter<TElement> elementReaderWriter,
        string setWireMemberName)
    {
        var writtenValues = value.Select(elementReaderWriter.Write).ToList();
        if (writtenValues.Count == 0)
            throw new InvalidOperationException(
                "DynamoDB sets cannot be empty; use a null property or a non-empty collection.");

        if (writtenValues.Any(static attributeValue => attributeValue.NULL == true))
            throw new InvalidOperationException("DynamoDB sets cannot contain null elements.");

        if (setWireMemberName == nameof(AttributeValue.SS))
            return new AttributeValue
            {
                SS = writtenValues
                    .Select(static attributeValue
                        => attributeValue.S
                        ?? throw new InvalidOperationException(
                            "Set element did not serialize to string wire value (S)."))
                    .ToList()
            };

        if (setWireMemberName == nameof(AttributeValue.NS))
            return new AttributeValue
            {
                NS = writtenValues
                    .Select(static attributeValue
                        => attributeValue.N
                        ?? throw new InvalidOperationException(
                            "Set element did not serialize to number wire value (N)."))
                    .ToList()
            };

        return new AttributeValue
        {
            BS = writtenValues
                .Select(static attributeValue
                    => attributeValue.B
                    ?? throw new InvalidOperationException(
                        "Set element did not serialize to binary wire value (B)."))
                .ToList()
        };
    }

    public static string FormatSetLiteral<TElement>(
        IEnumerable<TElement> value,
        DynamoValueReaderWriter<TElement> elementReaderWriter)
    {
        var literals = value.Select(elementReaderWriter.ToPartiQlLiteral).ToList();
        if (literals.Count == 0)
            throw new InvalidOperationException(
                "DynamoDB sets cannot be empty; use a null property or a non-empty collection.");

        return $"<<{string.Join(", ", literals)}>>";
    }

    public static IEnumerable<TElement> Enumerate<TCollection, TElement>(TCollection value)
        // Collection shapes are reference types, so this is a plain cast with no boxing; the
        // direct dispatch keeps read/write paths NativeAOT-safe without compiled delegates.
        => (IEnumerable<TElement>)(object)value!;

    public static TCollection MaterializeList<TCollection, TElement>(List<TElement> values)
        // List/array result shaping is a collection-shape concern; the direct cast avoids
        // compiled delegates so the read path stays NativeAOT-safe.
        => typeof(TCollection).IsArray
            ? (TCollection)(object)values.ToArray()
            : (TCollection)(object)values;

    public static TCollection MaterializeDictionary<TCollection, TValue>(
            Dictionary<string, TValue> values,
            bool readOnly)
        // Read-only dictionary wrapping is determined by the requested CLR collection shape, not
        // by the wire format.
        => readOnly
            ? (TCollection)(object)new ReadOnlyDictionary<string, TValue>(values)
            : (TCollection)(object)values;

    public static TCollection MaterializeSet<TCollection, TElement>(HashSet<TElement> values)
        // Sets always materialize through a HashSet first so the runtime path stays allocation-
        // focused on the collection itself.
        => (TCollection)(object)values;

    private static bool IsRequiredCollectionElement(IProperty? property, Type elementType)
        => property?.GetElementType()?.IsNullable == false
            || (elementType.IsValueType && Nullable.GetUnderlyingType(elementType) == null);
}
