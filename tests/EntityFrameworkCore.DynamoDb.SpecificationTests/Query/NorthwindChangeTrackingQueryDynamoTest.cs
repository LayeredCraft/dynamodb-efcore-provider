using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

/// <summary>Northwind change-tracking query specification tests for the DynamoDB provider.</summary>
public abstract class NorthwindChangeTrackingQueryDynamoTest
    : NorthwindChangeTrackingQueryTestBase<NorthwindQueryDynamoFixture<NoopModelCustomizer>>
{
    private const string CustomersSql = """
                                        SELECT "customerID", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                                        FROM "Customers"
                                        """;

    private const string EmployeesSql = """
                                        SELECT "employeeID", "city", "country", "firstName", "reportsTo", "title"
                                        FROM "Employees"
                                        """;

    protected NorthwindChangeTrackingQueryDynamoTest(
        NorthwindQueryDynamoFixture<NoopModelCustomizer> fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(NorthwindChangeTrackingQueryDynamoTest));

    public override void Entity_reverts_when_state_set_to_unchanged()
    {
        using var context = CreateContext();
        var customer = GetCustomerAsync(context, "ALFKI").GetAwaiter().GetResult();
        Assert.NotEqual("425-882-8080", customer.Phone);

        var entry = context.ChangeTracker.Entries<Customer>().Single();
        var phone = customer.Phone;
        customer.Phone = "425-882-8080";
        context.ChangeTracker.DetectChanges();

        Assert.Equal(customer.CustomerID, entry.Property(c => c.CustomerID).CurrentValue);
        Assert.Equal(EntityState.Modified, entry.State);
        Assert.Equal("425-882-8080", entry.Property(c => c.Phone).CurrentValue);

        entry.State = EntityState.Unchanged;

        Assert.Equal(customer.CustomerID, entry.Property(c => c.CustomerID).CurrentValue);
        Assert.Equal(phone, entry.Property(c => c.Phone).CurrentValue);
        Assert.Equal(EntityState.Unchanged, entry.State);
    }

    public override void Multiple_entities_can_revert()
    {
        using var context = CreateContext();
        var customers = context.Set<Customer>().ToListAsync().GetAwaiter().GetResult();
        var postalCodes = customers.Select(c => c.PostalCode).ToList();
        var regions = customers.Select(c => c.Region).ToList();

        foreach (var customer in customers)
        {
            customer.PostalCode = "98052";
            customer.Region = "'Murica";
        }

        Assert.Equal(91, context.ChangeTracker.Entries().Count());
        Assert.Equal("98052", customers[0].PostalCode);
        Assert.Equal("'Murica", customers[0].Region);

        foreach (var entry in context.ChangeTracker.Entries().ToList())
            entry.State = EntityState.Unchanged;

        Assert.Equal(postalCodes, customers.Select(c => c.PostalCode));
        Assert.Equal(regions, customers.Select(c => c.Region));
        AssertSql(CustomersSql);
    }

    public override void Entity_does_not_revert_when_attached_on_DbContext()
    {
        using var context = CreateContext();
        var customer = GetCustomerAsync(context, "ALFKI").GetAwaiter().GetResult();
        var entry = context.ChangeTracker.Entries<Customer>().Single();

        AssertAttachDoesNotRevert(context, customer, entry, () => context.Attach(customer));
    }

    public override void Entity_does_not_revert_when_attached_on_DbSet()
    {
        using var context = CreateContext();
        var customer = GetCustomerAsync(context, "ALFKI").GetAwaiter().GetResult();
        var entry = context.ChangeTracker.Entries<Customer>().Single();

        AssertAttachDoesNotRevert(
            context,
            customer,
            entry,
            () => context.Set<Customer>().Attach(customer));
    }

    public override void Entity_range_does_not_revert_when_attached_dbContext()
    {
        using var context = CreateContext();
        var customers = GetCustomersAsync(context, "ALFKI", "ANATR").GetAwaiter().GetResult();
        AssertRangeAttachDoesNotRevert(context, customers, () => context.AttachRange(customers));
    }

    public override void Entity_range_does_not_revert_when_attached_dbSet()
    {
        using var context = CreateContext();
        var customers = GetCustomersAsync(context, "ALFKI", "ANATR").GetAwaiter().GetResult();
        AssertRangeAttachDoesNotRevert(
            context,
            customers,
            () => context.Set<Customer>().AttachRange(customers));
    }

    public override void Can_disable_and_reenable_query_result_tracking()
    {
        using var context = CreateContext();
        Assert.Equal(QueryTrackingBehavior.TrackAll, context.ChangeTracker.QueryTrackingBehavior);

        var first = GetEmployeeAsync(context, 1).GetAwaiter().GetResult();
        Assert.NotNull(first);
        Assert.Single(context.ChangeTracker.Entries());

        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        var second = GetEmployeeAsync(context, 2).GetAwaiter().GetResult();
        Assert.NotNull(second);
        Assert.Single(context.ChangeTracker.Entries());

        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        var employees = context.Set<Employee>().ToListAsync().GetAwaiter().GetResult();
        Assert.Equal(9, employees.Count);
        Assert.Equal(9, context.ChangeTracker.Entries().Count());
    }

    public override void Can_disable_and_reenable_query_result_tracking_starting_with_NoTracking()
    {
        using var context = CreateNoTrackingContext();
        Assert.Equal(QueryTrackingBehavior.NoTracking, context.ChangeTracker.QueryTrackingBehavior);

        var first = GetEmployeeAsync(context, 1).GetAwaiter().GetResult();
        Assert.NotNull(first);
        Assert.Empty(context.ChangeTracker.Entries());

        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        var second = GetEmployeeAsync(context, 2).GetAwaiter().GetResult();
        Assert.NotNull(second);
        Assert.Single(context.ChangeTracker.Entries());
    }

    public override void Can_disable_and_reenable_query_result_tracking_query_caching()
    {
        using (var context = CreateContext())
        {
            Assert.Equal(
                QueryTrackingBehavior.TrackAll,
                context.ChangeTracker.QueryTrackingBehavior);
            var employees = context.Set<Employee>().ToListAsync().GetAwaiter().GetResult();
            Assert.Equal(9, employees.Count);
            Assert.Equal(9, context.ChangeTracker.Entries().Count());
        }

        using (var context = CreateContext())
        {
            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            var employees = context.Set<Employee>().ToListAsync().GetAwaiter().GetResult();
            Assert.Equal(9, employees.Count);
            Assert.Empty(context.ChangeTracker.Entries());
            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        }

        AssertSql(EmployeesSql, EmployeesSql);
    }

    public override void
        Can_disable_and_reenable_query_result_tracking_query_caching_using_options()
    {
        using (var context = CreateContext())
        {
            Assert.Equal(
                QueryTrackingBehavior.TrackAll,
                context.ChangeTracker.QueryTrackingBehavior);
            var employees = context.Set<Employee>().ToListAsync().GetAwaiter().GetResult();
            Assert.Equal(9, employees.Count);
            Assert.Equal(9, context.ChangeTracker.Entries().Count());
        }

        using (var context = CreateNoTrackingContext())
        {
            Assert.Equal(
                QueryTrackingBehavior.NoTracking,
                context.ChangeTracker.QueryTrackingBehavior);
            var employees = context.Set<Employee>().ToListAsync().GetAwaiter().GetResult();
            Assert.Equal(9, employees.Count);
            Assert.Empty(context.ChangeTracker.Entries());
        }

        AssertSql(EmployeesSql, EmployeesSql);
    }

    public override void
        Can_disable_and_reenable_query_result_tracking_query_caching_single_context()
    {
        using var context = CreateContext();
        Assert.Equal(QueryTrackingBehavior.TrackAll, context.ChangeTracker.QueryTrackingBehavior);

        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
        var employees = context.Set<Employee>().ToListAsync().GetAwaiter().GetResult();
        Assert.Equal(9, employees.Count);
        Assert.Empty(context.ChangeTracker.Entries());

        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        employees = context.Set<Employee>().ToListAsync().GetAwaiter().GetResult();
        Assert.Equal(9, employees.Count);
        Assert.Equal(9, context.ChangeTracker.Entries().Count());
        AssertSql(EmployeesSql, EmployeesSql);
    }

    public override void AsTracking_switches_tracking_on_when_off_in_options()
    {
        using var context = CreateNoTrackingContext();
        var employees = context.Set<Employee>().AsTracking().ToListAsync().GetAwaiter().GetResult();

        Assert.Equal(9, employees.Count);
        Assert.Equal(9, context.ChangeTracker.Entries().Count());
        AssertSql(EmployeesSql);
    }

    public override void Precedence_of_tracking_modifiers()
    {
        using var context = CreateContext();
        var employees =
            context
                .Set<Employee>()
                .AsNoTracking()
                .AsTracking()
                .ToListAsync()
                .GetAwaiter()
                .GetResult();

        Assert.Equal(9, employees.Count);
        Assert.Equal(9, context.ChangeTracker.Entries().Count());
        AssertSql(EmployeesSql);
    }

    public override void Precedence_of_tracking_modifiers2()
    {
        using var context = CreateContext();
        var employees =
            context
                .Set<Employee>()
                .AsTracking()
                .AsNoTracking()
                .ToListAsync()
                .GetAwaiter()
                .GetResult();

        Assert.Equal(9, employees.Count);
        Assert.Empty(context.ChangeTracker.Entries());
        AssertSql(EmployeesSql);
    }

    [ConditionalFact(Skip = SkipReason.JoinsNotSupported)]
    public override void Precedence_of_tracking_modifiers3()
        => base.Precedence_of_tracking_modifiers3();

    [ConditionalFact(Skip = SkipReason.JoinsNotSupported)]
    public override void Precedence_of_tracking_modifiers4()
        => base.Precedence_of_tracking_modifiers4();

    [ConditionalFact(Skip = SkipReason.JoinsNotSupported)]
    public override void Precedence_of_tracking_modifiers5()
        => base.Precedence_of_tracking_modifiers5();

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

    // The base tests use First(), OrderBy/Take, and OrderBy/Skip/Take query shapes. These helpers
    // use equivalent key predicates so the tests exercise tracking behavior on DynamoDB-safe reads.
    private static async Task<Customer> GetCustomerAsync(DbContext context, string customerId)
        => await context.Set<Customer>().FirstAsync(c => c.CustomerID == customerId);

    private static async Task<List<Customer>> GetCustomersAsync(
        DbContext context,
        string firstCustomerId,
        string secondCustomerId)
        => await context
            .Set<Customer>()
            .Where(c => c.CustomerID == firstCustomerId || c.CustomerID == secondCustomerId)
            .ToListAsync();

    private static async Task<Employee> GetEmployeeAsync(DbContext context, int employeeId)
        => await context.Set<Employee>().FirstAsync(e => e.EmployeeID == employeeId);

    private static void AssertAttachDoesNotRevert(
        DbContext context,
        Customer customer,
        EntityEntry<Customer> entry,
        Action attach)
    {
        Assert.Equal(EntityState.Unchanged, entry.State);
        Assert.NotEqual("425-882-8080", customer.Phone);
        Assert.NotEqual("425-882-8080", entry.Property(c => c.Phone).OriginalValue);

        customer.Phone = "425-882-8080";
        context.ChangeTracker.DetectChanges();
        Assert.Equal(EntityState.Modified, entry.State);

        attach();

        Assert.Equal(customer.CustomerID, entry.Property(c => c.CustomerID).CurrentValue);
        Assert.Equal(EntityState.Unchanged, entry.State);
        Assert.Equal("425-882-8080", entry.Property(c => c.Phone).CurrentValue);
        Assert.Equal("425-882-8080", entry.Property(c => c.Phone).OriginalValue);
    }

    private static void AssertRangeAttachDoesNotRevert(
        DbContext context,
        IReadOnlyList<Customer> customers,
        Action attach)
    {
        var entries = context.ChangeTracker.Entries<Customer>().ToList();
        Assert.Equal(2, customers.Count);
        Assert.Equal(2, entries.Count);

        foreach (var entry in entries)
        {
            Assert.Equal(EntityState.Unchanged, entry.State);
            Assert.NotEqual("425-882-8080", entry.Entity.Phone);
            Assert.NotEqual("425-882-8080", entry.Property(c => c.Phone).OriginalValue);
            entry.Entity.Phone = "425-882-8080";
        }

        context.ChangeTracker.DetectChanges();
        Assert.All(entries, entry => Assert.Equal(EntityState.Modified, entry.State));

        attach();

        foreach (var entry in entries)
        {
            Assert.Equal(entry.Entity.CustomerID, entry.Property(c => c.CustomerID).CurrentValue);
            Assert.Equal(EntityState.Unchanged, entry.State);
            Assert.Equal("425-882-8080", entry.Property(c => c.Phone).CurrentValue);
            Assert.Equal("425-882-8080", entry.Property(c => c.Phone).OriginalValue);
        }
    }

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class NorthwindChangeTrackingQueryDynamoTestDefault
        : NorthwindChangeTrackingQueryDynamoTest
    {
        public NorthwindChangeTrackingQueryDynamoTestDefault(
            NorthwindQueryDynamoFixture<NoopModelCustomizer> fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
