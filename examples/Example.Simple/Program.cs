// See https://aka.ms/new-console-template for more information

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("STARTING");

var services = new ServiceCollection();
services.AddDbContext<DynamoDbContext>();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();

var context = scope.ServiceProvider.GetRequiredService<DynamoDbContext>();

var items = await context.Items.Where(i => i.Id == "item-4").ToListAsync();

foreach (var item in items)
    Console.WriteLine($"Item: {item.Id}, {item.Name}, {item.Desciption}");

Console.WriteLine("DONE");

internal class DynamoDbContext : DbContext
{
    public DbSet<Item> Items { get; init; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseDynamo(options => options.ServiceUrl("http://localhost:8000"));

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.Entity<Item>().ToTable("SimpleItems").HasKey(x => x.Id);
}

public class Item
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Desciption { get; set; }
}
