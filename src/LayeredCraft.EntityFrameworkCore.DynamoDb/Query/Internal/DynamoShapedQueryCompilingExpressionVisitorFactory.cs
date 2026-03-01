using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

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
