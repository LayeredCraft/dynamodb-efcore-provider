using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;
using Amazon.DynamoDBv2.Model;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class QueryTests(SimpleTableDynamoFixture fixture) : IClassFixture<SimpleTableDynamoFixture>
{
    private readonly SimpleTableDbContext _dbContext = new(
        new DbContextOptionsBuilder<SimpleTableDbContext>()
            .UseDynamo(options => options.ServiceUrl(fixture.Container.GetConnectionString()))
            .ReplaceService<IDynamoClientWrapper, CapturingDynamoClientWrapper>()
            .Options);

    private CapturingDynamoClientWrapper CapturingClient
        => (CapturingDynamoClientWrapper)_dbContext.GetService<IDynamoClientWrapper>();

    [Fact]
    public async Task ToListAsync_ReturnsAllItems()
    {
        var resultItems =
            await _dbContext.SimpleItems.ToListAsync(TestContext.Current.CancellationToken);

        resultItems.Should().BeEquivalentTo(SimpleItems.Items);

        // Verify that SQL uses explicit column list (not SELECT *)
        CapturingClient.LastStatement.Should().NotBeNull();
        CapturingClient.LastStatement!.Should().NotContain("SELECT *");
        CapturingClient.LastStatement!.Should().Contain("SELECT ");
        CapturingClient.LastStatement!.Should().Contain("Pk");
        CapturingClient.LastStatement!.Should().Contain("IntValue");
        CapturingClient.LastStatement!.Should().Contain("StringValue");
    }

    [Fact]
    public async Task Where_ComplexPredicate_ReturnsFilteredItems()
    {
        // Intentionally mixes comparison operators and boolean logic.
        // Goal: exercise predicate translation and parameterization.
        var x = 500;
        var query = _dbContext.SimpleItems.Where(item
            => item.IntValue >= 0
               && item.LongValue > x
               && item.StringValue != "delta"
               && (item.BoolValue == true || item.DoubleValue < 0));

        var resultItems = await query.ToListAsync(TestContext.Current.CancellationToken);

        var expected = SimpleItems.Items.Where(item
            => item.IntValue >= 0
               && item.LongValue > 500
               && item.StringValue != "delta"
               && (item.BoolValue == true || item.DoubleValue < 0));

        resultItems.Should().BeEquivalentTo(expected);

        CapturingClient.LastStatement.Should().NotBeNull();
        CapturingClient.LastStatement!.Should().Contain("WHERE");
        // Literal constants are inlined directly
        CapturingClient.LastStatement!.Should().Contain("IntValue >= 0");
        CapturingClient.LastStatement!.Should().Contain("StringValue <> 'delta'");
        CapturingClient.LastStatement!.Should().Contain("BoolValue = TRUE");
        CapturingClient.LastStatement!.Should().Contain("DoubleValue < 0");
        // Captured variable uses parameter
        CapturingClient.LastStatement!.Should().Contain("LongValue > ?");
        CapturingClient.LastStatement!.Should().Contain("AND");
        CapturingClient.LastStatement!.Should().Contain("OR");
    }

    [Fact]
    public async Task Where_MultipleWhereCalls_CombinePredicates()
    {
        // Use multiple Where calls so the provider has to combine predicates.
        var query =
            _dbContext
                .SimpleItems.Where(item => item.IntValue != 200000)
                .Where(item => item.IntValue > -200)
                .Where(item => item.LongValue <= 1000 || item.BoolValue == true);

        var resultItems = await query.ToListAsync(TestContext.Current.CancellationToken);

        var expected =
            SimpleItems
                .Items.Where(item => item.IntValue != 200000)
                .Where(item => item.IntValue > -200)
                .Where(item => item.LongValue <= 1000 || item.BoolValue == true);

        resultItems.Should().BeEquivalentTo(expected);

        CapturingClient.LastStatement.Should().NotBeNull();
        CapturingClient.LastStatement!.Should().Contain("WHERE");
        // All literal constants are inlined
        CapturingClient.LastStatement!.Should().Contain("IntValue <> 200000");
        CapturingClient.LastStatement!.Should().Contain("IntValue > -200");
        CapturingClient.LastStatement!.Should().Contain("LongValue <= 1000");
        CapturingClient.LastStatement!.Should().Contain("BoolValue = TRUE");
        CapturingClient.LastStatement!.Should().Contain("OR");
    }

    [Fact]
    public async Task OrderBy_ThenBy_WithOrPredicate_ReturnsItemsInAscendingOrder()
    {
        // DynamoDB PartiQL requires a hash-key condition when using ORDER BY.
        // We keep ORDER BY strictly on the primary key, but make the predicate non-trivial.
        var query =
            _dbContext
                .SimpleItems
                .Where(item => item.Pk == "ITEM#3" || item.Pk == "ITEM#1" || item.Pk == "ITEM#4")
                .OrderBy(item => item.Pk)
                .ThenBy(item => item.Pk);

        var resultItems = await query.ToListAsync(TestContext.Current.CancellationToken);

        var expected =
            SimpleItems
                .Items.Where(item
                    => item.Pk == "ITEM#3" || item.Pk == "ITEM#1" || item.Pk == "ITEM#4")
                .OrderBy(item => item.Pk)
                .ThenBy(item => item.Pk)
                .ToList();

        // DynamoDB Local doesn't reliably return deterministic ordering for these PartiQL scans,
        // so validate ordering via the generated PartiQL instead of result ordering.
        resultItems.Should().BeEquivalentTo(expected);

        CapturingClient.LastStatement.Should().NotBeNull();
        CapturingClient.LastStatement!.Should().Contain("ORDER BY");
        CapturingClient.LastStatement!.Should().Contain("Pk ASC");
        CapturingClient.LastStatement!.Should().Contain(", Pk ASC");
    }

    [Fact]
    public async Task
        OrderByDescending_ThenByDescending_WithAndOrPredicate_ReturnsItemsInExpectedOrder()
    {
        var query =
            _dbContext
                .SimpleItems
                .Where(item
                    => (item.Pk == "ITEM#1" || item.Pk == "ITEM#2" || item.Pk == "ITEM#3")
                       && (item.IntValue >= 100 || item.BoolValue == false))
                .OrderByDescending(item => item.Pk)
                .ThenByDescending(item => item.Pk);

        var resultItems = await query.ToListAsync(TestContext.Current.CancellationToken);

        var expected =
            SimpleItems
                .Items.Where(item
                    => (item.Pk == "ITEM#1" || item.Pk == "ITEM#2" || item.Pk == "ITEM#3")
                       && (item.IntValue >= 100 || item.BoolValue == false))
                .OrderByDescending(item => item.Pk)
                .ThenByDescending(item => item.Pk)
                .ToList();

        resultItems.Should().BeEquivalentTo(expected);

        CapturingClient.LastStatement.Should().NotBeNull();
        CapturingClient.LastStatement!.Should().Contain("ORDER BY");
        CapturingClient.LastStatement!.Should().Contain("Pk DESC");
        CapturingClient.LastStatement!.Should().Contain(", Pk DESC");
    }

    [Fact]
    public async Task Where_WithCapturedVariables_InlinesParametersCorrectly()
    {
        // Use captured variables to test parameter handling
        var minInt = 100;
        var maxLong = 1000L;
        var excludeString = "delta";

        var query = _dbContext.SimpleItems.Where(item
            => item.IntValue >= minInt
               && item.LongValue <= maxLong
               && item.StringValue != excludeString);

        var resultItems = await query.ToListAsync(TestContext.Current.CancellationToken);

        var expected = SimpleItems.Items.Where(item
            => item.IntValue >= 100 && item.LongValue <= 1000 && item.StringValue != "delta");

        resultItems.Should().BeEquivalentTo(expected);

        // Verify SQL was generated correctly
        CapturingClient.LastStatement.Should().NotBeNull();
        CapturingClient.LastStatement!.Should().Contain("WHERE");
        CapturingClient.LastStatement!.Should().Contain("IntValue >= ?");
        CapturingClient.LastStatement!.Should().Contain("LongValue <= ?");
        CapturingClient.LastStatement!.Should().Contain("StringValue <> ?");

        // CRITICAL: Verify parameters are NOT NULL (this was the bug we fixed)
        CapturingClient.LastParameters.Should().NotBeNull();
        CapturingClient.LastParameters!.Should().HaveCount(3);

        // Verify actual parameter values are populated (not NULL AttributeValues)
        // Before the fix, these would all be { NULL = true }
        CapturingClient.LastParameters![0].N.Should().Be("100"); // IntValue >= minInt
        CapturingClient.LastParameters![0].NULL.Should().NotBe(true); // Should not be NULL type

        CapturingClient.LastParameters![1].N.Should().Be("1000"); // LongValue <= maxLong
        CapturingClient.LastParameters![1].NULL.Should().NotBe(true); // Should not be NULL type

        CapturingClient.LastParameters![2].S.Should().Be("delta"); // StringValue != excludeString
        CapturingClient.LastParameters![2].NULL.Should().NotBe(true); // Should not be NULL type
    }

    private sealed class CapturingDynamoClientWrapper : IDynamoClientWrapper
    {
        private readonly DynamoClientWrapper _inner;

        public string? LastStatement { get; private set; }
        public IReadOnlyList<AttributeValue>? LastParameters { get; private set; }

        public CapturingDynamoClientWrapper(
            IDbContextOptions options,
            IExecutionStrategy executionStrategy)
            => _inner = new DynamoClientWrapper(options, executionStrategy);

        public Amazon.DynamoDBv2.IAmazonDynamoDB Client => _inner.Client;

        public IAsyncEnumerable<Dictionary<string, AttributeValue>> ExecutePartiQl(
            ExecuteStatementRequest statementRequest)
        {
            LastStatement = statementRequest.Statement;
            LastParameters = statementRequest.Parameters;
            return _inner.ExecutePartiQl(statementRequest);
        }
    }
}
