using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class PaginationTests(SimpleTableDynamoFixture fixture) : SimpleTableTestBase(fixture)
{
    [Fact]
    public async Task FirstAsync_WithoutPageSize_UsesDefault()
    {
        LoggerFactory.Clear();

        var result = await Db.SimpleItems.FirstAsync(CancellationToken);

        result.Should().NotBeNull();

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().NotBeEmpty();
        // Default page size is null (DynamoDB scans up to 1MB)
        calls.Should().OnlyContain(call => call.Limit == null);
        LoggerFactory.RowLimitingWarnings.Should().ContainSingle().Which.Should().Be(1);
    }

    [Fact]
    public async Task Take_WithoutPageSize_UsesDefault()
    {
        LoggerFactory.Clear();

        var results = await Db.SimpleItems.Take(3).ToListAsync(CancellationToken);

        results.Should().HaveCount(3);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().NotBeEmpty();
        // Default page size is null (DynamoDB scans up to 1MB)
        calls.Should().OnlyContain(call => call.Limit == null);
        LoggerFactory.RowLimitingWarnings.Should().ContainSingle().Which.Should().Be(3);
    }

    [Fact]
    public async Task FirstAsync_WithPageSize_UsesCustomPageSize()
    {
        LoggerFactory.Clear();

        var result =
            await Db
                .SimpleItems.Where(item => item.IntValue > 0)
                .WithPageSize(5)
                .FirstAsync(CancellationToken);

        result.Should().NotBeNull();

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().NotBeEmpty();
        calls.Should().OnlyContain(call => call.Limit == 5);
        LoggerFactory.RowLimitingWarnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Take_WithPageSize_UsesCustomPageSize()
    {
        LoggerFactory.Clear();

        var results = await Db.SimpleItems.WithPageSize(10).Take(3).ToListAsync(CancellationToken);

        results.Should().HaveCount(3);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().NotBeEmpty();
        calls.Should().OnlyContain(call => call.Limit == 10);
    }

    [Fact]
    public async Task ToListAsync_WithPageSize_UsesCustomPageSize()
    {
        LoggerFactory.Clear();

        var results = await Db.SimpleItems.WithPageSize(7).ToListAsync(CancellationToken);

        results.Should().NotBeEmpty();

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().NotBeEmpty();
        calls.Should().OnlyContain(call => call.Limit == 7);
        LoggerFactory.RowLimitingWarnings.Should().BeEmpty();
    }

    [Fact]
    public async Task ToListAsync_WithoutPageSize_DoesNotEmitRowLimitingWarning()
    {
        LoggerFactory.Clear();

        var results = await Db.SimpleItems.ToListAsync(CancellationToken);

        results.Should().NotBeEmpty();
        LoggerFactory.RowLimitingWarnings.Should().BeEmpty();
    }

    [Fact]
    public async Task FirstAsync_WithoutPagination_SingleRequest()
    {
        LoggerFactory.Clear();

        var result =
            await Db.SimpleItems.WithoutPagination().FirstOrDefaultAsync(CancellationToken);

        result.Should().NotBeNull();

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().HaveCount(1);
    }

    [Fact]
    public async Task Take_WithoutPagination_SingleRequest()
    {
        LoggerFactory.Clear();

        var results =
            await Db.SimpleItems.WithoutPagination().Take(5).ToListAsync(CancellationToken);

        // May get fewer than 5 results with single page
        results.Should().NotBeEmpty();

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().HaveCount(1);
    }

    [Fact]
    public async Task MultipleExtensions_LastOneWins()
    {
        LoggerFactory.Clear();

        var result =
            await Db.SimpleItems.WithPageSize(10).WithPageSize(20).FirstAsync(CancellationToken);

        result.Should().NotBeNull();

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().NotBeEmpty();
        // Last WithPageSize should win
        calls.Should().OnlyContain(call => call.Limit == 20);
    }

    [Fact]
    public async Task MultipleExtensions_LastOneWins_WithOperatorsBetween()
    {
        LoggerFactory.Clear();

        var results =
            await Db
                .SimpleItems.WithPageSize(10)
                .Take(3)
                .WithPageSize(20)
                .ToListAsync(CancellationToken);

        results.Should().HaveCount(3);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().NotBeEmpty();
        calls.Should().OnlyContain(call => call.Limit == 20);
    }
}
