using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>Represents the DynamoShapedQueryCompilingExpressionVisitorFactory type.</summary>
public class DynamoShapedQueryCompilingExpressionVisitorFactory(
    ShapedQueryCompilingExpressionVisitorDependencies dependencies,
    IDynamoQuerySqlGeneratorFactory sqlGeneratorFactory)
    : IShapedQueryCompilingExpressionVisitorFactory
{
    /// <summary>Creates a shaped query compiling visitor for DynamoDB queries.</summary>
    public ShapedQueryCompilingExpressionVisitor Create(
        QueryCompilationContext queryCompilationContext)
        => new DynamoShapedQueryCompilingExpressionVisitor(
            dependencies,
            (DynamoQueryCompilationContext)queryCompilationContext,
            sqlGeneratorFactory);
}
