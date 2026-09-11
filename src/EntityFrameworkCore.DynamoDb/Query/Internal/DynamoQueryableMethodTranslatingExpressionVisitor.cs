using System.Linq.Expressions;
using System.Reflection;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Metadata;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>Represents the DynamoQueryableMethodTranslatingExpressionVisitor type.</summary>
public sealed class DynamoQueryableMethodTranslatingExpressionVisitor
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

#pragma warning disable EF9100
    private static readonly MethodInfo ValidateWithNextTokenMethodInfo =
        typeof(DynamoGeneratedQueryRuntime).GetMethod(
            nameof(DynamoGeneratedQueryRuntime.ValidateWithNextToken),
            BindingFlags.Static | BindingFlags.Public)!;
#pragma warning restore EF9100

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
            queryCompilationContext.Model,
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

    /// <summary>Checks whether a method call targets a specific DynamoDB queryable extension method.</summary>
    private static bool IsDynamoQueryableMethod(MethodInfo method, MethodInfo methodDefinition)
        => method.IsGenericMethod && method.GetGenericMethodDefinition() == methodDefinition;

    /// <summary>Provides functionality for this member.</summary>
    public override Expression Translate(Expression expression)
    {
        // Handle ToPageAsync(), which can only ever be the top-level node in the query tree.
        if (expression is MethodCallExpression { Method: var method, Arguments: var arguments }
            && IsDynamoQueryableMethod(method, DynamoQueryableMethods.ToPageAsync))
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
                    QueryExpression: SelectExpression selectExpression
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
                _ => null
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
            if (IsDynamoQueryableMethod(method, DynamoQueryableMethods.Limit))
            {
                // Visit inner source first so SelectExpression exists before applying the limit.
                var limitResult = Visit(methodCallExpression.Arguments[0]);
                if (limitResult is not ShapedQueryExpression
                    {
                        QueryExpression: SelectExpression limitSelectExpr
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

            if (IsDynamoQueryableMethod(method, DynamoQueryableMethods.WithConsistentRead))
            {
                var consistentReadResult = Visit(methodCallExpression.Arguments[0]);
                if (consistentReadResult is not ShapedQueryExpression
                    {
                        QueryExpression: SelectExpression consistentReadSelectExpr
                    })
                    return consistentReadResult;

                var consistentReadArg = methodCallExpression.Arguments[1];
                if (consistentReadArg is ConstantExpression { Value: bool consistentRead })
                    consistentReadSelectExpr.ApplyConsistentRead(consistentRead);
                else
                    consistentReadSelectExpr.ApplyConsistentReadExpression(consistentReadArg);

                return consistentReadResult;
            }

            if (IsDynamoQueryableMethod(method, DynamoQueryableMethods.WithoutIndex))
            {
                var context = (DynamoQueryCompilationContext)QueryCompilationContext;
                context.IndexSelectionDisabled = true;

                // Continue visiting the source (prune this extension from the tree)
                return Visit(methodCallExpression.Arguments[0]);
            }

            if (IsDynamoQueryableMethod(method, DynamoQueryableMethods.WithIndex))
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
                            Value: string indexName
                        })
                        throw new InvalidOperationException(
                            $"'{nameof(DynamoDbQueryableExtensions.WithIndex)}' requires a constant index name. "
                            + "Index names are embedded in the PartiQL FROM clause and cannot be query parameters. "
                            + "Pass a string literal or capture a local variable in a regular (non-compiled) query.");

                    context.ExplicitIndexName = indexName;
                }

                return Visit(methodCallExpression.Arguments[0]);
            }

            if (IsDynamoQueryableMethod(method, DynamoQueryableMethods.AsUnsafeFilteredQuery))
            {
                var unsafeFilteredResult = Visit(methodCallExpression.Arguments[0]);

                if (unsafeFilteredResult is ShapedQueryExpression
                    {
                        QueryExpression: SelectExpression unsafeFilteredSelectExpression
                    })
                    unsafeFilteredSelectExpression.AllowUnsafeFilteredQueries();

                return unsafeFilteredResult;
            }

            if (IsDynamoQueryableMethod(method, DynamoQueryableMethods.AllowScan))
            {
                var allowScanResult = Visit(methodCallExpression.Arguments[0]);

                if (allowScanResult is ShapedQueryExpression
                    {
                        QueryExpression: SelectExpression allowScanSelectExpression
                    })
                    allowScanSelectExpression.AllowScan();

                return allowScanResult;
            }

            if (IsDynamoQueryableMethod(method, DynamoQueryableMethods.WithNextToken))
            {
                // Visit inner source first so SelectExpression exists before applying the seed
                // token.
                var nextTokenResult = Visit(methodCallExpression.Arguments[0]);
                if (nextTokenResult is not ShapedQueryExpression
                    {
                        QueryExpression: SelectExpression nextTokenSelectExpr
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
                {
                    nextTokenSelectExpr.ApplySeedNextTokenExpression(
                        RegisterValidatedWithNextTokenRuntimeParameter(nextTokenArg));
                }

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
                new SqlDiscriminatorPredicateExpression(
                    discriminatorPredicate,
                    entityType.FindDiscriminatorProperty()?.GetAttributeName(),
                    DiscriminatorPredicateOrigin.RootMaterializer));

        // Store entity projection in projection mapping under root ProjectionMember
        var projectionMapping = new Dictionary<ProjectionMember, Expression>
        {
            [new ProjectionMember()] = entityProjection
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
            if (concreteType.ClrType.IsAbstract)
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
            _ => predicate
        };
    }

    private SqlExpression? CreateDiscriminatorPredicate(IEntityType entityType)
    {
        var discriminatorProperty = entityType.FindDiscriminatorProperty();
        if (discriminatorProperty is null)
            return null;

        var discriminatorColumn = _sqlExpressionFactory.ApplyTypeMapping(
            _sqlExpressionFactory.Property(
                discriminatorProperty.GetAttributeName(),
                discriminatorProperty.ClrType),
            discriminatorProperty.GetTypeMapping());

        SqlExpression? predicate = null;
        foreach (var concreteType in entityType.GetConcreteDerivedTypesInclusive())
        {
            if (concreteType.ClrType.IsAbstract)
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
            _ => predicate
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
                if (concreteType.ClrType.IsAbstract)
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

#if NET11_0
    /// <summary>Provides functionality for this member.</summary>
    protected override ShapedQueryExpression? TranslateFullJoin(
        ShapedQueryExpression outer,
        ShapedQueryExpression inner,
        LambdaExpression outerKeySelector,
        LambdaExpression innerKeySelector,
        LambdaExpression resultSelector)
        => UnsupportedOperator("FullJoin", DynamoStrings.JoinsNotSupported);
#endif

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
    {
        if (source.ShaperExpression is not StructuralTypeShaperExpression
            {
                StructuralType: IEntityType sourceEntityType
            })
            return UnsupportedOperator(
                nameof(Queryable.OfType),
                DynamoStrings.OfTypeNotSupportedYet);

        var targetEntityType = QueryCompilationContext.Model.FindEntityType(resultType);
        if (targetEntityType is null
            || sourceEntityType.GetRootType() != targetEntityType.GetRootType())
            return UnsupportedOperator(
                nameof(Queryable.OfType),
                DynamoStrings.OfTypeNotSupportedYet);

        var discriminatorPredicate = CreateDiscriminatorPredicate(targetEntityType);
        if (discriminatorPredicate is null)
        {
            if (IsOfTypeIdentityOrBaseType(sourceEntityType, targetEntityType))
                return source;

            return UnsupportedOperator(
                nameof(Queryable.OfType),
                DynamoStrings.OfTypeRequiresDiscriminatorPredicate);
        }

        var selectExpression = (SelectExpression)source.QueryExpression;
        selectExpression.ApplyPredicate(
            new SqlDiscriminatorPredicateExpression(
                discriminatorPredicate,
                targetEntityType.FindDiscriminatorProperty()?.GetAttributeName()));

        var entityProjection =
            new DynamoEntityProjectionExpression(targetEntityType, _sqlExpressionFactory);
        selectExpression.ReplaceProjectionMapping(
            new Dictionary<ProjectionMember, Expression>
            {
                [new ProjectionMember()] = entityProjection
            });

        var projectionBindingExpression = new ProjectionBindingExpression(
            selectExpression,
            new ProjectionMember(),
            typeof(ValueBuffer));

        return source.UpdateShaperExpression(
            new StructuralTypeShaperExpression(
                targetEntityType,
                projectionBindingExpression,
                false));
    }

    private static bool IsOfTypeIdentityOrBaseType(
        IEntityType sourceEntityType,
        IEntityType targetEntityType)
        => targetEntityType.IsAssignableFrom(sourceEntityType);

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
        if (predicate != null)
        {
            if (TranslateWhere(source, predicate) is not { } translatedSource)
                return null;

            source = translatedSource;
        }

        var selectExpression = (SelectExpression)source.QueryExpression;

        if (selectExpression.SeedNextTokenExpression is not null)
            throw new InvalidOperationException(
                DynamoStrings.SingleOrDefaultWithNextTokenNotSupported);

        // Single* needs provider-managed Limit=2 so EF Core's cardinality wrapper can detect
        // duplicates. DynamoDB Limit is an evaluated-item budget, so postprocessor validation
        // restricts this to key-condition-only paths.
        selectExpression.MarkAsSingleTerminal();
        selectExpression.ApplyImplicitLimit(2);

        return source.ShaperExpression.Type != returnType
            ? source.UpdateShaperExpression(Expression.Convert(source.ShaperExpression, returnType))
            : source;
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

    /// <summary>Translates an <c>ExecuteUpdate</c> call into a provider update expression.</summary>
    /// <remarks>
    ///     Validates the base-table key-complete singleton shape and parses/validates setters.
    ///     Index-targeted sources and synchronous execution are rejected; extra non-key predicates
    ///     are allowed because PartiQL supports them.
    /// </remarks>
    protected override Expression TranslateExecuteUpdate(
        ShapedQueryExpression source,
        IReadOnlyList<ExecuteUpdateSetter> setters)
    {
        if (!QueryCompilationContext.IsAsync)
            throw new NotSupportedException(DynamoStrings.ExecuteUpdateSyncNotSupported);

#if !NET11_0
        // EF Core 10's precompiled-query generator misroutes captured-variable extraction when a
        // single ExecuteUpdate mixes constant and computed (self-referencing) setter values: the
        // generated interceptor binds the computed setter's lambda delegate to the parameter the
        // template expects for the constant value, silently writing the wrong value. Fail fast at
        // generation time; EF Core 11 handles the mixed shape correctly.
        if (((DynamoQueryCompilationContext)QueryCompilationContext).IsPrecompiling
            && setters
                .Select(static setter => setter.ValueExpression is LambdaExpression)
                .Distinct()
                .Count()
            > 1)
            throw new NotSupportedException(
                "EF Core 10 precompiled query generation cannot combine constant and computed "
                + "(self-referencing) setter values in a single ExecuteUpdate. Split the update "
                + "into separate ExecuteUpdateAsync calls, or disable precompiled query "
                + "generation for this query.");
#endif

        if (source.ShaperExpression is not StructuralTypeShaperExpression
            {
                StructuralType: IEntityType entityType
            })
            throw new InvalidOperationException(DynamoStrings.ExecuteUpdateInvalidSource);

        var selectExpression = (SelectExpression)source.QueryExpression;
        var compilationContext = (DynamoQueryCompilationContext)QueryCompilationContext;

        if (selectExpression.IndexName is { } appliedIndex)
            throw new InvalidOperationException(
                DynamoStrings.ExecuteUpdateOnIndexNotSupported(appliedIndex));

        if (compilationContext.ExplicitIndexName is { } explicitIndex)
            throw new InvalidOperationException(
                DynamoStrings.ExecuteUpdateOnIndexNotSupported(explicitIndex));

        if (selectExpression.Limit is not null || selectExpression.LimitExpression is not null)
            throw new InvalidOperationException(
                DynamoStrings.ExecuteUpdateInvalidKeyPredicate(
                    "Limit(n) cannot be combined with ExecuteUpdate; the WHERE clause must "
                    + "identify the item without an evaluated-item limit."));

        // Finalize the deferred discriminator predicate so the WHERE tree is complete for
        // validation here and for PartiQL generation later.
        selectExpression.ApplyDeferredDiscriminatorPredicate();

        var keyEntityType = entityType.ResolveKeyMappedEntityType();
        var partitionKeyProperty = keyEntityType.GetPartitionKeyProperty()
            ?? throw new InvalidOperationException(
                $"Entity type '{entityType.DisplayName()}' does not define a partition key.");
        var sortKeyProperty = keyEntityType.GetSortKeyProperty();

        ValidateUpdateWhereClause(selectExpression, partitionKeyProperty, sortKeyProperty);

        var updateSetters = new List<DynamoUpdateSetter>(setters.Count);
        foreach (var setter in setters)
            updateSetters.Add(
                TranslateUpdateSetter(
                    setter,
                    entityType,
                    partitionKeyProperty,
                    sortKeyProperty,
                    updateSetters));

        return new DynamoUpdateExpression(selectExpression, entityType, updateSetters);
    }

    /// <summary>
    ///     Validates that the WHERE clause equality-constrains the full primary key and contains
    ///     no rejected key shapes (IN, ranges, or OR touching keys). Reuses the query-path
    ///     constraint extractor with a synthetic base-table descriptor.
    /// </summary>
    private static void ValidateUpdateWhereClause(
        SelectExpression selectExpression,
        IReadOnlyProperty partitionKeyProperty,
        IReadOnlyProperty? sortKeyProperty)
    {
        var constraints = new DynamoConstraintExtractionVisitor(
        [
            new DynamoIndexDescriptor(
                IndexName: null,
                Kind: DynamoIndexSourceKind.Table,
                ModelIndex: null,
                PartitionKeyProperty: partitionKeyProperty,
                SortKeyProperty: sortKeyProperty,
                ProjectionType: DynamoSecondaryIndexProjectionType.All)
        ]).Extract(selectExpression);

        var partitionKeyAttributeName = partitionKeyProperty.GetAttributeName();

        if (constraints.InConstraints.ContainsKey(partitionKeyAttributeName))
            throw new InvalidOperationException(
                DynamoStrings.ExecuteUpdateInvalidKeyPredicate(
                    "The partition key cannot be constrained with IN; ExecuteUpdate targets a "
                    + "single item."));

        if (!constraints.EqualityConstraints.ContainsKey(partitionKeyAttributeName))
            throw new InvalidOperationException(
                DynamoStrings.ExecuteUpdateRequiresKeyEquality("partition key"));

        var sortKeyAttributeName = sortKeyProperty?.GetAttributeName();
        if (sortKeyAttributeName is not null)
        {
            if (constraints.SkKeyConditions.TryGetValue(sortKeyAttributeName, out var condition)
                && condition.Operator != SkOperator.Equal)
                throw new InvalidOperationException(
                    DynamoStrings.ExecuteUpdateInvalidKeyPredicate(
                        "The sort key must be equality-constrained; range operators and "
                        + "begins_with are not supported."));

            if (!constraints.SkKeyConditions.ContainsKey(sortKeyAttributeName))
                throw new InvalidOperationException(
                    DynamoStrings.ExecuteUpdateRequiresKeyEquality(
                        "sort key (range operators, IN, and OR on the sort key are not "
                        + "supported)"));
        }

        if (selectExpression.Predicate is not null
            && DynamoPredicateAnalysis.PredicateHasOrTouchingKey(
                selectExpression.Predicate,
                partitionKeyAttributeName,
                sortKeyAttributeName))
            throw new InvalidOperationException(
                DynamoStrings.ExecuteUpdateInvalidKeyPredicate(
                    "OR predicates must not reference key attributes."));
    }

    /// <summary>Parses and validates a single ExecuteUpdate setter.</summary>
    private DynamoUpdateSetter TranslateUpdateSetter(
        ExecuteUpdateSetter setter,
        IEntityType entityType,
        IReadOnlyProperty partitionKeyProperty,
        IReadOnlyProperty? sortKeyProperty,
        IReadOnlyList<DynamoUpdateSetter> existingSetters)
    {
        var (property, attributePath) = ResolveSetterTarget(setter.PropertySelector, entityType);

        if (existingSetters.Any(s
            => string.Equals(s.AttributeNamePath, attributePath, StringComparison.Ordinal)))
            throw new InvalidOperationException(
                DynamoStrings.ExecuteUpdateDuplicateSetter(attributePath));

        if (property.IsPrimaryKey()
            || ReferenceEquals(property, partitionKeyProperty)
            || (sortKeyProperty is not null && ReferenceEquals(property, sortKeyProperty)))
            throw new InvalidOperationException(
                DynamoStrings.ExecuteUpdateKeyMutation(property.Name));

        SqlExpression? value;
        if (setter.ValueExpression is LambdaExpression valueLambda)
        {
            Dictionary<ParameterExpression, IEntityType> parameterEntityTypes = [];
            foreach (var parameter in valueLambda.Parameters)
                if (parameter.Type.IsAssignableFrom(entityType.ClrType)
                    || entityType.ClrType.IsAssignableFrom(parameter.Type))
                    parameterEntityTypes[parameter] = entityType;

            value = _sqlTranslator.Translate(
                valueLambda.Body,
                parameterEntityTypes.Count > 0 ? parameterEntityTypes : null);
        }
        else
        {
            value = _sqlTranslator.Translate(setter.ValueExpression);
        }

        if (value is null
            || ReferenceEquals(value, QueryCompilationContext.NotTranslatedExpression))
            throw new InvalidOperationException(
                DynamoStrings.ExecuteUpdateInvalidSetter(
                    "The value expression could not be translated."
                    + (_sqlTranslator.TranslationErrorDetails is { } details
                        ? $" {details}"
                        : string.Empty)));

        var isSelfReferencing = ValidateSetterValue(value, property, attributePath);

        return new DynamoUpdateSetter(property, attributePath, value, isSelfReferencing);
    }

    /// <summary>
    ///     Resolves the setter property selector to a scalar property and its dotted DynamoDB
    ///     attribute path. Only direct member paths over the entity parameter are supported:
    ///     intermediate members must be complex properties, the leaf must be a scalar property.
    /// </summary>
    private static (IProperty Property, string AttributePath) ResolveSetterTarget(
        LambdaExpression propertySelector,
        IEntityType entityType)
    {
        var segments = new List<MemberInfo>();
        var body = propertySelector.Body;
        while (body is MemberExpression member)
        {
            segments.Add(member.Member);
            body = member.Expression!;
        }

        if (segments.Count == 0 || body != propertySelector.Parameters[0])
            throw new InvalidOperationException(
                DynamoStrings.ExecuteUpdateInvalidSetter(
                    $"The property selector '{propertySelector}' must be a member path over the "
                    + "entity parameter (for example e => e.Property or e => e.Complex.Property)."));

        // Segments are leaf-first; walk root-to-leaf resolving complex properties and the leaf
        // scalar property.
        var currentType = (IReadOnlyTypeBase)entityType;
        IReadOnlyProperty? leafProperty = null;
        var pathSegments = new List<string>(segments.Count);

        for (var i = segments.Count - 1; i >= 0; i--)
        {
            var member = segments[i];

            if (i > 0)
            {
                if (currentType.FindComplexProperty(member) is not IReadOnlyComplexProperty
                    complexProperty)
                    throw new InvalidOperationException(
                        DynamoStrings.ExecuteUpdateInvalidSetter(
                            $"Member '{member.Name}' is not a complex property of "
                            + $"'{currentType.DisplayName()}'; only scalar and complex-property "
                            + "member paths are supported."));

                pathSegments.Add(complexProperty.GetAttributeName());
                currentType = complexProperty.ComplexType;
            }
            else
            {
                if (currentType.FindProperty(member) is not IReadOnlyProperty resolved)
                    throw new InvalidOperationException(
                        DynamoStrings.ExecuteUpdateInvalidSetter(
                            $"Member '{member.Name}' is not a mapped scalar property of "
                            + $"'{currentType.DisplayName()}'. Whole complex properties and "
                            + "navigations cannot be set with ExecuteUpdate."));

                leafProperty = resolved;
                pathSegments.Add(leafProperty.GetAttributeName());
            }
        }

        return ((IProperty)leafProperty!, string.Join(".", pathSegments));
    }

    /// <summary>
    ///     Validates the translated setter value and determines self-referencing. Allowed value
    ///     shapes: constants, parameters, null, direct self-reference, and numeric
    ///     <c>+</c>/<c>-</c> arithmetic between the target attribute and a constant or
    ///     parameter. String concatenation, multiplication/division, and attribute-to-attribute
    ///     assignment are rejected.
    /// </summary>
    private static bool ValidateSetterValue(
        SqlExpression value,
        IProperty property,
        string attributePath)
    {
        var referencedPaths = new HashSet<string>(StringComparer.Ordinal);
        CollectReferencedAttributePaths(value, referencedPaths);

        if (referencedPaths.Count == 0)
            return false;

        var otherPath = referencedPaths.FirstOrDefault(p
            => !string.Equals(p, attributePath, StringComparison.Ordinal));
        if (otherPath is not null)
            throw new InvalidOperationException(
                DynamoStrings.ExecuteUpdateUnsupportedValueShape(
                    $"Attribute-to-attribute assignment ('{otherPath}' -> '{attributePath}') is "
                    + "not supported; use a constant, a parameter, or the target property itself."));

        if (value.Type == typeof(string)
            && value is SqlBinaryExpression { OperatorType: ExpressionType.Add })
            throw new InvalidOperationException(
                DynamoStrings.ExecuteUpdateUnsupportedValueShape(
                    "String concatenation is not supported in SET clauses."));

        if (property.GetTypeMapping()?.Converter is not null)
            throw new InvalidOperationException(
                DynamoStrings.ExecuteUpdateUnsupportedValueShape(
                    $"Self-referencing setters on '{attributePath}' are not supported for "
                    + "properties with a value converter."));

        if (value is SqlBinaryExpression
            {
                OperatorType: ExpressionType.Add or ExpressionType.Subtract
            } arithmetic
            && (MatchesAttributePath(arithmetic.Left, attributePath)
                || MatchesAttributePath(arithmetic.Right, attributePath)))
        {
            var otherOperand = MatchesAttributePath(arithmetic.Left, attributePath)
                ? arithmetic.Right
                : arithmetic.Left;
            if (otherOperand is SqlConstantExpression or SqlParameterExpression)
                return true;

            throw new InvalidOperationException(
                DynamoStrings.ExecuteUpdateUnsupportedValueShape(
                    $"Self-referencing arithmetic on '{attributePath}' supports constant or "
                    + "parameter operands only."));
        }

        if (MatchesAttributePath(value, attributePath))
            return true;

        if (value is SqlBinaryExpression { OperatorType: var operatorType } binary
            && (MatchesAttributePath(binary.Left, attributePath)
                || MatchesAttributePath(binary.Right, attributePath)))
            throw new InvalidOperationException(
                DynamoStrings.ExecuteUpdateUnsupportedValueShape(
                    $"Self-referencing arithmetic on '{attributePath}' supports only addition "
                    + $"and subtraction; '{operatorType}' is not supported."));

        throw new InvalidOperationException(
            DynamoStrings.ExecuteUpdateUnsupportedValueShape(
                $"The value references '{attributePath}' in an unsupported shape; only direct "
                + "assignment and + / - arithmetic are supported."));
    }

    /// <summary>Collects the full attribute paths referenced by a SQL expression tree.</summary>
    private static void CollectReferencedAttributePaths(
        Expression expression,
        HashSet<string> paths)
    {
        if (expression is not SqlExpression sql)
            return;

        switch (sql)
        {
            case SqlPropertyExpression property:
                paths.Add(property.PropertyName);
                break;

            case DynamoScalarAccessExpression scalarAccess:
                if (TryResolveAttributePath(scalarAccess, out var scalarPath))
                    paths.Add(scalarPath);
                break;

            case DynamoComplexPropertyAccessExpression complexAccess:
                paths.Add(complexAccess.AttributeName);
                break;

            case DynamoListIndexExpression listIndex:
                CollectReferencedAttributePaths(listIndex.Source, paths);
                break;

            case SqlBinaryExpression binary:
                CollectReferencedAttributePaths(binary.Left, paths);
                CollectReferencedAttributePaths(binary.Right, paths);
                break;

            case SqlUnaryExpression unary:
                CollectReferencedAttributePaths(unary.Operand, paths);
                break;

            case SqlIsNullExpression isNull:
                CollectReferencedAttributePaths(isNull.Operand, paths);
                break;

            case SqlParenthesizedExpression parenthesized:
                CollectReferencedAttributePaths(parenthesized.Operand, paths);
                break;

            case SqlBetweenExpression between:
                CollectReferencedAttributePaths(between.Subject, paths);
                CollectReferencedAttributePaths(between.Low, paths);
                CollectReferencedAttributePaths(between.High, paths);
                break;

            case SqlFunctionExpression function:
                foreach (var argument in function.Arguments)
                    CollectReferencedAttributePaths(argument, paths);
                break;

            case SqlInExpression inExpression:
                CollectReferencedAttributePaths(inExpression.Item, paths);
                if (inExpression.Values is { } values)
                    foreach (var value in values)
                        CollectReferencedAttributePaths(value, paths);
                break;

            case SqlDiscriminatorPredicateExpression:
            case SqlConstantExpression:
            case SqlParameterExpression:
                break;
        }
    }

    /// <summary>
    ///     Resolves the full dotted attribute path of a scalar-access chain (root property plus
    ///     nested segments).
    /// </summary>
    private static bool TryResolveAttributePath(
        DynamoScalarAccessExpression scalarAccess,
        out string attributePath)
    {
        var segments = new Stack<string>();
        DynamoScalarAccessExpression? lastAccess = null;
        var current = scalarAccess;
        while (current is DynamoScalarAccessExpression access)
        {
            segments.Push(access.PropertyName);
            lastAccess = access;
            current = access.Parent as DynamoScalarAccessExpression;
        }

        if (lastAccess?.Parent is SqlPropertyExpression root)
        {
            attributePath = string.Join(".", [root.PropertyName, .. segments]);
            return true;
        }

        attributePath = string.Empty;
        return false;
    }

    /// <summary>Returns <c>true</c> when the expression resolves to the given attribute path.</summary>
    private static bool MatchesAttributePath(Expression expression, string attributePath)
        => expression switch
        {
            SqlPropertyExpression property => string.Equals(
                property.PropertyName,
                attributePath,
                StringComparison.Ordinal),
            DynamoScalarAccessExpression scalarAccess => TryResolveAttributePath(
                    scalarAccess,
                    out var path)
                && string.Equals(path, attributePath, StringComparison.Ordinal),
            SqlParenthesizedExpression parenthesized => MatchesAttributePath(
                parenthesized.Operand,
                attributePath),
            _ => false
        };

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
                StructuralType: IEntityType entityType
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
            _ => expression
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

        if (TryGetBetweenBound(leftBinary, true, out var lowerSubject, out low)
            && TryGetBetweenBound(rightBinary, false, out var upperSubject, out high)
            && lowerSubject.PropertyName == upperSubject.PropertyName)
        {
            subject = lowerSubject;
            return true;
        }

        if (TryGetBetweenBound(rightBinary, true, out lowerSubject, out low)
            && TryGetBetweenBound(leftBinary, false, out upperSubject, out high)
            && lowerSubject.PropertyName == upperSubject.PropertyName)
        {
            subject = lowerSubject;
            return true;
        }

        return false;
    }

    private static bool TryGetBetweenBound(
        SqlBinaryExpression comparison,
        bool lower,
        out SqlPropertyExpression subject,
        out SqlExpression bound)
    {
        subject = null!;
        bound = null!;

        var propertyOnLeftOperator = lower
            ? ExpressionType.GreaterThanOrEqual
            : ExpressionType.LessThanOrEqual;
        var propertyOnRightOperator = lower
            ? ExpressionType.LessThanOrEqual
            : ExpressionType.GreaterThanOrEqual;

        if (comparison.OperatorType == propertyOnLeftOperator
            && comparison.Left is SqlPropertyExpression leftProperty)
        {
            subject = leftProperty;
            bound = comparison.Right;
            return true;
        }

        if (comparison.OperatorType == propertyOnRightOperator
            && comparison.Right is SqlPropertyExpression rightProperty)
        {
            subject = rightProperty;
            bound = comparison.Left;
            return true;
        }

        return false;
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
