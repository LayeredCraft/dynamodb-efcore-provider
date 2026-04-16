using System.Diagnostics.CodeAnalysis;
using EntityFrameworkCore.DynamoDb.Query;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

/// <summary>Unit tests for DynamoQueryCompilationContext.</summary>
[SuppressMessage("Usage", "EF1001:Internal EF Core API usage.")]
public class QueryCompilationContextTests
{
    private sealed class TestDbContext : DbContext
    {
        /// <summary>Provides functionality for this member.</summary>
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        /// <summary>Provides functionality for this member.</summary>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<TestEntity>(b => b.HasPartitionKey(x => x.PK));
    }

    private sealed class TestEntity
    {
        /// <summary>Provides functionality for this member.</summary>
        public int PK { get; set; }
    }

    private static QueryCompilationContextDependencies CreateDependencies()
    {
        var dbContextOptionsBuilder = new DbContextOptionsBuilder<TestDbContext>();
        dbContextOptionsBuilder
            .UseDynamo()
            .ConfigureWarnings(w => w.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
        var dbContext = new TestDbContext(dbContextOptionsBuilder.Options);
        return dbContext.GetService<QueryCompilationContextDependencies>();
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var dependencies = CreateDependencies();

        var context = new DynamoQueryCompilationContext(dependencies, true);

        context.ExplicitIndexName.Should().BeNull();
        context.IndexSelectionDisabled.Should().BeFalse();
    }

    [Fact]
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
