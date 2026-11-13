using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.ComplexTypesTable;

/// <summary>
///     Verifies that a complex-type discriminator mapped to a CLR property is persisted inside the
///     nested complex map and materialized back onto the CLR property.
/// </summary>
public class ClrDiscriminatorTests(DynamoContainerFixture fixture)
    : ClrDiscriminatorTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Clr_mapped_discriminator_is_persisted_and_materialized()
    {
        await using (var context = CreateContext())
        {
            context.Items.Add(
                new ClrDiscriminatorItem
                {
                    Pk = "C#1",
                    OptionalComplex = new ClrDiscriminatedValue
                    {
                        Kind = "Primary", OptionalProperty = "value"
                    }
                });

            await context.SaveChangesAsync(CancellationToken);
        }

        var response = await Client.GetItemAsync(
            new GetItemRequest
            {
                TableName = ClrDiscriminatorTable.TableName,
                Key = new Dictionary<string, AttributeValue> { ["pk"] = new() { S = "C#1" } }
            },
            CancellationToken);

        var complexAttributeName = GetOptionalComplexAttributeName();
        response.Item.Should().ContainKey(complexAttributeName);

        var complexMap = response.Item[complexAttributeName].M;
        var discriminatorAttributeName = GetDiscriminatorAttributeName();

        complexMap.Should().ContainKey(discriminatorAttributeName);
        complexMap[discriminatorAttributeName].S.Should().Be("Primary");
        complexMap.Should().ContainKey("optionalProperty");
        complexMap["optionalProperty"].S.Should().Be("value");

        await using var readContext = CreateContext();
        var item = await readContext.Items.SingleAsync(x => x.Pk == "C#1", CancellationToken);

        item.OptionalComplex.Should().NotBeNull();
        item.OptionalComplex!.Kind.Should().Be("Primary");
        item.OptionalComplex.OptionalProperty.Should().Be("value");
    }

    private string GetOptionalComplexAttributeName()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(ClrDiscriminatorItem))!;
        var complexProperty =
            entityType.FindComplexProperty(nameof(ClrDiscriminatorItem.OptionalComplex))!;
        return complexProperty.GetAttributeName();
    }

    private string GetDiscriminatorAttributeName()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(ClrDiscriminatorItem))!;
        var complexType =
            entityType.FindComplexProperty(nameof(ClrDiscriminatorItem.OptionalComplex))!
                .ComplexType;
        var discriminator = complexType.FindDiscriminatorProperty();

        discriminator.Should().NotBeNull();
        discriminator!.IsShadowProperty().Should().BeFalse();
        return discriminator.GetAttributeName();
    }
}
