using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable;

public class DiscriminatorQueryTests(DynamoContainerFixture fixture)
    : SharedTableTestFixture(fixture)
{
    [Fact]
    public async Task Where_IncludesDiscriminatorPredicate_AndQuotedTableName()
    {
        var results =
            await Db.Users.Where(user => user.Pk == "TENANT#U").ToListAsync(CancellationToken);

        results.Should().HaveCount(2);
        results.Should().OnlyContain(user => user.Sk.StartsWith("USER#", StringComparison.Ordinal));

        AssertSql(
            """
            SELECT "Pk", "Sk", "$type", "Name"
            FROM "app-table"
            WHERE "Pk" = 'TENANT#U' AND "$type" = 'UserEntity'
            """);
    }

    [Fact]
    public async Task SelectProjection_StillAppliesDiscriminatorPredicate()
    {
        var results = await Db
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

public class DiscriminatorQueryCustomNameTests(DynamoContainerFixture fixture)
    : SharedTableTestFixture(fixture)
{
    private SharedTableCustomDiscriminatorNameDbContext CustomDiscriminatorDb
        => new(
            CreateOptions<SharedTableCustomDiscriminatorNameDbContext>(o
                => o.DynamoDbClient(Client)));

    [Fact]
    public async Task UsesEmbeddedDiscriminatorNameOverrideInPredicate()
    {
        var results = await CustomDiscriminatorDb
            .Users
            .Where(user => user.Pk == "TENANT#U")
            .ToListAsync(CancellationToken);

        results.Should().HaveCount(2);

        AssertSql(
            """
            SELECT "Pk", "Sk", "$kind", "Name"
            FROM "app-table"
            WHERE "Pk" = 'TENANT#U' AND "$kind" = 'UserEntity'
            """);
    }
}

public class DiscriminatorQuerySingleTypeTests(DynamoContainerFixture fixture)
    : SharedTableTestFixture(fixture)
{
    private SharedTableSingleTypeDbContext SingleTypeDb
        => new(CreateOptions<SharedTableSingleTypeDbContext>(o => o.DynamoDbClient(Client)));

    [Fact]
    public async Task SingleTypeTable_DoesNotInjectDiscriminatorPredicate()
    {
        var results = await SingleTypeDb
            .Users
            .Where(user => user.Pk == "TENANT#U")
            .ToListAsync(CancellationToken);

        results.Should().HaveCount(2);

        AssertSql(
            """
            SELECT "Pk", "Sk", "Name"
            FROM "app-table"
            WHERE "Pk" = 'TENANT#U'
            """);
    }
}

public class DiscriminatorInheritanceQueryTests(DynamoContainerFixture fixture)
    : SharedTableTestFixture(fixture)
{
    private SharedTableInheritanceDbContext InheritanceDb
        => new(CreateOptions<SharedTableInheritanceDbContext>(o => o.DynamoDbClient(Client)));

    [Fact]
    public async Task BaseQuery_MaterializesConcreteHierarchyTypes()
    {
        var results = await InheritanceDb
            .People
            .Where(person => person.Pk == "TENANT#H")
            .ToListAsync(CancellationToken);

        results.Should().HaveCount(2);

        var employee = results.OfType<EmployeeEntity>().Single();
        employee.Name.Should().Be("Eve");
        employee.Department.Should().Be("Engineering");

        var manager = results.OfType<ManagerEntity>().Single();
        manager.Name.Should().Be("Max");
        manager.Level.Should().Be(7);

        AssertSql(
            """
            SELECT "Pk", "Sk", "$type", "Name", "Department", "ManagerLevel"
            FROM "app-table"
            WHERE "Pk" = 'TENANT#H' AND ("$type" = 'EmployeeEntity' OR "$type" = 'ManagerEntity')
            """);
    }

    [Fact]
    public async Task BaseQuery_WithAdditionalFilter_UsesGroupedDiscriminatorPredicate()
    {
        var results = await InheritanceDb
            .People
            .Where(person => person.Pk == "TENANT#H")
            .Where(person => person.Name == "Eve")
            .ToListAsync(CancellationToken);

        results.Should().HaveCount(1);
        results[0].Should().BeOfType<EmployeeEntity>();

        AssertSql(
            """
            SELECT "Pk", "Sk", "$type", "Name", "Department", "ManagerLevel"
            FROM "app-table"
            WHERE "Pk" = 'TENANT#H' AND "Name" = 'Eve' AND ("$type" = 'EmployeeEntity' OR "$type" = 'ManagerEntity')
            """);
    }

    [Fact]
    public async Task DerivedQuery_UsesDerivedDiscriminatorPredicate()
    {
        var results = await InheritanceDb
            .Employees
            .Where(employee => employee.Pk == "TENANT#H")
            .ToListAsync(CancellationToken);

        results.Should().HaveCount(1);
        results[0].Department.Should().Be("Engineering");

        AssertSql(
            """
            SELECT "Pk", "Sk", "$type", "Name", "Department"
            FROM "app-table"
            WHERE "Pk" = 'TENANT#H' AND "$type" = 'EmployeeEntity'
            """);
    }

    [Fact]
    public async Task DerivedQuery_FirstOrDefault_PkAndSkEquality_ReturnsMatchingDerivedItem()
    {
        var result = await InheritanceDb
            .Employees
            .Where(employee => employee.Pk == "TENANT#H" && employee.Sk == "PERSON#EMP-1")
            .FirstOrDefaultAsync(CancellationToken);

        result.Should().NotBeNull();
        result!.Department.Should().Be("Engineering");

        AssertSql(
            """
            SELECT "Pk", "Sk", "$type", "Name", "Department"
            FROM "app-table"
            WHERE "Pk" = 'TENANT#H' AND "Sk" = 'PERSON#EMP-1' AND "$type" = 'EmployeeEntity'
            """);
    }

    [Fact]
    public async Task DerivedQuery_FirstOrDefault_PkOnly_ThrowsTranslationFailure()
    {
        var act = async () => await InheritanceDb
            .Employees
            .Where(employee => employee.Pk == "TENANT#H")
            .FirstOrDefaultAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*AsAsyncEnumerable*");
    }
}

public class DiscriminatorInheritanceBaseOnlyToTableQueryTests(DynamoContainerFixture fixture)
    : SharedTableTestFixture(fixture)
{
    private SharedTableInheritanceBaseOnlyToTableDbContext BaseOnlyToTableDb
        => new(
            CreateOptions<SharedTableInheritanceBaseOnlyToTableDbContext>(o
                => o.DynamoDbClient(Client)));

    [Fact]
    public async Task BaseQuery_MaterializesConcreteHierarchyTypes_WhenOnlyBaseConfiguresTable()
    {
        var results = await BaseOnlyToTableDb
            .People
            .Where(person => person.Pk == "TENANT#H")
            .ToListAsync(CancellationToken);

        results.Should().HaveCount(2);

        var employee = results.OfType<EmployeeEntity>().Single();
        employee.Name.Should().Be("Eve");
        employee.Department.Should().Be("Engineering");

        var manager = results.OfType<ManagerEntity>().Single();
        manager.Name.Should().Be("Max");
        manager.Level.Should().Be(7);

        AssertSql(
            """
            SELECT "Pk", "Sk", "$type", "Name", "Department", "ManagerLevel"
            FROM "app-table"
            WHERE "Pk" = 'TENANT#H' AND ("$type" = 'EmployeeEntity' OR "$type" = 'ManagerEntity')
            """);
    }

    [Fact]
    public async Task DerivedQuery_UsesRootTableMapping_WhenOnlyBaseConfiguresTable()
    {
        var results = await BaseOnlyToTableDb
            .Employees
            .Where(employee => employee.Pk == "TENANT#H")
            .ToListAsync(CancellationToken);

        results.Should().HaveCount(1);
        results[0].Department.Should().Be("Engineering");

        AssertSql(
            """
            SELECT "Pk", "Sk", "$type", "Name", "Department"
            FROM "app-table"
            WHERE "Pk" = 'TENANT#H' AND "$type" = 'EmployeeEntity'
            """);
    }
}

public class DiscriminatorInheritanceWithIndexQueryTests(DynamoContainerFixture fixture)
    : SharedTableTestFixture(fixture)
{
    private SharedTableInheritanceWithIndexesDbContext InheritanceWithIndexesDb
        => new(
            CreateOptions<SharedTableInheritanceWithIndexesDbContext>(o
                => o.DynamoDbClient(Client)));

    [Fact]
    public async Task DerivedQuery_WithSiblingOnlyIndex_ThrowsBeforeExecution()
    {
        var act = async () => await InheritanceWithIndexesDb
            .ArchivedWorkOrders
            .WithIndex("ByPriority")
            .ToListAsync(CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ByPriority*");
    }
}
