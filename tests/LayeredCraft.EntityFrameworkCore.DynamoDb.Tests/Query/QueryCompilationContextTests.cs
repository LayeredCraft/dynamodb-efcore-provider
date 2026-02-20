using System.Diagnostics.CodeAnalysis;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Query;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Tests.Query;

[SuppressMessage("Usage", "EF1001:Internal EF Core API usage.")]
public class QueryCompilationContextTests
{
    private sealed class TestDbContext : DbContext
    {
        public TestDbContext(DbContextOptions<TestDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<TestEntity>(b =>
            {
                b.HasKey(x => x.PK);
            });
    }

    private sealed class TestEntity
    {
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

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var dependencies = CreateDependencies();

        var context = new DynamoQueryCompilationContext(dependencies, true);

        context.PageSizeOverride.Should().BeNull();
        context.PaginationDisabled.Should().BeFalse();
    }

    [Fact]
    public void PageSizeOverride_CanBeSet()
    {
        var dependencies = CreateDependencies();

        var context =
            new DynamoQueryCompilationContext(dependencies, true) { PageSizeOverride = 100 };

        context.PageSizeOverride.Should().Be(100);
    }

    [Fact]
    public void PaginationDisabled_CanBeSet()
    {
        var dependencies = CreateDependencies();

        var context =
            new DynamoQueryCompilationContext(dependencies, true) { PaginationDisabled = true };

        context.PaginationDisabled.Should().BeTrue();
    }

    [Fact]
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
}
