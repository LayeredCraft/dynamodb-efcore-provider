using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

public class DynamoShapedQueryCompilingExpressionVisitor(
    ShapedQueryCompilingExpressionVisitorDependencies dependencies,
    DynamoQueryCompilationContext dynamoQueryCompilationContext
) : ShapedQueryCompilingExpressionVisitor(dependencies, dynamoQueryCompilationContext)
{
    protected override Expression VisitShapedQuery(ShapedQueryExpression shapedQueryExpression)
    {
        throw new NotImplementedException();
    }
}
