using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests.Query;

/// <summary>Northwind query-tagging specification tests for the DynamoDB provider.</summary>
public abstract class NorthwindQueryTaggingQueryDynamoTest
    : NorthwindQueryTaggingQueryTestBase<NorthwindQueryDynamoFixture<NoopModelCustomizer>>
{
    protected NorthwindQueryTaggingQueryDynamoTest(
        NorthwindQueryDynamoFixture<NoopModelCustomizer> fixture) : base(fixture)
        => fixture.ClearSql();

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(NorthwindQueryTaggingQueryDynamoTest));

    [ConditionalFact(Skip = SkipReason.SyncQueriesNotSupported)]
    public override void Single_query_tag() => base.Single_query_tag();

    [ConditionalFact(Skip = SkipReason.SyncQueriesNotSupported)]
    public override void Single_query_multiple_tags() => base.Single_query_multiple_tags();

    [ConditionalFact(Skip = SkipReason.SyncQueriesNotSupported)]
    public override void Duplicate_tags() => base.Duplicate_tags();

    [ConditionalFact(Skip = SkipReason.JoinsNotSupported)]
    public override void Tags_on_subquery() => base.Tags_on_subquery();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override void Tag_on_include_query() => base.Tag_on_include_query();

    [ConditionalFact(Skip = SkipReason.SyncQueriesNotSupported)]
    public override void Tag_on_scalar_query() => base.Tag_on_scalar_query();

    [ConditionalFact(Skip = SkipReason.SyncQueriesNotSupported)]
    public override void Single_query_multiline_tag() => base.Single_query_multiline_tag();

    [ConditionalFact(Skip = SkipReason.SyncQueriesNotSupported)]
    public override void Single_query_multiple_multiline_tag()
        => base.Single_query_multiple_multiline_tag();

    [ConditionalFact(Skip = SkipReason.SyncQueriesNotSupported)]
    public override void Single_query_multiline_tag_with_empty_lines()
        => base.Single_query_multiline_tag_with_empty_lines();

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class NorthwindQueryTaggingQueryDynamoTestDefault
        : NorthwindQueryTaggingQueryDynamoTest
    {
        public NorthwindQueryTaggingQueryDynamoTestDefault(
            NorthwindQueryDynamoFixture<NoopModelCustomizer> fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }
}
