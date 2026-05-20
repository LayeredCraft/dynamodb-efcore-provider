using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

[Collection(DynamoSpecificationCollection.Name)]
public sealed class SaveChangesInterceptionDynamoTest
    : SaveChangesInterceptionTestBase,
        IClassFixture<SaveChangesInterceptionDynamoTest.InterceptionDynamoFixture>
{
    public SaveChangesInterceptionDynamoTest(
        InterceptionDynamoFixture fixture,
        DynamoSpecificationContainerFixture containerFixture)
        : base(fixture)
        => _ = containerFixture;

    [ConditionalFact]
    public void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(SaveChangesInterceptionDynamoTest));

    [ConditionalTheory(Skip = SkipReason.TransactionsNotSupported)]
    public override Task Intercept_SaveChanges_passively(bool async, bool inject, bool noAcceptChanges)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.TransactionsNotSupported)]
    public override Task Intercept_SaveChanges_to_suppress_save(bool async, bool inject, bool noAcceptChanges)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.TransactionsNotSupported)]
    public override Task Intercept_SaveChanges_to_change_result(bool async, bool inject, bool noAcceptChanges)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.TransactionsNotSupported)]
    public override Task Intercept_SaveChanges_failed(bool async, bool inject, bool noAcceptChanges, bool concurrencyError)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.TransactionsNotSupported)]
    public override Task Intercept_to_suppress_concurrency_exception(bool async, bool inject, bool noAcceptChanges)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.TransactionsNotSupported)]
    public override Task Intercept_SaveChanges_with_multiple_interceptors(bool async, bool inject, bool noAcceptChanges)
        => Task.CompletedTask;

    protected override bool SupportsOptimisticConcurrency => false;

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

        protected override string StoreName => "SaveChangesInterception";

        protected override bool ShouldSubscribeToDiagnosticListener => true;
    }
}
