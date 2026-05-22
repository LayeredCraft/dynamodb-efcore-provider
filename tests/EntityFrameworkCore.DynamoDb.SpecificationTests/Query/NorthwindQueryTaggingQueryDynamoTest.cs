using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

/// <summary>Northwind query-tagging specification tests for the DynamoDB provider.</summary>
public abstract class NorthwindQueryTaggingQueryDynamoTest
    : NorthwindQueryTaggingQueryTestBase<NorthwindQueryDynamoFixture<NoopModelCustomizer>>
{
    protected NorthwindQueryTaggingQueryDynamoTest(
        NorthwindQueryDynamoFixture<NoopModelCustomizer> fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(NorthwindQueryTaggingQueryDynamoTest));

    public override void Single_query_tag() => AssertTaggedCustomer("Yanni");

    public override void Single_query_multiple_tags() => AssertTaggedCustomer("Yanni", "Enya");

    public override void Duplicate_tags() => AssertTaggedCustomer("Yanni", "Yanni");

    [ConditionalFact(Skip = SkipReason.JoinsNotSupported)]
    public override void Tags_on_subquery() => base.Tags_on_subquery();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void Tag_on_include_query() => base.Tag_on_include_query();

    public override void Tag_on_scalar_query()
    {
        using var context = CreateContext();
        // The base spec orders without constraining the partition key, which DynamoDB cannot
        // execute. Keep the OrderBy while adding the key predicate required for a safe First* path.
        var orderDate =
            context
                .Set<Order>()
                .Where(o => o.OrderID == 10248)
                .OrderBy(o => o.OrderID)
                .Select(o => o.OrderDate)
                .TagWith("Yanni")
                .FirstAsync()
                .GetAwaiter()
                .GetResult();

        Assert.NotNull(orderDate);
    }

    public override void Single_query_multiline_tag()
        => AssertTaggedCustomer("Yanni\r\nAND\r\nLaurel");

    public override void Single_query_multiple_multiline_tag()
        => AssertTaggedCustomer("Yanni\r\nAND\r\nLaurel", "Yet\r\nAnother\r\nMultiline\r\nTag");

    public override void Single_query_multiline_tag_with_empty_lines()
        => AssertTaggedCustomer("Yanni\r\n\r\nAND\r\n\r\nLaurel");

    private void AssertTaggedCustomer(params string[] tags)
    {
        using var context = CreateContext();

        // The base spec orders before First(), but DynamoDB requires a partition-key predicate for
        // ORDER BY. The predicate preserves a key-only First* path while keeping the ordered shape.
        IQueryable<Customer> query =
            context.Set<Customer>().Where(c => c.CustomerID == "ALFKI").OrderBy(c => c.CustomerID);
        foreach (var tag in tags)
            query = query.TagWith(tag);

        var customer = query.FirstAsync().GetAwaiter().GetResult();
        Assert.NotNull(customer);
    }

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class NorthwindQueryTaggingQueryDynamoTestDefault
        : NorthwindQueryTaggingQueryDynamoTest
    {
        public NorthwindQueryTaggingQueryDynamoTestDefault(
            NorthwindQueryDynamoFixture<NoopModelCustomizer> fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
