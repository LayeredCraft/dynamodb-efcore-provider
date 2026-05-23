using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.PkSkTable;

/// <summary>Integration tests for server-side Single* query behavior.</summary>
public class SingleTest(DynamoContainerFixture fixture) : PkSkTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SingleAsync_KeyOnly_PkAndSkEquality_ReturnsMatchingItem()
    {
        LoggerFactory.Clear();

        var result = await Db.Items.SingleAsync(
            item => item.Pk == "P#1" && item.Sk == "0002",
            CancellationToken);

        var expected = PkSkItems.Items.Single(item => item.Pk == "P#1" && item.Sk == "0002");
        result.Should().BeEquivalentTo(expected);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(2);

        AssertSql(
            """
            SELECT "pk", "sk", "category", "isTarget"
            FROM "PkSkItems"
            WHERE "pk" = 'P#1' AND "sk" = '0002'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SingleOrDefaultAsync_KeyOnly_NoMatch_ReturnsNull()
    {
        var result = await Db.Items.SingleOrDefaultAsync(
            item => item.Pk == "P#2" && item.Sk == "9999",
            CancellationToken);

        result.Should().BeNull();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SingleAsync_KeyOnly_NoMatch_ThrowsNoElements()
    {
        var act = async () => await Db.Items.SingleAsync(
            item => item.Pk == "P#2" && item.Sk == "9999",
            CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*no elements*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SingleAsync_KeyOnly_PkOnlyDuplicate_ThrowsMoreThanOneElement()
    {
        LoggerFactory.Clear();

        var act = async ()
            => await Db.Items.SingleAsync(item => item.Pk == "P#1", CancellationToken);

        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception
            .Which
            .Message
            .Should()
            .MatchRegex(
                "(Sequence contains more than one element\\.|.*continuation token.*Single/SingleOrDefault.*)");

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(2);

        AssertSql(
            """
            SELECT "pk", "sk", "category", "isTarget"
            FROM "PkSkItems"
            WHERE "pk" = 'P#1'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SingleOrDefaultAsync_KeyOnly_PkIn_ReturnsSingleMatch()
    {
        LoggerFactory.Clear();
        var pks = new[] { "P#2", "NOT" };

        var result = await Db.Items.SingleOrDefaultAsync(
            item => pks.Contains(item.Pk) && item.Sk == "0001",
            CancellationToken);

        var expected = PkSkItems.Items.Single(item => item.Pk == "P#2" && item.Sk == "0001");
        result.Should().BeEquivalentTo(expected);

        var calls = LoggerFactory.ExecuteStatementCalls.ToList();
        calls.Should().ContainSingle();
        calls[0].Limit.Should().Be(2);

        AssertSql(
            """
            SELECT "pk", "sk", "category", "isTarget"
            FROM "PkSkItems"
            WHERE "pk" IN [?, ?] AND "sk" = '0001'
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SingleOrDefaultAsync_NonKeyFilter_ThrowsTranslationFailure()
    {
        var act = async () => await Db.Items.SingleOrDefaultAsync(
            item => item.Pk == "P#1" && item.IsTarget,
            CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Single/SingleOrDefault*key-condition-only*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SingleOrDefaultAsync_UserLimit_ThrowsTranslationFailure()
    {
        var act = async () => await Db
            .Items
            .Limit(5)
            .SingleOrDefaultAsync(item => item.Pk == "P#1", CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Limit(n)*Single/SingleOrDefault*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SingleOrDefaultAsync_WithNextToken_ThrowsTranslationFailure()
    {
        var act = async () => await Db
            .Items
            .WithNextToken("seed-token")
            .SingleOrDefaultAsync(item => item.Pk == "P#1", CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*WithNextToken*Single/SingleOrDefault*");
    }
}
