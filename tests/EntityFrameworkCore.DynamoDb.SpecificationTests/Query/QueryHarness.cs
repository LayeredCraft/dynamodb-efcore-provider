using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestModels.Northwind;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

public sealed class DefaultSetSource(DbContext context) : ISetSource
{
    public IQueryable<TEntity> Set<TEntity>() where TEntity : class => context.Set<TEntity>();
}

public interface IQueryFixture
{
    ISetSource ExpectedData { get; }

    DbContext CreateContext();

    IReadOnlyDictionary<Type, Func<object, object?>> EntitySorters { get; }

    IReadOnlyDictionary<Type, Action<object, object>> EntityAsserters { get; }
}

public abstract class QueryFixtureBase<TContext> : IQueryFixture where TContext : DbContext
{
    public abstract ISetSource ExpectedData { get; }

    public abstract TContext CreateContext();

    DbContext IQueryFixture.CreateContext() => CreateContext();

    public virtual IReadOnlyDictionary<Type, Func<object, object?>> EntitySorters
        => new Dictionary<Type, Func<object, object?>>();

    public virtual IReadOnlyDictionary<Type, Action<object, object>> EntityAsserters
        => new Dictionary<Type, Action<object, object>>();
}

public enum QueryTrackingBehaviorVariant
{
    TrackAll,
    NoTracking,
    NoTrackingWithIdentityResolution
}

public sealed class QueryAsserter(IQueryFixture fixture)
{
    public async Task AssertQuery<TResult>(
        Func<ISetSource, IQueryable<TResult>> query,
        Func<TResult, object?>? elementSorter = null,
        Action<TResult, TResult>? elementAsserter = null,
        bool assertOrder = false,
        QueryTrackingBehaviorVariant tracking = QueryTrackingBehaviorVariant.TrackAll)
        where TResult : class
        => await AssertQuery(query, query, elementSorter, elementAsserter, assertOrder, tracking);

    public async Task AssertQuery<TResult>(
        Func<ISetSource, IQueryable<TResult>> actualQuery,
        Func<ISetSource, IQueryable<TResult>> expectedQuery,
        Func<TResult, object?>? elementSorter = null,
        Action<TResult, TResult>? elementAsserter = null,
        bool assertOrder = false,
        QueryTrackingBehaviorVariant tracking = QueryTrackingBehaviorVariant.TrackAll)
        where TResult : class
    {
        await using var context = fixture.CreateContext();

        var actualQueryable = ApplyTracking(actualQuery(new DefaultSetSource(context)), tracking);
        var expectedQueryable = expectedQuery(fixture.ExpectedData);

        var actual = await actualQueryable.ToListAsync();
        var expected = expectedQueryable.ToList();

        AssertResults(
            expected,
            actual,
            elementSorter ?? FindSorter<TResult>(),
            elementAsserter ?? FindAsserter<TResult>(),
            assertOrder);
    }

    private static IQueryable<TResult> ApplyTracking<TResult>(
        IQueryable<TResult> query,
        QueryTrackingBehaviorVariant tracking) where TResult : class
        => tracking switch
        {
            QueryTrackingBehaviorVariant.TrackAll => query,
            QueryTrackingBehaviorVariant.NoTracking => query.AsNoTracking(),
            QueryTrackingBehaviorVariant.NoTrackingWithIdentityResolution => query
                .AsNoTrackingWithIdentityResolution(),
            _ => throw new ArgumentOutOfRangeException(nameof(tracking), tracking, null)
        };

    public async Task AssertSingleResult<TResult>(
        Func<ISetSource, IQueryable<TResult>> actualQuery,
        Func<ISetSource, IQueryable<TResult>> expectedQuery,
        Func<IQueryable<TResult>, Task<TResult?>> actualElement,
        Func<IQueryable<TResult>, TResult?> expectedElement,
        Action<TResult?, TResult?>? asserter = null)
    {
        await using var context = fixture.CreateContext();
        var actual = await actualElement(actualQuery(new DefaultSetSource(context)));
        var expected = expectedElement(expectedQuery(fixture.ExpectedData));
        if (asserter is not null)
            asserter(expected, actual);
        else
            actual.Should().BeEquivalentTo(expected);
    }

    public async Task AssertUnsupportedScalar<TResult>(
        Func<ISetSource, IQueryable<TResult>> query,
        string message)
    {
        await using var context = fixture.CreateContext();
        var act = async () => await query(new DefaultSetSource(context)).CountAsync();
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage(message);
    }

    private Func<TResult, object?>? FindSorter<TResult>()
        => fixture.EntitySorters.TryGetValue(typeof(TResult), out var sorter)
            ? x => sorter(x!)
            : null;

    private Action<TResult, TResult>? FindAsserter<TResult>()
        => fixture.EntityAsserters.TryGetValue(typeof(TResult), out var asserter)
            ? (e, a) => asserter(e!, a!)
            : null;

    public static void AssertResults<TResult>(
        IReadOnlyList<TResult> expected,
        IReadOnlyList<TResult> actual,
        Func<TResult, object?>? elementSorter = null,
        Action<TResult, TResult>? elementAsserter = null,
        bool assertOrder = false)
    {
        IEnumerable<TResult> expectedSequence = expected;
        IEnumerable<TResult> actualSequence = actual;

        if (!assertOrder && elementSorter is not null)
        {
            expectedSequence = expectedSequence.OrderBy(elementSorter);
            actualSequence = actualSequence.OrderBy(elementSorter);
        }

        var expectedList = expectedSequence.ToList();
        var actualList = actualSequence.ToList();
        actualList.Count.Should().Be(expectedList.Count);

        for (var i = 0; i < expectedList.Count; i++)
        {
            if (elementAsserter is not null)
            {
                elementAsserter(expectedList[i], actualList[i]);
            }
            else
            {
                actualList[i].Should().BeEquivalentTo(expectedList[i]);
            }
        }
    }
}

public abstract class QueryTestBase<TFixture>(TFixture fixture) where TFixture : IQueryFixture
{
    protected TFixture Fixture { get; } = fixture;

    protected QueryAsserter AssertQuery { get; } = new(fixture);
}

public static class NorthwindEntitySorters
{
    public static IReadOnlyDictionary<Type, Func<object, object?>> Create()
        => new Dictionary<Type, Func<object, object?>>
        {
            [typeof(Customer)] = e => ((Customer)e).CustomerID,
            [typeof(Employee)] = e => ((Employee)e).EmployeeID,
            [typeof(Order)] = e => ((Order)e).OrderID,
            [typeof(OrderDetail)] = e => (((OrderDetail)e).OrderID, ((OrderDetail)e).ProductID),
            [typeof(Product)] = e => ((Product)e).ProductID,
            [typeof(CustomerQuery)] = e => ((CustomerQuery)e).CompanyName,
            [typeof(OrderQuery)] = e => ((OrderQuery)e).CustomerID,
            [typeof(ProductQuery)] = e => ((ProductQuery)e).ProductID,
            [typeof(ProductView)] = e => ((ProductView)e).ProductID,
            [typeof(CustomerQueryWithQueryFilter)] = e
                => ((CustomerQueryWithQueryFilter)e).CompanyName
        };
}

public static class NorthwindEntityAsserters
{
    public static IReadOnlyDictionary<Type, Action<object, object>> Create()
        => new Dictionary<Type, Action<object, object>>
        {
            [typeof(Customer)] = (e, a)
                => a.Should().BeEquivalentTo(e, o => o.Excluding(c => ((Customer)c).Orders)),
            [typeof(Employee)] = (e, a)
                => a.Should().BeEquivalentTo(e, o => o.Excluding(c => ((Employee)c).Manager)),
            [typeof(Order)] = (e, a)
                => a
                    .Should()
                    .BeEquivalentTo(
                        e,
                        o => o
                            .Excluding(c => ((Order)c).Customer)
                            .Excluding(c => ((Order)c).OrderDetails)),
            [typeof(OrderDetail)] = (e, a)
                => a
                    .Should()
                    .BeEquivalentTo(
                        e,
                        o => o
                            .Excluding(c => ((OrderDetail)c).Order)
                            .Excluding(c => ((OrderDetail)c).Product)),
            [typeof(Product)] = (e, a)
                => a
                    .Should()
                    .BeEquivalentTo(e, o => o.Excluding(c => ((Product)c).OrderDetails))
        };
}
