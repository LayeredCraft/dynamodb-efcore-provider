using Amazon.DynamoDBv2.Model;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
/// Represents a compiled PartiQL query with parameters.
/// </summary>
public record DynamoPartiQlQuery(string Sql, IReadOnlyList<AttributeValue> Parameters);
