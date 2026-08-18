using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

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
                => !string.IsNullOrEmpty(e.Name)
                && (e.Name.StartsWith(CommandPrefix, StringComparison.Ordinal)
                    || e.Name.StartsWith(QueryPrefix, StringComparison.Ordinal)));

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void SourceCarriesStabilityContract()
    {
        var sourcePath = FindDynamoEventIdSource();

        sourcePath
            .Should()
            .NotBeNull("the DynamoEventId source file must be discoverable from the repo root");

        File
            .ReadAllText(sourcePath!)
            .Should()
            .Contain("Warning: These values must not change between releases.");
    }

    private static string? FindDynamoEventIdSource()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory);
            dir is not null;
            dir = dir.Parent)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "src",
                "EntityFrameworkCore.DynamoDb",
                "Diagnostics",
                "DynamoEventId.cs");

            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}
