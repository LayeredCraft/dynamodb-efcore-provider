using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>Represents the SelectExpressionTests type.</summary>
public class SelectExpressionTests
{
    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void DefaultValues_AreNull()
    {
        var selectExpression = new SelectExpression("TestTable");

        selectExpression.ResultLimit.Should().BeNull();
        selectExpression.PageSize.Should().BeNull();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void ApplyResultLimit_SetsResultLimit()
    {
        var selectExpression = new SelectExpression("TestTable");

        selectExpression.ApplyResultLimit(10);

        selectExpression.ResultLimit.Should().Be(10);
        selectExpression.PageSize.Should().BeNull();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void ApplyPageSize_SetsPageSize()
    {
        var selectExpression = new SelectExpression("TestTable");

        selectExpression.ApplyPageSize(50);

        selectExpression.ResultLimit.Should().BeNull();
        selectExpression.PageSize.Should().Be(50);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void ApplyBoth_SetsBothIndependently()
    {
        var selectExpression = new SelectExpression("TestTable");

        selectExpression.ApplyResultLimit(5);
        selectExpression.ApplyPageSize(100);

        selectExpression.ResultLimit.Should().Be(5);
        selectExpression.PageSize.Should().Be(100);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void ApplyLimit_BackwardCompatibility_SetsPageSize()
    {
        var selectExpression = new SelectExpression("TestTable");

        selectExpression.ApplyLimit(10);

        selectExpression.Limit.Should().Be(10);
        selectExpression.PageSize.Should().Be(10);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void Limit_Property_BackwardCompatibility_ReturnsPageSize()
    {
        var selectExpression = new SelectExpression("TestTable");

        selectExpression.ApplyPageSize(25);

        selectExpression.Limit.Should().Be(25);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void MultipleApplies_OverwritePreviousValues()
    {
        var selectExpression = new SelectExpression("TestTable");

        selectExpression.ApplyResultLimit(10);
        selectExpression.ApplyResultLimit(20);

        selectExpression.ResultLimit.Should().Be(20);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void ApplyNull_ClearsValue()
    {
        var selectExpression = new SelectExpression("TestTable");

        selectExpression.ApplyResultLimit(10);
        selectExpression.ApplyResultLimit(null);

        selectExpression.ResultLimit.Should().BeNull();
    }
}
