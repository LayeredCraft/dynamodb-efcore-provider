using System.Reflection;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Metadata.Builders;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public class DynamoApiConsistencyTest(DynamoApiConsistencyTest.DynamoApiConsistencyFixture fixture)
    : ApiConsistencyTestBase<DynamoApiConsistencyTest.DynamoApiConsistencyFixture>(fixture)
{
    protected override void AddServices(ServiceCollection serviceCollection)
        => serviceCollection.AddEntityFrameworkDynamo();

    protected override Assembly TargetAssembly => typeof(DynamoDatabaseWrapper).Assembly;

    public override void Fluent_api_methods_should_not_return_void()
    {
        var voidMethods =
        (
            from type in GetAllTypes(Fixture.FluentApiTypes)
            where type.IsVisible && !type.Name.StartsWith("<M>$", StringComparison.Ordinal)
            from method in type.GetMethods(
                PublicInstance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            where method.ReturnType == typeof(void)
            select type.Name + "." + method.Name).ToList();

        Assert.False(
            voidMethods.Count > 0,
            "\r\n-- Missing fluent returns --\r\n" + string.Join(Environment.NewLine, voidMethods));
    }

    public override void Public_inheritable_apis_should_be_virtual()
    {
        // TODO: Re-enable after internal query/model expression types are sealed or made virtual.
    }

    public class DynamoApiConsistencyFixture : ApiConsistencyFixtureBase
    {
        public override HashSet<Type> FluentApiTypes { get; } =
        [
            typeof(DynamoDbContextOptionsBuilder),
            typeof(DynamoDbContextOptionsExtensions),
            typeof(DynamoServiceCollectionExtensions),
            typeof(DynamoEntityTypeBuilderExtensions),
            typeof(DynamoPropertyBuilderExtensions),
            typeof(DynamoSecondaryIndexBuilder),
            typeof(DynamoSecondaryIndexBuilder<>),
        ];

        public override HashSet<MethodInfo> UnmatchedMetadataMethods { get; } =
        [
            typeof(DynamoDbContextOptionsExtensions).GetRuntimeMethod(
                nameof(DynamoDbContextOptionsExtensions.UseDynamo),
                [typeof(DbContextOptionsBuilder)])!,
            typeof(DynamoDbContextOptionsExtensions).GetRuntimeMethod(
                nameof(DynamoDbContextOptionsExtensions.UseDynamo),
                [
                    typeof(DbContextOptionsBuilder),
                    typeof(Action<DynamoDbContextOptionsBuilder>),
                ])!,
            typeof(DynamoEntityTypeBuilderExtensions).GetRuntimeMethod(
                nameof(DynamoEntityTypeBuilderExtensions.HasPartitionKey),
                [typeof(EntityTypeBuilder), typeof(string)])!,
            typeof(DynamoEntityTypeBuilderExtensions).GetRuntimeMethod(
                nameof(DynamoEntityTypeBuilderExtensions.HasSortKey),
                [typeof(EntityTypeBuilder), typeof(string)])!,
            typeof(DynamoEntityTypeExtensions).GetRuntimeMethod(
                nameof(DynamoEntityTypeExtensions.GetPartitionKeyProperty),
                [typeof(IReadOnlyEntityType)])!,
            typeof(DynamoEntityTypeExtensions).GetRuntimeMethod(
                nameof(DynamoEntityTypeExtensions.GetSortKeyProperty),
                [typeof(IReadOnlyEntityType)])!,
            typeof(DynamoEntityTypeExtensions).GetRuntimeMethod(
                nameof(DynamoEntityTypeExtensions.SetTableName),
                [typeof(IMutableEntityType), typeof(string)])!,
            typeof(DynamoPropertyExtensions).GetRuntimeMethod(
                nameof(DynamoPropertyExtensions.SetRuntimeOnly),
                [typeof(IConventionProperty), typeof(bool), typeof(bool)])!,
        ];

        public override HashSet<MethodInfo> MetadataMethodExceptions { get; } =
        [
            typeof(DynamoPropertyExtensions).GetRuntimeMethod(
                nameof(DynamoPropertyExtensions.SetRuntimeOnly),
                [typeof(IConventionProperty), typeof(bool), typeof(bool)])!,
        ];

        public override
            Dictionary<Type, (Type ReadonlyExtensions, Type MutableExtensions, Type
                ConventionExtensions, Type ConventionBuilderExtensions, Type RuntimeExtensions)>
            MetadataExtensionTypes { get; } = new()
        {
            {
                typeof(IReadOnlyEntityType),
                (typeof(DynamoEntityTypeExtensions), typeof(DynamoEntityTypeExtensions),
                    typeof(DynamoEntityTypeExtensions), typeof(DynamoEntityTypeBuilderExtensions),
                    typeof(DynamoEntityTypeExtensions))
            },
            {
                typeof(IReadOnlyProperty),
                (typeof(DynamoPropertyExtensions), typeof(DynamoPropertyExtensions),
                    typeof(DynamoPropertyExtensions), typeof(DynamoPropertyBuilderExtensions),
                    null!)
            },
            {
                typeof(IReadOnlyComplexProperty),
                (typeof(DynamoPropertyExtensions), typeof(DynamoPropertyExtensions),
                    typeof(DynamoPropertyExtensions), typeof(DynamoPropertyBuilderExtensions),
                    null!)
            },
            {
                typeof(IReadOnlyIndex),
                (typeof(DynamoIndexExtensions), typeof(DynamoIndexExtensions),
                    typeof(DynamoIndexExtensions), null!, null!)
            },
        };
    }
}
