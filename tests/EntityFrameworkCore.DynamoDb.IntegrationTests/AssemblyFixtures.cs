using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

[assembly: AssemblyFixture(typeof(DynamoContainerFixture))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]
