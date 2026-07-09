using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

public class ScalarEqualityTests(DynamoContainerFixture fixture) : SimpleTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Equality_constants_cover_supported_scalar_families()
    {
        (await Db
                .SimpleItems
                .Where(i => i.BoolValue == true)
                .Select(i => i.Pk)
                .ToListAsync(CancellationToken))
            .Should()
            .BeEquivalentTo(["ITEM#1", "ITEM#3"]);
        (await Db
                .SimpleItems
                .Where(i => i.IntValue == 100)
                .Select(i => i.Pk)
                .ToListAsync(CancellationToken))
            .Should()
            .Equal("ITEM#1");
        (await Db
                .SimpleItems
                .Where(i => i.LongValue == 1000L)
                .Select(i => i.Pk)
                .ToListAsync(CancellationToken))
            .Should()
            .Equal("ITEM#1");
        (await Db
                .SimpleItems
                .Where(i => i.FloatValue == 1.5f)
                .Select(i => i.Pk)
                .ToListAsync(CancellationToken))
            .Should()
            .Equal("ITEM#1");
        (await Db
                .SimpleItems
                .Where(i => i.DoubleValue == 1.25)
                .Select(i => i.Pk)
                .ToListAsync(CancellationToken))
            .Should()
            .Equal("ITEM#1");
        (await Db
                .SimpleItems
                .Where(i => i.DecimalValue == 10.123m)
                .Select(i => i.Pk)
                .ToListAsync(CancellationToken))
            .Should()
            .Equal("ITEM#1");
        (await Db
                .SimpleItems
                .Where(i => i.GuidValue == new Guid("11111111-1111-1111-1111-111111111111"))
                .Select(i => i.Pk)
                .ToListAsync(CancellationToken))
            .Should()
            .Equal("ITEM#1");
        (await Db
                .SimpleItems
                .Where(i => i.DateOnlyValue == new DateOnly(2026, 1, 1))
                .Select(i => i.Pk)
                .ToListAsync(CancellationToken))
            .Should()
            .Equal("ITEM#1");
        (await Db
                .SimpleItems
                .Where(i => i.TimeOnlyValue == new TimeOnly(10, 0, 0))
                .Select(i => i.Pk)
                .ToListAsync(CancellationToken))
            .Should()
            .Equal("ITEM#1");
        (await Db
                .SimpleItems
                .Where(i => i.TimeSpanValue == TimeSpan.FromHours(1))
                .Select(i => i.Pk)
                .ToListAsync(CancellationToken))
            .Should()
            .Equal("ITEM#1");

        SqlCapture
            .PartiQlStatements
            .Should()
            .Contain(statement => statement.Contains("WHERE \"boolValue\" = TRUE"));
        SqlCapture
            .PartiQlStatements
            .Should()
            .Contain(statement => statement.Contains("WHERE \"intValue\" = 100"));
        SqlCapture
            .PartiQlStatements
            .Should()
            .Contain(statement => statement.Contains("WHERE \"longValue\" = 1000"));
        SqlCapture
            .PartiQlStatements
            .Should()
            .Contain(statement => statement.Contains("WHERE \"floatValue\" = 1.5"));
        SqlCapture
            .PartiQlStatements
            .Should()
            .Contain(statement => statement.Contains("WHERE \"doubleValue\" = 1.25"));
        SqlCapture
            .PartiQlStatements
            .Should()
            .Contain(statement => statement.Contains("WHERE \"decimalValue\" = 10.123"));
        SqlCapture
            .PartiQlStatements
            .Should()
            .Contain(statement
                => statement.Contains(
                    "WHERE \"guidValue\" = '11111111-1111-1111-1111-111111111111'"));
        SqlCapture
            .PartiQlStatements
            .Should()
            .Contain(statement => statement.Contains("WHERE \"dateOnlyValue\" = '2026-01-01'"));
        SqlCapture
            .PartiQlStatements
            .Should()
            .Contain(statement => statement.Contains("WHERE \"timeOnlyValue\" = '10:00:00'"));
        SqlCapture
            .PartiQlStatements
            .Should()
            .Contain(statement => statement.Contains("WHERE \"timeSpanValue\" = '01:00:00'"));
        SqlCapture.Clear();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Equality_parameters_cover_supported_scalar_families()
    {
        var boolValue = true;
        var intValue = 100;
        var longValue = 1000L;
        var floatValue = 1.5f;
        var doubleValue = 1.25;
        var decimalValue = 10.123m;
        var guidValue = new Guid("11111111-1111-1111-1111-111111111111");
        var dateOnlyValue = new DateOnly(2026, 1, 1);
        var timeOnlyValue = new TimeOnly(10, 0, 0);
        var timeSpanValue = TimeSpan.FromHours(1);

        (await Db
                .SimpleItems
                .Where(i => i.BoolValue == boolValue)
                .Select(i => i.Pk)
                .ToListAsync(CancellationToken))
            .Should()
            .BeEquivalentTo(["ITEM#1", "ITEM#3"]);
        (await Db
                .SimpleItems
                .Where(i => i.IntValue == intValue)
                .Select(i => i.Pk)
                .ToListAsync(CancellationToken))
            .Should()
            .Equal("ITEM#1");
        (await Db
                .SimpleItems
                .Where(i => i.LongValue == longValue)
                .Select(i => i.Pk)
                .ToListAsync(CancellationToken))
            .Should()
            .Equal("ITEM#1");
        (await Db
                .SimpleItems
                .Where(i => i.FloatValue == floatValue)
                .Select(i => i.Pk)
                .ToListAsync(CancellationToken))
            .Should()
            .Equal("ITEM#1");
        (await Db
                .SimpleItems
                .Where(i => i.DoubleValue == doubleValue)
                .Select(i => i.Pk)
                .ToListAsync(CancellationToken))
            .Should()
            .Equal("ITEM#1");
        (await Db
                .SimpleItems
                .Where(i => i.DecimalValue == decimalValue)
                .Select(i => i.Pk)
                .ToListAsync(CancellationToken))
            .Should()
            .Equal("ITEM#1");
        (await Db
                .SimpleItems
                .Where(i => i.GuidValue == guidValue)
                .Select(i => i.Pk)
                .ToListAsync(CancellationToken))
            .Should()
            .Equal("ITEM#1");
        (await Db
                .SimpleItems
                .Where(i => i.DateOnlyValue == dateOnlyValue)
                .Select(i => i.Pk)
                .ToListAsync(CancellationToken))
            .Should()
            .Equal("ITEM#1");
        (await Db
                .SimpleItems
                .Where(i => i.TimeOnlyValue == timeOnlyValue)
                .Select(i => i.Pk)
                .ToListAsync(CancellationToken))
            .Should()
            .Equal("ITEM#1");
        (await Db
                .SimpleItems
                .Where(i => i.TimeSpanValue == timeSpanValue)
                .Select(i => i.Pk)
                .ToListAsync(CancellationToken))
            .Should()
            .Equal("ITEM#1");

        SqlCapture
            .PartiQlStatements
            .Should()
            .OnlyContain(statement => statement.Contains("WHERE") && statement.Contains("= ?"));
        SqlCapture.PartiQlStatements.Should().HaveCount(10);
        SqlCapture.Clear();
    }
}
