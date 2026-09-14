using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>
///     Represents an <c>ExecuteDelete</c> operation over a base-table item fully identified by
///     its primary key, producing a 0/1 affected-item count.
/// </summary>
public sealed class DynamoDeleteExpression(
    SelectExpression selectExpression,
    IEntityType entityType) : Expression, IPrintableExpression
{
    /// <inheritdoc />
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <inheritdoc />
    public override Type Type { get; } = typeof(int);

    /// <summary>The underlying select expression carrying the WHERE predicate.</summary>
    public SelectExpression SelectExpression { get; } = selectExpression;

    /// <summary>The entity type being deleted.</summary>
    public IEntityType EntityType { get; } = entityType;

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
    {
        var selectExpression = (SelectExpression)visitor.Visit(SelectExpression);
        return selectExpression != SelectExpression
            ? new DynamoDeleteExpression(selectExpression, EntityType)
            : this;
    }

    /// <inheritdoc />
    public void Print(Microsoft.EntityFrameworkCore.Query.ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.AppendLine($"DynamoDeleteExpression ({EntityType.DisplayName()}):");
        expressionPrinter.Append("SelectExpression: ");
        expressionPrinter.Visit(SelectExpression);
    }
}
