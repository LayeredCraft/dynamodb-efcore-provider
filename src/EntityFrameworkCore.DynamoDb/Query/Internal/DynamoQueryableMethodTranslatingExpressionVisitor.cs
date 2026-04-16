using System.Linq.Expressions;
using System.Reflection;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>Represents the DynamoQueryableMethodTranslatingExpressionVisitor type.</summary>
public class DynamoQueryableMethodTranslatingExpressionVisitor
    : QueryableMethodTranslatingExpressionVisitor
{
    private readonly bool _subquery;
    private readonly DynamoSqlTranslatingExpressionVisitor _sqlTranslator;
    private readonly DynamoProjectionBindingExpressionVisitor _projectionBindingExpressionVisitor;
    private readonly ISqlExpressionFactory _sqlExpressionFactory;
    private int _runtimeParameterIndex;

    private static readonly MethodInfo ResolveEffectiveNextTokenMethodInfo =
        typeof(DynamoQueryableMethodTranslatingExpressionVisitor).GetMethod(
            nameof(ResolveEffectiveNextToken),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    private static readonly MethodInfo ValidateWithNextTokenMethodInfo =
        typeof(DynamoQueryableMethodTranslatingExpressionVisitor).GetMethod(
            nameof(ValidateWithNextToken),
            BindingFlags.Static | BindingFlags.NonPublic)!;

    /// <summary>Provides functionality for this member.</summary>
    public DynamoQueryableMethodTranslatingExpressionVisitor(
        QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
        QueryCompilationContext queryCompilationContext,
        ISqlExpressionFactory sqlExpressionFactory,
        bool subquery = false) : base(dependencies, queryCompilationContext, subquery)
    {
        _subquery = subquery;
        _sqlExpressionFactory = sqlExpressionFactory;
        _sqlTranslator = new DynamoSqlTranslatingExpressionVisitor(
            sqlExpressionFactory,
            queryCompilationContext as DynamoQueryCompilationContext);
        _projectionBindingExpressionVisitor = new DynamoProjectionBindingExpressionVisitor(
            _sqlTranslator,
            sqlExpressionFactory,
            queryCompilationContext.Model);
    }

    /// <summary>Creates a translation visitor for subquery pipelines.</summary>
    protected override QueryableMethodTranslatingExpressionVisitor CreateSubqueryVisitor()
        => new DynamoQueryableMethodTranslatingExpressionVisitor(
            Dependencies,
            QueryCompilationContext,
            _sqlExpressionFactory,
            true);

    /// <summary>Provides functionality for this member.</summary>
    public override Expression Translate(Expression expression)
    {
        // Handle ToPageAsync(), which can only ever be the top-level node in the query tree.
        if (expression is MethodCallExpression { Method: var method, Arguments: var arguments, }
            && method.DeclaringType == typeof(DynamoDbQueryableExtensions)
            && method.Name == nameof(DynamoDbQueryableExtensions.ToPageAsync))
        {
            if (_subquery)
            {
                AddTranslationErrorDetails(
                    $"'{nameof(DynamoDbQueryableExtensions.ToPageAsync)}' can only be used as the top-level terminal operation.");

                return QueryCompilationContext.NotTranslatedExpression;
            }

            var source = base.Translate(arguments[0]);
            if (source == QueryCompilationContext.NotTranslatedExpression)
                return source;

            if (source is not ShapedQueryExpression
                {
                    QueryExpression: SelectExpression selectExpression,
                } shapedQuery)
                throw new InvalidOperationException(
                    $"Expected a {nameof(ShapedQueryExpression)} when translating {nameof(DynamoDbQueryableExtensions.ToPageAsync)}.");

            // Per issue contract, ToPageAsync cannot be combined with Limit(n) on the same query.
            if (selectExpression.HasUserLimit)
                throw new InvalidOperationException(
                    $"'{nameof(DynamoDbQueryableExtensions.ToPageAsync)}' cannot be combined with '{nameof(DynamoDbQueryableExtensions.Limit)}'.");

            var limitArg = arguments[1];
            if (limitArg is ConstantExpression { Value: int constantLimit })
            {
                if (constantLimit <= 0)
                    throw new ArgumentOutOfRangeException(
                        "limit",
                        "Limit must be a positive integer.");

                selectExpression.ApplyUserLimit(constantLimit);
            }
            else
            {
                selectExpression.ApplyUserLimitExpression(limitArg);
            }

            ApplyToPageNextToken(selectExpression, arguments[2]);

#pragma warning disable EF9102
            return shapedQuery
                .UpdateShaperExpression(
                    new DynamoPagingExpression(
                        shapedQuery.ShaperExpression,
                        selectExpression.LimitExpression
                        ?? throw new InvalidOperationException(
                            $"'{nameof(DynamoDbQueryableExtensions.ToPageAsync)}' did not produce a limit expression."),
                        selectExpression.SeedNextTokenExpression,
                        typeof(DynamoPage<>).MakeGenericType(shapedQuery.ShaperExpression.Type)))
                .UpdateResultCardinality(ResultCardinality.Single);
#pragma warning restore EF9102
        }

        return base.Translate(expression);
    }

    private void ApplyToPageNextToken(
        SelectExpression selectExpression,
        Expression toPageNextTokenExpression)
    {
        var hasExistingSeed = selectExpression.SeedNextTokenExpression is not null;
        var hasConstantToken = TryGetNormalizedConstantToken(
            toPageNextTokenExpression,
            out var normalizedConstantToken);

        if (hasExistingSeed)
        {
            // Ambiguous only when ToPageAsync contributes a definitely non-null token.
            if (hasConstantToken && normalizedConstantToken is not null)
                throw new InvalidOperationException(
                    "Only one non-null pagination token may be specified. Use either WithNextToken(...) or ToPageAsync(..., nextToken: ...), but not both.");

            if (hasConstantToken)
                return;

            // Parameterized method token: resolve ambiguity at runtime so explicit null can flow
            // through while non-null still fails.
            selectExpression.ApplySeedNextTokenExpression(
                RegisterEffectiveNextTokenRuntimeParameter(
                    selectExpression.SeedNextTokenExpression!,
                    toPageNextTokenExpression));

            return;
        }

        if (hasConstantToken)
        {
            if (normalizedConstantToken is not null)
                selectExpression.ApplySeedNextToken(normalizedConstantToken);

            return;
        }

        selectExpression.ApplySeedNextTokenExpression(toPageNextTokenExpression);
    }

    private QueryParameterExpression RegisterEffectiveNextTokenRuntimeParameter(
        Expression existingSeedExpression,
        Expression toPageNextTokenExpression)
    {
        var parameterValuesExpression = Expression.Property(
            QueryCompilationContext.QueryContextParameter,
            nameof(QueryContext.Parameters));

        var existingTokenExpression = CreateRuntimeTokenReadExpression(
            existingSeedExpression,
            parameterValuesExpression);
        var toPageTokenExpression = CreateRuntimeTokenReadExpression(
            toPageNextTokenExpression,
            parameterValuesExpression);

        var valueExtractor = Expression.Lambda(
            Expression.Call(
                ResolveEffectiveNextTokenMethodInfo,
                existingTokenExpression,
                toPageTokenExpression),
            QueryCompilationContext.QueryContextParameter);

        return QueryCompilationContext.RegisterRuntimeParameter(
            $"__dynamo_effective_next_token_{_runtimeParameterIndex++}",
            valueExtractor);
    }

    private QueryParameterExpression RegisterValidatedWithNextTokenRuntimeParameter(
        Expression nextTokenExpression)
    {
        var parameterValuesExpression = Expression.Property(
            QueryCompilationContext.QueryContextParameter,
            nameof(QueryContext.Parameters));

        var tokenExpression = CreateRuntimeTokenReadExpression(
            nextTokenExpression,
            parameterValuesExpression);

        var valueExtractor = Expression.Lambda(
            Expression.Call(ValidateWithNextTokenMethodInfo, tokenExpression),
            QueryCompilationContext.QueryContextParameter);

        return QueryCompilationContext.RegisterRuntimeParameter(
            $"__dynamo_validated_with_next_token_{_runtimeParameterIndex++}",
            valueExtractor);
    }

    private static Expression CreateRuntimeTokenReadExpression(
        Expression tokenExpression,
        MemberExpression parameterValuesExpression)
    {
        if (TryGetNormalizedConstantToken(tokenExpression, out var normalizedConstantToken))
            return Expression.Constant(normalizedConstantToken, typeof(string));

        if (tokenExpression is QueryParameterExpression parameterExpression)
            return Expression.TypeAs(
                Expression.Property(
                    parameterValuesExpression,
                    "Item",
                    Expression.Constant(parameterExpression.Name)),
                typeof(string));

        throw new InvalidOperationException(
            "Next-token expression must be normalized before translation.");
    }

    private static string? ResolveEffectiveNextToken(string? existingToken, string? toPageToken)
    {
        existingToken = string.IsNullOrWhiteSpace(existingToken) ? null : existingToken;
        toPageToken = string.IsNullOrWhiteSpace(toPageToken) ? null : toPageToken;

        if (existingToken is not null && toPageToken is not null)
            throw new InvalidOperationException(
                "Only one non-null pagination token may be specified. Use either WithNextToken(...) or ToPageAsync(..., nextToken: ...), but not both.");

        return toPageToken ?? existingToken;
    }

    private static string ValidateWithNextToken(string? nextToken)
    {
        if (nextToken is null)
            throw new ArgumentNullException("nextToken");

        if (string.IsNullOrWhiteSpace(nextToken))
            throw new ArgumentException("Next token must not be empty.", "nextToken");

        return nextToken;
    }

    private static bool TryGetNormalizedConstantToken(
        Expression tokenExpression,
        out string? normalizedToken)
    {
        if (tokenExpression is ConstantExpression)
        {
            normalizedToken = tokenExpression switch
            {
                ConstantExpression { Value: string token } => string.IsNullOrWhiteSpace(token)
                    ? null
                    : token,
                ConstantExpression { Value: null } => null,
                _ => null,
            };

            return true;
        }

        normalizedToken = null;
        return false;
    }

    /// <summary>Provides functionality for this member.</summary>
    protected override Expression VisitMethodCall(MethodCallExpression methodCallExpression)
    {
        var method = methodCallExpression.Method;

        // Check for DynamoDB-specific extension methods
        if (method.DeclaringType == typeof(DynamoDbQueryableExtensions))
        {
            if (method.Name == nameof(DynamoDbQueryableExtensions.Limit))
            {
                // Visit inner source first so SelectExpression exists before applying the limit.
                var limitResult = Visit(methodCallExpression.Arguments[0]);
                if (limitResult is not ShapedQueryExpression
                    {
                        QueryExpression: SelectExpression limitSelectExpr,
                    })
                    return limitResult;

                var limitArg = methodCallExpression.Arguments[1];

                if (limitArg is ConstantExpression { Value: int constantLimit })
                {
                    // Constant: validate immediately, apply directly.
                    if (constantLimit <= 0)
                        throw new ArgumentOutOfRangeException(
                            "limit",
                            "Limit must be a positive integer.");
                    limitSelectExpr.ApplyUserLimit(constantLimit);
                }
                else
                {
                    // Parameterized: store expression, validate at runtime.
                    limitSelectExpr.ApplyUserLimitExpression(limitArg);
                }

                return limitResult;
            }

            if (method.Name == nameof(DynamoDbQueryableExtensions.WithoutIndex))
            {
                var context = (DynamoQueryCompilationContext)QueryCompilationContext;
                context.IndexSelectionDisabled = true;

                // Continue visiting the source (prune this extension from the tree)
                return Visit(methodCallExpression.Arguments[0]);
            }

            if (method.Name == nameof(DynamoDbQueryableExtensions.WithIndex))
            {
                var context = (DynamoQueryCompilationContext)QueryCompilationContext;

                if (context.ExplicitIndexName is null)
                {
                    // The indexName parameter is marked [NotParameterized], so EF Core's
                    // funcletizer
                    // leaves it as a ConstantExpression for normal queries. If it's not a constant,
                    // the index name cannot be embedded in the PartiQL FROM clause — throw rather
                    // than silently falling back to the base table.
                    if (methodCallExpression.Arguments[1] is not ConstantExpression
                        {
                            Value: string indexName,
                        })
                        throw new InvalidOperationException(
                            $"'{nameof(DynamoDbQueryableExtensions.WithIndex)}' requires a constant index name. "
                            + "Index names are embedded in the PartiQL FROM clause and cannot be query parameters. "
                            + "Pass a string literal or capture a local variable in a regular (non-compiled) query.");

                    context.ExplicitIndexName = indexName;
                }

                return Visit(methodCallExpression.Arguments[0]);
            }

            if (method.Name == nameof(DynamoDbQueryableExtensions.WithNextToken))
            {
                // Visit inner source first so SelectExpression exists before applying the seed
                // token.
                var nextTokenResult = Visit(methodCallExpression.Arguments[0]);
                if (nextTokenResult is not ShapedQueryExpression
                    {
                        QueryExpression: SelectExpression nextTokenSelectExpr,
                    })
                    return nextTokenResult;

                if (nextTokenSelectExpr.SeedNextTokenExpression is not null)
                    throw new InvalidOperationException(
                        $"'{nameof(DynamoDbQueryableExtensions.WithNextToken)}' can only be applied once per query.");

                var nextTokenArg = methodCallExpression.Arguments[1];

                if (nextTokenArg is ConstantExpression { Value: null })
                    throw new ArgumentNullException("nextToken");

                if (nextTokenArg is ConstantExpression { Value: string constantToken })
                {
                    if (string.IsNullOrWhiteSpace(constantToken))
                        throw new ArgumentException("Next token must not be empty.", "nextToken");

                    nextTokenSelectExpr.ApplySeedNextToken(constantToken);
                }
                else
                    nextTokenSelectExpr.ApplySeedNextTokenExpression(
                        RegisterValidatedWithNextTokenRuntimeParameter(nextTokenArg));

                return nextTokenResult;
            }
        }

        return base.VisitMethodCall(methodCallExpression);
    }

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? CreateShapedQueryExpression(IEntityType entityType)
    {
        // Get the table name from entity metadata.
        var tableName = entityType.GetTableGroupName();

        var queryExpression = new SelectExpression(tableName, entityType.Name);

        // Create entity projection expression as single source of truth for property mapping
        var entityProjection =
            new DynamoEntityProjectionExpression(entityType, _sqlExpressionFactory);

        var discriminatorPredicate = CreateDiscriminatorPredicate(entityType, entityProjection);
        if (discriminatorPredicate is not null)
            queryExpression.SetDeferredDiscriminatorPredicate(
                new SqlDiscriminatorPredicateExpression(discriminatorPredicate));

        // Store entity projection in projection mapping under root ProjectionMember
        var projectionMapping = new Dictionary<ProjectionMember, Expression>
        {
            [new ProjectionMember()] = entityProjection,
        };

        queryExpression.ReplaceProjectionMapping(projectionMapping);

        var projectionBindingExpression = new ProjectionBindingExpression(
            queryExpression,
            new ProjectionMember(),
            typeof(ValueBuffer));

        var structuralTypeShaperExpression =
            new StructuralTypeShaperExpression(entityType, projectionBindingExpression, false);

        return new ShapedQueryExpression(queryExpression, structuralTypeShaperExpression);
    }

    /// <summary>
    ///     Creates a discriminator predicate for root queries when the table group contains multiple
    ///     concrete entity types.
    /// </summary>
    private SqlExpression? CreateDiscriminatorPredicate(
        IEntityType entityType,
        DynamoEntityProjectionExpression entityProjection)
    {
        if (!RequiresDiscriminatorPredicate(entityType))
            return null;

        var discriminatorProperty = entityType.FindDiscriminatorProperty();
        if (discriminatorProperty is null)
            return null;

        var discriminatorColumn = entityProjection.BindProperty(discriminatorProperty);

        SqlExpression? predicate = null;
        foreach (var concreteType in entityType.GetConcreteDerivedTypesInclusive())
        {
            if (concreteType.IsOwned() || concreteType.ClrType.IsAbstract)
                continue;

            var discriminatorValue = concreteType.GetDiscriminatorValue();
            if (discriminatorValue is null)
                continue;

            var equals = _sqlExpressionFactory.Binary(
                ExpressionType.Equal,
                discriminatorColumn,
                _sqlExpressionFactory.Constant(discriminatorValue, discriminatorProperty.ClrType));

            if (equals is null)
                throw new InvalidOperationException(
                    $"Failed to create discriminator predicate for entity type '{entityType.DisplayName()}'.");

            predicate = predicate is null
                ? equals
                : _sqlExpressionFactory.Binary(ExpressionType.OrElse, predicate, equals)
                ?? throw new InvalidOperationException(
                    $"Failed to compose discriminator predicate for entity type '{entityType.DisplayName()}'.");
        }

        return predicate switch
        {
            SqlBinaryExpression { OperatorType: ExpressionType.OrElse } =>
                new SqlParenthesizedExpression(predicate),
            _ => predicate,
        };
    }

    /// <summary>
    ///     Determines whether discriminator filtering is required for the root entity type's table
    ///     group.
    /// </summary>
    private bool RequiresDiscriminatorPredicate(IEntityType entityType)
    {
        var tableGroupName = entityType.GetTableGroupName();

        HashSet<IReadOnlyEntityType> concreteTypes = [];
        foreach (var rootEntityType in QueryCompilationContext
            .Model
            .EnumerateRootEntityTypes()
            .Where(t => string.Equals(
                t.GetTableGroupName(),
                tableGroupName,
                StringComparison.Ordinal)))
        {
            foreach (var concreteType in rootEntityType.GetConcreteDerivedTypesInclusive())
            {
                if (concreteType.IsOwned() || concreteType.ClrType.IsAbstract)
                    continue;

                concreteTypes.Add(concreteType);
            }
        }

        return concreteTypes.Count > 1;
    }

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateAll(
        ShapedQueryExpression source,
        LambdaExpression predicate)
        => UnsupportedOperator(
            nameof(Queryable.All),
            DynamoStrings.ProviderOperatorNotSupportedYet(nameof(Queryable.All)));

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateAny(
        ShapedQueryExpression source,
        LambdaExpression? predicate)
        => UnsupportedOperator(
            nameof(Queryable.Any),
            DynamoStrings.ProviderOperatorNotSupportedYet(nameof(Queryable.Any)));

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateAverage(
        ShapedQueryExpression source,
        LambdaExpression? selector,
        Type resultType)
        => UnsupportedOperator(
            nameof(Queryable.Average),
            DynamoStrings.AggregatesNotSupported(nameof(Queryable.Average)));

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateCast(
        ShapedQueryExpression source,
        Type castType)
        => UnsupportedOperator(nameof(Queryable.Cast), DynamoStrings.CastNotSupported);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateConcat(
        ShapedQueryExpression source1,
        ShapedQueryExpression source2)
        => UnsupportedOperator(nameof(Queryable.Concat), DynamoStrings.SetOperationsNotSupported);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateContains(
        ShapedQueryExpression source,
        Expression item)
        => UnsupportedOperator(nameof(Queryable.Contains), DynamoStrings.ContainsNotSupportedYet);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateCount(
        ShapedQueryExpression source,
        LambdaExpression? predicate)
        => UnsupportedOperator(
            nameof(Queryable.Count),
            DynamoStrings.AggregatesNotSupported(nameof(Queryable.Count)));

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateDefaultIfEmpty(
        ShapedQueryExpression source,
        Expression? defaultValue)
        => UnsupportedOperator(nameof(Queryable.DefaultIfEmpty), DynamoStrings.JoinsNotSupported);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateDistinct(ShapedQueryExpression source)
        => UnsupportedOperator(nameof(Queryable.Distinct), DynamoStrings.DistinctNotSupported);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateElementAtOrDefault(
        ShapedQueryExpression source,
        Expression index,
        bool returnDefault)
        => UnsupportedOperator(
            returnDefault ? nameof(Queryable.ElementAtOrDefault) : nameof(Queryable.ElementAt),
            DynamoStrings.OffsetOperatorsNotSupported);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateExcept(
        ShapedQueryExpression source1,
        ShapedQueryExpression source2)
        => UnsupportedOperator(nameof(Queryable.Except), DynamoStrings.SetOperationsNotSupported);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateFirstOrDefault(
        ShapedQueryExpression source,
        LambdaExpression? predicate,
        Type returnType,
        bool returnDefault)
    {
        if (predicate != null)
        {
            if (TranslateWhere(source, predicate) is not { } translatedSource)
                return null;

            source = translatedSource;
        }

        var selectExpression = (SelectExpression)source.QueryExpression;

        if (selectExpression.SeedNextTokenExpression is not null)
            throw new InvalidOperationException(
                $"'{nameof(DynamoDbQueryableExtensions.WithNextToken)}' is not supported with First/FirstOrDefault query shapes.");

        // Mark as First* terminal — drives single-page execution and safe-path validation in the
        // postprocessor.
        selectExpression.MarkAsFirstTerminal();

        // Set implicit Limit=1 for key-only paths. When the user already called Limit(n),
        // HasUserLimit=true and ApplyImplicitLimit is a no-op (user's value wins).
        selectExpression.ApplyImplicitLimit(1);

        return source;
    }

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateGroupBy(
        ShapedQueryExpression source,
        LambdaExpression keySelector,
        LambdaExpression? elementSelector,
        LambdaExpression? resultSelector)
        => UnsupportedOperator(nameof(Queryable.GroupBy), DynamoStrings.GroupByNotSupported);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateGroupJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector)
        => UnsupportedOperator(nameof(Queryable.GroupJoin), DynamoStrings.JoinsNotSupported);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateIntersect(
        ShapedQueryExpression source1,
        ShapedQueryExpression source2)
        => UnsupportedOperator(
            nameof(Queryable.Intersect),
            DynamoStrings.SetOperationsNotSupported);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector)
        => UnsupportedOperator(nameof(Queryable.Join), DynamoStrings.JoinsNotSupported);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateLeftJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector)
        => UnsupportedOperator("LeftJoin", DynamoStrings.JoinsNotSupported);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateRightJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector)
        => UnsupportedOperator("RightJoin", DynamoStrings.JoinsNotSupported);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateLastOrDefault(
        ShapedQueryExpression source,
        LambdaExpression? predicate,
        Type returnType,
        bool returnDefault)
        => UnsupportedOperator(
            returnDefault ? nameof(Queryable.LastOrDefault) : nameof(Queryable.Last),
            DynamoStrings.LastNotSupported);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateLongCount(
        ShapedQueryExpression source,
        LambdaExpression? predicate)
        => UnsupportedOperator(
            nameof(Queryable.LongCount),
            DynamoStrings.AggregatesNotSupported(nameof(Queryable.LongCount)));

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateMax(
        ShapedQueryExpression source,
        LambdaExpression? selector,
        Type resultType)
        => UnsupportedOperator(
            nameof(Queryable.Max),
            DynamoStrings.AggregatesNotSupported(nameof(Queryable.Max)));

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateMin(
        ShapedQueryExpression source,
        LambdaExpression? selector,
        Type resultType)
        => UnsupportedOperator(
            nameof(Queryable.Min),
            DynamoStrings.AggregatesNotSupported(nameof(Queryable.Min)));

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateOfType(
        ShapedQueryExpression source,
        Type resultType)
        => UnsupportedOperator(nameof(Queryable.OfType), DynamoStrings.OfTypeNotSupportedYet);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateOrderBy(
        ShapedQueryExpression source,
        LambdaExpression keySelector,
        bool ascending)
    {
        var selectExpression = (SelectExpression)source.QueryExpression;
        var translation = TranslateLambdaExpression(source, keySelector);

        if (translation == null)
            return null;

        selectExpression.ApplyOrdering(new OrderingExpression(translation, ascending));
        return source;
    }

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateReverse(ShapedQueryExpression source)
        => UnsupportedOperator(nameof(Queryable.Reverse), DynamoStrings.ReverseNotSupported);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression TranslateSelect(
        ShapedQueryExpression source,
        LambdaExpression selector)
    {
        // Optimization: identity projection x => x
        if (selector.Body == selector.Parameters[0])
            return source;

        var selectExpression = (SelectExpression)source.QueryExpression;

        // Remap lambda body: replace parameter with current shaper
        var newSelectorBody = ReplacingExpressionVisitor.Replace(
            selector.Parameters[0],
            source.ShaperExpression,
            selector.Body);

        // Delegate to projection binding visitor
        var newShaper = _projectionBindingExpressionVisitor.Translate(
            selectExpression,
            newSelectorBody);

        return source.UpdateShaperExpression(newShaper);
    }

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateSelectMany(
        ShapedQueryExpression source,
        LambdaExpression collectionSelector,
        LambdaExpression resultSelector)
        => UnsupportedOperator(nameof(Queryable.SelectMany), DynamoStrings.JoinsNotSupported);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateSelectMany(
        ShapedQueryExpression source,
        LambdaExpression selector)
        => UnsupportedOperator(nameof(Queryable.SelectMany), DynamoStrings.JoinsNotSupported);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateSingleOrDefault(
        ShapedQueryExpression source,
        LambdaExpression? predicate,
        Type returnType,
        bool returnDefault)
    {
        var operatorName =
            returnDefault ? nameof(Queryable.SingleOrDefault) : nameof(Queryable.Single);
        return UnsupportedOperator(
            operatorName,
            DynamoStrings.ProviderOperatorNotSupportedYet(operatorName));
    }

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateSkip(
        ShapedQueryExpression source,
        Expression count)
        => UnsupportedOperator(nameof(Queryable.Skip), DynamoStrings.OffsetOperatorsNotSupported);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateSkipWhile(
        ShapedQueryExpression source,
        LambdaExpression predicate)
        => UnsupportedOperator(nameof(Queryable.SkipWhile), DynamoStrings.SkipWhileNotSupported);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateSum(
        ShapedQueryExpression source,
        LambdaExpression? selector,
        Type resultType)
        => UnsupportedOperator(
            nameof(Queryable.Sum),
            DynamoStrings.AggregatesNotSupported(nameof(Queryable.Sum)));

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateTake(
        ShapedQueryExpression source,
        Expression count)
        => UnsupportedOperator(nameof(Queryable.Take), DynamoStrings.TakeNotSupported);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateTakeWhile(
        ShapedQueryExpression source,
        LambdaExpression predicate)
        => UnsupportedOperator(nameof(Queryable.TakeWhile), DynamoStrings.TakeWhileNotSupported);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateThenBy(
        ShapedQueryExpression source,
        LambdaExpression keySelector,
        bool ascending)
    {
        var selectExpression = (SelectExpression)source.QueryExpression;
        var translation = TranslateLambdaExpression(source, keySelector);

        if (translation == null)
            return null;

        selectExpression.AppendOrdering(new OrderingExpression(translation, ascending));
        return source;
    }

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateUnion(
        ShapedQueryExpression source1,
        ShapedQueryExpression source2)
        => UnsupportedOperator(nameof(Queryable.Union), DynamoStrings.SetOperationsNotSupported);

    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateWhere(
        ShapedQueryExpression source,
        LambdaExpression predicate)
    {
        var selectExpression = (SelectExpression)source.QueryExpression;
        var translation = TranslateLambdaExpression(source, predicate);

        if (translation == null)
            return null;

        translation = NormalizePredicate(translation);

        selectExpression.ApplyPredicate(translation);
        return source;
    }

    /// <summary>
    /// Translates a lambda expression by translating its body.
    /// </summary>
    private SqlExpression? TranslateLambdaExpression(
        ShapedQueryExpression shapedQueryExpression,
        LambdaExpression lambdaExpression)
    {
        var parameterEntityTypes = TryBuildLambdaParameterEntityTypes(
            shapedQueryExpression,
            lambdaExpression);
        var translation = _sqlTranslator.Translate(lambdaExpression.Body, parameterEntityTypes);
        if (translation != null)
            return translation;

        AddTranslationErrorDetails(
            _sqlTranslator.TranslationErrorDetails ?? DynamoStrings.PredicateNotTranslatable);
        return null;
    }

    /// <summary>Tries to map lambda parameters to the source entity type used by the shaped query.</summary>
    private static IReadOnlyDictionary<ParameterExpression, IEntityType>?
        TryBuildLambdaParameterEntityTypes(
            ShapedQueryExpression shapedQueryExpression,
            LambdaExpression lambdaExpression)
    {
        if (shapedQueryExpression.ShaperExpression is not StructuralTypeShaperExpression
            {
                StructuralType: IEntityType entityType,
            })
            return null;

        Dictionary<ParameterExpression, IEntityType> mappings = [];
        foreach (var parameter in lambdaExpression.Parameters)
            if (parameter.Type.IsAssignableFrom(entityType.ClrType)
                || entityType.ClrType.IsAssignableFrom(parameter.Type))
                mappings[parameter] = entityType;

        return mappings.Count == 0 ? null : mappings;
    }

    /// <summary>
    ///     Registers translation details for an unsupported operator and returns an untranslated
    ///     marker.
    /// </summary>
    private ShapedQueryExpression? UnsupportedOperator(string operatorName, string reason)
    {
        AddTranslationErrorDetails(DynamoStrings.UnsupportedOperator(operatorName, reason));
        return null;
    }

    /// <summary>Normalizes boolean predicates into explicit comparisons for PartiQL.</summary>
    private SqlExpression NormalizePredicate(SqlExpression expression)
        => NormalizePredicate(expression, _sqlExpressionFactory, true);

    /// <summary>Normalizes search-condition terms while preserving normal comparisons.</summary>
    private static SqlExpression NormalizePredicate(
        SqlExpression expression,
        ISqlExpressionFactory sqlExpressionFactory,
        bool inSearchCondition)
        => expression switch
        {
            SqlBinaryExpression binaryExpression => NormalizeBinary(
                binaryExpression,
                sqlExpressionFactory),
            SqlUnaryExpression unaryExpression => unaryExpression.Update(
                NormalizePredicate(unaryExpression.Operand, sqlExpressionFactory, true)),
            SqlPropertyExpression propertyExpression when inSearchCondition
                && IsBooleanType(propertyExpression.Type) => WrapBooleanPredicate(
                    propertyExpression,
                    sqlExpressionFactory),
            SqlParameterExpression parameterExpression when inSearchCondition
                && IsBooleanType(parameterExpression.Type) => WrapBooleanPredicate(
                    parameterExpression,
                    sqlExpressionFactory),
            _ => expression,
        };

    /// <summary>Recursively normalizes logical predicates while preserving comparisons.</summary>
    private static SqlExpression NormalizeBinary(
        SqlBinaryExpression binaryExpression,
        ISqlExpressionFactory sqlExpressionFactory)
    {
        // Search conditions only exist at AND/OR boundaries.
        if (binaryExpression.OperatorType is ExpressionType.AndAlso or ExpressionType.OrElse)
        {
            // Detect (prop >= low) AND (prop <= high) → BETWEEN before recursing.
            if (binaryExpression.OperatorType is ExpressionType.AndAlso
                && TryExtractBetweenBounds(
                    binaryExpression,
                    out var subject,
                    out var low,
                    out var high))
                return sqlExpressionFactory.Between(subject!, low!, high!);

            var left = NormalizePredicate(binaryExpression.Left, sqlExpressionFactory, true);
            var right = NormalizePredicate(binaryExpression.Right, sqlExpressionFactory, true);
            return binaryExpression.Update(left, right);
        }

        // Avoid rewriting operands inside explicit comparisons.
        if (IsComparisonOperator(binaryExpression.OperatorType))
        {
            var left = NormalizePredicate(binaryExpression.Left, sqlExpressionFactory, false);
            var right = NormalizePredicate(binaryExpression.Right, sqlExpressionFactory, false);
            return binaryExpression.Update(left, right);
        }

        var normalizedLeft = NormalizePredicate(binaryExpression.Left, sqlExpressionFactory, false);
        var normalizedRight = NormalizePredicate(
            binaryExpression.Right,
            sqlExpressionFactory,
            false);
        return binaryExpression.Update(normalizedLeft, normalizedRight);
    }

    /// <summary>
    ///     Attempts to extract a BETWEEN pattern from an AND expression.
    ///     Succeeds only when both sides are inclusive comparisons (<c>&gt;=</c> and <c>&lt;=</c>)
    ///     on the same property.
    /// </summary>
    /// <returns> when both sides form an inclusive BETWEEN range.</returns>
    private static bool TryExtractBetweenBounds(
        SqlBinaryExpression andExpression,
        out SqlExpression? subject,
        out SqlExpression? low,
        out SqlExpression? high)
    {
        subject = low = high = null;

        if (andExpression.Left is not SqlBinaryExpression leftBinary
            || andExpression.Right is not SqlBinaryExpression rightBinary)
            return false;

        // Accept (prop >= low) AND (prop <= high)  or the reversed ordering.
        SqlBinaryExpression? geExpression = null;
        SqlBinaryExpression? leExpression = null;

        if (leftBinary.OperatorType is ExpressionType.GreaterThanOrEqual
            && rightBinary.OperatorType is ExpressionType.LessThanOrEqual)
        {
            geExpression = leftBinary;
            leExpression = rightBinary;
        }
        else if (leftBinary.OperatorType is ExpressionType.LessThanOrEqual
            && rightBinary.OperatorType is ExpressionType.GreaterThanOrEqual)
        {
            leExpression = leftBinary;
            geExpression = rightBinary;
        }

        if (geExpression is null || leExpression is null)
            return false;

        // Both sides must compare the same property.
        if (geExpression.Left is not SqlPropertyExpression geProp
            || leExpression.Left is not SqlPropertyExpression leProp
            || geProp.PropertyName != leProp.PropertyName)
            return false;

        subject = geProp;
        low = geExpression.Right;
        high = leExpression.Right;

        // TODO: consider normalizing for BETWEEN
        // NOTE: bounds are taken directly from the expression tree — no normalization or
        // reordering is performed. If the caller supplies inverted bounds (e.g. prop >= 500
        // && prop <= 100), the BETWEEN is emitted as-is and DynamoDB will return no results.
        // This is intentional: the provider does not validate value semantics, only structure.
        // For sort-key range queries this matters because DynamoDB allows only a single
        // condition on the sort key; the BETWEEN rewrite satisfies that constraint, but only
        // when the bounds are in the correct order (low, high).
        return true;
    }

    /// <summary>Wraps a boolean column/parameter into an explicit comparison.</summary>
    private static SqlExpression WrapBooleanPredicate(
        SqlExpression expression,
        ISqlExpressionFactory sqlExpressionFactory)
        => sqlExpressionFactory.Binary(
                ExpressionType.Equal,
                expression,
                sqlExpressionFactory.Constant(true, typeof(bool)))
            ?? expression;

    private static bool IsBooleanType(Type type) => type == typeof(bool) || type == typeof(bool?);

    private static bool IsComparisonOperator(ExpressionType operatorType)
        => operatorType is ExpressionType.Equal
            or ExpressionType.NotEqual
            or ExpressionType.LessThan
            or ExpressionType.LessThanOrEqual
            or ExpressionType.GreaterThan
            or ExpressionType.GreaterThanOrEqual;
}
