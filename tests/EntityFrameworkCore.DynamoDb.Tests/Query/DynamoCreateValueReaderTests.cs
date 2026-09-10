using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

#pragma warning disable EF9100

/// <summary>
///     Pins the precompiled-query value reader boundary (<c>CreateValueReader</c>): a NULL or
///     absent wire attribute must materialize as null for nullable properties on both the JIT
///     and NativeAOT converted-reader paths.
/// </summary>
public class DynamoCreateValueReaderTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateValueReader_NullWireValue_NullableConvertedEnum_MaterializesNull()
    {
        var reader = CreateValueReader<ReaderStatus?>(nameof(ReaderEntity.NullableStatus));

        var item = new Dictionary<string, AttributeValue>
        {
            [nameof(ReaderEntity.NullableStatus)] = new() { NULL = true }
        };

        reader(item).Should().BeNull();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateValueReader_NullWireValue_NullableRawEnum_MaterializesNull()
    {
        var reader = CreateValueReader<ReaderStatus?>(nameof(ReaderEntity.NullableRawStatus));

        var item = new Dictionary<string, AttributeValue>
        {
            [nameof(ReaderEntity.NullableRawStatus)] = new() { NULL = true }
        };

        reader(item).Should().BeNull();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateValueReader_NullWireValue_NullableInt_MaterializesNull()
    {
        var reader = CreateValueReader<int?>(nameof(ReaderEntity.OptionalCount));

        var item = new Dictionary<string, AttributeValue>
        {
            [nameof(ReaderEntity.OptionalCount)] = new() { NULL = true }
        };

        reader(item).Should().BeNull();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateValueReader_AbsentAttribute_OptionalProperty_MaterializesNull()
    {
        var reader = CreateValueReader<ReaderStatus?>(nameof(ReaderEntity.NullableStatus));

        new Dictionary<string, AttributeValue>()
            .Should()
            .NotContainKey(nameof(ReaderEntity.NullableStatus));
        reader([]).Should().BeNull();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateValueReader_NonNullWireValue_NullableConvertedEnum_MaterializesValue()
    {
        var reader = CreateValueReader<ReaderStatus?>(nameof(ReaderEntity.NullableStatus));

        var item = new Dictionary<string, AttributeValue>
        {
            [nameof(ReaderEntity.NullableStatus)] = new() { S = nameof(ReaderStatus.Active) }
        };

        reader(item).Should().Be(ReaderStatus.Active);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateValueReader_NullWireValue_RequiredProperty_Throws()
    {
        var reader = CreateValueReader<ReaderStatus?>(nameof(ReaderEntity.NullableStatus), true);

        var item = new Dictionary<string, AttributeValue>
        {
            [nameof(ReaderEntity.NullableStatus)] = new() { NULL = true }
        };

        var act = () => reader(item);

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Required property*did not contain a value*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void CreateValueReader_AbsentAttribute_RequiredProperty_Throws()
    {
        var reader = CreateValueReader<ReaderStatus>(nameof(ReaderEntity.Status), true);

        var act = () => reader([]);

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Required property*was not present*");
    }

    private static Func<Dictionary<string, AttributeValue>, T> CreateValueReader<T>(
        string propertyName,
        bool required = false)
    {
        using var context = CreateContext();
        var property =
            context.Model.FindEntityType(typeof(ReaderEntity))!.FindProperty(propertyName)!;
        var typeMapping = (DynamoTypeMapping)property.GetTypeMapping();

        return DynamoGeneratedQueryRuntime.CreateValueReader<T>(
            typeMapping,
            property,
            propertyName,
            propertyName,
            required);
    }

    private static ReaderContext CreateContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ReaderContext>();
        optionsBuilder
            .UseDynamo()
            .ConfigureWarnings(w
                => w
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                    .Ignore(DynamoEventId.ScanLikeQueryDetected));
        return new ReaderContext(optionsBuilder.Options);
    }

    private sealed class ReaderContext(DbContextOptions<ReaderContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ReaderEntity>(builder =>
            {
                builder.ToTable("ReaderItems");
                builder.HasPartitionKey(x => x.Pk);
                builder.Property(x => x.NullableStatus).HasConversion<string>();
                builder.Property(x => x.Status).HasConversion<string>();
            });
    }

    private sealed class ReaderEntity
    {
        public string Pk { get; set; } = null!;

        public ReaderStatus Status { get; set; }

        public ReaderStatus? NullableStatus { get; set; }

        public ReaderStatus? NullableRawStatus { get; set; }

        public int? OptionalCount { get; set; }
    }

    public enum ReaderStatus
    {
        Active = 1
    }
}
