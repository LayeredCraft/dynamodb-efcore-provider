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
                    .Ignore(CoreEventId.MappedEntityTypeIgnoredWarning)
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
        modelBuilder.Ignore<OrderDetail>();
        modelBuilder.Ignore<CustomerQuery>();
        modelBuilder.Ignore<CustomerQueryWithQueryFilter>();
        modelBuilder.Ignore<OrderQuery>();
        modelBuilder.Ignore<ProductQuery>();
        modelBuilder.Ignore<ProductView>();

        modelBuilder.Entity<Customer>().ToTable("Customers").HasPartitionKey(e => e.CustomerID);
        modelBuilder.Entity<Employee>().ToTable("Employees").HasPartitionKey(e => e.EmployeeID);
        modelBuilder.Entity<Order>().ToTable("Orders").HasPartitionKey(e => e.OrderID);
        modelBuilder.Entity<Product>().ToTable("Products").HasPartitionKey(e => e.ProductID);

    }

    protected override Task SeedAsync(NorthwindContext context)
    {
        context.Set<Customer>().AddRange(NorthwindData.CreateCustomers());
        context.Set<Employee>().AddRange(NorthwindData.CreateEmployees());
        context.Set<Order>().AddRange(NorthwindData.CreateOrders());
        context.Set<Product>().AddRange(NorthwindData.CreateProducts());

        return context.SaveChangesAsync();
    }
}
