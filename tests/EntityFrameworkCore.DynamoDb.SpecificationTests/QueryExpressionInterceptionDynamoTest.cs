using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

[Collection(DynamoSpecificationCollection.Name)]
public sealed class QueryExpressionInterceptionDynamoTest
    : QueryExpressionInterceptionTestBase,
        IClassFixture<QueryExpressionInterceptionDynamoTest.InterceptionDynamoFixture>
{
    public QueryExpressionInterceptionDynamoTest(
        InterceptionDynamoFixture fixture,
        DynamoSpecificationContainerFixture containerFixture)
        : base(fixture)
        => _ = containerFixture;

    [ConditionalFact]
    public void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(QueryExpressionInterceptionDynamoTest));

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Intercept_query_passively(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Intercept_query_with_multiple_interceptors(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Intercept_to_change_query_expression(bool async)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Interceptor_does_not_leak_across_contexts(bool async)
        => Task.CompletedTask;

    public class InterceptionDynamoFixture : InterceptionFixtureBase
    {
        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base
                .AddOptions(builder)
                .UseDynamo(options => options.DynamoDbClient(DynamoTestStoreFactory.Instance.Client));

        protected override IServiceCollection InjectInterceptors(
            IServiceCollection serviceCollection,
            IEnumerable<IInterceptor> injectedInterceptors)
            => base.InjectInterceptors(
                serviceCollection.AddEntityFrameworkDynamo(),
                injectedInterceptors);

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.Entity<Singularity>().ToTable("Singularities").HasPartitionKey(e => e.Id);
            modelBuilder.Entity<Brane>().ToTable("Branes").HasPartitionKey(e => e.Id);
        }

        protected override string StoreName => "QueryExpressionInterception";

        protected override bool ShouldSubscribeToDiagnosticListener => true;
    }
}
