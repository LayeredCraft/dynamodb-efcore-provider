using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

public class DynamoShapedQueryCompilingExpressionVisitorFactory(
    ShapedQueryCompilingExpressionVisitorDependencies dependencies,
    DynamoQuerySqlGenerator sqlGenerator) : IShapedQueryCompilingExpressionVisitorFactory
{
    public ShapedQueryCompilingExpressionVisitor Create(
        QueryCompilationContext queryCompilationContext)
        => new DynamoShapedQueryCompilingExpressionVisitor(
            dependencies,
            (DynamoQueryCompilationContext)queryCompilationContext,
            sqlGenerator);
}
