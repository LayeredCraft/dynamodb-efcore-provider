using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.SpecificationTests.SharedInfra;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestModels.Northwind;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

public sealed class NorthwindOrderByPagingQueryDynamoTest(DynamoContainerFixture containerFixture)
    : IAsyncLifetime
{
    private readonly NorthwindQueryDynamoFixture<NoopModelCustomizer> _fixture =
        new(containerFixture);

    public async ValueTask InitializeAsync() => await _fixture.InitializeAsync();

    public async ValueTask DisposeAsync() => await _fixture.DisposeAsync();

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task OrderBy_sort_key_inside_partition_is_supported()
    {
        await _fixture.AssertQuery.AssertQuery(
            ss => ss
                .Set<OrderDetail>()
                .Where(od => od.OrderID == 10248)
                .OrderBy(od => od.ProductID),
            elementSorter: od => od.ProductID,
            assertOrder: true);

        _fixture.AssertPartiQl(
            """
            SELECT "orderID", "productID", "discount", "quantity", "unitPrice"
            FROM "Northwind_OrderDetails"
            WHERE "orderID" = 10248
            ORDER BY "productID" ASC
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Limit_translates_to_limit()
    {
        await _fixture.AssertQuery.AssertQuery(
            ss => ss
                .Set<OrderDetail>()
                .Where(od => od.OrderID == 10248)
                .OrderBy(od => od.ProductID)
                .Limit(2),
            ss => ss
                .Set<OrderDetail>()
                .Where(od => od.OrderID == 10248)
                .OrderBy(od => od.ProductID)
                .Take(2),
            elementSorter: od => od.ProductID,
            assertOrder: true);

        _fixture.AssertPartiQl(
            """
            SELECT "orderID", "productID", "discount", "quantity", "unitPrice"
            FROM "Northwind_OrderDetails"
            WHERE "orderID" = 10248
            ORDER BY "productID" ASC
            """);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Skip_is_explicitly_unsupported()
    {
        await using var context = _fixture.CreateContext();
        var act = async ()
            => await context.Set<Customer>().OrderBy(c => c.CustomerID).Skip(5).ToListAsync();
        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Skip*not supported*");
    }
}
