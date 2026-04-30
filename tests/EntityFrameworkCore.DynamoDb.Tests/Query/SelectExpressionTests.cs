using System.Linq.Expressions;
using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>Unit tests for the new Limit/IsFirstTerminal API on SelectExpression.</summary>
public class SelectExpressionTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void DefaultValues_LimitIsNull_HasUserLimitFalse_IsFirstTerminalFalse()
    {
        var expr = new SelectExpression("TestTable");

        expr.Limit.Should().BeNull();
        expr.LimitExpression.Should().BeNull();
        expr.HasUserLimit.Should().BeFalse();
        expr.IsFirstTerminal.Should().BeFalse();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ApplyUserLimit_SetsLimitAndHasUserLimit()
    {
        var expr = new SelectExpression("TestTable");

        expr.ApplyUserLimit(10);

        expr.Limit.Should().Be(10);
        expr.LimitExpression.Should().BeOfType<ConstantExpression>().Which.Value.Should().Be(10);
        expr.HasUserLimit.Should().BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ApplyUserLimit_ChainedTwice_LastOneWins()
    {
        var expr = new SelectExpression("TestTable");

        expr.ApplyUserLimit(5);
        expr.ApplyUserLimit(10);

        expr.Limit.Should().Be(10);
        expr.HasUserLimit.Should().BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ApplyImplicitLimit_WhenNoUserLimit_SetsLimit()
    {
        var expr = new SelectExpression("TestTable");

        expr.ApplyImplicitLimit(1);

        expr.Limit.Should().Be(1);
        expr.HasUserLimit.Should().BeFalse();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ApplyImplicitLimit_WhenUserLimitAlreadySet_IsNoOp()
    {
        var expr = new SelectExpression("TestTable");

        expr.ApplyUserLimit(5);
        expr.ApplyImplicitLimit(1);

        // User limit wins — implicit is ignored.
        expr.Limit.Should().Be(5);
        expr.HasUserLimit.Should().BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void MarkAsFirstTerminal_SetsFlag()
    {
        var expr = new SelectExpression("TestTable");

        expr.MarkAsFirstTerminal();

        expr.IsFirstTerminal.Should().BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ApplyUserLimitExpression_WithNonConstant_SetsExpressionOnly()
    {
        var expr = new SelectExpression("TestTable");
        var parameter = Expression.Parameter(typeof(int), "n");

        expr.ApplyUserLimitExpression(parameter);

        expr.LimitExpression.Should().BeSameAs(parameter);
        expr.Limit.Should().BeNull(); // Cannot extract from non-constant.
        expr.HasUserLimit.Should().BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ApplyPredicate_StillWorks()
    {
        var expr = new SelectExpression("TestTable");
        var predicate = new SqlConstantExpression(true, typeof(bool), null);

        expr.ApplyPredicate(predicate);

        expr.Predicate.Should().BeSameAs(predicate);
    }
}
