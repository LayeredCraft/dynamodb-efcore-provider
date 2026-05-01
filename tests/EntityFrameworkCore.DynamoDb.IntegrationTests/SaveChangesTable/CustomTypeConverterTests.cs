using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

/// <summary>
///     Integration tests that verify the boxed scalar fallback write path for custom
///     (user-defined) model types with value converters. <see cref="ProductCode" /> is not in the
///     provider's type-dispatch table, so both the non-nullable and nullable property paths must go
///     through <c>BoxedScalarFallback</c> in <c>DynamoEntityItemSerializerSource</c>.
/// </summary>
public class CustomTypeConverterTests(DynamoContainerFixture fixture)
    : SaveChangesTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChanges_NonNullableCustomType_StoresConverterOutput()
    {
        // ProductCode → string via ProductCodeConverter, taking the boxed fallback path.
        var item = new CustomConverterItem
        {
            Pk = "TEST#CUSTOM",
            Sk = "CONVERTER#CUSTOM-1",
            Code = new ProductCode("PROD-42"),
            OptionalCode = null,
        };

        Db.CustomConverterItems.Add(item);
        await Db.SaveChangesAsync(CancellationToken);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);

        raw.Should().NotBeNull();
        // The converter should have serialized ProductCode("PROD-42") as the string "PROD-42".
        raw!["code"].S.Should().Be("PROD-42");
        raw["optionalCode"].NULL.Should().BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SaveChanges_NullableCustomTypeWithValue_StoresConverterOutput()
    {
        // Nullable<ProductCode> with a value — exercises the nullable wrapping boxed fallback path.
        var item = new CustomConverterItem
        {
            Pk = "TEST#CUSTOM",
            Sk = "CONVERTER#CUSTOM-2",
            Code = new ProductCode("PROD-1"),
            OptionalCode = new ProductCode("OPT-7"),
        };

        Db.CustomConverterItems.Add(item);
        await Db.SaveChangesAsync(CancellationToken);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);

        raw.Should().NotBeNull();
        raw!["code"].S.Should().Be("PROD-1");
        raw["optionalCode"].S.Should().Be("OPT-7");
    }
}
