using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

public sealed class DynamoQueryableMethodTranslatingExpressionVisitorFactory(
    QueryableMethodTranslatingExpressionVisitorDependencies dependencies)
    : IQueryableMethodTranslatingExpressionVisitorFactory
{
    private QueryableMethodTranslatingExpressionVisitorDependencies Dependencies { get; } =
        dependencies;

    public QueryableMethodTranslatingExpressionVisitor Create(
        QueryCompilationContext queryCompilationContext)
        => new DynamoQueryableMethodTranslatingExpressionVisitor(
            Dependencies,
            queryCompilationContext);
}
