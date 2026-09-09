using Amazon.DynamoDBv2.Model;
using EntityFrameworkCore.DynamoDb.Storage.Internal;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

#pragma warning disable EF9100

/// <summary>
///     Pins the generated-query <see cref="DynamoGeneratedQueryRuntime.ReadScalar{T}" /> boundary:
///     missing attributes, DynamoDB NULL, required/optional handling, and enum codecs.
/// </summary>
public class DynamoReadScalarTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ReadScalar_MissingAttribute_OptionalProperty_MaterializesDefault()
    {
        var reader = BuildReader<string?>(nameof(MatrixEntity.OptionalName), false);

        reader([]).Should().BeNull();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ReadScalar_MissingAttribute_RequiredProperty_Throws()
    {
        var reader = BuildReader<string>(nameof(MatrixEntity.RequiredName), true);

        var action = () => reader([]);

        action
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*Required property*was not present*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ReadScalar_NullWireValue_OptionalProperty_MaterializesDefault()
    {
        var reader = BuildReader<int?>(nameof(MatrixEntity.OptionalCount), false);

        reader(new Dictionary<string, AttributeValue> { ["OptionalCount"] = new() { NULL = true } })
            .Should()
            .BeNull();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ReadScalar_NullAttributeReference_BehavesLikeMissing()
    {
        // Generated converted-scalar expressions can probe HasValue on a null AttributeValue;
        // codecs must stay null-tolerant end to end.
        var reader = BuildReader<string?>(nameof(MatrixEntity.OptionalName), false);

        reader(new Dictionary<string, AttributeValue> { ["OptionalName"] = null! })
            .Should()
            .BeNull();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ReadScalar_UnconvertedEnum_RoundTrips()
    {
        var reader = BuildReader<MatrixStatus>(nameof(MatrixEntity.Status), false);

        reader(new Dictionary<string, AttributeValue> { ["Status"] = new() { N = "1" } })
            .Should()
            .Be(MatrixStatus.B);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void ReadScalar_NullableEnum_MaterializesNullFromNullWireValue()
    {
        var reader = BuildReader<MatrixStatus?>(nameof(MatrixEntity.OptionalStatus), false);

        reader(
                new Dictionary<string, AttributeValue>
                    {
                        ["OptionalStatus"] = new() { NULL = true }
                    })
            .Should()
            .BeNull();
    }

    [Theory(Timeout = TestConfiguration.DefaultTimeout)]
    [InlineData(typeof(ByteEnum), ByteEnum.Two, "2")]
    [InlineData(typeof(LongEnum), LongEnum.Large, "9223372036854775807")]
    [InlineData(typeof(ULongEnum), ULongEnum.Max, "18446744073709551615")]
    [InlineData(typeof(SByteEnum), SByteEnum.Min, "-128")]
    [InlineData(typeof(FlagsEnum), FlagsEnum.A | FlagsEnum.C, "5")]
    public void Enum_codecs_round_trip_non_int_underlying_types(
        Type enumType,
        Enum value,
        string wire)
    {
        var readerType = typeof(DynamoGeneratedQueryRuntime);
        var method =
            readerType.GetMethod(nameof(DynamoGeneratedQueryRuntime.ReadScalar)) !
                .MakeGenericMethod(enumType);

        var item = new Dictionary<string, AttributeValue> { ["value"] = new() { N = wire } };
        var parsed = method.Invoke(null, [item, "value", "value", false]);

        parsed.Should().Be(value);

        // Formatting must produce the same wire string (culture-invariant round trip).
        var formatMethod = typeof(EnumDynamoValueReaderWriter<>)
            .MakeGenericType(enumType)
            .GetMethod("ToPartiQlLiteral", [enumType]);
        var formatted = formatMethod!.Invoke(
            Activator.CreateInstance(
                typeof(EnumDynamoValueReaderWriter<>).MakeGenericType(enumType)),
            [value]);

        formatted.Should().Be(wire);
    }

    private static Func<Dictionary<string, AttributeValue>, T> BuildReader<T>(
        string propertyName,
        bool required)
    {
        var property = typeof(MatrixEntity).GetProperty(propertyName)!;
        var method =
            typeof(DynamoGeneratedQueryRuntime).GetMethod(
                nameof(DynamoGeneratedQueryRuntime.ReadScalar)) !.MakeGenericMethod(
                property.PropertyType);

        return item =>
        {
            try
            {
                return (T)method.Invoke(
                    null,
                    [item, propertyName, $"MatrixEntity.{propertyName}", required])!;
            }
            catch (System.Reflection.TargetInvocationException exception) when (exception
                .InnerException is not null)
            {
                System
                    .Runtime
                    .ExceptionServices
                    .ExceptionDispatchInfo
                    .Capture(exception.InnerException)
                    .Throw();
                throw;
            }
        };
    }

    private enum MatrixStatus
    {
        A,
        B
    }

    private enum ByteEnum : byte
    {
        Two = 2
    }

    private enum LongEnum : long
    {
        Large = long.MaxValue
    }

    private enum ULongEnum : ulong
    {
        Max = ulong.MaxValue
    }

    private enum SByteEnum : sbyte
    {
        Min = sbyte.MinValue
    }

    [Flags]
    private enum FlagsEnum : int
    {
        A = 1,
        B = 2,
        C = 4
    }

    private sealed class MatrixEntity
    {
        public string Pk { get; set; } = null!;
        public string RequiredName { get; set; } = "";
        public string? OptionalName { get; set; }
        public int? OptionalCount { get; set; }
        public MatrixStatus Status { get; set; }
        public MatrixStatus? OptionalStatus { get; set; }
    }
}

#pragma warning restore EF9100
