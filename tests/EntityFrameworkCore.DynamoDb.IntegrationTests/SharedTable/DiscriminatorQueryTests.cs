using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable;

/// <summary>Represents the DiscriminatorQueryTests type.</summary>
public class DiscriminatorQueryTests(SharedTableDynamoFixture fixture)
    : SharedTableTestBase<SharedTableDbContext>(fixture)
{
    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task Where_IncludesDiscriminatorPredicate_AndQuotedTableName()
    {
        var results =
            await Db.Users.Where(user => user.Pk == "TENANT#U").ToListAsync(CancellationToken);

        results.Should().HaveCount(2);
        results.Should().OnlyContain(user => user.Sk.StartsWith("USER#", StringComparison.Ordinal));

        AssertSql(
            """
            SELECT "Pk", "Sk", "Name", "$type"
            FROM "app-table"
            WHERE "Pk" = 'TENANT#U' AND "$type" = 'UserEntity'
            """);
    }

    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task SelectProjection_StillAppliesDiscriminatorPredicate()
    {
        var results =
            await Db
                .Users
                .Where(user => user.Pk == "TENANT#U")
                .Select(user => user.Pk)
                .ToListAsync(CancellationToken);

        results.Should().Equal("TENANT#U", "TENANT#U");

        AssertSql(
            """
            SELECT "Pk"
            FROM "app-table"
            WHERE "Pk" = 'TENANT#U' AND "$type" = 'UserEntity'
            """);
    }
}

/// <summary>Represents the DiscriminatorQueryCustomNameTests type.</summary>
public class DiscriminatorQueryCustomNameTests(SharedTableDynamoFixture fixture)
    : SharedTableTestBase<SharedTableCustomDiscriminatorNameDbContext>(fixture)
{
    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task UsesEmbeddedDiscriminatorNameOverrideInPredicate()
    {
        var results =
            await Db.Users.Where(user => user.Pk == "TENANT#U").ToListAsync(CancellationToken);

        results.Should().HaveCount(2);

        AssertSql(
            """
            SELECT "Pk", "Sk", "Name", "$kind"
            FROM "app-table"
            WHERE "Pk" = 'TENANT#U' AND "$kind" = 'UserEntity'
            """);
    }
}

/// <summary>Represents the DiscriminatorQuerySingleTypeTests type.</summary>
public class DiscriminatorQuerySingleTypeTests(SharedTableDynamoFixture fixture)
    : SharedTableTestBase<SharedTableSingleTypeDbContext>(fixture)
{
    /// <summary>Provides functionality for this member.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task SingleTypeTable_DoesNotInjectDiscriminatorPredicate()
    {
        var results =
            await Db.Users.Where(user => user.Pk == "TENANT#U").ToListAsync(CancellationToken);

        results.Should().HaveCount(2);

        AssertSql(
            """
            SELECT "Pk", "Sk", "Name"
            FROM "app-table"
            WHERE "Pk" = 'TENANT#U'
            """);
    }
}

/// <summary>Represents the DiscriminatorInheritanceQueryTests type.</summary>
public class DiscriminatorInheritanceQueryTests(SharedTableDynamoFixture fixture)
    : SharedTableTestBase<SharedTableInheritanceDbContext>(fixture)
{
    /// <summary>Verifies a base-type query materializes all concrete hierarchy types.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task BaseQuery_MaterializesConcreteHierarchyTypes()
    {
        var results =
            await Db.People.Where(person => person.Pk == "TENANT#H").ToListAsync(CancellationToken);

        results.Should().HaveCount(2);

        var employee = results.OfType<EmployeeEntity>().Single();
        employee.Name.Should().Be("Eve");
        employee.Department.Should().Be("Engineering");

        var manager = results.OfType<ManagerEntity>().Single();
        manager.Name.Should().Be("Max");
        manager.Level.Should().Be(7);

        AssertSql(
            """
            SELECT "Pk", "Sk", "Name", "Department", "ManagerLevel", "$type"
            FROM "app-table"
            WHERE "Pk" = 'TENANT#H' AND ("$type" = 'EmployeeEntity' OR "$type" = 'ManagerEntity')
            """);
    }

    /// <summary>Verifies discriminator OR conditions remain grouped when additional filters are combined.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task BaseQuery_WithAdditionalFilter_UsesGroupedDiscriminatorPredicate()
    {
        var results = await Db
            .People
            .Where(person => person.Pk == "TENANT#H")
            .Where(person => person.Name == "Eve")
            .ToListAsync(CancellationToken);

        results.Should().HaveCount(1);
        results[0].Should().BeOfType<EmployeeEntity>();

        AssertSql(
            """
            SELECT "Pk", "Sk", "Name", "Department", "ManagerLevel", "$type"
            FROM "app-table"
            WHERE "Pk" = 'TENANT#H' AND "Name" = 'Eve' AND ("$type" = 'EmployeeEntity' OR "$type" = 'ManagerEntity')
            """);
    }

    /// <summary>Verifies a derived-type query applies a derived discriminator predicate.</summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task DerivedQuery_UsesDerivedDiscriminatorPredicate()
    {
        var results =
            await Db
                .Employees
                .Where(employee => employee.Pk == "TENANT#H")
                .ToListAsync(CancellationToken);

        results.Should().HaveCount(1);
        results[0].Department.Should().Be("Engineering");

        AssertSql(
            """
            SELECT "Pk", "Sk", "Name", "Department", "$type"
            FROM "app-table"
            WHERE "Pk" = 'TENANT#H' AND "$type" = 'EmployeeEntity'
            """);
    }
}

/// <summary>Represents the DiscriminatorInheritanceBaseOnlyToTableQueryTests type.</summary>
public class DiscriminatorInheritanceBaseOnlyToTableQueryTests(SharedTableDynamoFixture fixture)
    : SharedTableTestBase<SharedTableInheritanceBaseOnlyToTableDbContext>(fixture)
{
    /// <summary>
    ///     Verifies a base-type query materializes all concrete hierarchy types when only the base
    ///     entity configures table mapping.
    /// </summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task BaseQuery_MaterializesConcreteHierarchyTypes_WhenOnlyBaseConfiguresTable()
    {
        var results =
            await Db.People.Where(person => person.Pk == "TENANT#H").ToListAsync(CancellationToken);

        results.Should().HaveCount(2);

        var employee = results.OfType<EmployeeEntity>().Single();
        employee.Name.Should().Be("Eve");
        employee.Department.Should().Be("Engineering");

        var manager = results.OfType<ManagerEntity>().Single();
        manager.Name.Should().Be("Max");
        manager.Level.Should().Be(7);

        AssertSql(
            """
            SELECT "Pk", "Sk", "Name", "Department", "ManagerLevel", "$type"
            FROM "app-table"
            WHERE "Pk" = 'TENANT#H' AND ("$type" = 'EmployeeEntity' OR "$type" = 'ManagerEntity')
            """);
    }

    /// <summary>
    ///     Verifies a derived query uses the root table mapping when only the base type configures
    ///     the table.
    /// </summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task DerivedQuery_UsesRootTableMapping_WhenOnlyBaseConfiguresTable()
    {
        var results = await Db
            .Employees
            .Where(employee => employee.Pk == "TENANT#H")
            .ToListAsync(CancellationToken);

        results.Should().HaveCount(1);
        results[0].Department.Should().Be("Engineering");

        AssertSql(
            """
            SELECT "Pk", "Sk", "Name", "Department", "$type"
            FROM "app-table"
            WHERE "Pk" = 'TENANT#H' AND "$type" = 'EmployeeEntity'
            """);
    }
}

/// <summary>Represents the DiscriminatorInheritanceWithIndexQueryTests type.</summary>
public class DiscriminatorInheritanceWithIndexQueryTests(SharedTableDynamoFixture fixture)
    : SharedTableTestBase<SharedTableInheritanceWithIndexesDbContext>(fixture)
{
    /// <summary>
    ///     Verifies explicit index validation for derived queries excludes sibling-only indexes in
    ///     the same inheritance root.
    /// </summary>
    [Fact]
    /// <summary>Provides functionality for this member.</summary>
    public async Task DerivedQuery_WithSiblingOnlyIndex_ThrowsBeforeExecution()
    {
        var act = async () => await Db
            .ArchivedWorkOrders
            .WithIndex("ByPriority")
            .ToListAsync(CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ByPriority*");
    }
}
