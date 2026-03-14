using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>Represents the DiscriminatorMaterializationSafetyTests type.</summary>
public class DiscriminatorMaterializationSafetyTests
{
    private static DbContextOptions BuildOptions<T>(IAmazonDynamoDB client) where T : DbContext
        => new DbContextOptionsBuilder<T>()
            .UseDynamo(o => o.DynamoDbClient(client))
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
            .Options;

    private static IAmazonDynamoDB CreateMockClient(
        IReadOnlyList<Dictionary<string, AttributeValue>> items)
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(new ExecuteStatementResponse { Items = [.. items] });

        return client;
    }

    private static IAmazonDynamoDB CreateProjectionRespectingMockClient(
        IReadOnlyList<Dictionary<string, AttributeValue>> items)
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        client
            .ExecuteStatementAsync(Arg.Any<ExecuteStatementRequest>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<ExecuteStatementRequest>();
                var selectedAttributes = ParseSelectedAttributes(request.Statement);

                List<Dictionary<string, AttributeValue>> projectedItems = [];
                foreach (var item in items)
                {
                    Dictionary<string, AttributeValue> projectedItem = [];
                    foreach (var selectedAttribute in selectedAttributes)
                        if (item.TryGetValue(selectedAttribute, out var value))
                            projectedItem[selectedAttribute] = value;

                    projectedItems.Add(projectedItem);
                }

                return new ExecuteStatementResponse { Items = projectedItems };
            });

        return client;
    }

    /// <summary>Parses SELECT list identifiers from a generated PartiQL statement.</summary>
    private static HashSet<string> ParseSelectedAttributes(string statement)
    {
        var selectIndex = statement.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
        var fromIndex = statement.IndexOf("FROM", StringComparison.OrdinalIgnoreCase);
        if (selectIndex < 0 || fromIndex <= selectIndex)
            return [];

        var projectionSegment = statement[(selectIndex + "SELECT".Length)..fromIndex];
        HashSet<string> attributes = new(StringComparer.Ordinal);
        foreach (var segment in projectionSegment.Split(','))
        {
            var identifier = segment.Trim();
            if (identifier.Length == 0)
                continue;

            if (identifier.StartsWith('"') && identifier.EndsWith('"'))
                identifier = identifier[1..^1];

            attributes.Add(identifier);
        }

        return attributes;
    }

    private sealed record UserEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string Name { get; set; } = null!;
    }

    private sealed record OrderEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string Description { get; set; } = null!;
    }

    private sealed class SharedTableContext(DbContextOptions options) : DbContext(options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<UserEntity> Users => Set<UserEntity>();

        /// <summary>Provides functionality for this member.</summary>
        public DbSet<OrderEntity> Orders => Set<OrderEntity>();

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserEntity>(b =>
            {
                b.ToTable("app-table");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
            });

            modelBuilder.Entity<OrderEntity>(b =>
            {
                b.ToTable("app-table");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
            });
        }

        /// <summary>Provides functionality for this member.</summary>
        public static SharedTableContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SharedTableContext>(client));
    }

    private abstract record PersonEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string PK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string SK { get; set; } = null!;

        /// <summary>Provides functionality for this member.</summary>
        public string Name { get; set; } = null!;
    }

    private sealed record EmployeeEntity : PersonEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public string Department { get; set; } = null!;
    }

    private sealed record ManagerEntity : PersonEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public int Level { get; set; }
    }

    private sealed class SharedTableInheritanceContext(DbContextOptions options) : DbContext(
        options)
    {
        /// <summary>Provides functionality for this member.</summary>
        public DbSet<PersonEntity> People => Set<PersonEntity>();

        /// <summary>Provides functionality for this member.</summary>
        public DbSet<EmployeeEntity> Employees => Set<EmployeeEntity>();

        /// <summary>Provides functionality for this member.</summary>
        public DbSet<ManagerEntity> Managers => Set<ManagerEntity>();

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PersonEntity>(b =>
            {
                b.ToTable("app-table");
                b.HasPartitionKey(x => x.PK);
                b.HasSortKey(x => x.SK);
            });

            modelBuilder.Entity<EmployeeEntity>(b =>
            {
                b.ToTable("app-table");
                b.HasBaseType<PersonEntity>();
            });

            modelBuilder.Entity<ManagerEntity>(b =>
            {
                b.ToTable("app-table");
                b.HasBaseType<PersonEntity>();
            });
        }

        /// <summary>Provides functionality for this member.</summary>
        public static SharedTableInheritanceContext Create(IAmazonDynamoDB client)
            => new(BuildOptions<SharedTableInheritanceContext>(client));
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task SharedTableQuery_MissingDiscriminatorAttribute_Throws()
    {
        var client = CreateMockClient(
        [
            new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = "TENANT#1" },
                ["SK"] = new() { S = "USER#1" },
                ["Name"] = new() { S = "Ada" },
            },
        ]);

        await using var context = SharedTableContext.Create(client);

        var act = async ()
            => await context
                .Users
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Required property*$type*");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task SharedTableQuery_WrongDiscriminatorValue_Throws()
    {
        var client = CreateMockClient(
        [
            new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = "TENANT#1" },
                ["SK"] = new() { S = "ORDER#1" },
                ["Name"] = new() { S = "Ada" },
                ["$type"] = new() { S = "OrderEntity" },
            },
        ]);

        await using var context = SharedTableContext.Create(client);

        var act = async ()
            => await context
                .Users
                .AsAsyncEnumerable()
                .ToListAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*discriminat*");
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task
        SharedTableInheritanceQuery_ProjectsHierarchyAttributes_ForDerivedMaterialization()
    {
        var client = CreateProjectionRespectingMockClient(
        [
            new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = "TENANT#H" },
                ["SK"] = new() { S = "PERSON#EMP-1" },
                ["Name"] = new() { S = "Eve" },
                ["Department"] = new() { S = "Engineering" },
                ["$type"] = new() { S = "EmployeeEntity" },
            },
            new Dictionary<string, AttributeValue>
            {
                ["PK"] = new() { S = "TENANT#H" },
                ["SK"] = new() { S = "PERSON#MGR-1" },
                ["Name"] = new() { S = "Max" },
                ["Level"] = new() { N = "7" },
                ["$type"] = new() { S = "ManagerEntity" },
            },
        ]);

        await using var context = SharedTableInheritanceContext.Create(client);

        var results = await context
            .People
            .AsAsyncEnumerable()
            .ToListAsync(TestContext.Current.CancellationToken);

        results.Should().HaveCount(2);
        results.Should().ContainSingle(person => person is EmployeeEntity);
        results.Should().ContainSingle(person => person is ManagerEntity);

        var employee = results.OfType<EmployeeEntity>().Single();
        employee.Department.Should().Be("Engineering");

        var manager = results.OfType<ManagerEntity>().Single();
        manager.Level.Should().Be(7);
    }
}
