using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;

/// <summary>Shared SQL-capture contract for DynamoDB specification fixtures.</summary>
public interface IDynamoSpecificationFixture
{
    /// <summary>Gets the PartiQL logger used by the fixture.</summary>
    TestSqlLoggerFactory TestSqlLoggerFactory { get; }
}

/// <summary>Helpers for DynamoDB specification fixtures.</summary>
public static class DynamoSpecificationFixtureExtensions
{
    /// <summary>Clears captured PartiQL statements.</summary>
    public static void ClearSql(this IDynamoSpecificationFixture fixture)
        => fixture.TestSqlLoggerFactory.Clear();

    /// <summary>Asserts captured PartiQL statements against a baseline.</summary>
    public static void AssertSql(this IDynamoSpecificationFixture fixture, params string[] expected)
        => fixture.TestSqlLoggerFactory.AssertBaseline(expected);

    /// <summary>Returns whether the logging category should be captured for DynamoDB SQL baselines.</summary>
    public static bool ShouldLogDynamoSql(string logCategory)
        => logCategory.StartsWith(DbLoggerCategory.Database.Command.Name, StringComparison.Ordinal)
            || logCategory.StartsWith(DbLoggerCategory.Query.Name, StringComparison.Ordinal);
}
