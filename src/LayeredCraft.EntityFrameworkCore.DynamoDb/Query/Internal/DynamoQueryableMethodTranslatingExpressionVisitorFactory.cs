using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

public class DynamoQueryableMethodTranslatingExpressionVisitorFactory(
    QueryableMethodTranslatingExpressionVisitorDependencies dependencies
) : IQueryableMethodTranslatingExpressionVisitorFactory
{
    public QueryableMethodTranslatingExpressionVisitor Create(
        QueryCompilationContext queryCompilationContext
    ) =>
        new DynamoQueryableMethodTranslatingExpressionVisitor(
            dependencies,
            queryCompilationContext
        );
}
