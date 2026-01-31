using System.Linq.Expressions;
using System.Text;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Helper class for printing expression trees for debugging.
/// </summary>
public class ExpressionPrinter : ExpressionVisitor
{
    private readonly StringBuilder _builder = new();
    private int _indent;

    public string Print(Expression expression)
    {
        _builder.Clear();
        _indent = 0;
        Visit(expression);
        return _builder.ToString();
    }

    public void Append(string value) => _builder.Append(value);

    public void AppendLine(string value = "")
    {
        _builder.AppendLine(value);
        _builder.Append(new string(' ', _indent * 2));
    }

    public void IncreaseIndent() => _indent++;

    public void DecreaseIndent() => _indent--;

    protected override Expression VisitExtension(Expression node)
    {
        if (node is SqlExpression sqlExpression)
        {
            sqlExpression.Print(this);
            return node;
        }

        return base.VisitExtension(node);
    }
}
