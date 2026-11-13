using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.ComplexTypesTable;

/// <summary>
///     Verifies the supported complex-type discriminator persistence behavior: the discriminator
///     shadow property is excluded from the nested map, while the complex values round-trip by
///     partition key.
/// </summary>
public class DiscriminatorTests(DynamoContainerFixture fixture)
    : ComplexTypeDiscriminatorTableTestFixture(fixture)
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Insert_and_read_by_partition_key_materializes_complex()
    {
        await using (var context = CreateContext())
        {
            context.Items.Add(
                new DiscriminatorItem
                {
                    Pk = "D#1",
                    OptionalComplex = new DiscriminatedValue { OptionalProperty = "first" }
                });
            await context.SaveChangesAsync(CancellationToken);
        }

        await using var readContext = CreateContext();
        var item = await readContext.Items.SingleAsync(x => x.Pk == "D#1", CancellationToken);

        item.OptionalComplex.Should().NotBeNull();
        item.OptionalComplex!.OptionalProperty.Should().Be("first");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Update_null_optional_complex_to_non_null_materializes()
    {
        await using (var context = CreateContext())
        {
            context.Items.Add(new DiscriminatorItem { Pk = "D#2", OptionalComplex = null });
            await context.SaveChangesAsync(CancellationToken);
        }

        await using (var context = CreateContext())
        {
            var item = await context.Items.SingleAsync(x => x.Pk == "D#2", CancellationToken);

            item.OptionalComplex.Should().BeNull();

            item.OptionalComplex = new DiscriminatedValue { OptionalProperty = "updated" };
            await context.SaveChangesAsync(CancellationToken);
        }

        await using var readContext = CreateContext();
        var reloaded = await readContext.Items.SingleAsync(x => x.Pk == "D#2", CancellationToken);

        reloaded.OptionalComplex.Should().NotBeNull();
        reloaded.OptionalComplex!.OptionalProperty.Should().Be("updated");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Stored_complex_map_omits_discriminator_shadow_property()
    {
        await using (var context = CreateContext())
        {
            context.Items.Add(
                new DiscriminatorItem
                {
                    Pk = "D#3",
                    OptionalComplex = new DiscriminatedValue { OptionalProperty = "stored" }
                });
            await context.SaveChangesAsync(CancellationToken);
        }

        var response = await Client.GetItemAsync(
            new GetItemRequest
            {
                TableName = ComplexTypeDiscriminatorTable.TableName,
                Key = new Dictionary<string, AttributeValue> { ["pk"] = new() { S = "D#3" } }
            },
            CancellationToken);

        var complexAttributeName = GetOptionalComplexAttributeName();
        response.Item.Should().ContainKey(complexAttributeName);

        var complexMap = response.Item[complexAttributeName].M;
        complexMap.Should().ContainKey("optionalProperty");

        var discriminatorAttributeName = GetDiscriminatorAttributeName();
        complexMap.Should().NotContainKey(discriminatorAttributeName);
    }

    private string GetOptionalComplexAttributeName()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(DiscriminatorItem))!;
        var complexProperty =
            entityType.FindComplexProperty(nameof(DiscriminatorItem.OptionalComplex))!;
        return complexProperty.GetAttributeName();
    }

    private string GetDiscriminatorAttributeName()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(DiscriminatorItem))!;
        var complexType =
            entityType.FindComplexProperty(nameof(DiscriminatorItem.OptionalComplex))!.ComplexType;
        var discriminator = complexType.FindDiscriminatorProperty();

        discriminator.Should().NotBeNull();
        return discriminator!.GetAttributeName();
    }
}
