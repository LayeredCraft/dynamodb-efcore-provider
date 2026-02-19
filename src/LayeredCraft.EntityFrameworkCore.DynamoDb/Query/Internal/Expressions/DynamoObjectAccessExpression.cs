using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>
///     Represents access to an embedded owned reference attribute in a DynamoDB projection.
///     Carries the <see cref="INavigation" /> so the projection-binding removal visitor can
///     materialise the owned reference without a model-wide navigation scan.
/// </summary>
/// <remarks>
///     Analogous to Cosmos's <c>ObjectAccessExpression</c>. By preserving the navigation at
///     translation time, ambiguity is eliminated when two entity types share the same owned navigation
///     name and CLR type.
/// </remarks>
public sealed class DynamoObjectAccessExpression(INavigation navigation) : SqlExpression(
    navigation.ClrType,
    null)
{
    /// <summary>The owned reference navigation this expression accesses.</summary>
    public INavigation Navigation { get; } = navigation;

    /// <summary>The DynamoDB attribute name used to store the owned reference map.</summary>
    public string PropertyName
        => Navigation.TargetEntityType.GetContainingAttributeName() ?? Navigation.Name;

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor) => this;

    /// <inheritdoc />
    protected override SqlExpression WithTypeMapping(CoreTypeMapping? typeMapping) => this;

    /// <inheritdoc />
    public override void Print(ExpressionPrinter expressionPrinter)
        => expressionPrinter.Append(PropertyName);

    /// <inheritdoc />
    protected override bool Equals(SqlExpression? other)
        => other is DynamoObjectAccessExpression o
            && Navigation.Name == o.Navigation.Name
            && Navigation.DeclaringEntityType.ClrType == o.Navigation.DeclaringEntityType.ClrType;

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(Navigation.Name, Navigation.DeclaringEntityType.ClrType);
}
