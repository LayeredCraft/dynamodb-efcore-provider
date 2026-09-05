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

            foreach (var segment in _segments)
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

            var bufferedValues = new List<object?>(segment.MaximumValueCount);
            foreach (var value in values)
            {
                if (bufferedValues.Count == segment.MaximumValueCount)
                    throw new InvalidOperationException(
                        DynamoStrings.InListTooLarge(
                            segment.MaximumValueCount,
                            segment.MaximumValueCount == 50));

                bufferedValues.Add(value);
            }

            if (bufferedValues.Count == 0)
            {
                sql.Append("1 = 0");
                return;
            }

            sql.Append(segment.Text);
            sql.Append(" IN [");
            for (var index = 0; index < bufferedValues.Count; index++)
            {
                if (index > 0)
                    sql.Append(", ");

                var value = bufferedValues[index];
                AppendParameter(
                    sql,
                    parameters,
                    value,
                    value?.GetType() ?? segment.SourceType!,
                    segment.TypeMapping!);
            }

            sql.Append(']');
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
    public static IAsyncEnumerable<T> CreateQueryingEnumerable<T>(
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
    public static IAsyncEnumerable<DynamoPage<T>> CreatePagingQueryingEnumerable<T>(
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

    /// <summary>Resolves the exact DynamoDB mapping used by generated command parameters.</summary>
    public static DynamoTypeMapping ResolveTypeMapping(
        MaterializerLiftableConstantContext context,
        Type clrType,
        string? declaringTypeName,
        string? propertyName)
    {
        if (declaringTypeName is not null && propertyName is not null)
            return ResolveProperty(context.Dependencies.Model, declaringTypeName, propertyName)
                    .GetTypeMapping() as DynamoTypeMapping
                ?? throw new InvalidOperationException(
                    $"Property '{declaringTypeName}.{propertyName}' does not use a DynamoDB type mapping.");

        return context.Dependencies.TypeMappingSource.FindMapping(clrType) as DynamoTypeMapping
            ?? throw new InvalidOperationException(
                $"CLR type '{clrType.Name}' does not use a DynamoDB type mapping.");
    }

    /// <summary>Creates a property-specific reader used by a generated row shaper.</summary>
    public static Func<Dictionary<string, AttributeValue>, T> CreateValueReader<T>(
        MaterializerLiftableConstantContext context,
        Type clrType,
        string? declaringTypeName,
        string? propertyName,
        string attributeName,
        string propertyPath,
        bool required)
    {
        var property = declaringTypeName is not null && propertyName is not null
            ? ResolveProperty(context.Dependencies.Model, declaringTypeName, propertyName)
            : null;
        var typeMapping = ResolveTypeMapping(context, clrType, declaringTypeName, propertyName);

        return CreateValueReader<T>(typeMapping, property, attributeName, propertyPath, required);
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
