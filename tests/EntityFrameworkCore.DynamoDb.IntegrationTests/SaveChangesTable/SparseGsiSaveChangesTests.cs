using EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

/// <summary>
///     Verifies the sparse-GSI INSERT behaviour: null GSI key attributes must be absent from the
///     persisted item (not written as <c>{ NULL: true }</c>), while unrelated nullable scalar
///     properties retain their <c>{ NULL: true }</c> wire representation.
/// </summary>
public class SparseGsiSaveChangesTests(DynamoContainerFixture fixture)
    : SaveChangesTableTestFixture(fixture)
{
    /// <summary>
    ///     Inserting an item whose GSI key properties are null must succeed and produce a DynamoDB
    ///     item that does not contain the GSI key attributes at all. DynamoDB rejects
    ///     <c>{ NULL: true }</c> for attributes that participate in a GSI key definition.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task AddAsync_WithNullGsiKeys_GsiKeyAttributesAbsentFromItem()
    {
        var item = new SparseGsiItem
        {
            Pk = "SPARSE#no-gsi",
            Sk = "ITEM#1",
            Version = 1,
            Name = "No GSI participation",
            Gs1Pk = null,
            Gs1Sk = null,
        };

        Db.SparseGsiItems.Add(item);
        await Db.SaveChangesAsync(CancellationToken);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw.Should().NotBeNull();
        raw!.Should().NotContainKey("gs1-pk");
        raw.Should().NotContainKey("gs1-sk");
    }

    /// <summary>
    ///     A null nullable scalar that is NOT a GSI key must still be persisted as
    ///     <c>{ NULL: true }</c>. The GSI-key omission must not bleed into unrelated nullable
    ///     properties.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task AddAsync_WithNullGsiKeys_NullableScalar_IsWrittenAsNullType()
    {
        var item = new SparseGsiItem
        {
            Pk = "SPARSE#null-scalar",
            Sk = "ITEM#2",
            Version = 1,
            Name = "Null note",
            OptionalNote = null,
            Gs1Pk = null,
            Gs1Sk = null,
        };

        Db.SparseGsiItems.Add(item);
        await Db.SaveChangesAsync(CancellationToken);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw.Should().NotBeNull();
        raw!["optionalNote"].NULL.Should().BeTrue();
        raw.Should().NotContainKey("gs1-pk");
        raw.Should().NotContainKey("gs1-sk");
    }

    /// <summary>
    ///     When GSI key properties are populated the item must contain the GSI key attributes with
    ///     the correct string values so the item is visible in the sparse index.
    /// </summary>
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public async Task AddAsync_WithPopulatedGsiKeys_WritesGsiKeyAttributesToItem()
    {
        var item = new SparseGsiItem
        {
            Pk = "SPARSE#with-gsi",
            Sk = "ITEM#3",
            Version = 1,
            Name = "GSI participant",
            Gs1Pk = "partition#active",
            Gs1Sk = "sort#001",
        };

        Db.SparseGsiItems.Add(item);
        await Db.SaveChangesAsync(CancellationToken);

        var raw = await GetItemAsync(item.Pk, item.Sk, CancellationToken);
        raw.Should().NotBeNull();
        raw!["gs1-pk"].S.Should().Be("partition#active");
        raw["gs1-sk"].S.Should().Be("sort#001");
    }
}
