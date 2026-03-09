using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Extensions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Infrastructure.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

public partial class DynamoShapedQueryCompilingExpressionVisitor(
    ShapedQueryCompilingExpressionVisitorDependencies dependencies,
    DynamoQueryCompilationContext dynamoQueryCompilationContext,
    IDynamoQuerySqlGeneratorFactory sqlGeneratorFactory) : ShapedQueryCompilingExpressionVisitor(
    dependencies,
    dynamoQueryCompilationContext)
{
    private readonly ShapedQueryCompilingExpressionVisitorDependencies _dependencies = dependencies;
    private int _runtimeParameterIndex;

    private static readonly MethodInfo EnsurePositivePageSizeMethodInfo =
        typeof(DynamoShapedQueryCompilingExpressionVisitor)
            .GetTypeInfo()
            .GetDeclaredMethod(nameof(EnsurePositivePageSize))!;

    /// <summary>Builds the runtime querying enumerable and shaper for a translated DynamoDB query.</summary>
    protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
    {
        var selectExpression = (SelectExpression)shapedQueryExpression.QueryExpression;

        // Apply deferred discriminator filtering after query composition so user predicates remain
        // first in generated SQL and discriminator clauses are appended.
        selectExpression.ApplyDeferredDiscriminatorPredicate();

        // Finalize projection mapping → concrete projection list
        selectExpression.ApplyProjection();

        if (dynamoQueryCompilationContext.ExplicitIndexName is { } indexName)
        {
            // Use query-root metadata carried by SelectExpression so validation remains scoped to
            // the original entity type even after Select(...) rewrites replace the final shaper.
            var queryRootEntityTypeName = selectExpression.RootEntityTypeName
                ?? TryGetRootEntityTypeNameFromShaper(shapedQueryExpression.ShaperExpression);

            ValidateExplicitIndexName(
                indexName,
                selectExpression.TableName,
                queryRootEntityTypeName);
            selectExpression.ApplyIndexName(indexName);
        }

        if (selectExpression.PageSize is null && selectExpression.PageSizeExpression is null)
        {
            if (dynamoQueryCompilationContext.PageSizeOverrideExpression is not null)
            {
                selectExpression.ApplyPageSizeExpression(
                    dynamoQueryCompilationContext.PageSizeOverrideExpression);
            }
            else if (dynamoQueryCompilationContext.PageSizeOverride.HasValue)
            {
                selectExpression.ApplyPageSize(
                    dynamoQueryCompilationContext.PageSizeOverride.Value);
            }
            else
            {
                var options = dynamoQueryCompilationContext.ContextOptions
                    .FindExtension<DynamoDbOptionsExtension>();
                if (options?.DefaultPageSize is not null)
                    selectExpression.ApplyPageSize(options.DefaultPageSize);
            }
        }

        if (selectExpression.ResultLimitExpression is not null)
            selectExpression.ApplyResultLimitExpression(
                NormalizeRuntimeIntExpression(
                    selectExpression.ResultLimitExpression,
                    "resultLimit",
                    false));

        if (selectExpression.PageSizeExpression is not null)
            selectExpression.ApplyPageSizeExpression(
                NormalizeRuntimeIntExpression(
                    selectExpression.PageSizeExpression,
                    "pageSize",
                    true));

        var shaperBody = shapedQueryExpression.ShaperExpression;

        // create shaper
        var itemParameter =
            Expression.Parameter(typeof(Dictionary<string, AttributeValue>), "item");

        // Step 1: Inject Dictionary<string, AttributeValue> variable handling
        // This adds null-checking and prepares the expression tree for materialization
        shaperBody = new DynamoInjectingExpressionVisitor().Visit(shaperBody);

        // Step 2: Inject EF Core's standard structural type materializers
        // This adds entity construction and property assignment logic
        shaperBody = InjectStructuralTypeMaterializers(shaperBody);

        // Step 3: Remove projection bindings and replace with actual dictionary access
        // This converts abstract ProjectionBindingExpression to concrete property access
        shaperBody = new DynamoProjectionBindingRemovingExpressionVisitor(
            itemParameter,
            selectExpression,
            QueryCompilationContext.Model,
            InjectStructuralTypeMaterializers).Visit(shaperBody);

        var shaperLambda = Expression.Lambda(
            shaperBody,
            QueryCompilationContext.QueryContextParameter,
            itemParameter);

        var queryContextParameter = Expression.Convert(
            QueryCompilationContext.QueryContextParameter,
            typeof(DynamoQueryContext));

        var standAloneStateManager = dynamoQueryCompilationContext.QueryTrackingBehavior
            == QueryTrackingBehavior.NoTrackingWithIdentityResolution;

        if (!dynamoQueryCompilationContext.IsAsync)
            throw new InvalidOperationException(
                "Synchronous query execution is not supported for DynamoDB. Use async methods (e.g. ToListAsync). ");

        return Expression.New(
            typeof(QueryingEnumerable<>)
                .MakeGenericType(shaperBody.Type)
                .GetConstructors(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Single(c => c.GetParameters().Length == 7),
            queryContextParameter,
            Expression.Constant(selectExpression),
            Expression.Constant(sqlGeneratorFactory),
            shaperLambda,
            Expression.Constant(standAloneStateManager),
            Expression.Constant(_dependencies.CoreSingletonOptions.AreThreadSafetyChecksEnabled),
            Expression.Constant(dynamoQueryCompilationContext.PaginationDisabled));
    }

    private Expression NormalizeRuntimeIntExpression(
        Expression expression,
        string parameterNamePrefix,
        bool requirePositive)
    {
        if (expression is ConstantExpression { Value: int })
            return expression;

        if (expression is QueryParameterExpression)
            return expression;

        var parameterName = $"__dynamo_{parameterNamePrefix}_{_runtimeParameterIndex++}";
        var injectedExpression = new DynamoInjectingExpressionVisitor().Visit(expression)
            ?? throw new InvalidOperationException(
                $"Unable to normalize {parameterNamePrefix} expression.");

        // Runtime parameters must be int-valued before registration so EF can cache and bind
        // query delegates consistently across executions.
        var convertedExpression = Expression.Convert(injectedExpression, typeof(int));
        Expression body = convertedExpression;
        if (requirePositive)
            body = Expression.Call(EnsurePositivePageSizeMethodInfo, convertedExpression);

        var valueExtractor = Expression.Lambda(body, QueryCompilationContext.QueryContextParameter);

        return QueryCompilationContext.RegisterRuntimeParameter(parameterName, valueExtractor);
    }

    private static int EnsurePositivePageSize(int value)
    {
        if (value <= 0)
            throw new InvalidOperationException(
                "WithPageSize must evaluate to a positive integer.");

        return value;
    }

    /// <summary>
    ///     Attempts to extract the root entity type name from the final shaper expression as a
    ///     fallback when query-root metadata is unavailable.
    /// </summary>
    private static string? TryGetRootEntityTypeNameFromShaper(Expression shaperExpression)
        => shaperExpression is StructuralTypeShaperExpression
        {
            StructuralType: IReadOnlyEntityType entityType,
        }
            ? entityType.GetRootType().Name
            : null;

    /// <summary>
    ///     Throws if the explicitly requested index name is not registered for the queried entity
    ///     type. Validation is scoped to the entity type being queried so that, in a shared-table
    ///     model, an index configured only on one entity type does not silently satisfy a query
    ///     against a different entity type that shares the same physical table.
    /// </summary>
    /// <param name="indexName">The index name supplied to <c>WithIndex</c>.</param>
    /// <param name="tableGroupName">Physical table group name from the <see cref="SelectExpression"/>.</param>
    /// <param name="rootEntityTypeName">
    ///     Name of the root entity type being queried, or <c>null</c> for non-entity projection queries.
    ///     When null, validation falls back to searching all entity type sources for the table group.
    /// </param>
    private void ValidateExplicitIndexName(
        string indexName,
        string tableGroupName,
        string? rootEntityTypeName)
    {
        var runtimeModel = dynamoQueryCompilationContext.Model.GetDynamoRuntimeTableModel();
        if (runtimeModel is null)
            return; // Runtime model not initialized; skip validation.

        if (!runtimeModel.Tables.TryGetValue(tableGroupName, out var tableDescriptor))
            return; // Table not found in runtime model; skip validation.

        // Scope to the specific entity type when available so that in a shared-table model an
        // index configured only on Order does not pass validation for an Invoice query.
        // This mirrors the lookup in DynamoSqlTranslatingExpressionVisitor.IsEffectivePartitionKey.
        IEnumerable<IReadOnlyList<DynamoIndexDescriptor>> sourceLists =
            rootEntityTypeName is not null
            && tableDescriptor.SourcesByEntityTypeName.TryGetValue(
                rootEntityTypeName,
                out var entitySources)
                ? [entitySources]
                : tableDescriptor.SourcesByEntityTypeName.Values;

        var indexExists = sourceLists.Any(sources => sources.Any(d => d.IndexName == indexName));

        if (!indexExists)
            throw new InvalidOperationException(
                $"Index '{indexName}' is not configured on table '{tableGroupName}'. "
                + "Use HasGlobalSecondaryIndex or HasLocalSecondaryIndex to register the "
                + "index before using WithIndex.");
    }

}
