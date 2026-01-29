// See https://aka.ms/new-console-template for more information

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("STARTING");

var services = new ServiceCollection();
services.AddDbContext<DynamoDbContext>();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();

var context = scope.ServiceProvider.GetRequiredService<DynamoDbContext>();

// Example 1: Query all items (with explicit column projections)
Console.WriteLine("\n=== Example 1: Query All Items ===");
var allItems = await context.Items.ToListAsync();
foreach (var item in allItems)
    Console.WriteLine($"Item: {item.Id}, {item.Name}, {item.Desciption}");

// Example 2: Parameterized WHERE clause (demonstrates parameter inlining)
Console.WriteLine("\n=== Example 2: Parameterized WHERE Clause ===");
var searchId = "item-4";
var filteredItems = await context.Items.Where(i => i.Id == searchId).ToListAsync();
foreach (var item in filteredItems)
    Console.WriteLine($"Filtered Item: {item.Id}, {item.Name}, {item.Desciption}");

// Example 3: Multiple WHERE conditions with parameters
Console.WriteLine("\n=== Example 3: Multiple Conditions ===");
var minId = "item-2";
var maxId = "item-5";
var rangeItems =
    await context
        .Items.Where(i => i.Id.CompareTo(minId) >= 0 && i.Id.CompareTo(maxId) <= 0)
        .ToListAsync();
foreach (var item in rangeItems)
    Console.WriteLine($"Range Item: {item.Id}, {item.Name}, {item.Desciption}");

// Example 4: OrderBy query
Console.WriteLine("\n=== Example 4: Ordered Query ===");
var orderedItems =
    await context
        .Items.Where(i => i.Id == "item-1" || i.Id == "item-2" || i.Id == "item-3")
        .OrderBy(i => i.Id)
        .ToListAsync();
foreach (var item in orderedItems)
    Console.WriteLine($"Ordered Item: {item.Id}, {item.Name}, {item.Desciption}");

Console.WriteLine("\nDONE");

internal class DynamoDbContext : DbContext
{
    public DbSet<Item> Items { get; init; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseDynamo(options => options.ServiceUrl("http://localhost:8002"));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Item>().ToTable("SimpleItems").HasKey(x => x.Id);
}

public class Item
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Desciption { get; set; }
}
