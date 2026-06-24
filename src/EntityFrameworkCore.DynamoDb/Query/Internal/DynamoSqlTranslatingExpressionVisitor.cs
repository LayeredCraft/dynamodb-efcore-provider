using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Translates C# expression trees to SQL expression trees.
/// </summary>
public sealed class DynamoSqlTranslatingExpressionVisitor(
    ISqlExpressionFactory sqlExpressionFactory,
    IModel model,
    DynamoQueryCompilationContext? queryCompilationContext = null) : ExpressionVisitor
{
    private static readonly MethodInfo StringCompareMethod =
        ((Func<string?, string?, int>)string.Compare).Method;

    private static readonly MethodInfo StringCompareToMethod =
        ((Func<string?, int>)string.Empty.CompareTo).Method;

    private static readonly MethodInfo EnumerableContainsMethod =
        ((Func<IEnumerable<object>, object, bool>)Enumerable.Contains).Method
        .GetGenericMethodDefinition();

    private static readonly MethodInfo QueryableContainsMethod =
        typeof(Queryable)
            .GetMethods()
            .Single(m => m is { Name: nameof(Queryable.Contains), IsGenericMethod: true }
                && m.GetParameters().Length == 2)
            .GetGenericMethodDefinition();

    private static readonly MethodInfo StringContainsMethod =
        ((Func<string, bool>)string.Empty.Contains).Method;

    private static readonly MethodInfo StringStartsWithMethod =
        ((Func<string, bool>)string.Empty.StartsWith).Method;

    private static readonly MethodInfo StringIsNullOrEmptyMethod =
        typeof(string).GetMethod(nameof(string.IsNullOrEmpty), [typeof(string)])!;

    private static readonly MethodInfo ObjectEqualsMethod =
        typeof(object).GetMethod(nameof(object.Equals), [typeof(object)])!;

    private static readonly MethodInfo ObjectStaticEqualsMethod =
        typeof(object).GetMethod(nameof(object.Equals), [typeof(object), typeof(object)])!;

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
    private void AddTranslationErrorDetails(string details)
        => TranslationErrorDetails =
            TranslationErrorDetails == null
                ? details
                : TranslationErrorDetails + Environment.NewLine + details;

    /// <inheritdoc />
    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual
            && TryTranslateGetTypeComparison(node) is { } getTypeComparison)
            return getTypeComparison;

        // Translate string.Compare(a, b) OP 0 → a OP b
        if (TryTranslateStringCompare(node) is { } stringCompareResult)
            return stringCompareResult;

        if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
        {
            SqlExpression? operand = null;
            if (node.Right is ConstantExpression { Value: null })
                operand = TryTranslateComplexPropertyForStructuralComparison(node.Left)
                    ?? TranslateInternal(node.Left);
            else if (node.Left is ConstantExpression { Value: null })
                operand = TryTranslateComplexPropertyForStructuralComparison(node.Right)
                    ?? TranslateInternal(node.Right);

            if (operand != null && operand.TypeMapping?.Converter?.ConvertsNulls != true)
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

        if (node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual)
        {
            // Whole complex-property access does not translate through the scalar member path.
            // Retry untranslated operands as complex map paths, then bind inline complex object
            // constants to the discovered complex map mapping when one side is a complex path.
            left ??= TryTranslateComplexPropertyForStructuralComparison(node.Left);
            right ??= TryTranslateComplexPropertyForStructuralComparison(node.Right);
            if (left is SqlExpression { TypeMapping: DynamoComplexTypeMapping } leftComplex)
                right ??= TryTranslateComplexConstant(node.Right, leftComplex);
            if (right is SqlExpression { TypeMapping: DynamoComplexTypeMapping } rightComplex)
                left ??= TryTranslateComplexConstant(node.Left, rightComplex);
        }

        if (left == null || right == null)
            return QueryCompilationContext.NotTranslatedExpression;

        var sqlBinaryExpression = sqlExpressionFactory.Binary(node.NodeType, left, right);
        if (sqlBinaryExpression != null)
            return sqlBinaryExpression;

        AddTranslationErrorDetails(DynamoStrings.UnsupportedBinaryOperator(node.NodeType));
        return QueryCompilationContext.NotTranslatedExpression;
    }

    /// <summary>Translates an inline complex object constant using the matched complex map mapping.</summary>
    private SqlExpression? TryTranslateComplexConstant(
        Expression operand,
        SqlExpression complexPath)
    {
        if (ContainsQueryReference(operand))
            return null;

        if (!IsLiteralShape(operand))
            return null;

        // ConstantExpression: extract the value directly without compilation
        if (operand is ConstantExpression constantExpression)
            return sqlExpressionFactory.ApplyTypeMapping(
                sqlExpressionFactory.Constant(constantExpression.Value, operand.Type),
                complexPath.TypeMapping);

        try
        {
            var value =
                Expression
                    .Lambda<Func<object?>>(Expression.Convert(operand, typeof(object)))
                    .Compile(true)
                    .Invoke();
            return sqlExpressionFactory.ApplyTypeMapping(
                sqlExpressionFactory.Constant(value, operand.Type),
                complexPath.TypeMapping);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            AddTranslationErrorDetails(
                $"Inline complex type constant could not be evaluated: {exception.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Returns true when the expression is a side-effect-free, safe-to-evaluate literal shape:
    ///     a <see cref="ConstantExpression"/>, a <see cref="MemberExpression"/> chain rooted at a
    ///     <see cref="ConstantExpression"/> (closed-over captures), or a Convert/ConvertChecked
    ///     <see cref="UnaryExpression"/> over one of those shapes.
    /// </summary>
    private static bool IsLiteralShape(Expression expression)
        => expression switch
        {
            ConstantExpression => true,
            // Only allow field access — property getters may execute arbitrary user code.
            MemberExpression { Member: System.Reflection.FieldInfo } memberExpression =>
                IsLiteralShape(memberExpression.Expression!),
            UnaryExpression
            {
                NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked
            } unaryExpression => IsLiteralShape(unaryExpression.Operand),
            _ => false
        };

    private static bool ContainsQueryReference(Expression expression)
    {
        var visitor = new QueryReferenceFindingExpressionVisitor();
        visitor.Visit(expression);
        return visitor.Found;
    }

    private sealed class QueryReferenceFindingExpressionVisitor : ExpressionVisitor
    {
        public bool Found { get; private set; }

        public override Expression? Visit(Expression? node)
        {
            if (node is null || Found)
                return node;

            if (node is ParameterExpression
                or QueryParameterExpression
                or StructuralTypeShaperExpression)
            {
                Found = true;
                return node;
            }

            return base.Visit(node);
        }
    }

    /// <inheritdoc />
    protected override Expression VisitTypeBinary(TypeBinaryExpression node)
        => node.NodeType == ExpressionType.TypeIs
            ? TryCreateDiscriminatorPredicate(node.Expression, node.TypeOperand, false)
            ?? QueryCompilationContext.NotTranslatedExpression
            : QueryCompilationContext.NotTranslatedExpression;

    private SqlExpression? TryTranslateGetTypeComparison(BinaryExpression node)
    {
        if (TryUnwrapGetTypeComparison(node.Left, node.Right, out var instance, out var type)
            || TryUnwrapGetTypeComparison(node.Right, node.Left, out instance, out type))
        {
            var predicate = TryCreateDiscriminatorPredicate(instance, type, true);
            if (predicate is null)
                return null;

            return node.NodeType == ExpressionType.NotEqual
                ? sqlExpressionFactory.Not(predicate)
                : predicate;
        }

        return null;
    }

    private static bool TryUnwrapGetTypeComparison(
        Expression getTypeSide,
        Expression typeSide,
        out Expression instance,
        out Type type)
    {
        instance = null!;
        type = null!;

        getTypeSide = UnwrapConvert(getTypeSide);
        typeSide = UnwrapConvert(typeSide);

        if (getTypeSide is MethodCallExpression
            {
                Object: { } source, Method.Name: nameof(object.GetType)
            }
            && typeSide is ConstantExpression { Value: Type comparedType })
        {
            instance = source;
            type = comparedType;
            return true;
        }

        return false;
    }

    private static Expression UnwrapConvert(Expression expression)
    {
        while (expression is UnaryExpression
            {
                NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked
            } unaryExpression)
            expression = unaryExpression.Operand;

        return expression;
    }

    private SqlExpression? TryCreateDiscriminatorPredicate(
        Expression source,
        Type targetClrType,
        bool exact)
    {
        var sourceEntityType = ResolveRootEntityType(source);
        var targetEntityType = model.FindEntityType(targetClrType);
        if (sourceEntityType is null || targetEntityType is null)
            return null;

        if (!sourceEntityType.GetRootType().IsAssignableFrom(targetEntityType)
            || !targetEntityType.GetRootType().IsAssignableFrom(sourceEntityType))
            return exact ? sqlExpressionFactory.Constant(false, typeof(bool)) : null;

        var discriminatorProperty = targetEntityType.FindDiscriminatorProperty();
        if (discriminatorProperty is null)
            return null;

        var discriminatorColumn = sqlExpressionFactory.ApplyTypeMapping(
            sqlExpressionFactory.Property(
                discriminatorProperty.GetAttributeName(),
                discriminatorProperty.ClrType),
            discriminatorProperty.GetTypeMapping());

        var concreteTypes = exact
            ? targetEntityType.ClrType.IsAbstract ? [] : [targetEntityType]
            : targetEntityType
                .GetConcreteDerivedTypesInclusive()
                .Where(static entityType => !entityType.ClrType.IsAbstract);

        SqlExpression? predicate = null;
        foreach (var concreteType in concreteTypes)
        {
            var discriminatorValue = concreteType.GetDiscriminatorValue();
            if (discriminatorValue is null)
                continue;

            var equals = sqlExpressionFactory.Binary(
                ExpressionType.Equal,
                discriminatorColumn,
                sqlExpressionFactory.Constant(discriminatorValue, discriminatorProperty.ClrType));

            if (equals is null)
                return null;

            predicate = predicate is null
                ? equals
                : sqlExpressionFactory.Binary(ExpressionType.OrElse, predicate, equals);
        }

        return predicate switch
        {
            null => sqlExpressionFactory.Constant(false, typeof(bool)),
            SqlBinaryExpression { OperatorType: ExpressionType.OrElse } =>
                new SqlParenthesizedExpression(predicate),
            _ => predicate
        };
    }

    /// <inheritdoc />
    protected override Expression VisitConditional(ConditionalExpression node)
        => QueryCompilationContext.NotTranslatedExpression;

    /// <summary>
    ///     Translates whole-complex-property member access to the underlying map attribute path.
    /// </summary>
    private SqlExpression? TryTranslateComplexPropertyForStructuralComparison(Expression operand)
    {
        if (!TryGetMemberAccessChain(operand, out var names, out var sourceExpression))
            return null;

        var rootEntityType = ResolveRootEntityType(sourceExpression);
        if (rootEntityType == null)
            return null;

        return TryTranslateComplexPropertyPath(names, rootEntityType, operand.Type);
    }

    /// <summary>
    ///     Extracts a root-first member-access chain from plain member access or nested
    ///     <c>EF.Property&lt;T&gt;</c> calls.
    /// </summary>
    private bool TryGetMemberAccessChain(
        Expression operand,
        out List<string> names,
        out Expression? sourceExpression)
    {
        names = [];
        sourceExpression = operand;

        while (true)
            if (sourceExpression is MemberExpression memberExpression)
            {
                if (!IsNullableMember(memberExpression.Member, nameof(Nullable<int>.Value)))
                    names.Add(memberExpression.Member.Name);

                sourceExpression = memberExpression.Expression;
            }
            else if (sourceExpression is MethodCallExpression
                {
                    Method.IsGenericMethod: true,
                    Arguments: [var source, ConstantExpression { Value: string efPropertyName }]
                } methodCall
                && methodCall.Method.GetGenericMethodDefinition() == EfPropertyMethod)
            {
                names.Add(efPropertyName);
                sourceExpression = source;
            }
            else
            {
                break;
            }

        names.Reverse();
        return names.Count > 0;
    }

    /// <summary>
    ///     Walks a root-first member chain and returns the SQL path for the final complex property
    ///     when the chain resolves exclusively through non-collection complex members.
    /// </summary>
    private SqlExpression? TryTranslateComplexPropertyPath(
        List<string> names,
        IEntityType rootEntityType,
        Type resultType)
    {
        SqlExpression? sqlExpression = null;
        ITypeBase currentType = rootEntityType;

        for (var i = 0; i < names.Count; i++)
        {
            if (currentType.FindComplexProperty(names[i]) is not
                {
                    IsCollection: false
                } complexProperty)
                return null;

            var attributeName = complexProperty.GetAttributeName();
            var isLast = i == names.Count - 1;
            var expressionType = isLast ? resultType : complexProperty.ClrType;
            var typeMapping = isLast
                ? new DynamoComplexTypeMapping(expressionType, complexProperty.ComplexType)
                : null;
            sqlExpression = sqlExpression == null
                ? sqlExpressionFactory.ApplyTypeMapping(
                    sqlExpressionFactory.Property(attributeName, expressionType),
                    typeMapping)
                : new DynamoScalarAccessExpression(
                    sqlExpression,
                    attributeName,
                    expressionType,
                    typeMapping);

            currentType = complexProperty.ComplexType;
        }

        return sqlExpression;
    }

    private static bool IsNullableMember(MemberInfo member, string memberName)
        => member.Name == memberName
            && member.DeclaringType is { IsGenericType: true } declaringType
            && declaringType.GetGenericTypeDefinition() == typeof(Nullable<>);

    /// <inheritdoc />
    protected override Expression VisitConstant(ConstantExpression node)
        => sqlExpressionFactory.Constant(node.Value, node.Type);

    /// <inheritdoc />
    protected override Expression VisitNew(NewExpression node)
        => TryEvaluateToConstant(node, out var sqlConstantExpression)
            ? sqlConstantExpression
            : QueryCompilationContext.NotTranslatedExpression;

    /// <inheritdoc />
    protected override Expression VisitNewArray(NewArrayExpression node)
        => TryEvaluateToConstant(node, out var sqlConstantExpression)
            ? sqlConstantExpression
            : QueryCompilationContext.NotTranslatedExpression;

    /// <inheritdoc />
    protected override Expression VisitMemberInit(MemberInitExpression node)
        => TryEvaluateToConstant(node, out var sqlConstantExpression)
            ? sqlConstantExpression
            : QueryCompilationContext.NotTranslatedExpression;

    /// <inheritdoc />
    protected override Expression VisitParameter(ParameterExpression node)
        => QueryCompilationContext.NotTranslatedExpression;

    /// <inheritdoc />
    protected override Expression VisitMember(MemberExpression node)
    {
        if (node is { Member.Name: nameof(string.Length), Expression.Type: var expressionType }
            && expressionType == typeof(string))
        {
            var instance = TranslateInternal(node.Expression);
            return instance is null
                ? QueryCompilationContext.NotTranslatedExpression
                : sqlExpressionFactory.Function("size", [instance], typeof(int));
        }

        if (IsNullableMember(node.Member, nameof(Nullable<int>.HasValue))
            && node.Expression is { } nullableExpression)
        {
            var nullableOperand =
                TryTranslateComplexPropertyForStructuralComparison(nullableExpression)
                ?? TranslateInternal(nullableExpression);

            return nullableOperand is null
                ? QueryCompilationContext.NotTranslatedExpression
                : sqlExpressionFactory.Binary(
                    ExpressionType.AndAlso,
                    sqlExpressionFactory.IsNotNull(nullableOperand),
                    sqlExpressionFactory.IsNotMissing(nullableOperand))!;
        }

        if (!TryGetMemberAccessChain(node, out var names, out var sourceExpression))
        {
            AddTranslationErrorDetails(DynamoStrings.MemberAccessNotSupported);
            return QueryCompilationContext.NotTranslatedExpression;
        }

        // Accept ParameterExpression (WHERE context) or StructuralTypeShaperExpression
        // (SELECT context — EF Core wraps the root entity in a shaper before visiting the lambda).
        IEntityType? rootEntityType = null;
        if (sourceExpression is ParameterExpression pe)
        {
            _lambdaParameterEntityTypes?.TryGetValue(pe, out rootEntityType);
        }
        else if (sourceExpression is StructuralTypeShaperExpression
            {
                StructuralType: IEntityType shaperRootType
            })
        {
            rootEntityType = shaperRootType;
        }
        else
        {
            AddTranslationErrorDetails(DynamoStrings.MemberAccessNotSupported);
            return QueryCompilationContext.NotTranslatedExpression;
        }

        // Fast path: single-level direct property access.
        if (names.Count == 1)
        {
            var memberName = names[0];
            if (rootEntityType?.FindProperty(memberName) is { } directProperty)
            {
                var isPartitionKey = IsEffectivePartitionKey(directProperty, rootEntityType);
                return sqlExpressionFactory.ApplyTypeMapping(
                    sqlExpressionFactory.Property(
                        directProperty.GetAttributeName(),
                        node.Type,
                        isPartitionKey),
                    directProperty.GetTypeMapping());
            }

            // When entity type is known but FindProperty returned null the member is a non-scalar
            // (complex property, or unsupported navigation). Complex property chains go through
            // TranslateNestedMemberChain for multi-segment access; single-segment access is
            // unsupported.
            if (rootEntityType != null)
            {
                AddTranslationErrorDetails(DynamoStrings.MemberAccessNotSupported);
                return QueryCompilationContext.NotTranslatedExpression;
            }

            return sqlExpressionFactory.Property(memberName, node.Type);
        }

        // Multi-level access rooted in a scalar property is CLR member access over the model value
        // (for example, value-converted List<T>.Count), not a DynamoDB document path.
        if (rootEntityType != null)
            return rootEntityType.FindProperty(names[0]) != null
                ? QueryCompilationContext.NotTranslatedExpression
                : TranslateNestedMemberChain(names, rootEntityType, node.Type);

        // Multi-level member access without entity type context is not translatable
        AddTranslationErrorDetails(DynamoStrings.MemberAccessNotSupported);
        return QueryCompilationContext.NotTranslatedExpression;
    }

    /// <summary>
    ///     Translates a multi-segment complex property chain to a nested path expression by walking
    ///     intermediate complex properties and resolving the leaf scalar property through the EF model.
    /// </summary>
    private Expression TranslateNestedMemberChain(
        List<string> names,
        IEntityType rootEntityType,
        Type leafType)
    {
        Expression? sqlExpr = null;
        ITypeBase currentType = rootEntityType;

        for (var i = 0; i < names.Count; i++)
        {
            var memberName = names[i];
            var isLast = i == names.Count - 1;

            if (isLast)
            {
                // Leaf must be a scalar property — whole-complex-type leaf access is not supported
                var property = currentType.FindProperty(memberName);
                if (property == null)
                {
                    AddTranslationErrorDetails(DynamoStrings.MemberAccessNotSupported);
                    return QueryCompilationContext.NotTranslatedExpression;
                }

                var attributeName = property.GetAttributeName();
                var typeMapping = property.GetTypeMapping();
                return sqlExpr == null
                    ? sqlExpressionFactory.ApplyTypeMapping(
                        sqlExpressionFactory.Property(attributeName, leafType),
                        typeMapping)
                    : new DynamoScalarAccessExpression(
                        sqlExpr,
                        attributeName,
                        leafType,
                        typeMapping);
            }

            // Intermediate segment: must be a non-collection complex property
            var complexProperty = currentType.FindComplexProperty(memberName);
            if (complexProperty == null || complexProperty.IsCollection)
            {
                AddTranslationErrorDetails(DynamoStrings.MemberAccessNotSupported);
                return QueryCompilationContext.NotTranslatedExpression;
            }

            var cpAttributeName = ((IReadOnlyComplexProperty)complexProperty).GetAttributeName();
            sqlExpr = sqlExpr == null
                ? new DynamoComplexPropertyAccessExpression(complexProperty)
                : new DynamoScalarAccessExpression(sqlExpr, cpAttributeName, typeof(object));
            currentType = complexProperty.ComplexType;
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
            return TranslateEfPropertyAccess(node, efPropName);

        if (node.TryGetIndexerArguments(model, out var indexerSource, out var indexerPropertyName))
            return TranslatePropertyAccess(indexerSource, indexerPropertyName, node.Type, true);

        if (node.Method == IsNullMethod
            || node.Method == IsNotNullMethod
            || node.Method == IsMissingMethod
            || node.Method == IsNotMissingMethod)
            return TranslateDynamoDbFunctions(node);

        if (TryTranslateEquals(node) is { } equalsExpression)
            return equalsExpression;

        if (node.Method == StringIsNullOrEmptyMethod)
            return TranslateStringIsNullOrEmpty(node);

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

    private Expression? TryTranslateEquals(MethodCallExpression node)
    {
        Expression? leftExpression = null;
        Expression? rightExpression = null;

        if (node.Object != null
            && node.Arguments.Count == 1
            && IsSupportedInstanceEqualsMethod(node.Method))
        {
            leftExpression = node.Object;
            rightExpression = node.Arguments[0];
        }
        else if (node.Object == null
            && node.Arguments.Count == 2
            && node.Method == ObjectStaticEqualsMethod)
        {
            leftExpression = node.Arguments[0];
            rightExpression = node.Arguments[1];
        }

        if (leftExpression == null || rightExpression == null)
            return null;

        leftExpression = UnwrapObjectConvert(leftExpression);
        rightExpression = UnwrapObjectConvert(rightExpression);

        var leftSql = TranslateInternal(leftExpression);
        var rightSql = TranslateInternal(rightExpression);

        // Whole complex-property access does not translate through the scalar member path.
        // Retry untranslated operands as complex map paths first, then bind inline complex
        // object constants to the discovered mapping — mirrors the same fallback in VisitBinary.
        leftSql ??= TryTranslateComplexPropertyForStructuralComparison(leftExpression);
        rightSql ??= TryTranslateComplexPropertyForStructuralComparison(rightExpression);
        if (leftSql is SqlExpression { TypeMapping: DynamoComplexTypeMapping } leftComplex)
            rightSql ??= TryTranslateComplexConstant(rightExpression, leftComplex);
        if (rightSql is SqlExpression { TypeMapping: DynamoComplexTypeMapping } rightComplex)
            leftSql ??= TryTranslateComplexConstant(leftExpression, rightComplex);

        if (leftSql == null || rightSql == null)
            return QueryCompilationContext.NotTranslatedExpression;

        var comparisonTypeMapping = InferTypeMapping(leftSql, rightSql);
        if (!IsSupportedEqualsOperand(leftSql, comparisonTypeMapping)
            || !IsSupportedEqualsOperand(rightSql, comparisonTypeMapping))
            return QueryCompilationContext.NotTranslatedExpression;

        if (TryTranslateEqualsNull(leftSql, rightSql) is { } nullComparison)
            return nullComparison;

        if (!AreEqualsOperandTypesCompatible(leftSql, rightSql, comparisonTypeMapping))
            return sqlExpressionFactory.Constant(false, typeof(bool));

        return sqlExpressionFactory.Binary(ExpressionType.Equal, leftSql, rightSql);
    }

    private SqlExpression? TryTranslateEqualsNull(SqlExpression left, SqlExpression right)
    {
        var leftNull = left is SqlConstantExpression { Value: null };
        var rightNull = right is SqlConstantExpression { Value: null };

        if (!leftNull && !rightNull)
            return null;

        if (leftNull && rightNull)
            return sqlExpressionFactory.Constant(true, typeof(bool));

        var operand = leftNull ? right : left;
        if (operand.TypeMapping?.Converter?.ConvertsNulls == true)
            return null;

        if (!CanBeNull(operand.Type))
            return sqlExpressionFactory.Constant(false, typeof(bool));

        return sqlExpressionFactory.Binary(
            ExpressionType.OrElse,
            sqlExpressionFactory.IsNull(operand),
            sqlExpressionFactory.IsMissing(operand));
    }

    private static bool CanBeNull(Type type)
        => !type.IsValueType || Nullable.GetUnderlyingType(type) != null;

    private bool IsConvertedEnumUnderlyingCastComparedToUnderlyingValue(
        Expression left,
        Expression right)
        => IsConvertedEnumUnderlyingCastComparedToUnderlyingValueFrom(left, right)
            || IsConvertedEnumUnderlyingCastComparedToUnderlyingValueFrom(right, left);

    private bool IsConvertedEnumUnderlyingCastComparedToUnderlyingValueFrom(
        Expression enumCastExpression,
        Expression otherExpression)
    {
        if (enumCastExpression is not UnaryExpression
            {
                NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked
            } unaryExpression)
            return false;

        var enumType = UnwrapNullableType(unaryExpression.Operand.Type);
        var targetType = UnwrapNullableType(unaryExpression.Type);
        if (!enumType.IsEnum || targetType != Enum.GetUnderlyingType(enumType))
            return false;

        if (!TryResolveProperty(unaryExpression.Operand, out var property)
            || property.GetTypeMapping().Converter is null)
            return false;

        return ExpressionHasTypeOrElementType(otherExpression, targetType);
    }

    private bool TryResolveProperty(Expression expression, out IProperty property)
    {
        if (TryGetMemberAccessChain(expression, out var names, out var sourceExpression)
            && names.Count > 0
            && ResolveRootEntityType(sourceExpression)
                ?.FindProperty(names[0]) is { } memberProperty)
        {
            property = memberProperty;
            return true;
        }

        property = null!;
        return false;
    }

    private static bool ExpressionHasTypeOrElementType(Expression expression, Type type)
    {
        if (expression is UnaryExpression
            {
                NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked
            } unaryExpression
            && UnwrapNullableType(unaryExpression.Operand.Type).IsEnum)
            return false;

        expression = UnwrapObjectConvert(expression);
        var expressionType = UnwrapNullableType(expression.Type);
        if (expressionType == type)
            return true;

        if (expressionType.IsArray)
            return UnwrapNullableType(expressionType.GetElementType()!) == type;

        if (expressionType.IsGenericType
            && expressionType.GetGenericArguments().Any(t => UnwrapNullableType(t) == type))
            return true;

        return false;
    }

    private static Expression UnwrapObjectConvert(Expression expression)
    {
        while (expression is UnaryExpression
            {
                NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked,
                Type: var targetType
            } unaryExpression
            && targetType == typeof(object))
            expression = unaryExpression.Operand;

        return expression;
    }

    private static bool IsSupportedInstanceEqualsMethod(MethodInfo method)
    {
        if (method.Name != nameof(object.Equals)
            || method.ReturnType != typeof(bool)
            || method.IsStatic
            || method.GetParameters().Length != 1)
            return false;

        var declaringType = method.DeclaringType;
        return method == ObjectEqualsMethod
            || declaringType == typeof(Enum)
            || (declaringType != null
                && (declaringType.IsValueType || IsSupportedEqualsType(declaringType)));
    }

    private static bool IsSupportedEqualsOperand(
        SqlExpression expression,
        CoreTypeMapping? comparisonTypeMapping)
        => expression is SqlConstantExpression { Value: null }
            || IsSupportedEqualsType(expression.Type)
            || IsCompatibleWithConverterMapping(expression, comparisonTypeMapping)
            || expression.TypeMapping is DynamoComplexTypeMapping;

    private static bool AreEqualsOperandTypesCompatible(
        SqlExpression left,
        SqlExpression right,
        CoreTypeMapping? comparisonTypeMapping)
    {
        if (left is SqlConstantExpression { Value: null }
            || right is SqlConstantExpression { Value: null })
            return true;

        var leftType = UnwrapNullableType(left.Type);
        var rightType = UnwrapNullableType(right.Type);

        return leftType == rightType
            || (IsCompatibleWithConverterMapping(left, comparisonTypeMapping)
                && IsCompatibleWithConverterMapping(right, comparisonTypeMapping));
    }

    private static bool IsCompatibleWithConverterMapping(
        SqlExpression expression,
        CoreTypeMapping? typeMapping)
    {
        if (typeMapping is not DynamoTypeMapping { Converter: not null } dynamoTypeMapping)
            return false;

        var expressionType = UnwrapNullableType(expression.Type);
        var modelType = UnwrapNullableType(dynamoTypeMapping.ClrType);

        // EF's FindAsync routes non-trivial struct keys through EF.Property<object>, which
        // produces an object-typed SqlParameterExpression at translation time. Mirror EF Core's
        // GenerateEqualExpression condition (ExpressionExtensions.BuildPredicate): allow the
        // object-typed passthrough only for value types that are not bool, numeric, or enum —
        // those types use a typed Expression.Equal path and must not be broadened here.
        var objectTypedParameterAllowed = expression is SqlParameterExpression
            && expressionType == typeof(object)
            && !modelType.IsEnum
            && modelType != typeof(bool)
            && !DynamoWireValueConversion.IsNumericType(modelType);

        return modelType.IsValueType
            && (expressionType == modelType || objectTypedParameterAllowed);
    }

    private static bool IsSupportedEqualsType(Type type)
    {
        type = UnwrapNullableType(type);
        return type.IsEnum
            || type.IsPrimitive
            || type == typeof(string)
            || type == typeof(decimal)
            || type == typeof(Guid)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(DateOnly)
            || type == typeof(TimeOnly)
            || type == typeof(TimeSpan);
    }

    private static Type UnwrapNullableType(Type type) => Nullable.GetUnderlyingType(type) ?? type;

    private static CoreTypeMapping? InferTypeMapping(SqlExpression left, SqlExpression right)
        => PreferNonValueMapping(left)
            ?? PreferNonValueMapping(right) ?? left.TypeMapping ?? right.TypeMapping;

    private static CoreTypeMapping? PreferNonValueMapping(SqlExpression expression)
        => expression is not SqlConstantExpression and not SqlParameterExpression
            ? expression.TypeMapping
            : null;

    private bool TryEvaluateToConstant(
        Expression expression,
        out SqlConstantExpression sqlConstantExpression)
    {
        if (!CanEvaluate(expression))
        {
            sqlConstantExpression = null!;
            return false;
        }

        try
        {
            var value = Expression
                .Lambda<Func<object?>>(Expression.Convert(expression, typeof(object)))
                .Compile(true)
                .Invoke();
            sqlConstantExpression = sqlExpressionFactory.Constant(value, expression.Type);
            return true;
        }
        catch
        {
            sqlConstantExpression = null!;
            return false;
        }
    }

    private static bool CanEvaluate(Expression expression)
        => expression switch
        {
            ConstantExpression => true,
            NewExpression newExpression => newExpression.Arguments.All(CanEvaluate),
            NewArrayExpression newArrayExpression =>
                newArrayExpression.Expressions.All(CanEvaluate),
            MemberInitExpression memberInitExpression => CanEvaluate(
                    memberInitExpression.NewExpression)
                && memberInitExpression.Bindings.All(CanEvaluate),
            UnaryExpression
            {
                NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked
            } unaryExpression => CanEvaluate(unaryExpression.Operand),
            _ => false
        };

    private static bool CanEvaluate(MemberBinding memberBinding)
        => memberBinding switch
        {
            MemberAssignment memberAssignment => CanEvaluate(memberAssignment.Expression),
            _ => false
        };

    /// <summary>
    ///     Translates <c>EF.Property&lt;T&gt;(source, "name")</c> to scalar property access,
    ///     supporting nested chains that represent owned-navigation paths.
    /// </summary>
    private Expression TranslateEfPropertyAccess(MethodCallExpression node, string propertyName)
        => TranslatePropertyAccess(node.Arguments[0], propertyName, node.Type);

    private Expression TranslatePropertyAccess(
        Expression source,
        string propertyName,
        Type resultType,
        bool requireMappedProperty = false)
    {
        var names = new List<string> { propertyName };
        var current = source;
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
                    var isPartitionKey = IsEffectivePartitionKey(rootProperty, rootEntityType);
                    var propertyExpressionType = resultType == typeof(object)
                        ? rootProperty.ClrType
                        : resultType;
                    return sqlExpressionFactory.ApplyTypeMapping(
                        sqlExpressionFactory.Property(
                            rootProperty.GetAttributeName(),
                            propertyExpressionType,
                            isPartitionKey),
                        rootProperty.GetTypeMapping());
                }

                AddTranslationErrorDetails(DynamoStrings.MemberAccessNotSupported);
                return QueryCompilationContext.NotTranslatedExpression;
            }

            return TranslateNestedMemberChain(names, rootEntityType, resultType);
        }

        if (requireMappedProperty)
        {
            AddTranslationErrorDetails(DynamoStrings.MemberAccessNotSupported);
            return QueryCompilationContext.NotTranslatedExpression;
        }

        var translatedSource = Visit(source);
        if (translatedSource is SqlExpression sqlSource)
            return new DynamoScalarAccessExpression(sqlSource, propertyName, resultType);

        return sqlExpressionFactory.Property(propertyName, resultType);
    }

    /// <summary>
    ///     Returns true when <paramref name="property" /> is the effective
    ///     partition key for the current query — either the table partition key, or the partition key of
    ///     the active secondary index when <c>.WithIndex()</c> has been applied.
    /// </summary>
    /// <remarks>
    ///     The distinction matters for IN-predicate value-count limits: DynamoDB allows up to 100
    ///     values for non-key comparisons but only 50 for partition-key comparisons (table or GSI).
    /// </remarks>
    private bool IsEffectivePartitionKey(IReadOnlyProperty property, IReadOnlyEntityType entityType)
    {
        // When a secondary index is active, only that index's partition key is effective.
        if (queryCompilationContext?.ExplicitIndexName is not { } indexName)
        {
            var keyEntityType = entityType.ResolveKeyMappedEntityType();
            return keyEntityType.GetPartitionKeyProperty()?.Name == property.Name;
        }

        var runtimeModel = queryCompilationContext.Model.GetDynamoRuntimeTableModel();
        if (runtimeModel is null)
            return false;

        var tableGroupName = entityType.GetTableGroupName();
        if (!runtimeModel.Tables.TryGetValue(tableGroupName, out var tableDescriptor))
            return false;

        if (!tableDescriptor.SourcesByQueryEntityTypeName.TryGetValue(
            entityType.Name,
            out var sources))
            return false;

        return sources.Any(d
            => d.IndexName == indexName && d.PartitionKeyProperty.Name == property.Name);
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
            StructuralType: IEntityType shaperType
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
            {
                if (sqlOperand.Type == node.Type)
                    return sqlOperand;

                if (IsImplicitClrConvert(node.Operand.Type, node.Type))
                    return sqlOperand;

                return sqlExpressionFactory.ApplyTypeMapping(sqlOperand, node.Type);
            }

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

    private static bool IsImplicitClrConvert(Type sourceType, Type targetType)
    {
        if (sourceType == targetType)
            return true;

        var sourceUnderlyingType = Nullable.GetUnderlyingType(sourceType) ?? sourceType;
        var targetUnderlyingType = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var sourceNullable = sourceType != sourceUnderlyingType;
        var targetNullable = targetType != targetUnderlyingType;

        if (sourceNullable != targetNullable)
            return false;

        if (sourceUnderlyingType.IsEnum)
        {
            var enumUnderlyingType = Enum.GetUnderlyingType(sourceUnderlyingType);
            return enumUnderlyingType == targetUnderlyingType
                || IsImplicitClrConvert(enumUnderlyingType, targetUnderlyingType);
        }

        return targetUnderlyingType == typeof(int)
            && sourceUnderlyingType is { } source
            && (source == typeof(byte)
                || source == typeof(sbyte)
                || source == typeof(short)
                || source == typeof(ushort)
                || source == typeof(char));
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
            _ => QueryCompilationContext.NotTranslatedExpression
        };
    }

    /// <summary>
    ///     Translates supported Dynamo lexical string comparison shapes to SQL binary comparisons.
    ///     Supported forms compare <c>string.Compare(a, b)</c> or <c>a.CompareTo(b)</c> to -1, 0, or 1.
    /// </summary>
    private Expression? TryTranslateStringCompare(BinaryExpression node)
    {
        SqlExpression? a = null;
        SqlExpression? b = null;
        var opType = node.NodeType;
        int? comparand = null;
        var swapOperands = false;

        if (node.Left is MethodCallExpression leftCall
            && TryTranslateSupportedStringComparisonOperands(leftCall, out a, out b)
            && node.Right is ConstantExpression { Value: int rightValue })
        {
            comparand = rightValue;
        }
        else if (node.Right is MethodCallExpression rightCall
            && TryTranslateSupportedStringComparisonOperands(rightCall, out a, out b)
            && node.Left is ConstantExpression { Value: int leftValue })
        {
            comparand = leftValue;
            swapOperands = true;
        }

        if (a is null || b is null || comparand is null)
            return null;

        var effectiveOp = swapOperands ? FlipComparison(opType) : opType;
        var binaryOp = TryMapStringCompareOperator(effectiveOp, comparand.Value);
        if (binaryOp is null)
            return null;

        var result = sqlExpressionFactory.Binary(binaryOp.Value, a, b);
        if (result is not null)
            return result;

        AddTranslationErrorDetails(DynamoStrings.UnsupportedBinaryOperator(binaryOp.Value));
        return QueryCompilationContext.NotTranslatedExpression;
    }

    private static ExpressionType? TryMapStringCompareOperator(ExpressionType op, int comparand)
        => comparand switch
        {
            0 => op,
            1 => op switch
            {
                ExpressionType.Equal or ExpressionType.GreaterThanOrEqual => ExpressionType
                    .GreaterThan,
                ExpressionType.NotEqual or ExpressionType.LessThan =>
                    ExpressionType.LessThanOrEqual,
                _ => null
            },
            -1 => op switch
            {
                ExpressionType.Equal or ExpressionType.LessThanOrEqual => ExpressionType.LessThan,
                ExpressionType.NotEqual or ExpressionType.GreaterThan => ExpressionType
                    .GreaterThanOrEqual,
                _ => null
            },
            _ => null
        };

    /// <summary>
    ///     Translates the string operands for supported compare calls used in Dynamo lexical
    ///     comparisons.
    /// </summary>
    private bool TryTranslateSupportedStringComparisonOperands(
        MethodCallExpression methodCall,
        out SqlExpression? left,
        out SqlExpression? right)
    {
        left = null;
        right = null;

        if (methodCall.Method == StringCompareMethod)
        {
            left = TranslateInternal(methodCall.Arguments[0]);
            right = TranslateInternal(methodCall.Arguments[1]);
            return left is not null && right is not null;
        }

        if (methodCall.Method == StringCompareToMethod)
        {
            left = TranslateInternal(methodCall.Object!);
            right = TranslateInternal(methodCall.Arguments[0]);
            return left is not null && right is not null;
        }

        return false;
    }

    /// <summary>Returns the mirrored comparison operator (e.g. <c>&gt;=</c> becomes <c>&lt;=</c>).</summary>
    private static ExpressionType FlipComparison(ExpressionType op)
        => op switch
        {
            ExpressionType.GreaterThan => ExpressionType.LessThan,
            ExpressionType.GreaterThanOrEqual => ExpressionType.LessThanOrEqual,
            ExpressionType.LessThan => ExpressionType.GreaterThan,
            ExpressionType.LessThanOrEqual => ExpressionType.GreaterThanOrEqual,
            _ => op
        };

    /// <summary>Translates string.IsNullOrEmpty to null, missing, or empty-string checks.</summary>
    private Expression TranslateStringIsNullOrEmpty(MethodCallExpression node)
    {
        if (TranslateInternal(node.Arguments[0]) is not SqlExpression argument)
            return QueryCompilationContext.NotTranslatedExpression;

        return sqlExpressionFactory.Binary(
            ExpressionType.OrElse,
            sqlExpressionFactory.Binary(
                ExpressionType.OrElse,
                sqlExpressionFactory.IsNull(argument),
                sqlExpressionFactory.IsMissing(argument))!,
            sqlExpressionFactory.Binary(
                ExpressionType.Equal,
                argument,
                sqlExpressionFactory.Constant(string.Empty, typeof(string)))!)!;
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
    ///     is a complex collection property.
    /// </summary>
    private Expression TranslateElementAt(MethodCallExpression node)
    {
        // Strip the .AsQueryable() wrapper that EF Core adds around complex collection sources.
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
        collectionExpression = StripAsQueryable(collectionExpression);
        if (collectionExpression == null || itemExpression == null)
        {
            AddTranslationErrorDetails(DynamoStrings.ContainsCollectionShapeNotSupported);
            return QueryCompilationContext.NotTranslatedExpression;
        }

        if (IsConvertedEnumUnderlyingCastComparedToUnderlyingValue(
            itemExpression,
            collectionExpression))
            throw new InvalidOperationException(
                DynamoStrings.ConvertedEnumUnderlyingCastNotSupported);

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
        if (translatedCollection is SqlExpression collectionSqlExpression
            && TryGetNativePrimitiveCollectionElementMapping(
                collectionSqlExpression,
                out var elementMapping))
        {
            var mappedItem = sqlExpressionFactory.ApplyTypeMapping(item, elementMapping);
            return sqlExpressionFactory.Function(
                "contains",
                [collectionSqlExpression, mappedItem],
                typeof(bool));
        }

        if (translatedCollection is SqlParameterExpression valuesParameter)
            return sqlExpressionFactory.In(item, valuesParameter, isPartitionKeyComparison);

        if (TryTranslateInlineValues(collectionExpression, item.Type, out var values))
            return sqlExpressionFactory.In(item, values, isPartitionKeyComparison);

        AddTranslationErrorDetails(DynamoStrings.ContainsCollectionShapeNotSupported);
        return QueryCompilationContext.NotTranslatedExpression;
    }

    /// <summary>Strips EF's primitive-collection queryable wrapper when present.</summary>
    private static Expression? StripAsQueryable(Expression? expression)
        => expression is MethodCallExpression
        {
            Method.Name: nameof(Queryable.AsQueryable)
        } asQueryable
            ? asQueryable.Arguments[0]
            : expression;

    /// <summary>
    ///     Returns the element mapping when the expression is a natively mapped DynamoDB primitive
    ///     list/set. Scalar value-converted collections intentionally do not qualify.
    /// </summary>
    private static bool TryGetNativePrimitiveCollectionElementMapping(
        SqlExpression collectionExpression,
        out CoreTypeMapping elementMapping)
    {
        elementMapping = null!;

        if (collectionExpression is not SqlPropertyExpression and not DynamoScalarAccessExpression)
            return false;

        if (!DynamoTypeMappingSource.TryGetListElementType(collectionExpression.Type, out _)
            && !DynamoTypeMappingSource.TryGetSetElementType(collectionExpression.Type, out _))
            return false;

        if (collectionExpression.TypeMapping?.ElementTypeMapping is not { } mapping)
            return false;

        elementMapping = mapping;
        return true;
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

        if (method.IsGenericMethod
            && method.GetGenericMethodDefinition() == QueryableContainsMethod)
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
