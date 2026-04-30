using Amazon.DynamoDBv2;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace EntityFrameworkCore.DynamoDb.Tests.Metadata.Conventions;

/// <summary>
///     Verifies provider-specific complex-property discovery behavior for nested POCO members.
/// </summary>
public class DynamoComplexPropertyDiscoveryConventionTests
{
    /// <summary>Builds context options configured for DynamoDB tests.</summary>
    /// <typeparam name="T">The test context type.</typeparam>
    /// <param name="client">The DynamoDB client mock.</param>
    /// <returns>Configured context options.</returns>
    private static DbContextOptions BuildOptions<T>(IAmazonDynamoDB client) where T : DbContext
        => new DbContextOptionsBuilder<T>()
            .UseDynamo(o => o.DynamoDbClient(client))
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

    private sealed record Address
    {
        public string Street { get; set; } = null!;
        public string City { get; set; } = null!;
    }

    private sealed record Customer
    {
        public string Pk { get; set; } = null!;
        public Address Profile { get; set; } = null!;
    }

    private sealed class PlainNestedPocoContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Customer> Customers => Set<Customer>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<Customer>(b =>
            {
                b.ToTable("customers");
                b.HasPartitionKey(x => x.Pk);
            });
    }

    /// <summary>
    ///     Verifies that an unannotated nested POCO member is discovered as a complex property.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void PlainNestedPoco_IsDiscoveredAsComplexProperty()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = new PlainNestedPocoContext(BuildOptions<PlainNestedPocoContext>(client));

        var customerType = ctx.Model.FindEntityType(typeof(Customer))!;
        var complexProperty = customerType.GetComplexProperties().Single(p => p.Name == "Profile");

        complexProperty.ClrType.Should().Be(typeof(Address));
        complexProperty.ComplexType.ClrType.Should().Be(typeof(Address));
        ctx.Model.FindEntityType(typeof(Address)).Should().BeNull();
    }

    private sealed record Employee
    {
        public string Pk { get; set; } = null!;
        public Address WorkAddress { get; set; } = null!;
    }

    private sealed class AddressEntityContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<Employee> Employees => Set<Employee>();
        public DbSet<Address> Addresses => Set<Address>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Employee>(b =>
            {
                b.ToTable("employees");
                b.HasPartitionKey(x => x.Pk);
            });

            modelBuilder.Entity<Address>(b =>
            {
                b.ToTable("addresses");
                b.HasPartitionKey(x => x.Street);
            });
        }
    }

    /// <summary>
    ///     Verifies that CLR types explicitly configured as entity types are not auto-converted to
    ///     complex properties.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void EntityTypeClrType_IsNotAutoConvertedToComplexProperty()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var ctx = new AddressEntityContext(BuildOptions<AddressEntityContext>(client));
        var act = () => ctx.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*Unable to determine the relationship represented by navigation 'Employee.WorkAddress'*");
    }
}
