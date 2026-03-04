using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Extensions;
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

    private static readonly MethodInfo StringStartsWithMethod =
        ((Func<string, bool>)string.Empty.StartsWith).Method;

    private static readonly MethodInfo ArrayEmptyMethod =
        ((Func<object[]>)Array.Empty<object>).Method.GetGenericMethodDefinition();

    private static readonly MethodInfo EnumerableEmptyMethod =
        ((Func<IEnumerable<object>>)Enumerable.Empty<object>).Method.GetGenericMethodDefinition();

    private static readonly MethodInfo IsNullMethod =
        typeof(DynamoDbFunctionsExtensions).GetMethod(nameof(DynamoDbFunctionsExtensions.IsNull))!;

    private static readonly MethodInfo IsNotNullMethod =
        typeof(DynamoDbFunctionsExtensions).GetMethod(nameof(DynamoDbFunctionsExtensions.IsNotNull))
        !;

    private static readonly MethodInfo IsMissingMethod =
        typeof(DynamoDbFunctionsExtensions).GetMethod(nameof(DynamoDbFunctionsExtensions.IsMissing))
        !;

    private static readonly MethodInfo IsNotMissingMethod =
        typeof(DynamoDbFunctionsExtensions).GetMethod(
            nameof(DynamoDbFunctionsExtensions.IsNotMissing))!;

    private static readonly MethodInfo EnumerableElementAtMethod = typeof(Enumerable)
        .GetMethods()
        .Single(m => m is { Name: nameof(Enumerable.ElementAt), IsGenericMethod: true }
            && m.GetParameters().Length == 2
            && m.GetParameters()[1].ParameterType == typeof(int))
        .GetGenericMethodDefinition();

    private static readonly MethodInfo QueryableElementAtMethod =
        typeof(Queryable)
            .GetMethods()
            .Single(m => m is { Name: nameof(Queryable.ElementAt), IsGenericMethod: true }
                && m.GetParameters().Length == 2
                && m.GetParameters()[1].ParameterType == typeof(int))
            .GetGenericMethodDefinition();

    private static readonly MethodInfo EfPropertyMethod =
        typeof(EF)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m is { Name: nameof(EF.Property), IsGenericMethod: true })
            .GetGenericMethodDefinition();

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
        if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
        {
            SqlExpression? operand = null;
            if (node.Right is ConstantExpression { Value: null })
                operand = TryTranslateOwnedNavigationOperandForNullComparison(node.Left)
                    ?? TranslateInternal(node.Left);
            else if (node.Left is ConstantExpression { Value: null })
                operand = TryTranslateOwnedNavigationOperandForNullComparison(node.Right)
                    ?? TranslateInternal(node.Right);

            if (operand != null)
            {
                var composed = node.NodeType == ExpressionType.Equal
                    ? sqlExpressionFactory.Binary(
                        ExpressionType.OrElse,
                        sqlExpressionFactory.IsNull(operand),
                        sqlExpressionFactory.IsMissing(operand))
                    : sqlExpressionFactory.Binary(
                        ExpressionType.AndAlso,
                        sqlExpressionFactory.IsNotNull(operand),
                        sqlExpressionFactory.IsNotMissing(operand));

                if (composed != null)
                    return composed;
            }
        }

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

    /// <summary>
    ///     Translates a single-segment owned reference navigation operand for null comparisons in
    ///     predicates.
    /// </summary>
    private SqlExpression? TryTranslateOwnedNavigationOperandForNullComparison(Expression operand)
    {
        string? memberName = null;
        Type? memberType = null;
        Expression? sourceExpression = null;

        if (operand is MemberExpression memberExpression)
        {
            memberName = memberExpression.Member.Name;
            memberType = memberExpression.Type;
            sourceExpression = memberExpression.Expression;
        }
        else if (operand is MethodCallExpression
            {
                Method.IsGenericMethod: true,
                Arguments: [var source, ConstantExpression { Value: string efPropertyName }],
            } methodCall
            && methodCall.Method.GetGenericMethodDefinition() == EfPropertyMethod)
        {
            memberName = efPropertyName;
            memberType = methodCall.Type;
            sourceExpression = source;
        }

        if (memberName == null || memberType == null)
            return null;

        var rootEntityType = ResolveRootEntityType(sourceExpression);
        if (rootEntityType?.FindNavigation(memberName) is not { IsCollection: false } navigation
            || !navigation.IsEmbedded())
            return null;

        var navigationAttributeName =
            navigation.TargetEntityType.GetContainingAttributeName() ?? navigation.Name;
        return sqlExpressionFactory.Property(navigationAttributeName, memberType);
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
        // Collect the full member chain upward to the root, handling both plain MemberExpression
        // nodes and EF.Property<T>(...) calls produced by navigation expansion.
        var names = new List<string>();
        Expression? current = node;
        while (true)
            if (current is MemberExpression me)
            {
                names.Add(me.Member.Name);
                current = me.Expression;
            }
            else if (current is MethodCallExpression { Method.IsGenericMethod: true } mc
                && mc.Method.GetGenericMethodDefinition() == EfPropertyMethod
                && mc.Arguments[1] is ConstantExpression { Value: string efPropName })
            {
                names.Add(efPropName);
                current = mc.Arguments[0];
            }
            else
            {
                break;
            }

        // Accept ParameterExpression (WHERE context) or StructuralTypeShaperExpression
        // (SELECT context — EF Core wraps the root entity in a shaper before visiting the lambda).
        IEntityType? rootEntityType = null;
        if (current is ParameterExpression pe)
        {
            _lambdaParameterEntityTypes?.TryGetValue(pe, out rootEntityType);
        }
        else if (current is StructuralTypeShaperExpression
            {
                StructuralType: IEntityType shaperRootType,
            })
        {
            rootEntityType = shaperRootType;
        }
        else
        {
            AddTranslationErrorDetails(DynamoStrings.MemberAccessNotSupported);
            return QueryCompilationContext.NotTranslatedExpression;
        }

        names.Reverse(); // Root-first order: e.g. [Profile, Address, City]

        // Fast path: single-level direct property access — preserve existing behaviour exactly
        if (names.Count == 1)
        {
            if (rootEntityType?.FindProperty(node.Member.Name) is { } directProperty)
            {
                var isPartitionKey =
                    rootEntityType.GetPartitionKeyProperty()?.Name == directProperty.Name;
                return sqlExpressionFactory.Property(
                    directProperty.GetAttributeName(),
                    node.Type,
                    isPartitionKey);
            }

            // When entity type is known but FindProperty returned null the member is a navigation
            // or other non-scalar — the binding visitor handles those via
            // DynamoObjectAccessExpression.
            if (rootEntityType != null)
            {
                AddTranslationErrorDetails(DynamoStrings.MemberAccessNotSupported);
                return QueryCompilationContext.NotTranslatedExpression;
            }

            return sqlExpressionFactory.Property(node.Member.Name, node.Type);
        }

        // Multi-level with entity type context: walk the EF model to resolve attribute names
        if (rootEntityType != null)
            return TranslateNestedMemberChain(names, rootEntityType, node.Type);

        // Multi-level member access without entity type context is not translatable
        AddTranslationErrorDetails(DynamoStrings.MemberAccessNotSupported);
        return QueryCompilationContext.NotTranslatedExpression;
    }

    /// <summary>
    ///     Translates a multi-segment owned navigation chain to a nested path expression by walking
    ///     intermediate owned navigations and resolving the leaf scalar property through the EF model.
    /// </summary>
    private Expression TranslateNestedMemberChain(
        List<string> names,
        IEntityType rootEntityType,
        Type leafType)
    {
        Expression? sqlExpr = null;
        var currentEntityType = rootEntityType;

        for (var i = 0; i < names.Count; i++)
        {
            var memberName = names[i];
            var isLast = i == names.Count - 1;

            if (isLast)
            {
                // Leaf must be a scalar property — whole-entity leaf navigation is not supported
                var property = currentEntityType.FindProperty(memberName);
                if (property == null)
                {
                    AddTranslationErrorDetails(DynamoStrings.MemberAccessNotSupported);
                    return QueryCompilationContext.NotTranslatedExpression;
                }

                var attributeName = property.GetAttributeName();
                var typeMapping = property.GetTypeMapping();
                return sqlExpr == null
                    ? sqlExpressionFactory.Property(attributeName, leafType)
                    : new DynamoScalarAccessExpression(
                        sqlExpr,
                        attributeName,
                        leafType,
                        typeMapping);
            }

            // Intermediate segment: must be an embedded, non-collection owned navigation
            var nav = currentEntityType.FindNavigation(memberName);
            if (nav == null || !nav.IsEmbedded() || nav.IsCollection)
            {
                AddTranslationErrorDetails(DynamoStrings.MemberAccessNotSupported);
                return QueryCompilationContext.NotTranslatedExpression;
            }

            var navAttributeName = nav.TargetEntityType.GetContainingAttributeName() ?? nav.Name;
            sqlExpr = sqlExpr == null
                ? sqlExpressionFactory.Property(navAttributeName, typeof(object))
                : new DynamoScalarAccessExpression(sqlExpr, navAttributeName, typeof(object));
            currentEntityType = nav.TargetEntityType;
        }

        // Unreachable — chain is always non-empty, loop always returns from leaf branch
        AddTranslationErrorDetails(DynamoStrings.MemberAccessNotSupported);
        return QueryCompilationContext.NotTranslatedExpression;
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
        // EF.Property<T>(source, "name") — translate to direct property access.
        // Appears in list element access contexts after navigation expansion, e.g.,
        // EF.Property<List<string>>(n, "Tags").AsQueryable().ElementAt(0).
        if (node.Method.IsGenericMethod
            && node.Method.GetGenericMethodDefinition() == EfPropertyMethod
            && node.Arguments[1] is ConstantExpression { Value: string efPropName })
        {
            return TranslateEfPropertyAccess(node, efPropName);
        }

        if (node.Method == IsNullMethod
            || node.Method == IsNotNullMethod
            || node.Method == IsMissingMethod
            || node.Method == IsNotMissingMethod)
            return TranslateDynamoDbFunctions(node);

        if (node.Method == StringContainsMethod)
            return TranslateStringContains(node);

        if (node.Method.DeclaringType == typeof(string)
            && node.Method.Name == nameof(string.Contains))
        {
            AddTranslationErrorDetails(DynamoStrings.StringContainsOverloadNotSupported);
            return QueryCompilationContext.NotTranslatedExpression;
        }

        if (node.Method == StringStartsWithMethod)
            return TranslateStringStartsWith(node);

        if (node.Method.DeclaringType == typeof(string)
            && node.Method.Name == nameof(string.StartsWith))
        {
            AddTranslationErrorDetails(DynamoStrings.StringStartsWithOverloadNotSupported);
            return QueryCompilationContext.NotTranslatedExpression;
        }

        if (IsCollectionContainsMethod(node.Method))
            return TranslateCollectionContains(node);

        if (node.Method.IsGenericMethod
            && (node.Method.GetGenericMethodDefinition() == EnumerableElementAtMethod
                || node.Method.GetGenericMethodDefinition() == QueryableElementAtMethod))
            return TranslateElementAt(node);

        AddTranslationErrorDetails(DynamoStrings.MethodCallInPredicateNotSupported);
        return QueryCompilationContext.NotTranslatedExpression;
    }

    /// <summary>
    ///     Translates <c>EF.Property&lt;T&gt;(source, "name")</c> to scalar property access,
    ///     supporting nested chains that represent owned-navigation paths.
    /// </summary>
    private Expression TranslateEfPropertyAccess(MethodCallExpression node, string propertyName)
    {
        var names = new List<string> { propertyName };
        var current = node.Arguments[0];
        while (true)
            if (current is MethodCallExpression { Method.IsGenericMethod: true } methodCall
                && methodCall.Method.GetGenericMethodDefinition() == EfPropertyMethod
                && methodCall.Arguments[1] is ConstantExpression { Value: string nestedProperty })
            {
                names.Add(nestedProperty);
                current = methodCall.Arguments[0];
            }
            else if (current is MemberExpression member)
            {
                names.Add(member.Member.Name);
                current = member.Expression;
            }
            else
            {
                break;
            }

        var rootEntityType = ResolveRootEntityType(current);
        names.Reverse();

        if (rootEntityType != null)
        {
            if (names.Count == 1)
            {
                if (rootEntityType.FindProperty(propertyName) is { } rootProperty)
                {
                    var isPartitionKey =
                        rootEntityType.GetPartitionKeyProperty()?.Name == rootProperty.Name;
                    return sqlExpressionFactory.Property(
                        rootProperty.GetAttributeName(),
                        node.Type,
                        isPartitionKey);
                }

                AddTranslationErrorDetails(DynamoStrings.MemberAccessNotSupported);
                return QueryCompilationContext.NotTranslatedExpression;
            }

            return TranslateNestedMemberChain(names, rootEntityType, node.Type);
        }

        var translatedSource = Visit(node.Arguments[0]);
        if (translatedSource is SqlExpression sqlSource)
            return new DynamoScalarAccessExpression(sqlSource, propertyName, node.Type);

        return sqlExpressionFactory.Property(propertyName, node.Type);
    }

    /// <summary>
    ///     Resolves the root entity type for parameter or shaper-root expressions used in member
    ///     translation.
    /// </summary>
    private IEntityType? ResolveRootEntityType(Expression? expression)
    {
        if (expression is ParameterExpression parameter && _lambdaParameterEntityTypes != null)
        {
            _lambdaParameterEntityTypes.TryGetValue(parameter, out var parameterEntityType);
            return parameterEntityType;
        }

        return expression is StructuralTypeShaperExpression
        {
            StructuralType: IEntityType shaperType,
        }
            ? shaperType
            : null;
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

        if (node.NodeType == ExpressionType.Not)
        {
            var operand = Visit(node.Operand);
            if (operand == QueryCompilationContext.NotTranslatedExpression)
                return QueryCompilationContext.NotTranslatedExpression;

            if (operand is SqlExpression sqlOperand)
            {
                if (IsBooleanType(sqlOperand.Type))
                    return sqlExpressionFactory.Not(sqlOperand);

                AddTranslationErrorDetails(DynamoStrings.BitwiseComplementNotSupported);
            }

            return QueryCompilationContext.NotTranslatedExpression;
        }

        AddTranslationErrorDetails(DynamoStrings.UnaryOperatorNotSupported);
        return QueryCompilationContext.NotTranslatedExpression;
    }

    /// <summary>Translates EF.Functions IS NULL/MISSING extension methods to SQL IS predicates.</summary>
    private Expression TranslateDynamoDbFunctions(MethodCallExpression node)
    {
        // node.Arguments[0] is DbFunctions, node.Arguments[1] is the value
        var operand = TranslateInternal(node.Arguments[1]);
        if (operand == null)
            return QueryCompilationContext.NotTranslatedExpression;

        return node.Method.Name switch
        {
            nameof(DynamoDbFunctionsExtensions.IsNull) => sqlExpressionFactory.IsNull(operand),
            nameof(DynamoDbFunctionsExtensions.IsNotNull) =>
                sqlExpressionFactory.IsNotNull(operand),
            nameof(DynamoDbFunctionsExtensions.IsMissing) =>
                sqlExpressionFactory.IsMissing(operand),
            nameof(DynamoDbFunctionsExtensions.IsNotMissing) => sqlExpressionFactory.IsNotMissing(
                operand),
            _ => QueryCompilationContext.NotTranslatedExpression,
        };
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

    /// <summary>Translates string.StartsWith to the DynamoDB begins_with function.</summary>
    private Expression TranslateStringStartsWith(MethodCallExpression node)
    {
        var instance = TranslateInternal(node.Object!);
        var argument = TranslateInternal(node.Arguments[0]);
        if (instance == null || argument == null)
            return QueryCompilationContext.NotTranslatedExpression;

        return sqlExpressionFactory.Function("begins_with", [instance, argument], typeof(bool));
    }

    /// <summary>
    ///     Translates <c>Enumerable.ElementAt(source, index)</c> or
    ///     <c>Queryable.ElementAt(source, index)</c> to a list index access expression. EF Core normalises
    ///     <c>list[i]</c> to this form, wrapping the source in <c>.AsQueryable()</c> when the collection
    ///     is an owned navigation property.
    /// </summary>
    private Expression TranslateElementAt(MethodCallExpression node)
    {
        // Strip .AsQueryable() wrapper that EF Core adds when the source is an owned collection
        // property
        var sourceArg = node.Arguments[0];
        if (sourceArg is MethodCallExpression { Method.Name: "AsQueryable" } asq)
            sourceArg = asq.Arguments[0];

        var translatedSource = Visit(sourceArg);
        if (translatedSource is not SqlExpression sqlSource)
        {
            AddTranslationErrorDetails(DynamoStrings.MemberAccessNotSupported);
            return QueryCompilationContext.NotTranslatedExpression;
        }

        if (node.Arguments[1] is not ConstantExpression { Value: int index })
        {
            AddTranslationErrorDetails(DynamoStrings.ListIndexMustBeConstant);
            return QueryCompilationContext.NotTranslatedExpression;
        }

        var elementType = node.Method.GetGenericArguments()[0];
        return new DynamoListIndexExpression(sqlSource, index, elementType);
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

    /// <summary>Returns whether the supplied type is a boolean or nullable boolean type.</summary>
    private static bool IsBooleanType(Type type) => type == typeof(bool) || type == typeof(bool?);

    /// <summary>Tries to extract collection and item arguments from a Contains call.</summary>
    private static (Expression? CollectionExpression, Expression? ItemExpression)
        TryGetCollectionContainsArguments(MethodCallExpression node)
        => node.Method.IsStatic
            ? (node.Arguments.ElementAtOrDefault(0), node.Arguments.ElementAtOrDefault(1))
            : (node.Object, node.Arguments.ElementAtOrDefault(0));
}
