using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>
///     Represents a provider-specific paging shaper marker for <c>ToPageAsync</c>.
/// </summary>
/// <remarks>
///     Carries the inner item shaper plus the runtime paging arguments needed to construct a
///     <c>DynamoPage&lt;T&gt;</c> during query compilation/execution.
/// </remarks>
public sealed class DynamoPagingExpression : Expression, IPrintableExpression
{
    /// <summary>Creates a new paging expression marker.</summary>
    public DynamoPagingExpression(
        Expression innerShaper,
        Expression limitExpression,
        Expression? nextTokenExpression,
        Type type)
    {
        InnerShaper = innerShaper;
        LimitExpression = limitExpression;
        NextTokenExpression = nextTokenExpression;
        Type = type;
    }

    /// <inheritdoc />
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <inheritdoc />
    public override Type Type { get; }

    /// <summary>The underlying item shaper expression.</summary>
    public Expression InnerShaper { get; }

    /// <summary>The runtime limit expression for the page request.</summary>
    public Expression LimitExpression { get; }

    /// <summary>The optional runtime next-token expression for the page request.</summary>
    public Expression? NextTokenExpression { get; }

    /// <summary>Creates an updated paging expression when children changed.</summary>
    public DynamoPagingExpression Update(
        Expression innerShaper,
        Expression limitExpression,
        Expression? nextTokenExpression)
        => innerShaper != InnerShaper
            || limitExpression != LimitExpression
            || nextTokenExpression != NextTokenExpression
                ? new DynamoPagingExpression(
                    innerShaper,
                    limitExpression,
                    nextTokenExpression,
                    Type)
                : this;

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor)
        => Update(
            visitor.Visit(InnerShaper),
            visitor.Visit(LimitExpression),
            NextTokenExpression is null ? null : visitor.Visit(NextTokenExpression));

    /// <inheritdoc />
    public void Print(Microsoft.EntityFrameworkCore.Query.ExpressionPrinter expressionPrinter)
    {
        expressionPrinter.AppendLine("DynamoPagingExpression:");
        expressionPrinter.Append("InnerShaper: ");
        expressionPrinter.Visit(InnerShaper);
        expressionPrinter.AppendLine();
        expressionPrinter.Append("Limit: ");
        expressionPrinter.Visit(LimitExpression);
        expressionPrinter.AppendLine();
        expressionPrinter.Append("NextToken: ");
        if (NextTokenExpression is null)
            expressionPrinter.Append("null");
        else
            expressionPrinter.Visit(NextTokenExpression);
    }
}
