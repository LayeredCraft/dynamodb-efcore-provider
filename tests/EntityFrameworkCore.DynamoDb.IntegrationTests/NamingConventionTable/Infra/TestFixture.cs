using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.NamingConventionTable;

/// <summary>
///     Test fixture for naming convention integration tests. Creates both DynamoDB tables once per
///     test class and provides a lazily-initialized <see cref="Db" /> context.
/// </summary>
public class NamingConventionTableTestFixture : DynamoTestFixtureBase
{
    /// <summary>
    ///     Initializes both the snake_case and camelCase tables. The base class drives delete and
    ///     active-wait for the snake_case table; the camelCase table is deleted manually inside the lambda
    ///     before creation.
    /// </summary>
    public NamingConventionTableTestFixture(DynamoContainerFixture fixture) : base(fixture)
        => EnsureClassTableInitialized(
            SnakeCaseItemTable.TableName,
            async (client, ct) =>
            {
                await SnakeCaseItemTable.CreateTable(client, ct);

                // Recreate the second table manually — base class only manages the first.
                try
                {
                    await client.DeleteTableAsync(
                        new DeleteTableRequest { TableName = KebabCaseItemTable.TableName },
                        ct);
                }
                catch (ResourceNotFoundException) { }

                await KebabCaseItemTable.CreateTable(client, ct);
            });

    /// <summary>Lazily-initialized DbContext shared across all tests in the class.</summary>
    public NamingConventionTableDbContext Db
    {
        get
        {
            field ??= new NamingConventionTableDbContext(
                CreateOptions<NamingConventionTableDbContext>(o => o.DynamoDbClient(Client)));
            return field;
        }
    }
}
