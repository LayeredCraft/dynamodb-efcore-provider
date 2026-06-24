using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

/// <summary>Built-in data type specification tests for the DynamoDB provider.</summary>
public class BuiltInDataTypesDynamoTest(
    BuiltInDataTypesDynamoTest.BuiltInDataTypesDynamoFixture fixture)
    : BuiltInDataTypesTestBase<BuiltInDataTypesDynamoTest.BuiltInDataTypesDynamoFixture>(fixture)
{
    /// <summary>Ensures all inherited specification tests are reviewed by this provider.</summary>
    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(BuiltInDataTypesDynamoTest));

    /// <inheritdoc />
    public override async Task Can_filter_projection_with_captured_enum_variable(bool async)
    {
        if (!async)
        {
            await AssertNoSync(() => base.Can_filter_projection_with_captured_enum_variable(async));
            return;
        }

        await base.Can_filter_projection_with_captured_enum_variable(async);
    }

    /// <inheritdoc />
    public override async Task Can_filter_projection_with_inline_enum_variable(bool async)
    {
        if (!async)
        {
            await AssertNoSync(() => base.Can_filter_projection_with_inline_enum_variable(async));
            return;
        }

        await base.Can_filter_projection_with_inline_enum_variable(async);
    }

#if NET10_0
    /// <inheritdoc />
    public override Task Can_query_using_any_data_type() => base.Can_query_using_any_data_type();

    /// <inheritdoc />
    public override Task Can_query_using_any_data_type_shadow()
        => base.Can_query_using_any_data_type_shadow();

    /// <inheritdoc />
    public override Task Can_query_using_any_nullable_data_type()
        => base.Can_query_using_any_nullable_data_type();

    /// <inheritdoc />
    public override Task Can_query_using_any_data_type_nullable_shadow()
        => base.Can_query_using_any_data_type_nullable_shadow();

    /// <inheritdoc />
    public override Task Can_query_using_any_nullable_data_type_as_literal()
        => base.Can_query_using_any_nullable_data_type_as_literal();

    /// <inheritdoc />
    public override Task Can_query_with_null_parameters_using_any_nullable_data_type()
        => base.Can_query_with_null_parameters_using_any_nullable_data_type();

    /// <inheritdoc />
    public override Task Can_insert_and_read_back_all_non_nullable_data_types()
        => base.Can_insert_and_read_back_all_non_nullable_data_types();
#endif

    /// <inheritdoc />
    public override Task Can_perform_query_with_max_length()
        => base.Can_perform_query_with_max_length();

    /// <inheritdoc />
    public override Task Can_perform_query_with_ansi_strings_test()
        => base.Can_perform_query_with_ansi_strings_test();

    /// <inheritdoc />
    public override Task Can_insert_and_read_with_max_length_set()
        => base.Can_insert_and_read_with_max_length_set();

    /// <inheritdoc />
    public override async Task Can_insert_and_read_back_with_binary_key()
    {
        await using (var context = CreateContext())
        {
            context
                .Set<BinaryKeyDataType>()
                .AddRange(
                    new BinaryKeyDataType { Id = [1, 2, 3], Ex = "X1" },
                    new BinaryKeyDataType { Id = [1, 2, 3, 4], Ex = "X3" },
                    new BinaryKeyDataType { Id = [1, 2, 3, 4, 5], Ex = "X2" });

            Assert.Equal(3, await context.SaveChangesAsync());
        }

        async Task<BinaryKeyDataType> QueryByBinaryKey(DbContext context, byte[] bytes)
            => (await context.Set<BinaryKeyDataType>().Where(e => e.Id == bytes).ToListAsync())
                .Single();

        await using (var context = CreateContext())
        {
            var entity1 = await QueryByBinaryKey(context, [1, 2, 3]);
            Assert.Equal(new byte[] { 1, 2, 3 }, entity1.Id);

            var entity2 = await QueryByBinaryKey(context, [1, 2, 3, 4]);
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, entity2.Id);

            var entity3 = await QueryByBinaryKey(context, [1, 2, 3, 4, 5]);
            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, entity3.Id);

            entity3.Ex = "Xx1";
            entity2.Ex = "Xx3";
            entity1.Ex = "Xx7";

            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext())
        {
            var entity1 = await QueryByBinaryKey(context, [1, 2, 3]);
            Assert.Equal("Xx7", entity1.Ex);

            var entity2 = await QueryByBinaryKey(context, [1, 2, 3, 4]);
            Assert.Equal("Xx3", entity2.Ex);

            var entity3 = await QueryByBinaryKey(context, [1, 2, 3, 4, 5]);
            Assert.Equal("Xx1", entity3.Ex);
        }
    }

    /// <inheritdoc />
    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task Can_insert_and_read_back_with_null_binary_foreign_key()
        => base.Can_insert_and_read_back_with_null_binary_foreign_key();

    /// <inheritdoc />
    public override async Task Can_insert_and_read_back_with_string_key()
    {
        await using (var context = CreateContext())
        {
            context.Set<StringKeyDataType>().Add(new StringKeyDataType { Id = "Gumball!" });

            Assert.Equal(1, await context.SaveChangesAsync());
        }

        await using (var context = CreateContext())
        {
            var entity = (await context
                .Set<StringKeyDataType>()
                .Where(e => e.Id == "Gumball!")
                .ToListAsync()).Single();

            Assert.Equal("Gumball!", entity.Id);
        }
    }

    /// <inheritdoc />
    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task Can_insert_and_read_back_with_null_string_foreign_key()
        => base.Can_insert_and_read_back_with_null_string_foreign_key();

#if NET10_0
    /// <inheritdoc />
    public override Task Can_insert_and_read_back_all_nullable_data_types_with_values_set_to_null()
        => base.Can_insert_and_read_back_all_nullable_data_types_with_values_set_to_null();

    /// <inheritdoc />
    public override Task
        Can_insert_and_read_back_all_nullable_data_types_with_values_set_to_non_null()
        => base.Can_insert_and_read_back_all_nullable_data_types_with_values_set_to_non_null();

    /// <inheritdoc />
    public override Task Can_insert_and_read_back_object_backed_data_types()
        => base.Can_insert_and_read_back_object_backed_data_types();

    /// <inheritdoc />
    public override Task Can_insert_and_read_back_nullable_backed_data_types()
        => base.Can_insert_and_read_back_nullable_backed_data_types();

    /// <inheritdoc />
    public override Task Can_insert_and_read_back_non_nullable_backed_data_types()
        => base.Can_insert_and_read_back_non_nullable_backed_data_types();
#endif

    /// <inheritdoc />
    public override async Task Can_read_back_mapped_enum_from_collection_first_or_default()
    {
        await using var context = CreateContext();
        var query =
            from animal in context.Set<DynamoAnimal>()
            select new { animal.Id, animal.IdentificationMethods.FirstOrDefault()!.Method };

        var result = await query.AsAsyncEnumerable().FirstOrDefaultAsync();
        Assert.Equal(IdentificationMethod.EarTag, result?.Method);
    }

    /// <inheritdoc />
    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Can_read_back_bool_mapped_as_int_through_navigation()
        => base.Can_read_back_bool_mapped_as_int_through_navigation();

    /// <inheritdoc />
    public override Task Can_compare_enum_to_constant() => base.Can_compare_enum_to_constant();

    /// <inheritdoc />
    public override Task Can_compare_enum_to_parameter() => base.Can_compare_enum_to_parameter();

    /// <inheritdoc />
    public override Task Object_to_string_conversion() => base.Object_to_string_conversion();

    /// <inheritdoc />
    public override Task Optional_datetime_reading_null_from_database()
        => base.Optional_datetime_reading_null_from_database();

    /// <inheritdoc />
    public override Task Can_insert_query_multiline_string()
        => base.Can_insert_query_multiline_string();

    private static async Task AssertNoSync(Func<Task> testCode)
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(testCode);

        Assert.Contains("Sync enumerating", exception.Message);
        Assert.Contains("DynamoDB", exception.Message);
    }

    private void AssertSql(params string[] expected) => Fixture.AssertSql(expected);

    /// <summary>Fixture for built-in data type specification tests.</summary>
    public class BuiltInDataTypesDynamoFixture
        : BuiltInDataTypesFixtureBase, IDynamoSpecificationFixture
    {
        /// <inheritdoc />
        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        /// <inheritdoc />
        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        /// <inheritdoc />
        protected override bool ShouldLogCategory(string logCategory)
            => DynamoSpecificationFixtureExtensions.ShouldLogDynamoSql(logCategory);

        /// <inheritdoc />
        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base
                .AddOptions(builder)
                .ConfigureWarnings(warnings => warnings.Ignore(
                    CoreEventId.ManyServiceProvidersCreatedWarning,
                    DynamoEventId.NoCompatibleSecondaryIndexFound,
                    DynamoEventId.ScanLikeQueryDetected))
                .UseDynamo(options
                    => options.DynamoDbClient(DynamoTestStoreFactory.Instance.Client))
                .UseAsyncSeeding(async (context, _, cancellationToken) =>
                {
                    if (await context
                        .FindAsync<DynamoAnimal>([1], cancellationToken)
                        .ConfigureAwait(false) is not null)
                        return;

                    context
                        .Set<DynamoAnimal>()
                        .Add(
                            new DynamoAnimal
                            {
                                Id = 1,
                                IdentificationMethods =
                                [
                                    new AnimalIdentification
                                    {
                                        Id = 1,
                                        AnimalId = 1,
                                        Method = IdentificationMethod.EarTag
                                    }
                                ],
                                Details = new AnimalDetails
                                {
                                    Id = 1, AnimalId = 1, BoolField = true
                                }
                            });

                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                });

        /// <inheritdoc />
        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.Entity<StringKeyDataType>(b => b.Ignore(e => e.Dependents));
            modelBuilder.Entity<StringKeyDataType>(b => b.Ignore(e => e.Dependents));

            modelBuilder.Entity<DynamoAnimal>(b =>
            {
                b.ComplexCollection(e => e.IdentificationMethods);
                b.ComplexProperty(e => e.Details);
            });

            modelBuilder.Ignore<Animal>();
            modelBuilder.Ignore<AnimalDetails>();
            // modelBuilder.Ignore<AnimalIdentification>();
            modelBuilder.Ignore<StringForeignKeyDataType>();
            modelBuilder.Ignore<BinaryForeignKeyDataType>();

            // TODO: remove and add better discriminator support
#if NET10_0
            modelBuilder.Entity<BuiltInDataTypesShadow>(b =>
            {
                b.Ignore("$type");
            });
#endif
        }

        /// <inheritdoc />
        public override bool StrictEquality => true;

        /// <inheritdoc />
        public override int IntegerPrecision => 64;

        /// <inheritdoc />
        public override bool SupportsAnsi => false;

        /// <inheritdoc />
        public override bool SupportsUnicodeToAnsiConversion => false;

        /// <inheritdoc />
        public override bool SupportsLargeStringComparisons => true;

        /// <inheritdoc />
        public override bool SupportsBinaryKeys => true;

        /// <inheritdoc />
        public override bool SupportsDecimalComparisons => true;

        /// <inheritdoc />
        public override DateTime DefaultDateTime => new();

        /// <inheritdoc />
        public override bool PreservesDateTimeKind => false;
    }

    protected class DynamoAnimal
    {
        public int Id { get; set; }
        public List<AnimalIdentification> IdentificationMethods { get; set; } = [];
        public required AnimalDetails Details { get; set; }
    }
}
