using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.Extensions;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.TestModels.Northwind;

public class NorthwindDynamoContext(DbContextOptions options) : NorthwindContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .Entity<Customer>()
            .ToTable(NorthwindTables.Customers)
            .HasPartitionKey(e => e.CustomerID);
        modelBuilder
            .Entity<Employee>()
            .ToTable(NorthwindTables.Employees)
            .HasPartitionKey(e => e.EmployeeID);
        modelBuilder
            .Entity<Order>()
            .ToTable(NorthwindTables.Orders)
            .HasPartitionKey(e => e.OrderID);
        modelBuilder
            .Entity<OrderDetail>()
            .ToTable(NorthwindTables.OrderDetails)
            .HasPartitionKey(e => e.OrderID)
            .HasSortKey(e => e.ProductID);
        modelBuilder
            .Entity<Product>()
            .ToTable(NorthwindTables.Products)
            .HasPartitionKey(e => e.ProductID);

        modelBuilder.Entity<Employee>(e =>
        {
            e.Ignore(em => em.Address);
            e.Ignore(em => em.BirthDate);
            e.Ignore(em => em.Extension);
            e.Ignore(em => em.HireDate);
            e.Ignore(em => em.HomePhone);
            e.Ignore(em => em.LastName);
            e.Ignore(em => em.Notes);
            e.Ignore(em => em.Photo);
            e.Ignore(em => em.PhotoPath);
            e.Ignore(em => em.PostalCode);
            e.Ignore(em => em.Region);
            e.Ignore(em => em.TitleOfCourtesy);
            e.Ignore(em => em.Manager);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.Ignore(p => p.CategoryID);
            e.Ignore(p => p.QuantityPerUnit);
            e.Ignore(p => p.ReorderLevel);
            e.Ignore(p => p.UnitsOnOrder);
            e.Ignore(p => p.OrderDetails);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.Ignore(o => o.Freight);
            e.Ignore(o => o.RequiredDate);
            e.Ignore(o => o.ShipAddress);
            e.Ignore(o => o.ShipCity);
            e.Ignore(o => o.ShipCountry);
            e.Ignore(o => o.ShipName);
            e.Ignore(o => o.ShipPostalCode);
            e.Ignore(o => o.ShipRegion);
            e.Ignore(o => o.ShipVia);
            e.Ignore(o => o.ShippedDate);
            e.Ignore(o => o.Customer);
            e.Ignore(o => o.OrderDetails);
        });

        modelBuilder.Entity<Customer>().Ignore(c => c.Orders);
        modelBuilder.Entity<OrderDetail>(e =>
        {
            e.Ignore(od => od.Order);
            e.Ignore(od => od.Product);
        });

        modelBuilder.Ignore<CustomerQuery>();
        modelBuilder.Ignore<OrderQuery>();
        modelBuilder.Ignore<ProductQuery>();
        modelBuilder.Ignore<ProductView>();
        modelBuilder.Ignore<CustomerQueryWithQueryFilter>();
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.ConfigureWarnings(w => w.Ignore(DynamoEventId.ScanLikeQueryDetected));
}
