using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

/// <summary>Integration tests for compiled queries using Limit(n) with runtime parameters.</summary>
#pragma warning disable EF9102
public class CompiledQueryPaginationTests(DynamoContainerFixture fixture)
    : SimpleTableTestFixture(fixture)
{
    // Parameterized Limit(n): limit = n + 1 evaluated at runtime.
    private static readonly Func<SimpleTableDbContext, int, IAsyncEnumerable<SimpleItem>>
        LimitPlusOneQuery = EF.CompileAsyncQuery((SimpleTableDbContext ctx, int n)
            => ctx.SimpleItems.Limit(n + 1));

    // Parameterized Limit(n): limit value resolved entirely at execution time.
    private static readonly Func<SimpleTableDbContext, int, IAsyncEnumerable<SimpleItem>>
        LimitRuntimeQuery = EF.CompileAsyncQuery((SimpleTableDbContext ctx, int n)
            => ctx.SimpleItems.Limit(n));

    // Parameterized token + limit with compiled query.
    private static readonly Func<SimpleTableDbContext, string, int, IAsyncEnumerable<SimpleItem>>
        SeededLimitQuery = EF.CompileAsyncQuery((SimpleTableDbContext ctx, string token, int n)
            => ctx.SimpleItems.WithNextToken(token).Limit(n));

    [Fact]
    public async Task Limit_WithArithmetic_EvaluatesAtRuntime()
    {
        SqlCapture.Clear();

        // n=4 → Limit=5; DynamoDB evaluates 5 items.
        var results = await LimitPlusOneQuery(Db, 4).ToListAsync(CancellationToken);

        // At most 5 items evaluated and returned.
        results.Count.Should().BeLessThanOrEqualTo(5);

        var calls = SqlCapture.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(5);
    }

    [Fact]
    public async Task Limit_RuntimeZero_ThrowsAtExecution()
    {
        SqlCapture.Clear();

        var act = async () => await LimitRuntimeQuery(Db, 0).ToListAsync(CancellationToken);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("limit");
    }

    [Fact]
    public async Task Limit_RuntimeNegative_ThrowsAtExecution()
    {
        SqlCapture.Clear();

        var act = async () => await LimitRuntimeQuery(Db, -5).ToListAsync(CancellationToken);

        await act.Should().ThrowAsync<ArgumentOutOfRangeException>().WithParameterName("limit");
    }

    [Fact]
    public async Task WithNextToken_CompiledRuntimeToken_SeedsFirstRequest()
    {
        SqlCapture.Clear();

        var firstPage = await Db.SimpleItems.ToPageAsync(1, null, CancellationToken);
        firstPage.NextToken.Should().NotBeNull();

        SqlCapture.Clear();

        var resumed =
            await SeededLimitQuery(Db, firstPage.NextToken!, 1).ToListAsync(CancellationToken);

        resumed.Should().ContainSingle();
        SqlCapture.ExecuteStatementCalls.Should().ContainSingle();
        SqlCapture.ExecuteStatementCalls[0].Limit.Should().Be(1);
        SqlCapture.ExecuteStatementCalls[0].RequestNextTokenPresent.Should().BeTrue();
        SqlCapture.ExecuteStatementCalls[0].SeedNextTokenPresent.Should().BeTrue();
    }
}
#pragma warning restore EF9102
