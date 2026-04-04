using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.Update;

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>
/// Compiles and caches strongly-typed DynamoDB item serializers per entity type.
/// Each serializer is built as an expression tree once per <see cref="IEntityType"/> and
/// reused on every subsequent SaveChanges call — eliminating per-call type
/// dispatch and value-type boxing for scalar properties.
/// </summary>
/// <remarks>
/// This service mirrors the role of <c>EntityMaterializerSource</c> on the read side:
/// model metadata drives a one-time compilation step, and the compiled delegate handles
/// the hot path without reflection. Register as a scoped service so the cache is shared
/// across a single <see cref="Microsoft.EntityFrameworkCore.DbContext"/> lifetime.
/// </remarks>
public sealed class DynamoEntityItemSerializerSource
{
    /// <summary>
    /// The <c>GetCurrentValue&lt;T&gt;</c> open generic on <see cref="IUpdateEntry"/>.
    /// Resolved once and stored so MakeGenericMethod can be called per property at build time.
    /// </summary>
    private static readonly MethodInfo GetCurrentValueOpenMethod = typeof(IUpdateEntry)
        .GetMethods()
        .Single(m => m.Name == nameof(IUpdateEntry.GetCurrentValue)
            && m.IsGenericMethod
            && m.GetParameters() is [{ ParameterType: var pt }]
            && pt == typeof(IPropertyBase));

    private readonly ConcurrentDictionary<IEntityType, CompiledEntitySerializer> _cache = new();

    // ──────────────────────────────────────────────────────────────────────────────
    //  Public surface used by DynamoDatabaseWrapper
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the fully assembled DynamoDB item dictionary for a root
    /// <see cref="IUpdateEntry"/> and its owned sub-entries.
    /// </summary>
    /// <param name="rootEntry">The root (non-owned) entity entry to serialize.</param>
    /// <param name="ownedEntries">
    /// Map from owned CLR entity object to its <see cref="IUpdateEntry"/>. Must cover all
    /// nesting depths so that recursive owned serialization can resolve every sub-entry.
    /// </param>
    public Dictionary<string, AttributeValue> BuildItem(
        IUpdateEntry rootEntry,
        IReadOnlyDictionary<object, IUpdateEntry> ownedEntries)
        => GetOrBuildSerializer(rootEntry.EntityType).Serialize(rootEntry, ownedEntries, this);

    // ──────────────────────────────────────────────────────────────────────────────
    //  Internal: called recursively for owned sub-entries
    // ──────────────────────────────────────────────────────────────────────────────

    internal Dictionary<string, AttributeValue> BuildItemFromOwnedEntry(
        IUpdateEntry entry,
        IReadOnlyDictionary<object, IUpdateEntry> ownedEntries)
        => GetOrBuildSerializer(entry.EntityType).Serialize(entry, ownedEntries, this);

    // ──────────────────────────────────────────────────────────────────────────────
    //  Compilation
    // ──────────────────────────────────────────────────────────────────────────────

    private CompiledEntitySerializer GetOrBuildSerializer(IEntityType entityType)
        => _cache.GetOrAdd(entityType, BuildSerializer);

    /// <summary>
    /// Compiles the serializer for <paramref name="entityType"/> by:
    /// 1. Building a strongly-typed <c>Func&lt;IUpdateEntry, AttributeValue&gt;</c> per
    ///    scalar/collection property (no per-call type dispatch or boxing).
    /// 2. Collecting owned navigation metadata for recursive map/list serialization.
    /// </summary>
    private static CompiledEntitySerializer BuildSerializer(IEntityType entityType)
    {
        // Scalar and primitive-collection properties.
        // Shadow key properties (FK shadows, __OwnedOrdinal) are omitted because they are
        // EF Core tracking artefacts and must not appear in the DynamoDB item document.
        var propertySerializers = entityType
            .GetProperties()
            .Where(static p => !(p.IsShadowProperty() && p.IsKey()))
            .Select(p => (name: p.Name, fn: CompilePropertySerializer(p)))
            .ToList();

        // Owned navigations are serialized dynamically at runtime because their CLR types
        // are heterogeneous and the lookup against ownedEntries requires an object key.
        var ownedNavigations = entityType
            .GetNavigations()
            .Where(static n => !n.IsOnDependent && n.TargetEntityType.IsOwned())
            .ToList();

        return new CompiledEntitySerializer(propertySerializers, ownedNavigations);
    }

    /// <summary>
    /// Builds an expression tree for a single EF Core property that produces an
    /// <see cref="AttributeValue"/> without boxing scalar values or performing
    /// runtime type switches.
    /// </summary>
    private static Func<IUpdateEntry, AttributeValue> CompilePropertySerializer(IProperty property)
    {
        var entryParam = Expression.Parameter(typeof(IUpdateEntry), "entry");
        var clrType = property.ClrType;
        var converter = property.GetTypeMapping().Converter;

        // entry.GetCurrentValue<ClrType>(property) — strongly typed, no boxing for value types.
        Expression valueExpr = Expression.Call(
            entryParam,
            GetCurrentValueOpenMethod.MakeGenericMethod(clrType),
            Expression.Constant(property, typeof(IPropertyBase)));

        Type effectiveType = clrType;
        bool needsNullableGuard = false;

        if (converter is not null)
        {
            var convertExpr = converter.ConvertToProviderExpression;
            var converterInputType = convertExpr.Parameters[0].Type;

            // EF Core registers converters against the non-nullable underlying type (e.g.
            // DateTimeOffsetToStringConverter has a DateTimeOffset parameter). When the CLR
            // property type is Nullable<T> and the converter expects T, we must:
            //   1. Unwrap .Value before inlining the converter body — the expression tree
            //      body calls methods on T that don't exist on Nullable<T>.
            //   2. Emit the outer null guard ourselves rather than relying on
            //      BuildAttributeValueExpression, because that guard needs to test the
            //      original Nullable<T> value before the converter runs.
            Expression converterInput = valueExpr;
            if (Nullable.GetUnderlyingType(clrType) == converterInputType)
            {
                converterInput = Expression.Property(valueExpr, "Value");
                needsNullableGuard = true;
            }

            // Inline the converter's expression tree body rather than calling the object-based
            // delegate (ConvertToProvider) which would box value types unnecessarily.
            valueExpr = InlineConverterExpression(converterInput, convertExpr);
            effectiveType = converter.ProviderClrType;
        }

        var body = BuildAttributeValueExpression(valueExpr, effectiveType);

        // Add the pre-converter null guard when we unwrapped a Nullable<T>.
        // BuildAttributeValueExpression may add a null guard for the converter's output type, but
        // that doesn't protect against the original Nullable<T> being null before conversion.
        if (needsNullableGuard)
        {
            Expression rawValueExpr = Expression.Call(
                entryParam,
                GetCurrentValueOpenMethod.MakeGenericMethod(clrType),
                Expression.Constant(property, typeof(IPropertyBase)));
            body = Expression.Condition(
                Expression.Equal(rawValueExpr, Expression.Constant(null, clrType)),
                NullAttributeValueExpr,
                body);
        }

        return Expression.Lambda<Func<IUpdateEntry, AttributeValue>>(body, entryParam).Compile();
    }

    /// <summary>
    /// Inlines the body of <paramref name="convertLambda"/> by substituting
    /// <paramref name="inputExpr"/> for the lambda's single parameter — composing
    /// the converter into the caller's expression tree without a delegate invocation.
    /// </summary>
    private static Expression InlineConverterExpression(
        Expression inputExpr,
        LambdaExpression convertLambda)
    {
        // The lambda has exactly one parameter (the model CLR value).
        // Replace it with our typed input expression.
        return ParameterSubstitutor.Substitute(
            convertLambda.Parameters[0],
            inputExpr,
            convertLambda.Body);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  AttributeValue expression builders
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns an expression that produces an <see cref="AttributeValue"/> for
    /// <paramref name="valueExpr"/> whose compile-time type is <paramref name="clrType"/>.
    /// Nullable types get an additional null-guard that returns <c>{ NULL = true }</c>.
    /// </summary>
    private static Expression BuildAttributeValueExpression(Expression valueExpr, Type clrType)
    {
        var nullableUnderlying = Nullable.GetUnderlyingType(clrType);
        var coreType = nullableUnderlying ?? clrType;
        bool isNullable = nullableUnderlying is not null || !clrType.IsValueType;

        Expression serializeExpr = BuildNonNullAttributeValueExpression(
            // For Nullable<T>, unwrap the value (.Value) before passing to the builder.
            nullableUnderlying is not null ? Expression.Property(valueExpr, "Value") : valueExpr,
            coreType);

        if (!isNullable)
            return serializeExpr;

        // Guard: if (value == null) return { NULL = true }
        Expression nullTest = nullableUnderlying is not null
            ? Expression.Equal(valueExpr, Expression.Constant(null, clrType))
            : Expression.ReferenceEqual(valueExpr, Expression.Constant(null, clrType));

        return Expression.Condition(nullTest, NullAttributeValueExpr, serializeExpr);
    }

    /// <summary>
    /// Builds the non-null serialization expression for a known non-nullable core type.
    /// Dispatches to scalar, set, dictionary, or list builders based on <paramref name="coreType"/>.
    /// </summary>
    private static Expression BuildNonNullAttributeValueExpression(
        Expression valueExpr,
        Type coreType)
    {
        // --- Scalars ---
        if (coreType == typeof(string))
            return MakeAttributeValueS(valueExpr);

        if (coreType == typeof(bool))
            return MakeAttributeValueBool(valueExpr);

        if (coreType == typeof(Guid))
            return MakeAttributeValueS(CallToString(valueExpr));

        if (coreType == typeof(DateTime))
            return MakeAttributeValueS(CallToStringFormat(valueExpr, "O"));

        if (coreType == typeof(DateTimeOffset))
            return MakeAttributeValueS(CallToStringFormat(valueExpr, "O"));

        if (IsIntegralType(coreType))
            return MakeAttributeValueN(CallToString(valueExpr));

        if (IsFloatingPointType(coreType))
            return MakeAttributeValueN(CallToStringFormat(valueExpr, "R"));

        if (coreType == typeof(decimal))
            return MakeAttributeValueN(CallToString(valueExpr));

        // --- Collections: call typed static helpers to avoid complex expression trees ---

        if (DynamoTypeMappingSource.TryGetSetElementType(coreType, out var setElementType))
            return BuildSetHelperCall(valueExpr, coreType, setElementType);

        if (DynamoTypeMappingSource.TryGetDictionaryValueType(
            coreType,
            out var dictValueType,
            out _))
            return BuildDictionaryHelperCall(valueExpr, coreType, dictValueType);

        if (DynamoTypeMappingSource.TryGetListElementType(coreType, out var listElementType))
            return BuildListHelperCall(valueExpr, coreType, listElementType);

        throw new NotSupportedException(
            $"CLR type '{coreType.FullName}' has no DynamoDB AttributeValue mapping. "
            + "Add explicit support or register a value converter for this property.");
    }

    // --- Typed collection helper call builders ---

    private static Expression BuildSetHelperCall(
        Expression valueExpr,
        Type setType,
        Type elementType)
    {
        if (elementType == typeof(string))
        {
            var method =
                typeof(DynamoAttributeValueCollectionHelpers).GetMethod(
                    nameof(DynamoAttributeValueCollectionHelpers.SerializeStringSet))!;
            return Expression.Call(method, valueExpr);
        }

        // Numeric set — generic method dispatched on element type.
        var genericMethod =
            typeof(DynamoAttributeValueCollectionHelpers).GetMethod(
                    nameof(DynamoAttributeValueCollectionHelpers.SerializeNumericSet))!
                .MakeGenericMethod(elementType);
        return Expression.Call(genericMethod, valueExpr);
    }

    private static Expression BuildDictionaryHelperCall(
        Expression valueExpr,
        Type dictType,
        Type valueElementType)
    {
        var genericMethod =
            typeof(DynamoAttributeValueCollectionHelpers).GetMethod(
                    nameof(DynamoAttributeValueCollectionHelpers.SerializeDictionary))!
                .MakeGenericMethod(valueElementType);
        return Expression.Call(genericMethod, valueExpr);
    }

    private static Expression BuildListHelperCall(
        Expression valueExpr,
        Type listType,
        Type elementType)
    {
        var genericMethod =
            typeof(DynamoAttributeValueCollectionHelpers).GetMethod(
                nameof(DynamoAttributeValueCollectionHelpers.SerializeList))!.MakeGenericMethod(
                elementType);
        return Expression.Call(genericMethod, valueExpr);
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  Expression helpers
    // ──────────────────────────────────────────────────────────────────────────────

    private static readonly MemberInitExpression NullAttributeValueExpr = Expression.MemberInit(
        Expression.New(typeof(AttributeValue)),
        Expression.Bind(
            typeof(AttributeValue).GetProperty(nameof(AttributeValue.NULL))!,
            // NULL is bool? — must convert from bool constant to avoid invalid IL.
            Expression.Convert(Expression.Constant(true), typeof(bool?))));

    private static MemberInitExpression MakeAttributeValueS(Expression valueExpr)
        => Expression.MemberInit(
            Expression.New(typeof(AttributeValue)),
            Expression.Bind(
                typeof(AttributeValue).GetProperty(nameof(AttributeValue.S))!,
                valueExpr));

    private static MemberInitExpression MakeAttributeValueBool(Expression valueExpr)
        => Expression.MemberInit(
            Expression.New(typeof(AttributeValue)),
            Expression.Bind(
                typeof(AttributeValue).GetProperty(nameof(AttributeValue.BOOL))!,
                // BOOL is bool? — must convert from bool to avoid invalid IL.
                Expression.Convert(valueExpr, typeof(bool?))));

    private static MemberInitExpression MakeAttributeValueN(Expression valueExpr)
        => Expression.MemberInit(
            Expression.New(typeof(AttributeValue)),
            Expression.Bind(
                typeof(AttributeValue).GetProperty(nameof(AttributeValue.N))!,
                valueExpr));

    /// <summary>Builds <c>value.ToString()</c> for a type that has a zero-argument ToString.</summary>
    private static MethodCallExpression CallToString(Expression valueExpr)
        => Expression.Call(valueExpr, valueExpr.Type.GetMethod("ToString", Type.EmptyTypes)!);

    /// <summary>Builds <c>value.ToString(format)</c> for a type that accepts a format string.</summary>
    private static MethodCallExpression CallToStringFormat(Expression valueExpr, string format)
        => Expression.Call(
            valueExpr,
            valueExpr.Type.GetMethod("ToString", [typeof(string)])!,
            Expression.Constant(format));

    private static bool IsIntegralType(Type t)
        => t == typeof(int)
            || t == typeof(long)
            || t == typeof(short)
            || t == typeof(byte)
            || t == typeof(sbyte)
            || t == typeof(ushort)
            || t == typeof(uint)
            || t == typeof(ulong);

    private static bool IsFloatingPointType(Type t) => t == typeof(double) || t == typeof(float);

    // ──────────────────────────────────────────────────────────────────────────────
    //  CompiledEntitySerializer — holds the compiled state for one entity type
    // ──────────────────────────────────────────────────────────────────────────────

    private sealed class CompiledEntitySerializer(
        List<(string name, Func<IUpdateEntry, AttributeValue> fn)> propertySerializers,
        List<INavigation> ownedNavigations)
    {
        public Dictionary<string, AttributeValue> Serialize(
            IUpdateEntry entry,
            IReadOnlyDictionary<object, IUpdateEntry> ownedEntries,
            DynamoEntityItemSerializerSource source)
        {
            var result = new Dictionary<string, AttributeValue>(
                propertySerializers.Count + ownedNavigations.Count);

            // Scalar and primitive-collection properties — no boxing, no runtime type switch.
            foreach (var (name, fn) in propertySerializers)
                result[name] = fn(entry);

            // Owned navigations — inherently dynamic (cross-type CLR object lookups).
            foreach (var nav in ownedNavigations)
            {
                var navValue = entry.GetCurrentValue(nav);
                if (navValue is null)
                    continue; // null OwnsOne → omit attribute

                if (nav.IsCollection)
                {
                    var elements = new List<AttributeValue>();
                    if (navValue is IEnumerable collection)
                    {
                        foreach (var element in collection)
                        {
                            if (element is not null
                                && ownedEntries.TryGetValue(element, out var ownedEntry))
                                elements.Add(
                                    new AttributeValue
                                    {
                                        M = source.BuildItemFromOwnedEntry(
                                            ownedEntry,
                                            ownedEntries),
                                    });
                        }
                    }

                    result[nav.Name] = new AttributeValue { L = elements };
                }
                else
                {
                    if (ownedEntries.TryGetValue(navValue, out var ownedEntry))
                        result[nav.Name] = new AttributeValue
                        {
                            M = source.BuildItemFromOwnedEntry(ownedEntry, ownedEntries),
                        };
                }
            }

            return result;
        }
    }

    // ──────────────────────────────────────────────────────────────────────────────
    //  ParameterSubstitutor — inlines converter lambda bodies
    // ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Replaces occurrences of the <paramref name="from"/> parameter with <paramref name="to"/>
    /// when visiting an expression tree, enabling converter lambdas to be inlined without an
    /// Invoke node.
    /// </summary>
    private sealed class ParameterSubstitutor(ParameterExpression from, Expression to)
        : ExpressionVisitor
    {
        public static Expression Substitute(
            ParameterExpression from,
            Expression to,
            Expression body)
            => new ParameterSubstitutor(from, to).Visit(body);

        protected override Expression VisitParameter(ParameterExpression node)
            => node == from ? to : base.VisitParameter(node);
    }
}
