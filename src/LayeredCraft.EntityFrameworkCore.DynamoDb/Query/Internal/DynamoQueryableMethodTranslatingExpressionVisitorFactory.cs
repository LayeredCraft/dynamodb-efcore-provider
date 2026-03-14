using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>Represents the DynamoQueryableMethodTranslatingExpressionVisitorFactory type.</summary>
public sealed class DynamoQueryableMethodTranslatingExpressionVisitorFactory(
    QueryableMethodTranslatingExpressionVisitorDependencies dependencies,
    ISqlExpressionFactory sqlExpressionFactory)
    : IQueryableMethodTranslatingExpressionVisitorFactory
{
    /// <summary>Provides functionality for this member.</summary>
    public QueryableMethodTranslatingExpressionVisitor Create(
        QueryCompilationContext queryCompilationContext)
        => new DynamoQueryableMethodTranslatingExpressionVisitor(
            dependencies,
            queryCompilationContext,
            sqlExpressionFactory);
}
