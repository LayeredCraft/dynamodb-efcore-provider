using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class CompiledQueryPaginationTests(SimpleTableDynamoFixture fixture)
    : SimpleTableTestBase(fixture)
{
    private static readonly Func<SimpleTableDbContext, int, IAsyncEnumerable<SimpleItem>>
        TakePlusOneQuery = EF.CompileAsyncQuery((SimpleTableDbContext ctx, int n)
            => ctx.SimpleItems.Take(n + 1));

    private static readonly Func<SimpleTableDbContext, int, IAsyncEnumerable<SimpleItem>>
        PageSizePlusOneQuery = EF.CompileAsyncQuery((SimpleTableDbContext ctx, int n)
            => ctx.SimpleItems.WithPageSize(n + 1).Take(3));

    [Fact]
    public async Task Take_WithArithmetic_EvaluatesAtRuntime()
    {
        var results = await TakePlusOneQuery(Db, 2).ToListAsync(CancellationToken);

        results.Should().HaveCount(3);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().BeNull();
    }

    [Fact]
    public async Task WithPageSize_WithArithmetic_UsesComputedLimit()
    {
        var results = await PageSizePlusOneQuery(Db, 6).ToListAsync(CancellationToken);

        results.Should().HaveCount(3);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(7);
    }
}
