using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

/// <summary>Northwind query fixture for DynamoDB specification tests.</summary>
public class NorthwindQueryDynamoFixture<TModelCustomizer>
    : NorthwindQueryFixtureBase<TModelCustomizer>, IDynamoSpecificationFixture
    where TModelCustomizer : ITestModelCustomizer, new()
{
    public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

    protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

    protected override bool UsePooling => false;

    protected override bool ShouldLogCategory(string logCategory)
        => DynamoSpecificationFixtureExtensions.ShouldLogDynamoSql(logCategory);

    public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
        => base
            .AddOptions(builder)
            .ConfigureWarnings(warnings
                => warnings
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                    .Ignore(CoreEventId.MappedNavigationIgnoredWarning)
                    .Ignore(DynamoEventId.ScanLikeQueryDetected))
            .UseDynamo(options
                => options
                    .DynamoDbClient(DynamoTestStoreFactory.Instance.Client)
                    .TransactionOverflowBehavior(TransactionOverflowBehavior.UseChunking));

    protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
    {
        base.OnModelCreating(modelBuilder, context);

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.Ignore(e => e.Orders);
            entity.Ignore(e => e.Context);
        });
        modelBuilder.Entity<Employee>().Ignore(e => e.Manager);
        modelBuilder.Entity<Order>(entity =>
        {
            entity.Ignore(e => e.Customer);
            entity.Ignore(e => e.OrderDetails);
        });
        modelBuilder.Entity<Product>().Ignore(e => e.OrderDetails);
        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.Ignore(e => e.Order);
            entity.Ignore(e => e.Product);
        });
        modelBuilder.Entity<OrderQuery>().Ignore(e => e.Customer);

        modelBuilder.Entity<Customer>().ToTable("Customers").HasPartitionKey(e => e.CustomerID);
        modelBuilder.Entity<Employee>().ToTable("Employees").HasPartitionKey(e => e.EmployeeID);
        modelBuilder.Entity<Order>().ToTable("Orders").HasPartitionKey(e => e.OrderID);
        modelBuilder.Entity<Product>().ToTable("Products").HasPartitionKey(e => e.ProductID);
        modelBuilder.Entity<OrderDetail>().ToTable("OrderDetails").HasPartitionKey(e => e.OrderID).HasSortKey(e => e.ProductID);

        modelBuilder.Entity<CustomerQuery>().HasKey(e => e.CompanyName);
        modelBuilder.Entity<CustomerQuery>().ToTable("CustomerQueries").HasPartitionKey(e => e.CompanyName);
        modelBuilder.Entity<CustomerQueryWithQueryFilter>().HasKey(e => e.CompanyName);
        modelBuilder.Entity<CustomerQueryWithQueryFilter>().ToTable("CustomerQueriesWithQueryFilter").HasPartitionKey(e => e.CompanyName);
        modelBuilder.Entity<OrderQuery>().HasKey(e => e.CustomerID);
        modelBuilder.Entity<OrderQuery>().ToTable("OrderQueries").HasPartitionKey(e => e.CustomerID);
        modelBuilder.Entity<ProductQuery>().HasKey(e => e.ProductID);
        modelBuilder.Entity<ProductQuery>().ToTable("ProductQueries").HasPartitionKey(e => e.ProductID);
        modelBuilder.Entity<ProductView>().HasKey(e => e.ProductID);
        modelBuilder.Entity<ProductView>().ToTable("ProductViews").HasPartitionKey(e => e.ProductID);
    }
}
