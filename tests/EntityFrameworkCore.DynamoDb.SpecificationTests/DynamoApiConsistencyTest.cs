using System.Reflection;
using EntityFrameworkCore.DynamoDb.Extensions;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Metadata.Builders;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
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
            GetAllTypes(Fixture.FluentApiTypes)
                .Where(type
                    => type.IsVisible && !type.Name.StartsWith("<M>$", StringComparison.Ordinal))
                .SelectMany(
                    type => type.GetMethods(
                        PublicInstance | BindingFlags.Static | BindingFlags.DeclaredOnly),
                    (type, method) => new { type, method })
                .Where(t => t.method.ReturnType == typeof(void))
                .Select(t => t.type.Name + "." + t.method.Name)
                .ToList();

        Assert.False(
            voidMethods.Count > 0,
            "\r\n-- Missing fluent returns --\r\n" + string.Join(Environment.NewLine, voidMethods));
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
