using Xunit;

namespace EntityFrameworkCore.DynamoDb.NativeAotTests;

public sealed class NativeAotRuntimeTests(DynamoFixture fixture)
{
    private const int Timeout = 60_000;

    [Fact(Timeout = Timeout)]
    public async Task DynamoDB_container_starts_once()
    {
        await fixture.ResetAndSeedAsync(TestContext.Current.CancellationToken);
        if (DynamoFixture.ContainerStartCount != 1)
            throw new InvalidOperationException(
                $"Expected one DynamoDB container start, but observed {DynamoFixture.ContainerStartCount}.");
    }

    [Fact(Timeout = Timeout)]
    public async Task Collection_parameter_query_materializes_entity()
    {
        await fixture.ResetAndSeedAsync(TestContext.Current.CancellationToken);
        var items = await AotRuntimeQueries.LoadItemsAsync();
        AssertSingleItem(items, AotRuntimeData.ExpectedNativeItem);
    }

    [Fact(Timeout = Timeout)]
    public async Task Unconverted_enum_projection_materializes()
    {
        await fixture.ResetAndSeedAsync(TestContext.Current.CancellationToken);
        var statuses = await AotRuntimeQueries.ProjectRawStatusAsync();
        if (statuses is not [AotRuntimeStatus.Inactive])
            throw new InvalidOperationException(
                "Expected the raw enum projection to return Inactive.");
    }

    [Fact(Timeout = Timeout)]
    public async Task Nullable_unconverted_enum_projection_materializes()
    {
        await fixture.ResetAndSeedAsync(TestContext.Current.CancellationToken);
        var statuses = await AotRuntimeQueries.ProjectOptionalStatusAsync();
        if (statuses is not [AotRuntimeStatus.Active])
            throw new InvalidOperationException(
                "Expected the nullable raw enum projection to return Active.");
    }

    [Fact(Timeout = Timeout)]
    public async Task Converted_enum_parameter_query_executes()
    {
        await fixture.ResetAndSeedAsync(TestContext.Current.CancellationToken);
        var items = await AotRuntimeQueries.LoadActiveItemsAsync();
        AssertSingleItem(items, AotRuntimeData.ExpectedNativeItem);
    }

    [Fact(Timeout = Timeout)]
    public async Task Numeric_parameter_query_executes()
    {
        await fixture.ResetAndSeedAsync(TestContext.Current.CancellationToken);
        var items = await AotRuntimeQueries.LoadItemsByCountAsync();
        AssertSingleItem(items, AotRuntimeData.ExpectedNativeItem);
    }

    [Fact(Timeout = Timeout)]
    public async Task Composite_partition_and_sort_key_query_executes()
    {
        await fixture.ResetAndSeedAsync(TestContext.Current.CancellationToken);
        var items = await AotRuntimeQueries.LoadItemsBySortKeyAsync();
        AssertSingleItem(items, AotRuntimeData.ExpectedNativeItem);
    }

    [Fact(Timeout = Timeout)]
    public async Task Converted_scalar_projection_materializes()
    {
        await fixture.ResetAndSeedAsync(TestContext.Current.CancellationToken);
        var status = await AotRuntimeQueries.ProjectConvertedStatusAsync();
        if (status != AotRuntimeStatus.Active)
            throw new InvalidOperationException(
                $"Expected converted projection Active but received {status}.");
    }

    [Fact(Timeout = Timeout)]
    public async Task Null_predicate_query_executes()
    {
        await fixture.ResetAndSeedAsync(TestContext.Current.CancellationToken);
        var items = await AotRuntimeQueries.LoadItemsWithNullCountAsync();
        AssertSingleItem(items, AotRuntimeData.ExpectedNullableItem);
    }

    [Fact(Timeout = Timeout)]
    public async Task Limit_applies_evaluation_budget_and_exposes_next_token()
    {
        await fixture.ResetAndSeedAsync(TestContext.Current.CancellationToken);
        var (items, actualToken) = await AotRuntimeQueries.LoadLimitedItemsAsync();
        if (items.Count != 2 || actualToken is null)
            throw new InvalidOperationException(
                $"Expected two items and a continuation token, received {items.Count} items.");

        var expectedToken = await fixture.BootstrapNextTokenAsync(
            "tenant-limit",
            2,
            null,
            TestContext.Current.CancellationToken);
        if (DynamoFixture.ExtractDynamoDbLocalContinuationKey(actualToken)
            != DynamoFixture.ExtractDynamoDbLocalContinuationKey(expectedToken))
            throw new InvalidOperationException(
                "The generated query returned the wrong continuation key.");
    }

    [Fact(Timeout = Timeout)]
    public async Task Parameterized_limit_and_next_token_paginate_without_overlap()
    {
        await fixture.ResetAndSeedAsync(TestContext.Current.CancellationToken);
        var page1 = await AotRuntimeQueries.LoadFirstPageAsync(2);
        var token1 = await fixture.BootstrapNextTokenAsync(
            "tenant-page",
            2,
            null,
            TestContext.Current.CancellationToken);
        var page2 = await AotRuntimeQueries.LoadNextPageAsync(2, token1!);
        var token2 = await fixture.BootstrapNextTokenAsync(
            "tenant-page",
            2,
            token1,
            TestContext.Current.CancellationToken);
        var page3 = await AotRuntimeQueries.LoadNextPageAsync(1, token2!);
        var token3 = await fixture.BootstrapNextTokenAsync(
            "tenant-page",
            1,
            token2,
            TestContext.Current.CancellationToken);

        if (page1.Count != 2 || page2.Count != 2 || page3.Count != 1)
            throw new InvalidOperationException("Expected pagination pages of 2, 2, and 1 item.");

        if (token3 is not null && (await AotRuntimeQueries.LoadNextPageAsync(1, token3)).Count != 0)
            throw new InvalidOperationException("Expected the final pagination page to be empty.");

        var allNames = page1.Concat(page2).Concat(page3).ToArray();
        var expectedNames = new[] { "Page1", "Page2", "Page3", "Page4", "Page5" };
        if (allNames.Length != expectedNames.Length
            || allNames.Distinct().Count() != expectedNames.Length
            || !allNames
                .OrderBy(name => name, StringComparer.Ordinal)
                .SequenceEqual(expectedNames.OrderBy(name => name, StringComparer.Ordinal)))
            throw new InvalidOperationException(
                "Pagination did not return each item exactly once.");
    }

    [Fact(Timeout = Timeout)]
    public async Task Save_changes_writes_and_query_reads_back_entity()
    {
        await fixture.ResetAndSeedAsync(TestContext.Current.CancellationToken);
        var expected = new AotRuntimeItem
        {
            Pk = "tenant-9",
            Sk = "sk-9",
            Name = "Saved",
            Status = AotRuntimeStatus.Inactive,
            Count = null,
            Enabled = false,
            Payload = [9],
            Aliases = ["write"]
        };

        await using (var context = new AotRuntimeContext())
        {
            context.Add(expected);
            await context.SaveChangesAsync();
        }

        var actual = await AotRuntimeQueries.LoadByKeyAsync(expected.Pk, expected.Sk);
        if (actual is null)
            throw new InvalidOperationException("Expected the saved item to be queryable.");
        AssertSingleItem([actual], expected);
    }

    [Fact(Timeout = Timeout)]
    public async Task Execute_delete_deletes_matching_entity()
    {
        await fixture.ResetAndSeedAsync(TestContext.Current.CancellationToken);
        var deleted = await AotRuntimeQueries.ExecuteDeleteAsync();
        if (deleted != 1)
            throw new InvalidOperationException($"Expected one deleted item, received {deleted}.");

        var item = await AotRuntimeQueries.LoadByKeyAsync("tenant-null", "sk-null");
        if (item is not null)
            throw new InvalidOperationException("Expected the deleted item to be absent.");
    }

    [Fact(Timeout = Timeout)]
    public async Task Precompiled_query_uses_runtime_configured_table_name()
    {
        var previousTableName =
            Environment.GetEnvironmentVariable("DYNAMO_AOT_TEST_RUNTIME_TABLE_NAME");
        try
        {
            Environment.SetEnvironmentVariable(
                "DYNAMO_AOT_TEST_RUNTIME_TABLE_NAME",
                AotRuntimeContext.RuntimeTableName);
            AotRuntimeContext.ConfigureRuntimeTableName(AotRuntimeContext.RuntimeTableName);
            await fixture.ResetAndSeedAsync(TestContext.Current.CancellationToken);
            await fixture.SeedRuntimeMappedItemAsync(TestContext.Current.CancellationToken);

            var names = await AotRuntimeQueries.LoadRuntimeItemNamesAsync();
            if (names is not ["RuntimeConfigured"])
                throw new InvalidOperationException(
                    "Expected the precompiled query to use the runtime-configured table name.");
        }
        finally
        {
            AotRuntimeContext.ConfigureRuntimeTableName(AotRuntimeContext.DesignTimeTableName);
            Environment.SetEnvironmentVariable(
                "DYNAMO_AOT_TEST_RUNTIME_TABLE_NAME",
                previousTableName);
        }
    }

    private static void AssertSingleItem(List<AotRuntimeItem> items, AotRuntimeItem expected)
    {
        if (items.Count != 1)
            throw new InvalidOperationException($"Expected one item but received {items.Count}.");

        var actual = items[0];
        if (actual.Pk != expected.Pk
            || actual.Sk != expected.Sk
            || actual.Name != expected.Name
            || actual.Status != expected.Status
            || actual.RawStatus != expected.RawStatus
            || actual.OptionalStatus != expected.OptionalStatus
            || actual.Count != expected.Count
            || actual.Enabled != expected.Enabled
            || !actual.Payload.SequenceEqual(expected.Payload)
            || !actual.Aliases.SequenceEqual(expected.Aliases))
            throw new InvalidOperationException(
                "The generated query returned an unexpected result.");
    }
}
