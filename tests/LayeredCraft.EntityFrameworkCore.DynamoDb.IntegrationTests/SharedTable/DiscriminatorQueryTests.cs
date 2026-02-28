using Microsoft.EntityFrameworkCore;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.IntegrationTests.SharedTable;

public class DiscriminatorQueryTests(SharedTableDynamoFixture fixture)
    : SharedTableTestBase<SharedTableDbContext>(fixture)
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
            SELECT Pk, Sk, Name, "$type"
            FROM "app-table"
            WHERE Pk = 'TENANT#U' AND "$type" = 'UserEntity'
            """);
    }

    [Fact]
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
            SELECT Pk
            FROM "app-table"
            WHERE Pk = 'TENANT#U' AND "$type" = 'UserEntity'
            """);
    }
}

public class DiscriminatorQueryCustomNameTests(SharedTableDynamoFixture fixture)
    : SharedTableTestBase<SharedTableCustomDiscriminatorNameDbContext>(fixture)
{
    [Fact]
    public async Task UsesEmbeddedDiscriminatorNameOverrideInPredicate()
    {
        var results =
            await Db.Users.Where(user => user.Pk == "TENANT#U").ToListAsync(CancellationToken);

        results.Should().HaveCount(2);

        AssertSql(
            """
            SELECT Pk, Sk, Name, "$kind"
            FROM "app-table"
            WHERE Pk = 'TENANT#U' AND "$kind" = 'UserEntity'
            """);
    }
}

public class DiscriminatorQuerySingleTypeTests(SharedTableDynamoFixture fixture)
    : SharedTableTestBase<SharedTableSingleTypeDbContext>(fixture)
{
    [Fact]
    public async Task SingleTypeTable_DoesNotInjectDiscriminatorPredicate()
    {
        var results =
            await Db.Users.Where(user => user.Pk == "TENANT#U").ToListAsync(CancellationToken);

        results.Should().HaveCount(2);

        AssertSql(
            """
            SELECT Pk, Sk, Name
            FROM "app-table"
            WHERE Pk = 'TENANT#U'
            """);
    }
}

public class DiscriminatorInheritanceQueryTests(SharedTableDynamoFixture fixture)
    : SharedTableTestBase<SharedTableInheritanceDbContext>(fixture)
{
    [Fact]
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
            SELECT Pk, Sk, Name, Department, "$type"
            FROM "app-table"
            WHERE Pk = 'TENANT#H' AND "$type" = 'EmployeeEntity'
            """);
    }
}
