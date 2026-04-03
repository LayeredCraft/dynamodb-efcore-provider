using System.Linq.Expressions;

namespace EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

/// <summary>A single attribute name/value pair in a PartiQL INSERT VALUE document literal.</summary>
/// <param name="AttributeName">The DynamoDB attribute name (e.g. <c>Pk</c>, <c>$type</c>).</param>
/// <param name="Value">The value expression — typically a <see cref="SqlParameterExpression" />.</param>
public sealed record InsertFieldExpression(string AttributeName, SqlExpression Value);

/// <summary>
///     Models a complete PartiQL INSERT statement:
///     <c>INSERT INTO "TableName" VALUE {'Key': ?, ...}</c>
/// </summary>
/// <remarks>
///     This is a write-side statement node. It is NOT a <see cref="SqlExpression" /> subclass
///     because it is statement-level, not value-producing. It does not participate in the
///     <see cref="SqlExpressionVisitor" /> dispatch table — write-side generation uses
///     <see cref="EntityFrameworkCore.DynamoDb.Update.Internal.DynamoWriteSqlGenerator" /> directly.
/// </remarks>
public sealed class InsertExpression : Expression
{
    /// <summary>Initializes an INSERT expression for the given table and field assignments.</summary>
    /// <param name="tableName">The DynamoDB table name (unquoted).</param>
    /// <param name="fields">Ordered list of attribute name/value pairs for the VALUE document.</param>
    public InsertExpression(string tableName, IReadOnlyList<InsertFieldExpression> fields)
    {
        TableName = tableName;
        Fields = fields;
    }

    /// <summary>The DynamoDB table name, unquoted.</summary>
    public string TableName { get; }

    /// <summary>Ordered list of attribute name/value expression pairs for the VALUE document literal.</summary>
    public IReadOnlyList<InsertFieldExpression> Fields { get; }

    /// <inheritdoc />
    public override ExpressionType NodeType => ExpressionType.Extension;

    /// <inheritdoc />
    public override Type Type => typeof(void);

    /// <inheritdoc />
    protected override Expression VisitChildren(ExpressionVisitor visitor) => this;

    /// <inheritdoc />
    public override string ToString() => $"INSERT INTO \"{TableName}\" VALUE {{…}}";
}
