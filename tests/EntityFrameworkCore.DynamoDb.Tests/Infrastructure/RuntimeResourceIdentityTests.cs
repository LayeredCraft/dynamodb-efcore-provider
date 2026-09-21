using EntityFrameworkCore.DynamoDb.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;

namespace EntityFrameworkCore.DynamoDb.Tests.Infrastructure;

/// <summary>
///     The effective physical resource identities that result from a runtime mapping must be
///     consistent for the whole table, not only for the resources the mapping mentions, and the
///     logical table identity must be resolved per table group.
/// </summary>
public class RuntimeResourceIdentityTests
{
    private static IModel Initialize<TContext>(
        Func<DbContextOptions<TContext>, TContext> create,
        Action<DynamoRuntimeResourceNamesBuilder>? map)
        where TContext : DbContext
    {
        var context = create(
            new DbContextOptionsBuilder<TContext>()
                .UseDynamo(configure =>
                {
                    if (map is not null)
                        configure.RuntimeResourceNames(map);
                })
                .ConfigureWarnings(warnings
                    => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning))
                .Options);
        return context.Model;
    }

    private static IModel Collide(Action<DynamoRuntimeResourceNamesBuilder>? map)
        => Initialize<CollisionContext>(options => new CollisionContext(options), map);

    // ----- mapped index versus an unmapped index's physical name ---------------------------------

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Mapped_index_onto_an_unmapped_index_with_an_equivalent_signature_fails_fast()
    {
        // ByA and ByA2 index the same property, so the runtime table model alone would silently merge
        // them into one physical index.
        var act = () => Collide(names => names.SecondaryIndex("Collide", "ByA", "a2-gsi"));

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*logical table 'Collide'*physical index 'a2-gsi'*'ByA'*mapped*'ByA2'*not mapped*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Mapped_index_onto_an_unmapped_index_with_a_different_signature_fails_fast_with_an_actionable_message()
    {
        var act = () => Collide(names => names.SecondaryIndex("Collide", "ByA", "b-gsi"));

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*logical table 'Collide'*physical index 'b-gsi'*'ByA'*mapped*'ByB'*not mapped*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Unmapped_index_first_in_the_model_is_detected_the_same_way()
    {
        var act = () => Collide(names => names.SecondaryIndex("Collide", "ByA2", "a-gsi"));

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*physical index 'a-gsi'*'ByA2'*mapped*'ByA'*not mapped*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Two_mapped_indexes_resolving_to_one_physical_index_fail_fast()
    {
        var act = () => Collide(names => names
            .SecondaryIndex("Collide", "ByA", "same-gsi")
            .SecondaryIndex("Collide", "ByB", "same-gsi"));

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*physical index 'same-gsi'*'ByA'*'ByB'*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Swapping_the_physical_names_of_two_indexes_is_not_a_collision()
    {
        var model = Collide(names => names
            .SecondaryIndex("Collide", "ByA", "a2-gsi")
            .SecondaryIndex("Collide", "ByA2", "a-gsi"));

        var entityType = model.FindEntityType(typeof(CollisionRow))!;
        entityType
            .GetIndexes()
            .ToDictionary(static index => index.Name!, static index => index.GetSecondaryIndexName())
            .Should()
            .Equal(
                new Dictionary<string, string?>
                {
                    ["ByA"] = "a2-gsi",
                    ["ByA2"] = "a-gsi",
                    ["ByB"] = "b-gsi"
                });
    }

    // ----- indexes shared by several entity types of one table ----------------------------------

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Equivalent_declarations_of_one_logical_index_across_entity_types_are_one_index()
    {
        var model = Initialize<SharedSameNameContext>(options => new SharedSameNameContext(options),
            names => names
                .Table("SharedOwner", "real-owner-table")
                .SecondaryIndex("SharedOwner", "ByOwner", "real-owner-gsi"));

        model
            .GetEntityTypes()
            .SelectMany(static entityType => entityType.GetDeclaredIndexes())
            .Select(static index => index.GetSecondaryIndexName())
            .Should()
            .Equal("real-owner-gsi", "real-owner-gsi");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void One_physical_index_declared_under_different_logical_names_can_be_mapped_consistently()
    {
        var model = Initialize<SharedDifferentNamesContext>(options => new SharedDifferentNamesContext(options),
            names => names
                .SecondaryIndex("SharedOwner", "ByOwner", "real-owner-gsi")
                .SecondaryIndex("SharedOwner", "OwnerIdx", "real-owner-gsi"));

        model
            .GetEntityTypes()
            .SelectMany(static entityType => entityType.GetDeclaredIndexes())
            .Select(static index => index.GetSecondaryIndexName())
            .Should()
            .Equal("real-owner-gsi", "real-owner-gsi");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Mapping_only_one_of_the_logical_names_of_a_shared_physical_index_fails_fast()
    {
        var act = () => Initialize<SharedDifferentNamesContext>(options => new SharedDifferentNamesContext(options),
            names => names.SecondaryIndex("SharedOwner", "ByOwner", "real-owner-gsi"));

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*physical index 'owner-gsi'*'ByOwner'*'OwnerIdx'*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Mapping_the_logical_names_of_a_shared_physical_index_to_different_names_fails_fast()
    {
        var act = () => Initialize<SharedDifferentNamesContext>(options => new SharedDifferentNamesContext(options),
            names => names
                .SecondaryIndex("SharedOwner", "ByOwner", "real-a")
                .SecondaryIndex("SharedOwner", "OwnerIdx", "real-b"));

        act.Should()
            .Throw<InvalidOperationException>()
            .WithMessage("*physical index 'owner-gsi'*'ByOwner'*'OwnerIdx'*");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Shared_indexes_are_unchanged_when_no_mapping_is_configured()
    {
        var model = Initialize<SharedDifferentNamesContext>(options => new SharedDifferentNamesContext(options), map: null);

        model
            .GetEntityTypes()
            .SelectMany(static entityType => entityType.GetDeclaredIndexes())
            .Select(static index => index.GetSecondaryIndexName())
            .Should()
            .Equal("owner-gsi", "owner-gsi");
    }

    // ----- GetLogicalTableName is a table-group property ----------------------------------------

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Logical_table_identity_declared_on_one_root_is_returned_for_every_root_of_the_table()
    {
        var model = Initialize<GetterContext>(options => new GetterContext(options), map: null);

        model.FindEntityType(typeof(GetterRootA))!.GetLogicalTableName().Should().Be("SharedGetter");
        model.FindEntityType(typeof(GetterRootB))!.GetLogicalTableName().Should().Be("SharedGetter");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void A_derived_entity_sharing_its_base_table_returns_the_tables_logical_identity()
    {
        var model = Initialize<GetterContext>(options => new GetterContext(options), map: null);

        model.FindEntityType(typeof(GetterDerived))!.GetLogicalTableName().Should().Be("BaseLogical");
        model.FindEntityType(typeof(GetterBase))!.GetLogicalTableName().Should().Be("BaseLogical");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void A_derived_entity_in_another_table_does_not_inherit_the_base_tables_logical_identity()
    {
        var model = Initialize<GetterContext>(options => new GetterContext(options), map: null);

        model.FindEntityType(typeof(GetterDerivedElsewhere))!.GetLogicalTableName().Should().BeNull();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void An_entity_of_a_table_without_a_logical_identity_returns_null()
    {
        var model = Initialize<GetterContext>(options => new GetterContext(options), map: null);

        model.FindEntityType(typeof(GetterPlain))!.GetLogicalTableName().Should().BeNull();
    }
}

// ----- models -------------------------------------------------------------------------------------

/// <summary>One table with three indexes: two with the same key property, one with a different one.</summary>
public sealed class CollisionContext(DbContextOptions<CollisionContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.Entity<CollisionRow>(entity =>
        {
            entity.ToTable("collision-table").HasLogicalTableName("Collide");
            entity.HasPartitionKey(row => row.Pk);
            entity.HasGlobalSecondaryIndex("ByA", nameof(CollisionRow.A)).HasSecondaryIndexName("a-gsi");
            entity.HasGlobalSecondaryIndex("ByA2", nameof(CollisionRow.A)).HasSecondaryIndexName("a2-gsi");
            entity.HasGlobalSecondaryIndex("ByB", nameof(CollisionRow.B)).HasSecondaryIndexName("b-gsi");
        });
}

public sealed class CollisionRow
{
    public string Pk { get; set; } = string.Empty;

    public string A { get; set; } = string.Empty;

    public string B { get; set; } = string.Empty;
}

/// <summary>Two entity types share a table and both declare the same logical index.</summary>
public sealed class SharedSameNameContext(DbContextOptions<SharedSameNameContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OwnerRowOne>(entity =>
        {
            entity.ToTable("shared-owner-table").HasLogicalTableName("SharedOwner");
            entity.HasPartitionKey(row => row.Pk);
            entity
                .HasGlobalSecondaryIndex("ByOwner", nameof(OwnerRowOne.Owner))
                .HasSecondaryIndexName("owner-gsi");
        });
        modelBuilder.Entity<OwnerRowTwo>(entity =>
        {
            entity.ToTable("shared-owner-table");
            entity.HasPartitionKey(row => row.Pk);
            entity
                .HasGlobalSecondaryIndex("ByOwner", nameof(OwnerRowTwo.Owner))
                .HasSecondaryIndexName("owner-gsi");
        });
    }
}

/// <summary>
///     Two entity types share a table and one physical index but give the EF index different names.
/// </summary>
public sealed class SharedDifferentNamesContext(DbContextOptions<SharedDifferentNamesContext> options)
    : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OwnerRowOne>(entity =>
        {
            entity.ToTable("shared-owner-table").HasLogicalTableName("SharedOwner");
            entity.HasPartitionKey(row => row.Pk);
            entity
                .HasGlobalSecondaryIndex("ByOwner", nameof(OwnerRowOne.Owner))
                .HasSecondaryIndexName("owner-gsi");
        });
        modelBuilder.Entity<OwnerRowTwo>(entity =>
        {
            entity.ToTable("shared-owner-table");
            entity.HasPartitionKey(row => row.Pk);
            entity
                .HasGlobalSecondaryIndex("OwnerIdx", nameof(OwnerRowTwo.Owner))
                .HasSecondaryIndexName("owner-gsi");
        });
    }
}

public sealed class OwnerRowOne
{
    public string Pk { get; set; } = string.Empty;

    public string Owner { get; set; } = string.Empty;
}

public sealed class OwnerRowTwo
{
    public string Pk { get; set; } = string.Empty;

    public string Owner { get; set; } = string.Empty;
}

/// <summary>Table-group shapes for <c>GetLogicalTableName</c>.</summary>
public sealed class GetterContext(DbContextOptions<GetterContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Two roots share a table; only one declares the logical identity.
        modelBuilder.Entity<GetterRootA>(entity =>
        {
            entity.ToTable("shared-getter").HasLogicalTableName("SharedGetter");
            entity.HasPartitionKey(row => row.Pk);
        });
        modelBuilder.Entity<GetterRootB>(entity =>
        {
            entity.ToTable("shared-getter");
            entity.HasPartitionKey(row => row.Pk);
        });

        // A hierarchy: one derived type shares the base table, another is mapped elsewhere.
        modelBuilder.Entity<GetterBase>(entity =>
        {
            entity.ToTable("base-table").HasLogicalTableName("BaseLogical");
            entity.HasPartitionKey(row => row.Pk);
        });
        modelBuilder.Entity<GetterDerived>();
        modelBuilder.Entity<GetterDerivedElsewhere>(entity => entity.ToTable("elsewhere-table"));

        // A table with no logical identity.
        modelBuilder.Entity<GetterPlain>(entity =>
        {
            entity.ToTable("plain-table");
            entity.HasPartitionKey(row => row.Pk);
        });
    }
}

public sealed class GetterRootA
{
    public string Pk { get; set; } = string.Empty;
}

public sealed class GetterRootB
{
    public string Pk { get; set; } = string.Empty;
}

public class GetterBase
{
    public string Pk { get; set; } = string.Empty;
}

public sealed class GetterDerived : GetterBase;

public sealed class GetterDerivedElsewhere : GetterBase;

public sealed class GetterPlain
{
    public string Pk { get; set; } = string.Empty;
}
