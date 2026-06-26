using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

/// <summary>Northwind AsTracking query specification tests for the DynamoDB provider.</summary>
public abstract class NorthwindAsTrackingQueryDynamoTest
    : NorthwindAsTrackingQueryTestBase<NorthwindQueryDynamoFixture<NoopModelCustomizer>>
{
    protected NorthwindAsTrackingQueryDynamoTest(
        NorthwindQueryDynamoFixture<NoopModelCustomizer> fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(NorthwindAsTrackingQueryDynamoTest));

    public override void Entity_added_to_state_manager(bool useParam)
    {
        using var context = CreateContext();
        var query = context.Set<Customer>().AsQueryable();

        // The base spec is sync-only, but DynamoDB query execution is async-only.
        var list =
            (useParam ? query.AsTracking(QueryTrackingBehavior.TrackAll) : query.AsTracking())
            .ToListAsync()
            .GetAwaiter()
            .GetResult();

        Assert.Equal(91, list.Count);
        Assert.Equal(91, context.ChangeTracker.Entries().Count());
        AssertSql(
            """
            SELECT "customerID", "$type", "address", "city", "companyName", "contactName", "contactTitle", "country", "fax", "phone", "postalCode", "region"
            FROM "Customers"
            """);
    }

    [ConditionalFact(Skip = SkipReason.JoinsNotSupported)]
    public override void Applied_to_body_clause() => base.Applied_to_body_clause();

    [ConditionalFact(Skip = SkipReason.JoinsNotSupported)]
    public override void Applied_to_multiple_body_clauses()
        => base.Applied_to_multiple_body_clauses();

    [ConditionalFact(Skip = SkipReason.JoinsNotSupported)]
    public override void Applied_to_body_clause_with_projection()
        => base.Applied_to_body_clause_with_projection();

    [ConditionalFact(Skip = SkipReason.JoinsNotSupported)]
    public override void Applied_to_projection() => base.Applied_to_projection();

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class NorthwindAsTrackingQueryDynamoTestDefault
        : NorthwindAsTrackingQueryDynamoTest
    {
        public NorthwindAsTrackingQueryDynamoTestDefault(
            NorthwindQueryDynamoFixture<NoopModelCustomizer> fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
