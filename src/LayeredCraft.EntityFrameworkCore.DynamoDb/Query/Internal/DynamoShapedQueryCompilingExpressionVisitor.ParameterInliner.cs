using System.Linq.Expressions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

public partial class DynamoShapedQueryCompilingExpressionVisitor
{
    /// <summary>
    /// Visitor that inlines parameter expressions by replacing them with constant expressions.
    /// This is necessary because parameter values are only available at runtime, not at compile time.
    /// </summary>
    private sealed class ParameterInliner(
        ISqlExpressionFactory sqlExpressionFactory,
        IReadOnlyDictionary<string, object?> parameterValues) : SqlExpressionVisitor
    {
        protected override Expression VisitSqlParameter(
            SqlParameterExpression sqlParameterExpression)
        {
            // Look up the actual parameter value
            if (!parameterValues.TryGetValue(sqlParameterExpression.Name, out var value))
                throw new InvalidOperationException(
                    $"Parameter '{sqlParameterExpression.Name}' not found in parameter values.");

            // Create a constant expression with the actual value
            // The type mapping from the parameter is preserved
            var constantExpression =
                sqlExpressionFactory.Constant(value, sqlParameterExpression.Type);

            // Apply the original type mapping if present
            if (sqlParameterExpression.TypeMapping != null)
                return constantExpression.ApplyTypeMapping(sqlParameterExpression.TypeMapping);

            return constantExpression;
        }

        // For all other expression types, just visit children
        protected override Expression VisitSqlBinary(SqlBinaryExpression sqlBinaryExpression)
            => sqlBinaryExpression.Update(
                (SqlExpression)Visit(sqlBinaryExpression.Left),
                (SqlExpression)Visit(sqlBinaryExpression.Right));

        protected override Expression VisitSqlConstant(SqlConstantExpression sqlConstantExpression)
            => sqlConstantExpression; // Constants don't need inlining

        protected override Expression VisitSqlProperty(SqlPropertyExpression sqlPropertyExpression)
            => sqlPropertyExpression; // Properties don't need inlining

        protected override Expression VisitProjection(ProjectionExpression projectionExpression)
            => projectionExpression.Update((SqlExpression)Visit(projectionExpression.Expression));

        protected override Expression VisitSelect(SelectExpression selectExpression)
        {
            // Visit predicate (WHERE clause)
            var predicate = selectExpression.Predicate != null
                ? (SqlExpression)Visit(selectExpression.Predicate)
                : null;

            // Visit orderings
            var orderings =
                selectExpression
                    .Orderings.Select(o => o.Update((SqlExpression)Visit(o.Expression)))
                    .ToList();

            // Visit projections
            var projections =
                selectExpression.Projection.Select(p => (ProjectionExpression)Visit(p)).ToList();

            // Check if anything changed
            if (predicate == selectExpression.Predicate
                && orderings.SequenceEqual(selectExpression.Orderings)
                && projections.SequenceEqual(selectExpression.Projection))
                return selectExpression;

            // Create updated SelectExpression
            var updatedSelect = new SelectExpression(selectExpression.TableName);
            if (predicate != null)
                updatedSelect.ApplyPredicate(predicate);

            // Add orderings - use AppendOrdering for all except first, or use ApplyOrdering +
            // AppendOrdering
            for (var i = 0; i < orderings.Count; i++)
                if (i == 0)
                    updatedSelect.ApplyOrdering(orderings[i]);
                else
                    updatedSelect.AppendOrdering(orderings[i]);

            // Add projections
            foreach (var projection in projections)
                updatedSelect.AddToProjection(projection);

            return updatedSelect;
        }
    }
}
