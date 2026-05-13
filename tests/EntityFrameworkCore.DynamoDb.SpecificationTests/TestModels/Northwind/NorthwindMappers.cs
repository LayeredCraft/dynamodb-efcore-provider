using Amazon.DynamoDBv2.Model;
using LayeredCraft.DynamoMapper.Runtime;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.TestModels.Northwind;

internal static class NorthwindMappers
{
    internal static Dictionary<string, AttributeValue> ToItem(Customer source)
        => CustomerItemMapper.ToItem(
            new CustomerItem(
                source.CustomerID,
                source.Address,
                source.City,
                source.CompanyName,
                source.ContactName,
                source.ContactTitle,
                source.Country,
                source.Fax,
                source.Phone,
                source.PostalCode,
                source.Region));

    internal static Dictionary<string, AttributeValue> ToItem(Employee source)
        => EmployeeItemMapper.ToItem(
            new EmployeeItem(
                (int)source.EmployeeID,
                source.City,
                source.Country,
                source.FirstName,
                (int?)source.ReportsTo,
                source.Title));

    internal static Dictionary<string, AttributeValue> ToItem(Order source)
        => OrderItemMapper.ToItem(
            new OrderItem(
                source.OrderID,
                source.CustomerID,
                (int?)source.EmployeeID,
                source.OrderDate));

    internal static Dictionary<string, AttributeValue> ToItem(OrderDetail source)
        => OrderDetailItemMapper.ToItem(
            new OrderDetailItem(
                source.OrderID,
                source.ProductID,
                (int)source.Quantity,
                source.UnitPrice,
                source.Discount));

    internal static Dictionary<string, AttributeValue> ToItem(Product source)
        => ProductItemMapper.ToItem(
            new ProductItem(
                source.ProductID,
                source.Discontinued,
                source.ProductName,
                source.SupplierID,
                source.UnitPrice,
                (int)source.UnitsInStock));
}

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = true)]
internal static partial class CustomerItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(CustomerItem source);
}

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = true)]
internal static partial class EmployeeItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(EmployeeItem source);
}

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = true)]
internal static partial class OrderItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(OrderItem source);
}

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = true)]
internal static partial class OrderDetailItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(OrderDetailItem source);
}

[DynamoMapper(Convention = DynamoNamingConvention.CamelCase, OmitNullValues = true)]
internal static partial class ProductItemMapper
{
    internal static partial Dictionary<string, AttributeValue> ToItem(ProductItem source);
}

internal sealed record CustomerItem(
    string CustomerID,
    string? Address,
    string? City,
    string? CompanyName,
    string? ContactName,
    string? ContactTitle,
    string? Country,
    string? Fax,
    string? Phone,
    string? PostalCode,
    string? Region);

internal sealed record EmployeeItem(
    int EmployeeID,
    string? City,
    string? Country,
    string? FirstName,
    int? ReportsTo,
    string? Title);

internal sealed record OrderItem(
    int OrderID,
    string? CustomerID,
    int? EmployeeID,
    DateTime? OrderDate);

internal sealed record OrderDetailItem(
    int OrderID,
    int ProductID,
    int Quantity,
    decimal UnitPrice,
    double Discount);

internal sealed record ProductItem(
    int ProductID,
    bool Discontinued,
    string? ProductName,
    int? SupplierID,
    decimal? UnitPrice,
    int UnitsInStock);
