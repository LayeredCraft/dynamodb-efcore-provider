using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>Represents a shaped embedded collection projection with an element inner shaper.</summary>
/// <remarks>
///     A provider-specific alternative to EF Core's <c>CollectionShaperExpression</c>. The custom
///     type prevents shared EF Core pipeline visitors from pattern-matching on it, keeping collection
///     shaping isolated to DynamoDB-specific visitors.
/// </remarks>
public sealed class DynamoCollectionShaperExpression : Expression, IPrintableExpression
{
    /// <summary>Creates a collection shaper for an embedded owned collection projection.</summary>
    public DynamoCollectionShaperExpression(
        Expression projection,
        Expression innerShaper,
        INavigationBase? navigation,
        Type elementType)
    {
        Projection = projection;
        InnerShaper = innerShaper;
        Navigation = navigation;
        ElementType = elementType;
    }

    /// <inheritdoc />
    public override Type Type => Navigation?.ClrType ?? typeof(List<>).MakeGenericType(ElementType);

    /// <inheritdoc />
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <summary>The projected embedded array access expression.</summary>
    public Expression Projection { get; }

    /// <summary>The element structural type shaper.</summary>
    public Expression InnerShaper { get; }

    /// <summary>The navigation represented by this collection shaper.</summary>
    public INavigationBase? Navigation { get; }

    /// <summary>The CLR element type of the collection.</summary>
    public Type ElementType { get; }

    /// <summary>Creates a new collection shaper expression with updated children.</summary>
    public DynamoCollectionShaperExpression Update(Expression projection, Expression innerShaper)
        => projection != Projection || innerShaper != InnerShaper
            ? new DynamoCollectionShaperExpression(projection, innerShaper, Navigation, ElementType)
            : this;

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
        => Update(visitor.Visit(Projection), visitor.Visit(InnerShaper));

    /// <inheritdoc />
    public void Print(Microsoft.EntityFrameworkCore.Query.ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.AppendLine("DynamoCollectionShaperExpression:");
        expressionPrinter.Append("Projection: ");
        expressionPrinter.Visit(Projection);
        expressionPrinter.AppendLine();
        expressionPrinter.Append("InnerShaper: ");
        expressionPrinter.Visit(InnerShaper);
        if (Navigation != null)
        {
            expressionPrinter.AppendLine();
            expressionPrinter.Append("Navigation: ");
            expressionPrinter.Append(Navigation.Name);
        }
    }
}
