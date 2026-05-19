using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SimpleTable;

/// <summary>Integration tests for DynamoDB consistent-read query preference.</summary>
public class ConsistentReadTests(DynamoContainerFixture fixture) : SimpleTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task WithConsistentRead_ReadsWrittenItem()
    {
        var item = new SimpleItem
        {
            Pk = $"CONSISTENT#{Guid.NewGuid():N}",
            BoolValue = true,
            IntValue = 171,
            LongValue = 1710,
            FloatValue = 1.71f,
            DoubleValue = 17.1,
            DecimalValue = 171.171m,
            StringValue = "consistent-read",
            GuidValue = Guid.NewGuid(),
            DateTimeOffsetValue = new DateTimeOffset(2026, 5, 7, 12, 0, 0, TimeSpan.Zero),
            NullableBoolValue = null,
            NullableIntValue = null,
            NullableStringValue = null,
            DateOnlyValue = new DateOnly(2026, 5, 7),
            TimeOnlyValue = new TimeOnly(12, 0, 0),
            TimeSpanValue = TimeSpan.FromMinutes(17),
        };

        Db.SimpleItems.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        Db.ChangeTracker.Clear();

        var results = await Db
            .SimpleItems
            .Where(x => x.Pk == item.Pk)
            .WithConsistentRead()
            .ToListAsync(CancellationToken);

        results.Should().ContainSingle().Which.Should().BeEquivalentTo(item);
    }
}
