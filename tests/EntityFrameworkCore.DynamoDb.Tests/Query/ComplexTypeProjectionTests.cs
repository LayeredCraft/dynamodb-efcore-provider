using System.ComponentModel.DataAnnotations.Schema;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>
///     Tests for complex type property projection, covering materialisation of nullable and required
///     complex properties, anonymous type projections, WHERE null comparisons, and entities sharing
///     the same CLR type for their complex property.
/// </summary>
public class ComplexTypeProjectionTests
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

    /// <summary>Creates a mocked client that returns one item and captures the request.</summary>
    private static IAmazonDynamoDB CreateMockClientReturning(
        Dictionary<string, AttributeValue> item,
        Action<ExecuteStatementRequest> captureRequest)
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Do(captureRequest), Arg.Any<CancellationToken>())
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
        var item = new Dictionary<string, AttributeValue> { ["pk"] = new() { S = pk } };

        if (includeProfileAttribute)
            item["profile"] = profileIsNull
                ? new AttributeValue { NULL = true }
                : new AttributeValue { M = profileMap ?? new Dictionary<string, AttributeValue>() };

        return item;
    }

    /// <summary>Selecting a complex property from EntityA materialises the nested map correctly.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SelectComplexProperty_EntityA_ReturnsCorrectProfile()
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "A#1" },
            ["profile"] = new()
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["displayName"] = new() { S = "Ada" }, ["age"] = new() { N = "39" },
                },
            },
        };

        var client = CreateMockClientReturning(item);
        await using var ctx = SharedProfileDbContext.Create(client);

        var profiles =
            await ctx
                .EntityAs
                .Select(a => a.Profile)
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        profiles.Should().HaveCount(1);
        profiles[0].Should().NotBeNull();
        profiles[0]!.DisplayName.Should().Be("Ada");
        profiles[0]!.Age.Should().Be(39);
    }

    /// <summary>Selecting from EntityB resolves its own complex property, not EntityA's.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SelectComplexProperty_EntityB_ReturnsCorrectProfile()
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "B#1" },
            ["profile"] = new()
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["displayName"] = new() { S = "Bob" }, ["age"] = new() { N = "25" },
                },
            },
        };

        var client = CreateMockClientReturning(item);
        await using var ctx = SharedProfileDbContext.Create(client);

        var profiles =
            await ctx
                .EntityBs
                .Select(b => b.Profile)
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        profiles.Should().HaveCount(1);
        profiles[0].Should().NotBeNull();
        profiles[0]!.DisplayName.Should().Be("Bob");
        profiles[0]!.Age.Should().Be(25);
    }

    /// <summary>Anonymous type projection { Pk, Profile } exercises index-based binding.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SelectAnonymousWithComplexProperty_MaterialisesCorrectly()
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new() { S = "A#2" },
            ["profile"] = new()
            {
                M = new Dictionary<string, AttributeValue>
                {
                    ["displayName"] =
                        new() { S = "Cleo" },
                    ["age"] = new() { N = "42" },
                },
            },
        };

        var client = CreateMockClientReturning(item);
        await using var ctx = SharedProfileDbContext.Create(client);

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

    /// <summary>EF.Property&lt;T&gt; access to a complex property materialises it correctly.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SelectComplexProperty_WithEfProperty_ReturnsCorrectProfile()
    {
        var item = CreateItem(
            "A#3",
            new Dictionary<string, AttributeValue>
            {
                ["displayName"] = new() { S = "Dora" }, ["age"] = new() { N = "31" },
            });

        var client = CreateMockClientReturning(item);
        await using var ctx = SharedProfileDbContext.Create(client);

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

    /// <summary>Anonymous type with EF.Property access to a complex property materialises correctly.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SelectAnonymousWithEfPropertyComplexProperty_MaterialisesCorrectly()
    {
        var item = CreateItem(
            "A#4",
            new Dictionary<string, AttributeValue>
            {
                ["displayName"] = new() { S = "Eve" }, ["age"] = new() { N = "28" },
            });

        var client = CreateMockClientReturning(item);
        await using var ctx = SharedProfileDbContext.Create(client);

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

    /// <summary>
    ///     Nested scalar projection from a nullable complex property returns null when the complex
    ///     map attribute is missing.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SelectNestedScalarFromComplexProperty_AttributeMissing_ReturnsNull()
    {
        var item = CreateItem("A#4b", null, false);

        var client = CreateMockClientReturning(item);
        await using var ctx = SharedProfileDbContext.Create(client);

        var displayNames =
            await ctx
                .EntityAs
                .Select(a => a.Profile!.DisplayName)
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        displayNames.Should().HaveCount(1);
        displayNames[0].Should().BeNull();
    }

    /// <summary>When the complex property map attribute is absent, null is returned for a nullable property.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SelectComplexProperty_AttributeMissing_ReturnsNullForNullableProperty()
    {
        var item = CreateItem("A#5", null, false);

        var client = CreateMockClientReturning(item);
        await using var ctx = SharedProfileDbContext.Create(client);

        var profiles =
            await ctx
                .EntityAs
                .Select(a => a.Profile)
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        profiles.Should().HaveCount(1);
        profiles[0].Should().BeNull();
    }

    /// <summary>When the complex property map attribute is DynamoDB NULL, null is returned for a nullable property.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SelectComplexProperty_AttributeIsNull_ReturnsNullForNullableProperty()
    {
        var item = CreateItem("A#6", null, true, true);

        var client = CreateMockClientReturning(item);
        await using var ctx = SharedProfileDbContext.Create(client);

        var profiles =
            await ctx
                .EntityAs
                .Select(a => a.Profile)
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        profiles.Should().HaveCount(1);
        profiles[0].Should().BeNull();
    }

    /// <summary>
    ///     Nested predicate access on an explicitly configured complex property includes the root
    ///     map attribute in the generated PartiQL path.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Where_NestedComplexProperty_UsesRootAttributeName()
    {
        ExecuteStatementRequest? capturedRequest = null;
        var item = CreateItem(
            "A#6b",
            new Dictionary<string, AttributeValue>
            {
                ["displayName"] = new() { S = "Ada" }, ["age"] = new() { N = "39" },
            });

        var client = CreateMockClientReturning(item, r => capturedRequest = r);
        await using var ctx = SharedProfileDbContext.Create(client);

        var results =
            await ctx
                .EntityAs
                .Where(a => a.Profile!.DisplayName == "Ada")
                .Select(a => a.Pk)
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        results.Should().Equal("A#6b");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Statement.Should().Contain("\"profile\".\"displayName\" = 'Ada'");
    }

    /// <summary>WHERE clause comparing a nullable complex property to null translates to IS NULL OR IS MISSING.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task Where_NullableComplexPropertyIsNull_TranslatesToNullOrMissingPredicate()
    {
        ExecuteStatementRequest? capturedRequest = null;
        var item = CreateItem("A#7", null, false);

        var client = CreateMockClientReturning(item, r => capturedRequest = r);
        await using var ctx = SharedProfileDbContext.Create(client);

        var results =
            await ctx
                .EntityAs
                .Where(a => a.Profile == null)
                .Select(a => a.Pk)
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        results.Should().Equal("A#7");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Statement.Should().Contain("\"profile\" IS NULL");
        capturedRequest.Statement.Should().Contain("\"profile\" IS MISSING");
    }

    /// <summary>WHERE via EF.Property comparing a nullable complex property to null translates to IS NULL OR IS MISSING.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task
        Where_EfPropertyNullableComplexPropertyIsNull_TranslatesToNullOrMissingPredicate()
    {
        ExecuteStatementRequest? capturedRequest = null;
        var item = CreateItem("A#8", null, false);

        var client = CreateMockClientReturning(item, r => capturedRequest = r);
        await using var ctx = SharedProfileDbContext.Create(client);

        var results =
            await ctx
                .EntityAs
                .Where(a => EF.Property<SharedProfile?>(a, "Profile") == null)
                .Select(a => a.Pk)
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        results.Should().Equal("A#8");
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Statement.Should().Contain("\"profile\" IS NULL");
        capturedRequest.Statement.Should().Contain("\"profile\" IS MISSING");
    }

    /// <summary>Required (non-nullable) complex property missing from the DynamoDB item throws a clear error.</summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task SelectComplexProperty_RequiredAttributeMissing_ThrowsClearError()
    {
        var item = CreateItem("R#1", null, false);

        var client = CreateMockClientReturning(item);
        await using var ctx = RequiredComplexPropertyDbContext.Create(client);

        var act = async ()
            => await ctx
                .RequiredEntities
                .Select(r => r.Profile)
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Required complex property*missing or NULL*");
    }

    // Shared complex CLR type used by both root entity types.
    private sealed record SharedProfile
    {
        /// <summary>Age of the profile subject.</summary>
        public int? Age { get; set; }

        /// <summary>Display name of the profile subject.</summary>
        public string DisplayName { get; set; } = null!;
    }

    private sealed record EntityA
    {
        /// <summary>Partition key.</summary>
        public string Pk { get; set; } = null!;

        /// <summary>Nullable complex property.</summary>
        public SharedProfile? Profile { get; set; }
    }

    private sealed record EntityB
    {
        /// <summary>Partition key.</summary>
        public string Pk { get; set; } = null!;

        /// <summary>Nullable complex property.</summary>
        public SharedProfile? Profile { get; set; }
    }

    private sealed record RequiredEntity
    {
        /// <summary>Partition key.</summary>
        public string Pk { get; set; } = null!;

        /// <summary>Non-nullable complex property — required in the DynamoDB item.</summary>
        public SharedProfile Profile { get; set; } = null!;
    }

    // DbContext with EntityA and EntityB each declaring a complex property of the same CLR type.
    private sealed class SharedProfileDbContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>EntityA set.</summary>
        public DbSet<EntityA> EntityAs { get; set; }

        /// <summary>EntityB set.</summary>
        public DbSet<EntityB> EntityBs { get; set; }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<EntityA>(b =>
            {
                b.ToTable("EntityATable");
                b.HasPartitionKey(x => x.Pk);
                b.ComplexProperty(x => x.Profile);
            });

            modelBuilder.Entity<EntityB>(b =>
            {
                b.ToTable("EntityBTable");
                b.HasPartitionKey(x => x.Pk);
                b.ComplexProperty(x => x.Profile);
            });
        }

        /// <summary>Creates a context backed by the given mock DynamoDB client.</summary>
        public static SharedProfileDbContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<SharedProfileDbContext>()
                    .UseDynamo(o => o.DynamoDbClient(client))
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }

    /// <summary>DbContext with a required (non-nullable) complex property to validate requiredness errors.</summary>
    private sealed class RequiredComplexPropertyDbContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>RequiredEntity set.</summary>
        public DbSet<RequiredEntity> RequiredEntities { get; set; }

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<RequiredEntity>(b =>
            {
                b.ToTable("RequiredEntityTable");
                b.HasPartitionKey(x => x.Pk);
                b.ComplexProperty(x => x.Profile);
            });

        /// <summary>Creates a context backed by the given mock DynamoDB client.</summary>
        public static RequiredComplexPropertyDbContext Create(IAmazonDynamoDB client)
            => new(
                new DbContextOptionsBuilder<RequiredComplexPropertyDbContext>()
                    .UseDynamo(o => o.DynamoDbClient(client))
                    .ConfigureWarnings(w
                        => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                    .Options);
    }
}
