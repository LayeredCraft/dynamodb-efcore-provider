using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Query.Internal;
using static System.Linq.Expressions.Expression;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
///     Rewrites EF Core primitive-collection materialization calls so non-list targets can be
///     populated safely.
/// </summary>
/// <remarks>
///     EF Core emits calls to <c>PopulateList&lt;T&gt;</c> during primitive collection
///     materialization. That works for <see cref="IList{T}" /> targets, but set and dictionary-backed
///     targets typically only expose <see cref="ICollection{T}" /> semantics. This visitor swaps in a
///     population method based on <see cref="ICollection{T}" /> when needed.
/// </remarks>
internal sealed class DynamoPrimitiveCollectionMaterializationFixupExpressionVisitor
    : ExpressionVisitor
{
    private static readonly MethodInfo PopulateCollectionMethodInfo =
        typeof(DynamoPrimitiveCollectionMaterializationFixupExpressionVisitor).GetMethod(
            nameof(PopulateCollection),
            BindingFlags.NonPublic | BindingFlags.Static)!;

#pragma warning disable EF1001
    private static readonly MethodInfo EfPopulateListMethodDefinition =
        StructuralTypeMaterializerSource.PopulateListMethod;
#pragma warning restore EF1001

    /// <summary>
    ///     Rewrites matching EF Core <c>PopulateList&lt;T&gt;</c> calls to
    ///     <see cref="PopulateCollection{T}" /> when the target is collection-shaped but not list-shaped.
    /// </summary>
    /// <param name="node">The method-call expression to visit.</param>
    /// <returns>
    ///     The original call when no rewrite is needed; otherwise a call that populates via
    ///     <see cref="ICollection{T}" /> semantics.
    /// </returns>
    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        var visitedNode = (MethodCallExpression)base.VisitMethodCall(node);
        if (visitedNode.Arguments.Count < 2)
            return visitedNode;

        var targetExpression = UnwrapConvert(visitedNode.Arguments[1]);

        if (IsIListType(targetExpression.Type))
            // List targets already follow EF Core's PopulateList path.
            return visitedNode;

        if (!TryGetPopulateListElementType(visitedNode.Method, out var elementType))
            return visitedNode;

        var listType = typeof(IList<>).MakeGenericType(elementType);
        if (listType.IsAssignableFrom(targetExpression.Type))
            // Keep EF Core's original PopulateList path for true list targets.
            return visitedNode;

        var collectionType = typeof(ICollection<>).MakeGenericType(elementType);
        if (!collectionType.IsAssignableFrom(targetExpression.Type))
            return visitedNode;

        return Call(
            PopulateCollectionMethodInfo.MakeGenericMethod(elementType),
            visitedNode.Arguments[0],
            Convert(targetExpression, collectionType));
    }

    /// <summary>
    ///     Replaces all items in <paramref name="target" /> with values from
    ///     <paramref name="buffer" />.
    /// </summary>
    /// <typeparam name="T">The collection element type.</typeparam>
    /// <param name="buffer">The source values to copy from.</param>
    /// <param name="target">The destination collection to clear and repopulate.</param>
    /// <returns>The same <paramref name="target" /> instance after population.</returns>
    private static ICollection<T> PopulateCollection<T>(
        IEnumerable<T> buffer,
        ICollection<T> target)
    {
        // Match EF's populate behavior by replacing current contents.
        target.Clear();
        foreach (var value in buffer)
            target.Add(value);

        return target;
    }

    /// <summary>
    ///     Determines whether the method is EF Core's primitive-collection
    ///     <c>PopulateList&lt;T&gt;</c> helper and extracts its element type.
    /// </summary>
    /// <param name="method">The method to inspect.</param>
    /// <param name="elementType">The extracted generic element type when the method matches.</param>
    /// <returns><see langword="true" /> when the method matches; otherwise <see langword="false" />.</returns>
    private static bool TryGetPopulateListElementType(MethodInfo method, out Type elementType)
    {
        elementType = null!;
        if (!method.IsGenericMethod)
            return false;

        var genericMethodDefinition = method.GetGenericMethodDefinition();
        if (!ReferenceEquals(genericMethodDefinition, EfPopulateListMethodDefinition))
            return false;

        var genericArguments = method.GetGenericArguments();
        if (genericArguments.Length != 1)
            return false;

        elementType = genericArguments[0];
        return true;
    }

    /// <summary>
    ///     Removes outer convert nodes so assignability checks can evaluate the underlying target
    ///     type.
    /// </summary>
    /// <param name="expression">The expression that may be wrapped in convert nodes.</param>
    /// <returns>The inner expression with leading convert wrappers removed.</returns>
    private static Expression UnwrapConvert(Expression expression)
    {
        while (expression is UnaryExpression
            {
                NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked,
            } unaryExpression)
            expression = unaryExpression.Operand;

        return expression;
    }

    /// <summary>
    ///     Determines whether <paramref name="clrType" /> is list-shaped for materialization
    ///     purposes.
    /// </summary>
    /// <param name="clrType">The CLR type to inspect.</param>
    /// <returns><see langword="true" /> when the type is an array or implements <see cref="IList{T}" />.</returns>
    private static bool IsIListType(Type clrType)
    {
        if (clrType.IsArray)
            return true;

        return GetIListInterface(clrType) is not null;
    }

    /// <summary>
    ///     Finds the closed <see cref="IList{T}" /> interface implemented by
    ///     <paramref name="clrType" />.
    /// </summary>
    /// <param name="clrType">The CLR type to inspect.</param>
    /// <returns>The implemented <see cref="IList{T}" /> interface; otherwise <see langword="null" />.</returns>
    private static Type? GetIListInterface(Type clrType)
    {
        if (clrType.IsGenericType && clrType.GetGenericTypeDefinition() == typeof(IList<>))
            return clrType;

        foreach (var implementedInterface in clrType.GetInterfaces())
            if (implementedInterface.IsGenericType
                && implementedInterface.GetGenericTypeDefinition() == typeof(IList<>))
                return implementedInterface;

        return null;
    }
}
