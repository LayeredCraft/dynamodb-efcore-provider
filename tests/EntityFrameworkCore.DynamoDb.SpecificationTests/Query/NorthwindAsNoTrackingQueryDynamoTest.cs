using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

/// <summary>Northwind AsNoTracking query specification tests for the DynamoDB provider.</summary>
public abstract class NorthwindAsNoTrackingQueryDynamoTest
    : NorthwindAsNoTrackingQueryTestBase<NorthwindQueryDynamoFixture<NoopModelCustomizer>>
{
    protected NorthwindAsNoTrackingQueryDynamoTest(
        NorthwindQueryDynamoFixture<NoopModelCustomizer> fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(NorthwindAsNoTrackingQueryDynamoTest));

    public override Task Entity_not_added_to_state_manager(bool useParam, bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Entity_not_added_to_state_manager(useParam, a);
                AssertSql(
                    """
                    SELECT "customerID", "$type", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Applied_to_body_clause(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Applied_to_multiple_body_clauses(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Applied_to_body_clause_with_projection(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task Applied_to_projection(bool async) => Task.CompletedTask;

    public override Task Can_get_current_values(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                if (!a)
                {
                    DynamoTestHelpers.Instance.NoSyncTest(() =>
                    {
                        using var syncContext = CreateContext();
                        _ = syncContext
                            .Set<Customer>()
                            .Where(c => c.CustomerID == "ALFKI")
                            .AsNoTracking()
                            .First();
                    });
                    return;
                }

                await using var context = CreateContext();
                var customer =
                    await context.Set<Customer>().FirstAsync(c => c.CustomerID == "ALFKI");
                customer.CompanyName = "foo";
                var customer2 =
                    await context
                        .Set<Customer>()
                        .AsNoTracking()
                        .FirstAsync(c => c.CustomerID == "ALFKI");

                Assert.NotEqual(customer.CompanyName, customer2.CompanyName);
            },
            false);

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Include_reference_and_collection(bool async) => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Applied_after_navigation_expansion(bool async) => Task.CompletedTask;

    public override Task Where_simple_shadow(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Where_simple_shadow(a);
                AssertSql(
                    """
                    SELECT "employeeID", "$type", "city", "country", "firstName", "reportsTo", "title"
                    FROM "Employees"
                    WHERE "title" = 'Sales Representative'
                    """);
            });

    public override Task Query_fast_path_when_ctor_binding(bool async)
        => NoSyncTest(
            async,
            async a =>
            {
                await base.Query_fast_path_when_ctor_binding(a);
                AssertSql(
                    """
                    SELECT "customerID", "$type", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
                    FROM "Customers"
                    """);
            });

    [ConditionalTheory(Skip = SkipReason.JoinsNotSupported)]
    public override Task SelectMany_simple(bool async) => Task.CompletedTask;

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

    private static Task NoSyncTest(
        bool async,
        Func<bool, Task> testCode,
        bool expectSyncFailure = true)
        => DynamoTestHelpers.Instance.NoSyncTest(async, testCode, expectSyncFailure);

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class NorthwindAsNoTrackingQueryDynamoTestDefault
        : NorthwindAsNoTrackingQueryDynamoTest
    {
        public NorthwindAsNoTrackingQueryDynamoTestDefault(
            NorthwindQueryDynamoFixture<NoopModelCustomizer> fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
