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
        public List<Address> PreviousAddresses { get; set; } = [];
        public IList<Address> InterfaceAddresses { get; set; } = [];
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

    /// <summary>Verifies that an unannotated list of POCOs is discovered as a complex collection.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void PlainNestedPocoList_IsDiscoveredAsComplexCollection()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = new PlainNestedPocoContext(BuildOptions<PlainNestedPocoContext>(client));

        var customerType = ctx.Model.FindEntityType(typeof(Customer))!;
        var complexProperty = customerType
            .GetComplexProperties()
            .Single(p => p.Name == nameof(Customer.PreviousAddresses));

        complexProperty.IsCollection.Should().BeTrue();
        complexProperty.ClrType.Should().Be(typeof(List<Address>));
        complexProperty.ComplexType.ClrType.Should().Be(typeof(Address));
        ctx.Model.FindEntityType(typeof(Address)).Should().BeNull();
    }

    /// <summary>Verifies that an <see cref="IList{T}" /> of POCOs is discovered as a complex collection.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void PlainNestedPocoIList_IsDiscoveredAsComplexCollection()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        using var ctx = new PlainNestedPocoContext(BuildOptions<PlainNestedPocoContext>(client));

        var customerType = ctx.Model.FindEntityType(typeof(Customer))!;
        var complexProperty = customerType
            .GetComplexProperties()
            .Single(p => p.Name == nameof(Customer.InterfaceAddresses));

        complexProperty.IsCollection.Should().BeTrue();
        complexProperty.ClrType.Should().Be(typeof(IList<Address>));
        complexProperty.ComplexType.ClrType.Should().Be(typeof(Address));
        ctx.Model.FindEntityType(typeof(Address)).Should().BeNull();
    }

    private sealed record Employee
    {
        public string Pk { get; set; } = null!;
        public Address WorkAddress { get; set; } = null!;
        public List<Address> WorkAddresses { get; set; } = [];
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

    private sealed record Team
    {
        public string Pk { get; set; } = null!;
        public List<Address> Offices { get; set; } = [];
    }

    private sealed record UnsupportedCollectionCustomer
    {
        public string Pk { get; set; } = null!;
        public ICollection<Address> Addresses { get; set; } = [];
    }

    private sealed record UnsupportedReadOnlyListCustomer
    {
        public string Pk { get; set; } = null!;
        public IReadOnlyList<Address> Addresses { get; set; } = [];
    }

    private sealed class AddressCollectionEntityContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<Team> Teams => Set<Team>();
        public DbSet<Address> Addresses => Set<Address>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Team>(b =>
            {
                b.ToTable("teams");
                b.HasPartitionKey(x => x.Pk);
            });

            modelBuilder.Entity<Address>(b =>
            {
                b.ToTable("addresses");
                b.HasPartitionKey(x => x.Street);
            });
        }
    }

    private sealed class UnsupportedCollectionContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<UnsupportedCollectionCustomer> Customers
            => Set<UnsupportedCollectionCustomer>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<UnsupportedCollectionCustomer>(b =>
            {
                b.ToTable("unsupported-collection-customers");
                b.HasPartitionKey(x => x.Pk);
                b.ComplexCollection(x => x.Addresses);
            });
    }

    private sealed class UnsupportedReadOnlyListContext(DbContextOptions options) : DbContext(
        options)
    {
        public DbSet<UnsupportedReadOnlyListCustomer> Customers
            => Set<UnsupportedReadOnlyListCustomer>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<UnsupportedReadOnlyListCustomer>(b =>
            {
                b.ToTable("unsupported-readonly-list-customers");
                b.HasPartitionKey(x => x.Pk);
                b.ComplexCollection(x => x.Addresses);
            });
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

    /// <summary>
    ///     Verifies that CLR types explicitly configured as entity types are not auto-converted to
    ///     complex collections either.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void EntityTypeClrType_IsNotAutoConvertedToComplexCollection()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var ctx = new AddressCollectionEntityContext(
            BuildOptions<AddressCollectionEntityContext>(client));
        var act = () => ctx.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*Unable to determine the relationship represented by navigation 'Team.Offices'*");
    }

    /// <summary>Verifies that unsupported collection abstractions remain rejected at model validation.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ICollectionShape_RemainsUnsupportedForComplexCollections()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var ctx = new UnsupportedCollectionContext(
            BuildOptions<UnsupportedCollectionContext>(client));
        var act = () => ctx.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*complex collection 'UnsupportedCollectionCustomer'.'Addresses' is of type "
                + "'ICollection<Address>' which does not implement 'IList<Address>'*");
    }

    /// <summary>
    ///     Verifies that <see cref="IReadOnlyList{T}" /> remains outside the supported complex
    ///     collection shape set.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void IReadOnlyListShape_RemainsUnsupportedForComplexCollections()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var ctx = new UnsupportedReadOnlyListContext(
            BuildOptions<UnsupportedReadOnlyListContext>(client));
        var act = () => ctx.Model;

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "*complex collection 'UnsupportedReadOnlyListCustomer'.'Addresses' is of type "
                + "'IReadOnlyList<Address>' which does not implement 'IList<Address>'*");
    }
}
