using Amazon.DynamoDBv2.Model;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;

public record PartiQlQuery(string Statement, List<AttributeValue> Parameters);
