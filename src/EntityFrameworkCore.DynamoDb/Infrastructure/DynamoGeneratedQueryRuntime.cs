using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Query.Internal;
using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using EntityFrameworkCore.DynamoDb.Storage;
using EntityFrameworkCore.DynamoDb.Storage.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCore.DynamoDb.Infrastructure;

/// <summary>Runtime support used by EF Core generated DynamoDB query interceptors.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
[Experimental("EF9100")]
public static class DynamoGeneratedQueryRuntime
{
    /// <summary>Represents a generated PartiQL command segment.</summary>
    public sealed class CommandSegment
    {
        private CommandSegment(
            SegmentKind kind,
            string? text,
            string? parameterName,
            object? constantValue,
            Type? sourceType,
            DynamoTypeMapping? typeMapping,
            int maximumValueCount)
        {
            Kind = kind;
            Text = text;
            ParameterName = parameterName;
            ConstantValue = constantValue;
            SourceType = sourceType;
            TypeMapping = typeMapping;
            MaximumValueCount = maximumValueCount;
        }

        internal SegmentKind Kind { get; }
        internal string? Text { get; }
        internal string? ParameterName { get; }
        internal object? ConstantValue { get; }
        internal Type? SourceType { get; }
        internal DynamoTypeMapping? TypeMapping { get; }
        internal int MaximumValueCount { get; }

        /// <summary>Creates a literal PartiQL text segment.</summary>
        public static CommandSegment TextSegment(string text)
            => new(SegmentKind.Text, text, null, null, null, null, 0);

        /// <summary>Creates a scalar query-parameter segment.</summary>
        public static CommandSegment Parameter(
            string parameterName,
            Type sourceType,
            DynamoTypeMapping typeMapping)
            => new(SegmentKind.Parameter, null, parameterName, null, sourceType, typeMapping, 0);

        /// <summary>Creates a generated constant-parameter segment.</summary>
        public static CommandSegment Constant(
            object constantValue,
            Type sourceType,
            DynamoTypeMapping typeMapping)
            => new(SegmentKind.Constant, null, null, constantValue, sourceType, typeMapping, 0);

        /// <summary>Creates a runtime-expanded collection-parameter segment.</summary>
        public static CommandSegment Collection(
            string itemSql,
            string parameterName,
            Type elementType,
            DynamoTypeMapping typeMapping,
            int maximumValueCount)
            => new(
                SegmentKind.Collection,
                itemSql,
                parameterName,
                null,
                elementType,
                typeMapping,
                maximumValueCount);
    }

    /// <summary>Contains a generated command and its DynamoDB execution settings.</summary>
    public sealed class QueryTemplate
    {
        private readonly CommandSegment[] _segments;

        internal QueryTemplate(
            CommandSegment[] segments,
            string tableName,
            string? indexName,
            bool isGlobalSecondaryIndex,
            bool isScanLike,
            string? scanMessage,
            bool scanAllowed,
            int? limit,
            string? limitParameterName,
            string? seedNextToken,
            string? seedNextTokenParameterName,
            bool? consistentRead,
            string? consistentReadParameterName,
            bool hasUserLimit,
            bool isFirstTerminal,
            bool isSingleTerminal)
        {
            _segments = segments;
            TableName = tableName;
            IndexName = indexName;
            IsGlobalSecondaryIndex = isGlobalSecondaryIndex;
            IsScanLike = isScanLike;
            ScanMessage = scanMessage;
            ScanAllowed = scanAllowed;
            Limit = limit;
            LimitParameterName = limitParameterName;
            SeedNextToken = seedNextToken;
            SeedNextTokenParameterName = seedNextTokenParameterName;
            ConsistentRead = consistentRead;
            ConsistentReadParameterName = consistentReadParameterName;
            HasUserLimit = hasUserLimit;
            IsFirstTerminal = isFirstTerminal;
            IsSingleTerminal = isSingleTerminal;
        }

        internal string TableName { get; }
        internal string? IndexName { get; }
        internal bool IsGlobalSecondaryIndex { get; }
        internal bool IsScanLike { get; }
        internal string? ScanMessage { get; }
        internal bool ScanAllowed { get; }
        internal int? Limit { get; }
        internal string? LimitParameterName { get; }
        internal string? SeedNextToken { get; }
        internal string? SeedNextTokenParameterName { get; }
        internal bool? ConsistentRead { get; }
        internal string? ConsistentReadParameterName { get; }
        internal bool HasUserLimit { get; }
        internal bool IsFirstTerminal { get; }
        internal bool IsSingleTerminal { get; }
        internal IReadOnlyList<CommandSegment> Segments => _segments;

        internal DynamoPartiQlQuery Render(IReadOnlyDictionary<string, object?> parameterValues)
        {
            var sql = new StringBuilder();
            var parameters = new List<AttributeValue>();

            RenderSegments(_segments, parameterValues, sql, parameters);

            return new DynamoPartiQlQuery(sql.ToString(), parameters);
        }

        internal SelectExpression CreateExecutionExpression()
        {
            var selectExpression = new SelectExpression(TableName);
            selectExpression.ApplyIndexName(IndexName);
            if (IsGlobalSecondaryIndex)
                selectExpression.ApplyIndexSourceKind(DynamoIndexSourceKind.GlobalSecondaryIndex);

            if (IsScanLike)
                selectExpression.ApplyScanQueryClassification(
                    new DynamoScanQueryClassification(
                        true,
                        IndexName is null ? $"table '{TableName}'" : $"index '{IndexName}'",
                        "Generated query classification",
                        ScanMessage
                        ?? "The generated DynamoDB query requires scan-like execution."));

            if (ScanAllowed)
                selectExpression.AllowScan();

            if (LimitParameterName is not null)
            {
                selectExpression.ApplyUserLimitExpression(
                    new QueryParameterExpression(LimitParameterName, typeof(int)));
            }
            else if (Limit is { } limit)
            {
                if (HasUserLimit)
                    selectExpression.ApplyUserLimit(limit);
                else
                    selectExpression.ApplyImplicitLimit(limit);
            }

            if (SeedNextTokenParameterName is not null)
                selectExpression.ApplySeedNextTokenExpression(
                    new QueryParameterExpression(SeedNextTokenParameterName, typeof(string)));
            else if (SeedNextToken is not null)
                selectExpression.ApplySeedNextToken(SeedNextToken);

            if (ConsistentReadParameterName is not null)
                selectExpression.ApplyConsistentReadExpression(
                    new QueryParameterExpression(ConsistentReadParameterName, typeof(bool)));
            else if (ConsistentRead is { } consistentRead)
                selectExpression.ApplyConsistentRead(consistentRead);

            if (IsFirstTerminal)
                selectExpression.MarkAsFirstTerminal();
            if (IsSingleTerminal)
                selectExpression.MarkAsSingleTerminal();

            return selectExpression;
        }
    }

    private static void AppendCollection(
        StringBuilder sql,
        List<AttributeValue> parameters,
        IReadOnlyDictionary<string, object?> parameterValues,
        CommandSegment segment)
    {
        if (!parameterValues.TryGetValue(segment.ParameterName!, out var parameterValue))
            throw new InvalidOperationException(
                $"Parameter '{segment.ParameterName}' not found in parameter values.");

        if (parameterValue is null)
        {
            sql.Append("1 = 0");
            return;
        }

        if (parameterValue is string || parameterValue is not IEnumerable values)
            throw new InvalidOperationException(
                DynamoStrings.ContainsCollectionParameterMustBeEnumerable);

        // Stream values straight into SQL/parameters: no fixed-capacity buffer, one-shot
        // enumerables are enumerated exactly once, and the limit check fires on the first
        // value beyond the allowed count.
        var count = 0;
        var segmentStart = sql.Length;
        sql.Append(segment.Text);
        sql.Append(" IN [");
        foreach (var value in values)
        {
            if (count == segment.MaximumValueCount)
                throw new InvalidOperationException(
                    DynamoStrings.InListTooLarge(
                        segment.MaximumValueCount,
                        segment.MaximumValueCount == 50));

            if (count > 0)
                sql.Append(", ");

            AppendParameter(
                sql,
                parameters,
                value,
                value?.GetType() ?? segment.SourceType!,
                segment.TypeMapping!);
            count++;
        }

        if (count == 0)
        {
            sql.Length = segmentStart;
            sql.Append("1 = 0");
            return;
        }

        sql.Append(']');
    }

    private static void RenderSegments(
        CommandSegment[] segments,
        IReadOnlyDictionary<string, object?> parameterValues,
        StringBuilder sql,
        List<AttributeValue> parameters)
    {
        foreach (var segment in segments)
            switch (segment.Kind)
            {
                case SegmentKind.Text:
                    sql.Append(segment.Text);
                    break;

                case SegmentKind.Parameter:
                    if (!parameterValues.TryGetValue(segment.ParameterName!, out var value))
                        throw new InvalidOperationException(
                            $"Parameter '{segment.ParameterName}' not found in parameter values.");

                    AppendParameter(
                        sql,
                        parameters,
                        value,
                        segment.SourceType!,
                        segment.TypeMapping!);
                    break;

                case SegmentKind.Constant:
                    AppendParameter(
                        sql,
                        parameters,
                        segment.ConstantValue,
                        segment.SourceType!,
                        segment.TypeMapping!);
                    break;

                case SegmentKind.Collection:
                    AppendCollection(sql, parameters, parameterValues, segment);
                    break;

                default:
                    throw new UnreachableException();
            }
    }

    private static void AppendParameter(
        StringBuilder sql,
        List<AttributeValue> parameters,
        object? value,
        Type sourceType,
        DynamoTypeMapping typeMapping)
    {
        sql.Append('?');
        parameters.Add(typeMapping.CreateAttributeValue(value, sourceType));
    }

    /// <summary>Creates the runtime form of a generated query template.</summary>
    public static QueryTemplate CreateQueryTemplate(
        CommandSegment[] segments,
        string tableName,
        string? indexName,
        bool globalSecondaryIndex,
        bool scanLike,
        string? scanMessage,
        bool scanAllowed,
        int? limit,
        string? limitParameterName,
        string? seedNextToken,
        string? seedNextTokenParameterName,
        bool? consistentRead,
        string? consistentReadParameterName,
        bool userLimit,
        bool firstTerminal,
        bool singleTerminal)
        => new(
            segments,
            tableName,
            indexName,
            globalSecondaryIndex,
            scanLike,
            scanMessage,
            scanAllowed,
            limit,
            limitParameterName,
            seedNextToken,
            seedNextTokenParameterName,
            consistentRead,
            consistentReadParameterName,
            userLimit,
            firstTerminal,
            singleTerminal);

    /// <summary>Creates a generated asynchronous query enumerable.</summary>
    public static IAsyncEnumerable<T> CreateAsyncQueryingEnumerable<T>(
        QueryContext queryContext,
        QueryTemplate queryTemplate,
        Func<QueryContext, Dictionary<string, AttributeValue>, T> shaper,
        bool standAloneStateManager,
        bool threadSafetyChecksEnabled)
        => new DynamoShapedQueryCompilingExpressionVisitor.QueryingEnumerable<T>(
            (DynamoQueryContext)queryContext,
            queryTemplate,
            shaper,
            standAloneStateManager,
            threadSafetyChecksEnabled);

    /// <summary>Creates a generated asynchronous paging enumerable.</summary>
#pragma warning disable EF9102
    public static IAsyncEnumerable<DynamoPage<T>> CreateAsyncPagingQueryingEnumerable<T>(
        QueryContext queryContext,
        QueryTemplate queryTemplate,
        Func<QueryContext, Dictionary<string, AttributeValue>, T> shaper,
        bool standAloneStateManager,
        bool threadSafetyChecksEnabled)
        => new DynamoShapedQueryCompilingExpressionVisitor.PagingQueryingEnumerable<T>(
            (DynamoQueryContext)queryContext,
            queryTemplate,
            shaper,
            standAloneStateManager,
            threadSafetyChecksEnabled);
#pragma warning restore EF9102

    /// <summary>Contains a generated ExecuteUpdate command and its target table.</summary>
    public sealed class UpdateTemplate
    {
        private readonly CommandSegment[] _segments;

        internal UpdateTemplate(CommandSegment[] segments, string tableName)
        {
            _segments = segments;
            TableName = tableName;
        }

        internal string TableName { get; }

        internal IReadOnlyList<CommandSegment> Segments => _segments;

        /// <summary>Renders the generated UPDATE statement and its positional parameters.</summary>
        public DynamoPartiQlQuery Render(IReadOnlyDictionary<string, object?> parameterValues)
        {
            var sql = new StringBuilder();
            var parameters = new List<AttributeValue>();

            RenderSegments(_segments, parameterValues, sql, parameters);

            var statement = sql.ToString();
            DynamoPartiQlStatementValidator.ValidateStatementLength(statement, "write");

            return new DynamoPartiQlQuery(statement, parameters);
        }
    }

    /// <summary>Creates the runtime form of a generated ExecuteUpdate template.</summary>
    public static UpdateTemplate CreateUpdateTemplate(CommandSegment[] segments, string tableName)
        => new(segments, tableName);

    /// <summary>Creates a generated asynchronous ExecuteUpdate executor.</summary>
    public static Task<int> CreateUpdateExecutorAsync(
        QueryContext queryContext,
        UpdateTemplate updateTemplate)
    {
        var dynamoQueryContext = (DynamoQueryContext)queryContext;
        var sqlQuery = updateTemplate.Render(dynamoQueryContext.Parameters);

        return dynamoQueryContext.Client.ExecuteWriteResultAsync(
            sqlQuery.Sql,
            [.. sqlQuery.Parameters],
            updateTemplate.TableName,
            dynamoQueryContext.CancellationToken);
    }

    /// <summary>Resolves the exact DynamoDB mapping used by generated command parameters.</summary>
    /// <remarks>
    ///     Generated code must bind every mapping to its owning model property (optionally through
    ///     an element-mapping chain). The previous CLR-type <c>FindMapping</c> fallback built codecs
    ///     via <c>MakeGenericMethod</c>/<c>Activator.CreateInstance</c> reflection, which fails under
    ///     NativeAOT at first query execution, so it was removed in favor of fail-fast errors.
    /// </remarks>
    public static DynamoTypeMapping ResolveTypeMapping(
        MaterializerLiftableConstantContext context,
        string declaringTypeName,
        string propertyName,
        int elementTypeMappingDepth = 0)
        => ResolveTypeMapping(
            ResolveProperty(context.Dependencies.Model, declaringTypeName, propertyName),
            elementTypeMappingDepth);

    private static DynamoTypeMapping ResolveTypeMapping(
        IProperty property,
        int elementTypeMappingDepth)
    {
        var mapping = property.GetTypeMapping() as DynamoTypeMapping;
        for (var index = 0; index < elementTypeMappingDepth && mapping is not null; index++)
            mapping = mapping.ElementTypeMapping as DynamoTypeMapping;

        return mapping
            ?? throw new InvalidOperationException(
                $"Property '{property.DeclaringType.Name}.{property.Name}' does not use a DynamoDB type mapping "
                + $"at element depth {elementTypeMappingDepth}.");
    }

    /// <summary>Creates a property-specific reader used by a generated row shaper.</summary>
    public static Func<Dictionary<string, AttributeValue>, T> CreateValueReader<T>(
        MaterializerLiftableConstantContext context,
        string declaringTypeName,
        string propertyName,
        int elementTypeMappingDepth,
        string attributeName,
        string propertyPath,
        bool required)
    {
        var property = ResolveProperty(context.Dependencies.Model, declaringTypeName, propertyName);
        var typeMapping = ResolveTypeMapping(property, elementTypeMappingDepth);

        return CreateValueReader<T>(typeMapping, property, attributeName, propertyPath, required);
    }

    /// <summary>Reads a converter-less scalar property directly from a DynamoDB item.</summary>
    /// <remarks>
    ///     Generated (precompiled) shapers call this directly for unconverted scalar properties:
    ///     a static typed call that embeds safely anywhere in generated C#, with no per-property
    ///     reader delegate and no expression-tree Block variables. Missing and NULL handling
    ///     matches the generated reader path (<c>CreateValueReader</c>).
    /// </remarks>
    public static T ReadScalar<T>(
        Dictionary<string, AttributeValue> item,
        string attributeName,
        string propertyPath,
        bool required)
    {
        if (!item.TryGetValue(attributeName, out var attributeValue))
        {
            if (required)
                throw new InvalidOperationException(
                    $"Required property '{propertyPath}' was not present in the DynamoDB item.");

            return default!;
        }

        return DynamoScalarCodecs<T>.Instance.Read(attributeValue, propertyPath, required, null);
    }

    /// <summary>Validates a runtime <c>WithNextToken(...)</c> continuation-token value.</summary>
    /// <remarks>
    ///     A parameterized <c>WithNextToken(nextToken)</c> call resolves its argument through a
    ///     generated runtime-parameter extractor rather than a query-time constant, so the
    ///     resulting generated C# calls this method by name. It therefore needs a public,
    ///     stable-signature home outside the translation visitor, which generated code cannot
    ///     otherwise reference.
    /// </remarks>
    /// <param name="nextToken">The candidate continuation token.</param>
    /// <returns><paramref name="nextToken" />, unchanged, once validated.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="nextToken" /> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="nextToken" /> is empty or whitespace.</exception>
    public static string ValidateWithNextToken(string? nextToken)
    {
        if (nextToken is null)
            throw new ArgumentNullException(nameof(nextToken));

        if (string.IsNullOrWhiteSpace(nextToken))
            throw new ArgumentException("Next token must not be empty.", nameof(nextToken));

        return nextToken;
    }

    private static class DynamoScalarCodecs<T>
    {
        // Scalar codecs are stateless; one instance per closed generic type replaces per-row
        // construction and per-property reader delegates.
        public static readonly DynamoValueReaderWriter<T> Instance = Create();

        private static DynamoValueReaderWriter<T> Create()
            => DynamoValueReaderWriterFactory.Create(typeof(T)) is DynamoValueReaderWriter<T> codec
                ? codec
                : throw new InvalidOperationException(
                    $"CLR type '{typeof(T).Name}' does not have a DynamoDB scalar codec.");
    }

    internal static Func<Dictionary<string, AttributeValue>, T> CreateValueReader<T>(
        DynamoTypeMapping typeMapping,
        IProperty? property,
        string attributeName,
        string propertyPath,
        bool required)
    {
        var readerWriter = typeMapping.ReaderWriter
            ?? throw new InvalidOperationException(
                $"Property '{propertyPath}' has no DynamoDB value reader.");

        return item =>
        {
            if (!item.TryGetValue(attributeName, out var attributeValue))
            {
                if (required)
                    throw new InvalidOperationException(
                        $"Required property '{propertyPath}' was not present in the DynamoDB item.");

                return default!;
            }

            // A NULL wire value must materialize as the CLR default for the requested reader
            // type. Nullable properties read through converted wrappers lose the null marker
            // otherwise (default(provider type) is boxed as a non-null value), so resolve the
            // missing value here, before any wrapper-specific read runs.
            if (!readerWriter.HasValue(attributeValue))
            {
                if (required)
                    throw new InvalidOperationException(
                        $"Required property '{propertyPath}' did not contain a value for expected "
                        + $"DynamoDB wire member '{readerWriter.WireMemberName}'.");

                return default!;
            }

            if (readerWriter is DynamoValueReaderWriter<T> typedReaderWriter)
                return typedReaderWriter.Read(attributeValue, propertyPath, required, property);

            var value = readerWriter.ReadObject(attributeValue, propertyPath, required, property);
            return value is null ? default! : (T)value;
        };
    }

    private static IProperty
        ResolveProperty(IModel model, string declaringTypeName, string propertyName)
        => model
                .GetEntityTypes()
                .SelectMany(static entityType => entityType.GetFlattenedProperties())
                .Distinct<IProperty>(ReferenceEqualityComparer.Instance)
                .SingleOrDefault(property
                    => property.DeclaringType.Name == declaringTypeName
                    && property.Name == propertyName)
            ?? throw new InvalidOperationException(
                $"The generated query property '{declaringTypeName}.{propertyName}' was not found in the runtime model.");

    internal enum SegmentKind
    {
        Text,
        Parameter,
        Constant,
        Collection
    }
}
