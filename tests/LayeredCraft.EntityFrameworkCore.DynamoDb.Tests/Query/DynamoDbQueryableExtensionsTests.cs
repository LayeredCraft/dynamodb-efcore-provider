using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>Represents the DynamoDbQueryableExtensionsTests type.</summary>
public class DynamoDbQueryableExtensionsTests
{
    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task FirstAsync_WithPageSizeOverload_Zero_ThrowsException()
    {
        var source = new[] { new TestEntity() }.AsQueryable();

        var act = () => source.FirstAsync(0);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("pageSize");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task FirstAsync_WithPageSizeOverload_Negative_ThrowsException()
    {
        var source = new[] { new TestEntity() }.AsQueryable();

        var act = () => source.FirstAsync(-1);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("pageSize");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task FirstOrDefaultAsync_WithPageSizeOverload_Zero_ThrowsException()
    {
        var source = new[] { new TestEntity() }.AsQueryable();

        var act = () => source.FirstOrDefaultAsync(0);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("pageSize");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task FirstOrDefaultAsync_WithPageSizeOverload_Negative_ThrowsException()
    {
        var source = new[] { new TestEntity() }.AsQueryable();

        var act = () => source.FirstOrDefaultAsync(-1);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("pageSize");
    }

    private sealed class TestEntity;
}
