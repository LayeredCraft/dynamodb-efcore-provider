using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using DynamoDbLoggerCategory = Microsoft.EntityFrameworkCore.DynamoDB.DbLoggerCategory;

namespace EntityFrameworkCore.DynamoDb.Tests.Diagnostics;

/// <summary>Tests the <see cref="DynamoEventId" /> event surface for stability invariants.</summary>
public class DynamoEventIdTests
{
    private static readonly EventId[] EventIds =
        typeof(DynamoEventId)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(static f => f.FieldType == typeof(EventId))
            .Select(static f => (EventId)f.GetValue(null)!)
            .ToArray();

    private static readonly string CommandPrefix = DbLoggerCategory.Database.Command.Name + ".";
    private static readonly string QueryPrefix = DbLoggerCategory.Query.Name + ".";
    private static readonly string CapacityPrefix = DynamoDbLoggerCategory.Capacity.Name + ".";

    private static readonly string[] KnownCategoryPrefixes =
    [
        CommandPrefix, QueryPrefix, CapacityPrefix
    ];

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Ids_AreUnique()
        => EventIds
            .GroupBy(static e => e.Id)
            .Where(static g => g.Count() > 1)
            .Select(static g => g.Key)
            .Should()
            .BeEmpty();

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Ids_BelongToProviderIdSpace()
    {
        var min = CoreEventId.ProviderBaseId + 100;
        var max = CoreEventId.ProviderBaseId + 199;

        EventIds.Should().OnlyContain(e => e.Id >= min && e.Id <= max);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Names_CarryACategoryPrefix()
        => EventIds
            .Should()
            .OnlyContain(static e
                => !string.IsNullOrEmpty(e.Name) && KnownCategoryPrefixes.Any(e.Name.StartsWith));
}
