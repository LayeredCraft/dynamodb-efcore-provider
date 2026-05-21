using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

[Collection(DynamoSpecificationCollection.Name)]
public sealed class MaterializationInterceptionDynamoTest(NonSharedFixture fixture)
    : MaterializationInterceptionTestBase<MaterializationInterceptionDynamoTest.DynamoLibraryContext>(fixture)
{
    [ConditionalFact]
    public void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(MaterializationInterceptionDynamoTest));

    public override Task Binding_interceptors_are_used_by_queries(bool inject, bool usePooling)
        => base.Binding_interceptors_are_used_by_queries(inject, usePooling);

    public override Task Binding_interceptors_are_used_when_creating_instances(bool inject, bool usePooling)
        => base.Binding_interceptors_are_used_when_creating_instances(inject, usePooling);

    public override Task Intercept_query_materialization_for_empty_constructor(bool inject, bool usePooling)
        => base.Intercept_query_materialization_for_empty_constructor(inject, usePooling);

    [ConditionalTheory(Skip = SkipReason.OwnedEntityTypesNotSupported)]
    public override Task Intercept_query_materialization_with_owned_types(bool async, bool usePooling)
        => Task.CompletedTask;

    [ConditionalTheory(Skip = SkipReason.OwnedEntityTypesNotSupported)]
    public override Task Intercept_query_materialization_with_owned_types_projecting_collection(bool async, bool usePooling)
        => Task.CompletedTask;

    public override Task Intercept_query_materialization_for_full_constructor(bool inject, bool usePooling)
        => base.Intercept_query_materialization_for_full_constructor(inject, usePooling);

    public override Task Multiple_materialization_interceptors_can_be_used(bool inject, bool usePooling)
        => base.Multiple_materialization_interceptors_can_be_used(inject, usePooling);

    protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

    protected override IServiceCollection InjectInterceptors(
        IServiceCollection serviceCollection,
        IEnumerable<ISingletonInterceptor> injectedInterceptors)
        => base.InjectInterceptors(
            serviceCollection.AddEntityFrameworkDynamo(),
            injectedInterceptors);

    protected override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
        => base
            .AddOptions(builder)
            .UseDynamo(options => options.DynamoDbClient(DynamoTestStoreFactory.Instance.Client))
            .ConfigureWarnings(w => w.Ignore(DynamoEventId.ScanLikeQueryDetected));

    public class DynamoLibraryContext(DbContextOptions options) : LibraryContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Book>(b =>
            {
                b.ToTable("Books");
                b.HasPartitionKey(e => e.Id);
            });

            modelBuilder.Entity<Pamphlet>(b =>
            {
                b.ToTable("Pamphlets");
                b.HasPartitionKey(e => e.Id);
            });
        }
    }
}
