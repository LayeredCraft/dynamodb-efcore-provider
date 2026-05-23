using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.TestModels.ConcurrencyModel;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public class F1DynamoFixture : F1DynamoFixtureBase<byte[]>;

public abstract class F1DynamoFixtureBase<TRowVersion>
    : F1FixtureBase<TRowVersion>, IDynamoSpecificationFixture
{
    public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

    public override TestHelpers TestHelpers => DynamoTestHelpers.Instance;

    protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

    protected override bool ShouldLogCategory(string logCategory)
        => DynamoSpecificationFixtureExtensions.ShouldLogDynamoSql(logCategory);

    public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
        => builder
            .EnableSensitiveDataLogging()
            .UseModel(CreateDynamoModel())
            .UseSeeding((c, _) =>
            {
                if (!ShouldSeed((F1Context)c))
                    return;

                c.ChangeTracker.LazyLoadingEnabled = false;
                F1Context.AddSeedData((F1Context)c);
                c.SaveChanges();
            })
            .UseAsyncSeeding(async (c, _, t) =>
            {
                if (!await ShouldSeedAsync((F1Context)c))
                    return;

                c.ChangeTracker.LazyLoadingEnabled = false;
                F1Context.AddSeedData((F1Context)c);
                await c.SaveChangesAsync(t);
            })
            .ConfigureWarnings(warnings
                => warnings
                    .Default(WarningBehavior.Throw)
                    .Log(CoreEventId.SensitiveDataLoggingEnabledWarning)
                    .Log(CoreEventId.PossibleUnintendedReferenceComparisonWarning)
                    .Ignore(CoreEventId.SaveChangesStarting)
                    .Ignore(CoreEventId.SaveChangesCompleted)
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .UseDynamo(options => options.DynamoDbClient(DynamoTestStoreFactory.Instance.Client));

    private IModel CreateDynamoModel()
    {
        var modelBuilder = CreateModelBuilder();
        BuildModelExternal(modelBuilder);
        return (IModel)modelBuilder.Model;
    }

    protected override bool ShouldSeed(F1Context context) => true;

    protected override async Task<bool> ShouldSeedAsync(F1Context context)
    {
        await Task.CompletedTask;
        return true;
    }

    protected override void BuildModelExternal(ModelBuilder modelBuilder)
    {
        base.BuildModelExternal(modelBuilder);

        modelBuilder.Ignore<Chassis>();

        modelBuilder.Entity<Driver>(b =>
        {
            b.ToTable("Drivers").HasPartitionKey(e => e.Id);
            b.Property<TRowVersion>("Version").ValueGeneratedNever();
            b.Ignore(e => e.Team);
        });

        modelBuilder.Entity<Engine>(b =>
        {
            b.ToTable("Engines").HasPartitionKey(e => e.Id);
            b.Ignore(e => e.EngineSupplier);
            b.Ignore(e => e.Teams);
            b.Ignore(e => e.Gearboxes);
            b.Ignore(e => e.StorageLocation);
            b.ComplexProperty(e => e.StorageLocation);
        });
        modelBuilder.Entity<EngineSupplier>(b =>
        {
            b.ToTable("EngineSuppliers").HasPartitionKey(e => e.Name);
            b.Ignore(e => e.Engines);
            MarkPrimaryKeyAsConvention(b.Metadata);
        });
        modelBuilder.Entity<Gearbox>(b => b.ToTable("Gearboxes").HasPartitionKey(e => e.Id));
        modelBuilder.Entity<Sponsor>(b =>
        {
            b.ToTable("Sponsors").HasPartitionKey(e => e.Id);
            b.Property<TRowVersion>("Version").ValueGeneratedNever();
            b.Ignore(e => e.Teams);
        });
        modelBuilder.Entity<Team>(b =>
        {
            b.ToTable("Teams").HasPartitionKey(e => e.Id);
            b.Property<TRowVersion>("Version").ValueGeneratedNever();
            b.Ignore(e => e.Engine);
            b.ComplexProperty(e => e.Chassis);
            b.Ignore(e => e.Drivers);
            b.Ignore(e => e.Sponsors);
            b.Ignore(e => e.Gearbox);
        });
        modelBuilder.Entity<TeamSponsor>(b =>
        {
            b.ToTable("TeamSponsors").HasPartitionKey(e => e.SponsorId).HasSortKey(e => e.TeamId);
            b.Ignore(e => e.Sponsor);
            b.Ignore(e => e.Team);
            MarkPrimaryKeyAsConvention(b.Metadata);
        });

        modelBuilder.Entity<Fan>(b => b.ToTable("Fans").HasPartitionKey(e => e.Id));
        modelBuilder.Entity<SuperFan>(b => b.ComplexProperty(e => e.Swag));
        modelBuilder.Entity<MegaFan>(b => b.ComplexProperty(e => e.Swag));
        modelBuilder.Entity<FanTpt>(b => b.ToTable("FanTpts").HasPartitionKey(e => e.Id));
        modelBuilder.Entity<SuperFanTpt>(b => b.ComplexProperty(e => e.Swag));
        modelBuilder.Entity<MegaFanTpt>(b => b.ComplexProperty(e => e.Swag));
        modelBuilder.Entity<FanTpc>(b => b.ToTable("FanTpcs").HasPartitionKey(e => e.Id));
        modelBuilder.Entity<SuperFanTpc>(b => b.ComplexProperty(e => e.Swag));
        modelBuilder.Entity<MegaFanTpc>(b => b.ComplexProperty(e => e.Swag));

        modelBuilder.Entity<Circuit>(b => b.ToTable("Circuits").HasPartitionKey(e => e.Id));
        modelBuilder.Entity<StreetCircuit>(b => b.Ignore(e => e.City));
        modelBuilder.Entity<City>(b => b.ToTable("Cities").HasPartitionKey(e => e.Id));
        modelBuilder.Entity<CircuitTpt>(b => b.ToTable("CircuitTpts").HasPartitionKey(e => e.Id));
        modelBuilder.Entity<StreetCircuitTpt>(b => b.Ignore(e => e.City));
        modelBuilder.Entity<CityTpt>(b => b.ToTable("CityTpts").HasPartitionKey(e => e.Id));
        modelBuilder.Entity<CircuitTpc>(b => b.ToTable("CircuitTpcs").HasPartitionKey(e => e.Id));
        modelBuilder.Entity<StreetCircuitTpc>(b => b.Ignore(e => e.City));
        modelBuilder.Entity<CityTpc>(b => b.ToTable("CityTpcs").HasPartitionKey(e => e.Id));

        modelBuilder.Entity<TitleSponsor>(b =>
        {
            b.Ignore(e => e.Details);
            b.ComplexProperty(e => e.Details);
        });
    }

    private static void MarkPrimaryKeyAsConvention(IMutableEntityType entityType)
    {
        if (entityType.FindPrimaryKey() is not Key key)
            return;

        typeof(Key).GetField(
            "_configurationSource",
            BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(
            key,
            ConfigurationSource.Convention);
    }
}
