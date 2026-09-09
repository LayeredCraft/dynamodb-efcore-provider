using System.Collections;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>
///     Matrix test over every mapped property shape: a NULL or absent wire attribute must
///     materialize as the CLR null/default of the property type through the precompiled-query
///     reader boundary. Rows are derived from the model, so new property shapes are covered
///     automatically.
/// </summary>
#pragma warning disable EF9100
public class DynamoNullReadMatrixTests
{
    public static TheoryData<string> PropertyNames()
        => new(
            typeof(MatrixEntity)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name)
                .Where(name => name != nameof(MatrixEntity.Pk)));

    [Theory(Timeout = TestConfiguration.DefaultTimeout)]
    [MemberData(nameof(PropertyNames))]
    public void Null_wire_value_materializes_as_clr_default_for_property_type(string propertyName)
    {
        var (property, reader) = CreateReader(propertyName);

        var nullItem = new Dictionary<string, AttributeValue>
        {
            [propertyName] = new() { NULL = true }
        };
        var emptyItem = new Dictionary<string, AttributeValue>();

        // Property nullability (not CLR-type nullability) decides whether a missing wire value
        // is allowed: required non-nullable properties must fail loudly, matching EF semantics.
        var expectedNull = property.IsNullable;

        if (expectedNull)
        {
            reader(nullItem).Should().BeNull($"{propertyName} is nullable");
            reader(emptyItem).Should().BeNull($"{propertyName} is nullable");
        }
        else
        {
            // Required properties must fail loudly when their wire value is missing.
            var actNull = () => reader(nullItem);
            var actAbsent = () => reader(emptyItem);
            actNull.Should().Throw<InvalidOperationException>();
            actAbsent.Should().Throw<InvalidOperationException>();
        }
    }

    private static object Default(Type type)
        => type.IsValueType ? Activator.CreateInstance(type)! : null!;

    private static (IProperty Property, Func<Dictionary<string, AttributeValue>, object?> Reader)
        CreateReader(string propertyName)
    {
        using var context = CreateContext();
        var property =
            context.Model.FindEntityType(typeof(MatrixEntity))!.FindProperty(propertyName)!;
        var typeMapping = (DynamoTypeMapping)property.GetTypeMapping();

        var createValueReader = typeof(DynamoGeneratedQueryRuntime).GetMethod(
            "CreateValueReader",
            BindingFlags.NonPublic | BindingFlags.Static,
            [
                typeof(DynamoTypeMapping),
                typeof(IProperty),
                typeof(string),
                typeof(string),
                typeof(bool)
            ])!.MakeGenericMethod(property.ClrType);

        var reader = createValueReader.Invoke(
            null,
            [typeMapping, property, propertyName, propertyName, !property.IsNullable])!;

        // Value-type readers need boxing to be observed as object results here; unwrap the
        // TargetInvocationException wrapper so assertion failures show the real exception.
        object? BoxedReader(Dictionary<string, AttributeValue> item)
        {
            try
            {
                return ((Delegate)reader).DynamicInvoke(item);
            }
            catch (System.Reflection.TargetInvocationException ex) when (
                ex.InnerException is not null)
            {
                System
                    .Runtime
                    .ExceptionServices
                    .ExceptionDispatchInfo
                    .Capture(ex.InnerException)
                    .Throw();
                throw;
            }
        }

        return (property, BoxedReader);
    }

    private static MatrixContext CreateContext()
    {
        var optionsBuilder = new DbContextOptionsBuilder<MatrixContext>();
        optionsBuilder
            .UseDynamo()
            .ConfigureWarnings(w
                => w
                    .Ignore(CoreEventId.ManyServiceProvidersCreatedWarning)
                    .Ignore(DynamoEventId.ScanLikeQueryDetected));
        return new MatrixContext(optionsBuilder.Options);
    }

#pragma warning disable EF9100

    private sealed class MatrixContext(DbContextOptions<MatrixContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<MatrixEntity>(builder =>
            {
                builder.ToTable("MatrixItems");
                builder.HasPartitionKey(x => x.Pk);
                builder.Property(x => x.ConvertedStatus).HasConversion<string>();
                builder.Property(x => x.NullableConvertedStatus).HasConversion<string>();
                builder.Property(x => x.ConvertedId).HasConversion<string>();
            });
    }

    private sealed class MatrixEntity
    {
        public string Pk { get; set; } = null!;

        public string Name { get; set; } = null!;

        public string? OptionalName { get; set; }

        public int Count { get; set; }

        public int? OptionalCount { get; set; }

        public bool Enabled { get; set; }

        public bool? OptionalEnabled { get; set; }

        public double Amount { get; set; }

        public decimal Price { get; set; }

        public DateTime Timestamp { get; set; }

        public Guid Id { get; set; }

        public MatrixStatus Status { get; set; }

        public MatrixStatus? NullableStatus { get; set; }

        public MatrixStatus ConvertedStatus { get; set; }

        public MatrixStatus? NullableConvertedStatus { get; set; }

        public Guid ConvertedId { get; set; }

        public byte[] Blob { get; set; } = null!;

        public List<int> Scores { get; set; } = [];

        public List<int?> OptionalScores { get; set; } = [];

        public HashSet<string> Labels { get; set; } = [];

        public Dictionary<string, int> StrictCharges { get; set; } = [];

        public Dictionary<string, int?> Charges { get; set; } = [];
    }

    public enum MatrixStatus
    {
        Active = 1
    }
}
