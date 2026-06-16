using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query.Translations;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query.Translations;

/// <summary>Basic types query fixture for DynamoDB translation specification tests.</summary>
public class BasicTypesQueryDynamoFixture : BasicTypesQueryFixtureBase, IDynamoSpecificationFixture
{
    public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

    protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

    protected override bool UsePooling => false;

    protected override bool ShouldLogCategory(string logCategory)
        => DynamoSpecificationFixtureExtensions.ShouldLogDynamoSql(logCategory);

    public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
        => base
            .AddOptions(builder)
            .ConfigureWarnings(warnings
                => warnings
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                    .Ignore(DynamoEventId.ScanLikeQueryDetected))
            .UseDynamo(options
                => options
                    .DynamoDbClient(DynamoTestStoreFactory.Instance.Client)
                    .TransactionOverflowBehavior(TransactionOverflowBehavior.UseChunking));
}
