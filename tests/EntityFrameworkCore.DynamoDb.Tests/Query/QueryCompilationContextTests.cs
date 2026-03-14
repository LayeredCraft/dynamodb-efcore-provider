using System.Diagnostics.CodeAnalysis;
using EntityFrameworkCore.DynamoDb.Query;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>Represents the QueryCompilationContextTests type.</summary>
[SuppressMessage("Usage", "EF1001:Internal EF Core API usage.")]
/// <summary>Represents the QueryCompilationContextTests type.</summary>
public class QueryCompilationContextTests
{
    private sealed class TestDbContext : DbContext
    {
        /// <summary>Provides functionality for this member.</summary>
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<TestEntity>(b =>
            {
                b.HasPartitionKey(x => x.PK);
            });
    }

    private sealed class TestEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public int PK { get; set; }
    }

    private static QueryCompilationContextDependencies CreateDependencies()
    {
        var dbContextOptionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        dbContextOptionsBuilder.UseDynamo();
        var dbContextOptions = dbContextOptionsBuilder.Options;

        var dbContext = new TestDbContext(dbContextOptions);
        return dbContext.GetService<QueryCompilationContextDependencies>();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void DefaultValues_AreCorrect()
    {
        var dependencies = CreateDependencies();

        var context = new DynamoQueryCompilationContext(dependencies, true);

        context.PageSizeOverride.Should().BeNull();
        context.PaginationDisabled.Should().BeFalse();
        context.ExplicitIndexName.Should().BeNull();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void PageSizeOverride_CanBeSet()
    {
        var dependencies = CreateDependencies();

        var context =
            new DynamoQueryCompilationContext(dependencies, true) { PageSizeOverride = 100 };

        context.PageSizeOverride.Should().Be(100);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void PaginationDisabled_CanBeSet()
    {
        var dependencies = CreateDependencies();

        var context =
            new DynamoQueryCompilationContext(dependencies, true) { PaginationDisabled = true };

        context.PaginationDisabled.Should().BeTrue();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void BothProperties_CanBeSetIndependently()
    {
        var dependencies = CreateDependencies();

        var context = new DynamoQueryCompilationContext(dependencies, true)
        {
            PageSizeOverride = 50, PaginationDisabled = true,
        };

        context.PageSizeOverride.Should().Be(50);
        context.PaginationDisabled.Should().BeTrue();
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public void ExplicitIndexName_CanBeSet()
    {
        var dependencies = CreateDependencies();

        var context = new DynamoQueryCompilationContext(dependencies, true)
        {
            ExplicitIndexName = "ByCustomerCreatedAt",
        };

        context.ExplicitIndexName.Should().Be("ByCustomerCreatedAt");
    }
}
