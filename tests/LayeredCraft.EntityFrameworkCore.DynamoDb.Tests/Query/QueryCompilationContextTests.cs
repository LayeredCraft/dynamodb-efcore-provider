using LayeredCraft.EntityFrameworkCore.DynamoDb.Query;
using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Query;

public class QueryCompilationContextTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var dependencies = new QueryCompilationContextDependencies(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var context = new DynamoQueryCompilationContext(dependencies, true);

        context.PageSizeOverride.Should().BeNull();
        context.PaginationDisabled.Should().BeFalse();
    }

    [Fact]
    public void PageSizeOverride_CanBeSet()
    {
        var dependencies = new QueryCompilationContextDependencies(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var context =
            new DynamoQueryCompilationContext(dependencies, true) { PageSizeOverride = 100 };

        context.PageSizeOverride.Should().Be(100);
    }

    [Fact]
    public void PaginationDisabled_CanBeSet()
    {
        var dependencies = new QueryCompilationContextDependencies(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var context =
            new DynamoQueryCompilationContext(dependencies, true) { PaginationDisabled = true };

        context.PaginationDisabled.Should().BeTrue();
    }

    [Fact]
    public void BothProperties_CanBeSetIndependently()
    {
        var dependencies = new QueryCompilationContextDependencies(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        var context = new DynamoQueryCompilationContext(dependencies, true)
        {
            PageSizeOverride = 50, PaginationDisabled = true,
        };

        context.PageSizeOverride.Should().Be(50);
        context.PaginationDisabled.Should().BeTrue();
    }
}
