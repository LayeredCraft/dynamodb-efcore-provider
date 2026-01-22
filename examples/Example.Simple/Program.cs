// See https://aka.ms/new-console-template for more information

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("STARTING");

var services = new ServiceCollection();
services.AddDbContext<DynamoDbContext>();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();

var context = scope.ServiceProvider.GetRequiredService<DynamoDbContext>();

await context.Items.ToListAsync();

Console.WriteLine("DONE");

internal class DynamoDbContext : DbContext
{
    public DbSet<Item> Items { get; init; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) =>
        optionsBuilder.UseDynamo(options => options.ServiceUrl("http://localhost:8000"));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Item>().HasKey(x => x.Id);
    }
}

public class Item
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Desciption { get; set; }
}
