using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SaveChangesTable;

public class ModifiedScalarSaveChangesTests(SaveChangesTableDynamoFixture fixture)
    : SaveChangesTableTestBase(fixture)
{
    [Fact]
    public async Task SaveChangesAsync_ModifiedSingleScalar_PersistsAndEmitsUpdate()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#MOD-1",
            Version = 1,
            Email = "initial+1@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero),
            NullableNote = "note-1",
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Email = "updated+1@example.com";

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var actual = (await GetItemAsync(item.Pk, item.Sk, CancellationToken))?.ToCustomerItem();
        actual.Should().NotBeNull();
        actual.Should().BeEquivalentTo(item);

        AssertSql(
            """
            UPDATE "AppItems"
            SET "Email" = ?
            WHERE "Pk" = ? AND "Sk" = ? AND "Version" = ?
            """);
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedMultipleScalars_PersistsInSingleStatement()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#MOD-2",
            Version = 1,
            Email = "initial+2@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 3, 2, 12, 0, 0, TimeSpan.Zero),
            NullableNote = null,
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Email = "updated+2@example.com";
        item.IsPreferred = true;

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var actual = (await GetItemAsync(item.Pk, item.Sk, CancellationToken))?.ToCustomerItem();
        actual.Should().NotBeNull();
        actual.Should().BeEquivalentTo(item);

        AssertSql(
            """
            UPDATE "AppItems"
            SET "Email" = ?, "IsPreferred" = ?
            WHERE "Pk" = ? AND "Sk" = ? AND "Version" = ?
            """);
    }

    [Fact]
    public async Task SaveChangesAsync_ModifiedNullableScalar_NullTransitionPersists()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#MOD-3",
            Version = 1,
            Email = "initial+3@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 3, 3, 12, 0, 0, TimeSpan.Zero),
            NullableNote = "to-null",
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.NullableNote = null;

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        var actual = (await GetItemAsync(item.Pk, item.Sk, CancellationToken))?.ToCustomerItem();
        actual.Should().NotBeNull();
        actual.Should().BeEquivalentTo(item);

        AssertSql(
            """
            UPDATE "AppItems"
            SET "NullableNote" = ?
            WHERE "Pk" = ? AND "Sk" = ? AND "Version" = ?
            """);
    }

    [Fact]
    public async Task SaveChangesAsync_NoTrackedChanges_DoesNotEmitWrites()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#MOD-4",
            Version = 1,
            Email = "initial+4@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 3, 4, 12, 0, 0, TimeSpan.Zero),
            NullableNote = null,
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(0);

        var actual = (await GetItemAsync(item.Pk, item.Sk, CancellationToken))?.ToCustomerItem();
        actual.Should().NotBeNull();
        actual.Should().BeEquivalentTo(item);

        AssertSql();
    }

    [Fact]
    public async Task
        SaveChangesAsync_ModifiedScalar_TransitionsToUnchangedAndRefreshesOriginalValues()
    {
        var item = new CustomerItem
        {
            Pk = "TENANT#1",
            Sk = "CUSTOMER#MOD-5",
            Version = 1,
            Email = "initial+5@example.com",
            IsPreferred = false,
            CreatedAt = new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero),
            NullableNote = null,
        };
        Db.Customers.Add(item);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        item.Email = "post-save@example.com";
        var entry = Db.Entry(item);
        entry.State.Should().Be(EntityState.Modified);

        var affected = await Db.SaveChangesAsync(CancellationToken);
        affected.Should().Be(1);

        entry.State.Should().Be(EntityState.Unchanged);
        entry.Property(x => x.Email).OriginalValue.Should().Be("post-save@example.com");

        var actual = (await GetItemAsync(item.Pk, item.Sk, CancellationToken))?.ToCustomerItem();
        actual.Should().NotBeNull();
        actual.Should().BeEquivalentTo(item);

        AssertSql(
            """
            UPDATE "AppItems"
            SET "Email" = ?
            WHERE "Pk" = ? AND "Sk" = ? AND "Version" = ?
            """);
    }

    [Fact]
    public async Task SaveChangesAsync_OwnedOnlyMutation_ThrowsNotSupported()
    {
        var product = new ProductItem
        {
            Pk = "TENANT#1",
            Sk = "PRODUCT#MOD-OWNED-1",
            Version = 1,
            Name = "Owned Product",
            Price = 5m,
            IsActive = true,
            PublishedAt = null,
            Dimensions =
                new ProductDimensions { Height = 1m, Width = 2m, Depth = 3m, Weight = 4m },
        };
        Db.Products.Add(product);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        product.Dimensions.Should().NotBeNull();
        product.Dimensions!.Height += 1;

        var rootEntry = Db.Entry(product);
        var rootStateBeforeSave = rootEntry.State;

        var act = async () => await Db.SaveChangesAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<NotSupportedException>()
            .WithMessage("*owned or nested mutations*");

        rootEntry.State.Should().Be(rootStateBeforeSave);

        AssertSql();
    }

    [Fact]
    public async Task SaveChangesAsync_OwnedCollectionAddOnlyMutation_ThrowsNotSupported()
    {
        var product = new ProductItem
        {
            Pk = "TENANT#1",
            Sk = "PRODUCT#MOD-OWNED-ADD-1",
            Version = 1,
            Name = "Owned Product",
            Price = 5m,
            IsActive = true,
            PublishedAt = null,
            Variants =
            [
                new ProductVariant { Code = "BASE", Color = "Blue", Backordered = false },
            ],
        };
        Db.Products.Add(product);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        product.Variants.Add(
            new ProductVariant { Code = "NEW", Color = "Red", Backordered = true });

        var act = async () => await Db.SaveChangesAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<NotSupportedException>()
            .WithMessage("*owned or nested mutations*");

        AssertSql();
    }

    [Fact]
    public async Task SaveChangesAsync_OwnedCollectionDeleteOnlyMutation_ThrowsNotSupported()
    {
        var product = new ProductItem
        {
            Pk = "TENANT#1",
            Sk = "PRODUCT#MOD-OWNED-DEL-1",
            Version = 1,
            Name = "Owned Product",
            Price = 5m,
            IsActive = true,
            PublishedAt = null,
            Variants =
            [
                new ProductVariant { Code = "DROP", Color = "Blue", Backordered = false },
                new ProductVariant { Code = "KEEP", Color = "Green", Backordered = false },
            ],
        };
        Db.Products.Add(product);
        await Db.SaveChangesAsync(CancellationToken);
        LoggerFactory.Clear();

        product.Variants.RemoveAt(0);

        var act = async () => await Db.SaveChangesAsync(CancellationToken);

        await act
            .Should()
            .ThrowAsync<NotSupportedException>()
            .WithMessage("*owned or nested mutations*");

        AssertSql();
    }
}
