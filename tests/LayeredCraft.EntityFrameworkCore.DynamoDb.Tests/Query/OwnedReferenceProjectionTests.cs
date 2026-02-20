using System.Linq.Expressions;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using NSubstitute;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>
///     Tests for owned reference navigation projection, specifically covering the ambiguity
///     scenario where multiple entity types declare a navigation with the same name and CLR type.
/// </summary>
public class OwnedReferenceProjectionTests
{
    private static IAmazonDynamoDB CreateMockClientReturning(
        Dictionary<string, AttributeValue> item)
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [item] });
        return client;
    }

    /// <summary>Builds an item with optional top-level profile attribute state.</summary>
    private static Dictionary<string, AttributeValue> CreateItem(
        string pk,
        Dictionary<string, AttributeValue>? profileMap,
        bool includeProfileAttribute = true,
        bool profileIsNull = false)
    {
        var item = new Dictionary<string, AttributeValue> { ["Pk"] = new() { S = pk } };

        if (includeProfileAttribute)
            item["Profile"] = profileIsNull
                ? new AttributeValue { NULL = true }
                : new AttributeValue { M = profileMap ?? new Dictionary<string, AttributeValue>() };

        return item;
    }

    [Fact]
    public void Model_EntityA_HasEmbeddedProfileNavigation()
    {
        using var ctx = AmbiguousModelDbContext.Create(Substitute.For<IAmazonDynamoDB>());
        var entityAType = ctx.Model.FindEntityType(typeof(EntityA))!;
        Assert.NotNull(entityAType);

        var nav = entityAType.FindNavigation("Profile");
        Assert.NotNull(nav);
        Assert.False(nav.IsCollection, "Profile should not be a collection");
        Assert.True(
            nav.TargetEntityType.IsOwned(),
            $"SharedProfile should be owned (IsOwned={nav.TargetEntityType.IsOwned()})");
        Assert.True(
            nav.IsEmbedded(),
            $"Profile should be embedded (IsOnDependent={nav.IsOnDependent}, TargetIsOwned={nav.TargetEntityType.IsOwned()})");
    }

    [Fact]
    public async Task SelectOwnedReference_WithAmbiguousModel_ReturnsCorrectProfile()
    {
        // Arrange: build item representing an EntityA row with a Profile map attribute.
        var item = new Dictionary<string, AttributeValue>
        {
            ["Pk"] = new() { S = "A#1" },
            ["Profile"] = new()
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["DisplayName"] = new() { S = "Ada" }, ["Age"] = new() { N = "39" },
                },
            },
        };

        var client = CreateMockClientReturning(item);
        await using var ctx = AmbiguousModelDbContext.Create(client);

        // Act: Select the owned reference navigation.
        // Before the fix, TryResolveOwnedEmbeddedReferenceNavigation would find two candidates
        // (EntityA.Profile and EntityB.Profile), return false (ambiguous), and fall back to
        // scalar extraction — throwing at materialisation time.
        var profiles =
            await ctx
                .EntityAs
                .Select(a => a.Profile)
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        // Assert: navigation resolved to EntityA.Profile and materialised correctly.
        profiles.Should().HaveCount(1);
        profiles[0].Should().NotBeNull();
        profiles[0]!.DisplayName.Should().Be("Ada");
        profiles[0]!.Age.Should().Be(39);
    }

    [Fact]
    public async Task
        SelectOwnedReference_WithAmbiguousModel_SelectingEntityB_ReturnsCorrectProfile()
    {
        // Arrange: item representing an EntityB row with a Profile map attribute.
        var item = new Dictionary<string, AttributeValue>
        {
            ["Pk"] = new() { S = "B#1" },
            ["Profile"] = new()
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["DisplayName"] = new() { S = "Bob" }, ["Age"] = new() { N = "25" },
                },
            },
        };

        var client = CreateMockClientReturning(item);
        await using var ctx = AmbiguousModelDbContext.Create(client);

        // Act: Select Profile from EntityB — must resolve EntityB.Profile, not EntityA.Profile.
        var profiles =
            await ctx
                .EntityBs
                .Select(b => b.Profile)
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        profiles.Should().HaveCount(1);
        profiles[0].Should().NotBeNull();
        profiles[0]!.DisplayName.Should().Be("Bob");
        profiles[0]!.Age.Should().Be(25);
    }

    [Fact]
    public async Task SelectAnonymousWithOwnedReference_WithAmbiguousModel_MaterialisesCorrectly()
    {
        // Arrange: anonymous type projection { Pk, Profile } — exercises index-based binding.
        var item = new Dictionary<string, AttributeValue>
        {
            ["Pk"] = new() { S = "A#2" },
            ["Profile"] = new()
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["DisplayName"] = new() { S = "Cleo" },
                    ["Age"] = new() { N = "42" },
                },
            },
        };

        var client = CreateMockClientReturning(item);
        await using var ctx = AmbiguousModelDbContext.Create(client);

        var results =
            await ctx
                .EntityAs
                .Select(a => new { a.Pk, a.Profile })
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        results.Should().HaveCount(1);
        results[0].Pk.Should().Be("A#2");
        results[0].Profile!.DisplayName.Should().Be("Cleo");
        results[0].Profile!.Age.Should().Be(42);
    }

    [Fact]
    public async Task SelectOwnedReference_WithEfProperty_InAmbiguousModel_ReturnsCorrectProfile()
    {
        var item = CreateItem(
            "A#3",
            new Dictionary<string, AttributeValue>
            {
                ["DisplayName"] = new() { S = "Dora" }, ["Age"] = new() { N = "31" },
            });

        var client = CreateMockClientReturning(item);
        await using var ctx = AmbiguousModelDbContext.Create(client);

        var profiles =
            await ctx
                .EntityAs
                .Select(a => EF.Property<SharedProfile?>(a, "Profile"))
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        profiles.Should().HaveCount(1);
        profiles[0].Should().NotBeNull();
        profiles[0]!.DisplayName.Should().Be("Dora");
        profiles[0]!.Age.Should().Be(31);
    }

    [Fact]
    public async Task
        SelectAnonymousWithOwnedReferenceEfProperty_WithAmbiguousModel_MaterialisesCorrectly()
    {
        var item = CreateItem(
            "A#4",
            new Dictionary<string, AttributeValue>
            {
                ["DisplayName"] = new() { S = "Eve" }, ["Age"] = new() { N = "28" },
            });

        var client = CreateMockClientReturning(item);
        await using var ctx = AmbiguousModelDbContext.Create(client);

        var results =
            await ctx
                .EntityAs
                .Select(a => new { a.Pk, Profile = EF.Property<SharedProfile?>(a, "Profile") })
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        results.Should().HaveCount(1);
        results[0].Pk.Should().Be("A#4");
        results[0].Profile.Should().NotBeNull();
        results[0].Profile!.DisplayName.Should().Be("Eve");
        results[0].Profile!.Age.Should().Be(28);
    }

    [Fact]
    public async Task
        SelectOwnedReference_ProfileAttributeMissing_ReturnsNullForOptionalNavigation()
    {
        var item = CreateItem("A#5", null, false);

        var client = CreateMockClientReturning(item);
        await using var ctx = AmbiguousModelDbContext.Create(client);

        var profiles =
            await ctx
                .EntityAs
                .Select(a => a.Profile)
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        profiles.Should().HaveCount(1);
        profiles[0].Should().BeNull();
    }

    [Fact]
    public async Task SelectOwnedReference_ProfileAttributeNull_ReturnsNullForOptionalNavigation()
    {
        var item = CreateItem("A#6", null, true, true);

        var client = CreateMockClientReturning(item);
        await using var ctx = AmbiguousModelDbContext.Create(client);

        var profiles =
            await ctx
                .EntityAs
                .Select(a => a.Profile)
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        profiles.Should().HaveCount(1);
        profiles[0].Should().BeNull();
    }

    [Fact]
    public async Task SelectOwnedReference_RequiredProfileMissing_ThrowsClearError()
    {
        var item = CreateItem("R#1", null, false);

        var client = CreateMockClientReturning(item);
        await using var ctx = RequiredOwnedDbContext.Create(client);

        var act = async ()
            => await ctx
                .RequiredEntities
                .Select(r => r.Profile)
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Required owned navigation*missing or NULL*");
    }

    [Fact]
    public void ProjectionBinding_ForOwnedType_WithoutNavigationMetadata_ThrowsClearError()
    {
        using var ctx = AmbiguousModelDbContext.Create(Substitute.For<IAmazonDynamoDB>());
        var model = ctx.Model;

        var selectExpression = new SelectExpression("EntityATable");
        var projectionMember = new ProjectionMember();
        selectExpression.ReplaceProjectionMapping(
            new Dictionary<ProjectionMember, Expression>
            {
                [projectionMember] = Expression.Constant(0),
            });
        selectExpression.AddToProjection(
            new ProjectionExpression(
                new SqlPropertyExpression("Profile", typeof(SharedProfile), null),
                "Profile"));

        var visitor = new DynamoProjectionBindingRemovingExpressionVisitor(
            Expression.Parameter(typeof(Dictionary<string, AttributeValue>), "item"),
            selectExpression,
            model);

        var act = ()
            => visitor.Visit(
                new ProjectionBindingExpression(
                    selectExpression,
                    projectionMember,
                    typeof(SharedProfile)));

        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*without navigation metadata*DynamoObjectAccessExpression*");
    }

    // Shared owned CLR type used by both root entities to trigger the ambiguity scenario.
    private sealed record SharedProfile
    {
        public int? Age { get; set; }
        public string DisplayName { get; set; } = null!;
    }

    private sealed record EntityA
    {
        public string Pk { get; set; } = null!;
        public SharedProfile? Profile { get; set; }
    }

    private sealed record EntityB
    {
        public string Pk { get; set; } = null!;
        public SharedProfile? Profile { get; set; }
    }

    private sealed record RequiredEntity
    {
        public string Pk { get; set; } = null!;
        public SharedProfile Profile { get; set; } = null!;
    }

    // DbContext that registers both EntityA and EntityB, both owning SharedProfile.
    // This creates the ambiguous model: two navigations named "Profile" targeting the same CLR
    // type.
    private sealed class AmbiguousModelDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<EntityA> EntityAs { get; set; }
        public DbSet<EntityB> EntityBs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntityA>(b =>
            {
                b.ToTable("EntityATable");
                b.HasKey(x => x.Pk);
                b.HasPartitionKey(x => x.Pk);
                b.OwnsOne(x => x.Profile);
            });

            modelBuilder.Entity<EntityB>(b =>
            {
                b.ToTable("EntityBTable");
                b.HasKey(x => x.Pk);
                b.HasPartitionKey(x => x.Pk);
                b.OwnsOne(x => x.Profile);
            });
        }

        public static AmbiguousModelDbContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<AmbiguousModelDbContext>()
                    .UseDynamo(o => o.DynamoDbClient(client))
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }

    /// <summary>DbContext with a required owned-reference navigation used to validate requiredness errors.</summary>
    private sealed class RequiredOwnedDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<RequiredEntity> RequiredEntities { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RequiredEntity>(b =>
            {
                b.ToTable("RequiredEntityTable");
                b.HasKey(x => x.Pk);
                b.HasPartitionKey(x => x.Pk);
                b.OwnsOne(x => x.Profile);
                b.Navigation(x => x.Profile).IsRequired();
            });

        public static RequiredOwnedDbContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<RequiredOwnedDbContext>()
                    .UseDynamo(o => o.DynamoDbClient(client))
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }
}
