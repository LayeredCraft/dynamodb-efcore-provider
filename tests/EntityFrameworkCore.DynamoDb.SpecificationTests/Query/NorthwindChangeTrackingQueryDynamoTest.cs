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
                                        SELECT "customerID", "$type", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                                        FROM "Customers"
                                        """;

    private const string EmployeesSql = """
                                        SELECT "employeeID", "$type", "city", "country", "firstName", "reportsTo", "title"
                                        FROM "Employees"
                                        """;

    protected NorthwindChangeTrackingQueryDynamoTest(
        NorthwindQueryDynamoFixture<NoopModelCustomizer> fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(NorthwindChangeTrackingQueryDynamoTest));

    [ConditionalFact(Skip = SkipReason.SyncQueriesNotSupported)]
    public override void Entity_reverts_when_state_set_to_unchanged()
        => base.Entity_reverts_when_state_set_to_unchanged();

    [ConditionalFact(Skip = SkipReason.SyncQueriesNotSupported)]
    public override void Multiple_entities_can_revert() => base.Multiple_entities_can_revert();

    [ConditionalFact(Skip = SkipReason.SyncQueriesNotSupported)]
    public override void Entity_does_not_revert_when_attached_on_DbContext()
        => base.Entity_does_not_revert_when_attached_on_DbContext();

    [ConditionalFact(Skip = SkipReason.SyncQueriesNotSupported)]
    public override void Entity_does_not_revert_when_attached_on_DbSet()
        => base.Entity_does_not_revert_when_attached_on_DbSet();

    [ConditionalFact(Skip = SkipReason.SyncQueriesNotSupported)]
    public override void Entity_range_does_not_revert_when_attached_dbContext()
        => base.Entity_range_does_not_revert_when_attached_dbContext();

    [ConditionalFact(Skip = SkipReason.SyncQueriesNotSupported)]
    public override void Entity_range_does_not_revert_when_attached_dbSet()
        => base.Entity_range_does_not_revert_when_attached_dbSet();

    [ConditionalFact(Skip = SkipReason.SyncQueriesNotSupported)]
    public override void Can_disable_and_reenable_query_result_tracking()
        => base.Can_disable_and_reenable_query_result_tracking();

    [ConditionalFact(Skip = SkipReason.SyncQueriesNotSupported)]
    public override void Can_disable_and_reenable_query_result_tracking_starting_with_NoTracking()
        => base.Can_disable_and_reenable_query_result_tracking_starting_with_NoTracking();

    [ConditionalFact(Skip = SkipReason.SyncQueriesNotSupported)]
    public override void Can_disable_and_reenable_query_result_tracking_query_caching()
        => base.Can_disable_and_reenable_query_result_tracking_query_caching();

    [ConditionalFact(Skip = SkipReason.SyncQueriesNotSupported)]
    public override void
        Can_disable_and_reenable_query_result_tracking_query_caching_using_options()
        => base.Can_disable_and_reenable_query_result_tracking_query_caching_using_options();

    [ConditionalFact(Skip = SkipReason.SyncQueriesNotSupported)]
    public override void
        Can_disable_and_reenable_query_result_tracking_query_caching_single_context()
        => base.Can_disable_and_reenable_query_result_tracking_query_caching_single_context();

    [ConditionalFact(Skip = SkipReason.SyncQueriesNotSupported)]
    public override void AsTracking_switches_tracking_on_when_off_in_options()
        => base.AsTracking_switches_tracking_on_when_off_in_options();

    [ConditionalFact(Skip = SkipReason.SyncQueriesNotSupported)]
    public override void Precedence_of_tracking_modifiers()
        => base.Precedence_of_tracking_modifiers();

    [ConditionalFact(Skip = SkipReason.SyncQueriesNotSupported)]
    public override void Precedence_of_tracking_modifiers2()
        => base.Precedence_of_tracking_modifiers2();

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
