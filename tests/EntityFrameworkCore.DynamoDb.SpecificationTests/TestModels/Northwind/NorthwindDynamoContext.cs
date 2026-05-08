using EntityFrameworkCore.DynamoDb.Diagnostics;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.TestModels.Northwind;

public sealed class NorthwindDynamoContext(DbContextOptions options) : NorthwindContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Customer>().ToTable(NorthwindTables.Customers);
        modelBuilder.Entity<Employee>().ToTable(NorthwindTables.Employees);
        modelBuilder.Entity<Order>().ToTable(NorthwindTables.Orders);
        modelBuilder.Entity<OrderDetail>().ToTable(NorthwindTables.OrderDetails);
        modelBuilder.Entity<Product>().ToTable(NorthwindTables.Products);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.ConfigureWarnings(w => w.Ignore(DynamoEventId.ScanLikeQueryDetected));
}
