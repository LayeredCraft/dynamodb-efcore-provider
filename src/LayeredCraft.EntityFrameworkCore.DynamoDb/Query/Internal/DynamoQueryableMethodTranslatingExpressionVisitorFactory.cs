using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

public sealed class DynamoQueryableMethodTranslatingExpressionVisitorFactory(
    QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
    ISqlExpressionFactory sqlExpressionFactory)
    : IQueryableMethodTranslatingExpressionVisitorFactory
{
    public QueryableMethodTranslatingExpressionVisitor Create(
        QueryCompilationContext queryCompilationContext)
        => new DynamoQueryableMethodTranslatingExpressionVisitor(
            dependencies,
            queryCompilationContext,
            sqlExpressionFactory);
}
