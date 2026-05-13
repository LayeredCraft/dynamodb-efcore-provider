namespace EntityFrameworkCore.DynamoDb.SpecificationTests.TestModels.Northwind;

public interface ISetSource
{
    IQueryable<TEntity> Set<TEntity>()
        where TEntity : class;
}
