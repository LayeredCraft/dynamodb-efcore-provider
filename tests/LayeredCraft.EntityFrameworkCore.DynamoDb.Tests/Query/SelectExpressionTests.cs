using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Query;

public class SelectExpressionTests
{
    [Fact]
    public void DefaultValues_AreNull()
    {
        var selectExpression = new SelectExpression("TestTable");

        selectExpression.ResultLimit.Should().BeNull();
        selectExpression.PageSize.Should().BeNull();
    }

    [Fact]
    public void ApplyResultLimit_SetsResultLimit()
    {
        var selectExpression = new SelectExpression("TestTable");

        selectExpression.ApplyResultLimit(10);

        selectExpression.ResultLimit.Should().Be(10);
        selectExpression.PageSize.Should().BeNull();
    }

    [Fact]
    public void ApplyPageSize_SetsPageSize()
    {
        var selectExpression = new SelectExpression("TestTable");

        selectExpression.ApplyPageSize(50);

        selectExpression.ResultLimit.Should().BeNull();
        selectExpression.PageSize.Should().Be(50);
    }

    [Fact]
    public void ApplyBoth_SetsBothIndependently()
    {
        var selectExpression = new SelectExpression("TestTable");

        selectExpression.ApplyResultLimit(5);
        selectExpression.ApplyPageSize(100);

        selectExpression.ResultLimit.Should().Be(5);
        selectExpression.PageSize.Should().Be(100);
    }

    [Fact]
    public void ApplyLimit_BackwardCompatibility_SetsPageSize()
    {
        var selectExpression = new SelectExpression("TestTable");

        selectExpression.ApplyLimit(10);

        selectExpression.Limit.Should().Be(10);
        selectExpression.PageSize.Should().Be(10);
    }

    [Fact]
    public void Limit_Property_BackwardCompatibility_ReturnsPageSize()
    {
        var selectExpression = new SelectExpression("TestTable");

        selectExpression.ApplyPageSize(25);

        selectExpression.Limit.Should().Be(25);
    }

    [Fact]
    public void MultipleApplies_OverwritePreviousValues()
    {
        var selectExpression = new SelectExpression("TestTable");

        selectExpression.ApplyResultLimit(10);
        selectExpression.ApplyResultLimit(20);

        selectExpression.ResultLimit.Should().Be(20);
    }

    [Fact]
    public void ApplyNull_ClearsValue()
    {
        var selectExpression = new SelectExpression("TestTable");

        selectExpression.ApplyResultLimit(10);
        selectExpression.ApplyResultLimit(null);

        selectExpression.ResultLimit.Should().BeNull();
    }
}
