namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public sealed class ComplianceInventoryTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Compliance_inventory_tracks_current_spec_areas()
    {
        var inventoryPath = Path.Combine(AppContext.BaseDirectory, "ComplianceInventory.md");
        File.Exists(inventoryPath).Should().BeTrue();

        var inventory = File.ReadAllText(inventoryPath);
        string[] expectedAreas =
        [
            "Northwind Where predicates",
            "Northwind Select projections",
            "Northwind string functions",
            "Northwind ordering/paging",
            "Scalar/single-result operators",
            "Aggregates",
            "Joins/includes/navigation queries",
            "Query filters",
            "Null semantics",
            "Find",
            "SaveChanges",
            "Type mapping/value conversion",
            "Complex types/primitive collections"
        ];

        foreach (var area in expectedAreas)
            inventory.Should().Contain(area);
    }
}
