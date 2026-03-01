using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Translates C# expression trees to SQL expression trees.
/// </summary>
public class DynamoSqlTranslatingExpressionVisitor(ISqlExpressionFactory sqlExpressionFactory)
    : ExpressionVisitor
{
    private static readonly MethodInfo EnumerableContainsMethod =
        ((Func<IEnumerable<object>, object, bool>)Enumerable.Contains).Method
        .GetGenericMethodDefinition();

    private static readonly MethodInfo StringContainsMethod =
        ((Func<string, bool>)string.Empty.Contains).Method;

    private static readonly MethodInfo ArrayEmptyMethod =
        ((Func<object[]>)Array.Empty<object>).Method.GetGenericMethodDefinition();

    private static readonly MethodInfo EnumerableEmptyMethod =
        ((Func<IEnumerable<object>>)Enumerable.Empty<object>).Method.GetGenericMethodDefinition();

    private IReadOnlyDictionary<ParameterExpression, IEntityType>? _lambdaParameterEntityTypes;

    /// <summary>Gets the latest translation error details captured for the current translation attempt.</summary>
    public string? TranslationErrorDetails { get; private set; }

    /// <summary>
    /// Translates a C# expression to a SQL expression.
    /// </summary>
    public SqlExpression? Translate(Expression expression) => Translate(expression, null);

    /// <summary>Translates a C# expression to a SQL expression using lambda parameter entity metadata.</summary>
    public SqlExpression? Translate(
        Expression expression,
        IReadOnlyDictionary<ParameterExpression, IEntityType>? lambdaParameterEntityTypes)
    {
        TranslationErrorDetails = null;
        _lambdaParameterEntityTypes = lambdaParameterEntityTypes;

        try
        {
            return TranslateInternal(expression);
        }
        finally
        {
            _lambdaParameterEntityTypes = null;
        }
    }

    /// <summary>Translates an expression without resetting translation error details.</summary>
    private SqlExpression? TranslateInternal(Expression expression)
        => Visit(expression) as SqlExpression;

    /// <summary>Adds a translation error detail message for the current translation attempt.</summary>
    protected virtual void AddTranslationErrorDetails(string details)
        => TranslationErrorDetails =
            TranslationErrorDetails == null
                ? details
                : TranslationErrorDetails + Environment.NewLine + details;

    /// <inheritdoc />
    protected override Expression VisitBinary(BinaryExpression node)
    {
        var left = TranslateInternal(node.Left);
        var right = TranslateInternal(node.Right);

        if (left == null || right == null)
            return QueryCompilationContext.NotTranslatedExpression;

        var sqlBinaryExpression = sqlExpressionFactory.Binary(node.NodeType, left, right);
        if (sqlBinaryExpression != null)
            return sqlBinaryExpression;

        AddTranslationErrorDetails(DynamoStrings.UnsupportedBinaryOperator(node.NodeType));
        return QueryCompilationContext.NotTranslatedExpression;
    }

    /// <inheritdoc />
    protected override Expression VisitConstant(ConstantExpression node)
        => sqlExpressionFactory.Constant(node.Value, node.Type);

    /// <inheritdoc />
    protected override Expression VisitParameter(ParameterExpression node)
        => QueryCompilationContext.NotTranslatedExpression;

    /// <inheritdoc />
    protected override Expression VisitMember(MemberExpression node)
    {
        if (node.Expression is not ParameterExpression parameterExpression)
        {
            AddTranslationErrorDetails(DynamoStrings.MemberAccessNotSupported);
            return QueryCompilationContext.NotTranslatedExpression;
        }

        if (_lambdaParameterEntityTypes != null
            && _lambdaParameterEntityTypes.TryGetValue(parameterExpression, out var entityType)
            && entityType.FindProperty(node.Member.Name) is { } property)
        {
            var isPartitionKey = entityType.GetPartitionKeyProperty()?.Name == property.Name;
            return sqlExpressionFactory.Property(
                property.GetAttributeName(),
                node.Type,
                isPartitionKey);
        }

        return sqlExpressionFactory.Property(node.Member.Name, node.Type);
    }

    /// <inheritdoc />
    protected override Expression VisitExtension(Expression extensionExpression)
    {
        switch (extensionExpression)
        {
            case SqlExpression sqlExpression:
                return sqlExpression;

            case QueryParameterExpression queryParameter:
                return sqlExpressionFactory.Parameter(queryParameter.Name, queryParameter.Type);

            default:
                return QueryCompilationContext.NotTranslatedExpression;
        }
    }

    /// <inheritdoc />
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method == StringContainsMethod)
            return TranslateStringContains(node);

        if (node.Method.DeclaringType == typeof(string)
            && node.Method.Name == nameof(string.Contains))
        {
            AddTranslationErrorDetails(DynamoStrings.StringContainsOverloadNotSupported);
            return QueryCompilationContext.NotTranslatedExpression;
        }

        if (IsCollectionContainsMethod(node.Method))
            return TranslateCollectionContains(node);

        AddTranslationErrorDetails(DynamoStrings.MethodCallInPredicateNotSupported);
        return QueryCompilationContext.NotTranslatedExpression;
    }

    /// <inheritdoc />
    protected override Expression VisitUnary(UnaryExpression node)
    {
        if (node.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked)
        {
            var operand = Visit(node.Operand);
            if (operand == QueryCompilationContext.NotTranslatedExpression)
                return QueryCompilationContext.NotTranslatedExpression;

            if (operand is SqlExpression sqlOperand)
                return sqlExpressionFactory.ApplyTypeMapping(sqlOperand, node.Type);

            return QueryCompilationContext.NotTranslatedExpression;
        }

        AddTranslationErrorDetails(DynamoStrings.UnaryOperatorNotSupported);
        return QueryCompilationContext.NotTranslatedExpression;
    }

    /// <summary>Translates string.Contains to the DynamoDB contains function.</summary>
    private Expression TranslateStringContains(MethodCallExpression node)
    {
        var instance = TranslateInternal(node.Object!);
        var argument = TranslateInternal(node.Arguments[0]);
        if (instance == null || argument == null)
            return QueryCompilationContext.NotTranslatedExpression;

        return sqlExpressionFactory.Function("contains", [instance, argument], typeof(bool));
    }

    /// <summary>Translates in-memory collection Contains calls to IN predicates.</summary>
    private Expression TranslateCollectionContains(MethodCallExpression node)
    {
        var (collectionExpression, itemExpression) = TryGetCollectionContainsArguments(node);
        if (collectionExpression == null || itemExpression == null)
        {
            AddTranslationErrorDetails(DynamoStrings.ContainsCollectionShapeNotSupported);
            return QueryCompilationContext.NotTranslatedExpression;
        }

        var item = TranslateInternal(itemExpression);
        if (item == null)
            return QueryCompilationContext.NotTranslatedExpression;

        if (item is not SqlPropertyExpression
            and not SqlParameterExpression
            and not SqlConstantExpression)
        {
            AddTranslationErrorDetails(DynamoStrings.ContainsCollectionShapeNotSupported);
            return QueryCompilationContext.NotTranslatedExpression;
        }

        var isPartitionKeyComparison =
            item is SqlPropertyExpression property && property.IsPartitionKey;

        var translatedCollection = TranslateInternal(collectionExpression);
        if (translatedCollection is SqlParameterExpression valuesParameter)
            return sqlExpressionFactory.In(item, valuesParameter, isPartitionKeyComparison);

        if (TryTranslateInlineValues(collectionExpression, item.Type, out var values))
            return sqlExpressionFactory.In(item, values, isPartitionKeyComparison);

        AddTranslationErrorDetails(DynamoStrings.ContainsCollectionShapeNotSupported);
        return QueryCompilationContext.NotTranslatedExpression;
    }

    /// <summary>Tries to translate a collection expression into inline SQL values.</summary>
    private bool TryTranslateInlineValues(
        Expression collectionExpression,
        Type itemType,
        out List<SqlExpression> values)
    {
        values = [];

        if (collectionExpression is NewArrayExpression newArrayExpression)
        {
            foreach (var element in newArrayExpression.Expressions)
            {
                var translated = TranslateInternal(element);
                if (translated == null)
                    return false;

                values.Add(sqlExpressionFactory.ApplyTypeMapping(translated, itemType));
            }

            return true;
        }

        if (collectionExpression is ConstantExpression constantExpression
            && TryGetConstantEnumerableValues(constantExpression.Value, itemType, values))
            return true;

        if (collectionExpression is MethodCallExpression methodCallExpression
            && IsEmptyCollectionFactoryMethod(methodCallExpression))
            return true;

        return false;
    }

    /// <summary>Tries to translate constant enumerable values into SQL constants.</summary>
    private bool TryGetConstantEnumerableValues(
        object? constantValue,
        Type itemType,
        List<SqlExpression> values)
    {
        if (constantValue is null)
            return false;

        if (constantValue is string)
            return false;

        if (constantValue is not IEnumerable enumerable)
            return false;

        foreach (var value in enumerable)
            values.Add(sqlExpressionFactory.Constant(value, itemType));

        return true;
    }

    /// <summary>Returns whether a method represents an in-memory collection Contains call.</summary>
    private static bool IsCollectionContainsMethod(MethodInfo method)
    {
        if (method.IsGenericMethod
            && method.GetGenericMethodDefinition() == EnumerableContainsMethod)
            return true;

        if (method.DeclaringType is not { } declaringType)
            return false;

        return !method.IsStatic
            && method.Name == nameof(ICollection<object>.Contains)
            && method.ReturnType == typeof(bool)
            && declaringType != typeof(string)
            && method.GetParameters().Length == 1
            && typeof(IEnumerable).IsAssignableFrom(declaringType);
    }

    /// <summary>Returns whether a method call creates a known empty in-memory collection.</summary>
    private static bool IsEmptyCollectionFactoryMethod(MethodCallExpression methodCallExpression)
    {
        if (!methodCallExpression.Method.IsGenericMethod
            || methodCallExpression.Arguments.Count != 0)
            return false;

        var genericMethodDefinition = methodCallExpression.Method.GetGenericMethodDefinition();

        return genericMethodDefinition == ArrayEmptyMethod
            || genericMethodDefinition == EnumerableEmptyMethod;
    }

    /// <summary>Tries to extract collection and item arguments from a Contains call.</summary>
    private static (Expression? CollectionExpression, Expression? ItemExpression)
        TryGetCollectionContainsArguments(MethodCallExpression node)
        => node.Method.IsStatic
            ? (node.Arguments.ElementAtOrDefault(0), node.Arguments.ElementAtOrDefault(1))
            : (node.Object, node.Arguments.ElementAtOrDefault(0));
}
