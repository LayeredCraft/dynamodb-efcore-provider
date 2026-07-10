using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public class ConvertToProviderTypesDynamoTest(
    ConvertToProviderTypesDynamoTest.ConvertToProviderTypesDynamoFixture fixture)
    : ConvertToProviderTypesTestBase<
        ConvertToProviderTypesDynamoTest.ConvertToProviderTypesDynamoFixture>(fixture)
{
    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(
            typeof(ConvertToProviderTypesDynamoTest));

    public override Task Can_filter_projection_with_captured_enum_variable(bool async)
        => async
            ? base.Can_filter_projection_with_captured_enum_variable(async)
            : DynamoTestHelpers.Instance.NoSyncTest(()
                => base.Can_filter_projection_with_captured_enum_variable(async));

    public override Task Can_filter_projection_with_inline_enum_variable(bool async)
        => async
            ? base.Can_filter_projection_with_inline_enum_variable(async)
            : DynamoTestHelpers.Instance.NoSyncTest(()
                => base.Can_filter_projection_with_inline_enum_variable(async));

#if NET10_0
    public override Task Can_query_using_any_data_type() => base.Can_query_using_any_data_type();

    public override Task Can_query_using_any_data_type_shadow()
        => base.Can_query_using_any_data_type_shadow();

    public override Task Can_query_using_any_nullable_data_type()
        => base.Can_query_using_any_nullable_data_type();

    public override Task Can_query_using_any_data_type_nullable_shadow()
        => base.Can_query_using_any_data_type_nullable_shadow();

    public override Task Can_query_using_any_nullable_data_type_as_literal()
        => base.Can_query_using_any_nullable_data_type_as_literal();

    public override Task Can_query_with_null_parameters_using_any_nullable_data_type()
        => base.Can_query_with_null_parameters_using_any_nullable_data_type();

    public override Task Can_insert_and_read_back_all_non_nullable_data_types()
        => base.Can_insert_and_read_back_all_non_nullable_data_types();
#endif

    public override Task Can_perform_query_with_max_length()
        => base.Can_perform_query_with_max_length();

    public override Task Can_perform_query_with_ansi_strings_test()
        => base.Can_perform_query_with_ansi_strings_test();

    public override Task Can_insert_and_read_with_max_length_set()
        => base.Can_insert_and_read_with_max_length_set();

    [ConditionalFact(Skip = SkipReason.SharedDataTypesFixtureRequiresForeignKeys)]
    public override Task Can_insert_and_read_back_with_binary_key()
        => base.Can_insert_and_read_back_with_binary_key();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task Can_insert_and_read_back_with_null_binary_foreign_key()
        => base.Can_insert_and_read_back_with_null_binary_foreign_key();

    [ConditionalFact(Skip = SkipReason.SharedDataTypesFixtureRequiresForeignKeys)]
    public override Task Can_insert_and_read_back_with_string_key()
        => base.Can_insert_and_read_back_with_string_key();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task Can_insert_and_read_back_with_null_string_foreign_key()
        => base.Can_insert_and_read_back_with_null_string_foreign_key();

#if NET10_0
    public override Task Can_insert_and_read_back_all_nullable_data_types_with_values_set_to_null()
        => base.Can_insert_and_read_back_all_nullable_data_types_with_values_set_to_null();

    public override Task
        Can_insert_and_read_back_all_nullable_data_types_with_values_set_to_non_null()
        => base.Can_insert_and_read_back_all_nullable_data_types_with_values_set_to_non_null();

    public override Task Can_insert_and_read_back_object_backed_data_types()
        => base.Can_insert_and_read_back_object_backed_data_types();

    public override Task Can_insert_and_read_back_nullable_backed_data_types()
        => base.Can_insert_and_read_back_nullable_backed_data_types();

    public override Task Can_insert_and_read_back_non_nullable_backed_data_types()
        => base.Can_insert_and_read_back_non_nullable_backed_data_types();
#else
    public override Task Can_insert_and_read_back_object_backed_data_types()
        => base.Can_insert_and_read_back_object_backed_data_types();
#endif

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Can_read_back_mapped_enum_from_collection_first_or_default()
        => base.Can_read_back_mapped_enum_from_collection_first_or_default();

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Can_read_back_bool_mapped_as_int_through_navigation()
        => base.Can_read_back_bool_mapped_as_int_through_navigation();

    public override Task Can_compare_enum_to_constant() => base.Can_compare_enum_to_constant();

    public override Task Can_compare_enum_to_parameter() => base.Can_compare_enum_to_parameter();

    public override Task Object_to_string_conversion() => base.Object_to_string_conversion();

    public override Task Optional_datetime_reading_null_from_database()
        => base.Optional_datetime_reading_null_from_database();

    public override Task Can_insert_query_multiline_string()
        => base.Can_insert_query_multiline_string();

    public override void Equals_method_over_enum_works()
        => DynamoTestHelpers.Instance.NoSyncTest(() => base.Equals_method_over_enum_works());

    public override void Object_equals_method_over_enum_works()
        => DynamoTestHelpers.Instance.NoSyncTest(() => base.Object_equals_method_over_enum_works());

    public class ConvertToProviderTypesDynamoFixture
        : ConvertToProviderTypesFixtureBase, IDynamoSpecificationFixture
    {
        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        protected override bool ShouldLogCategory(string logCategory)
            => DynamoSpecificationFixtureExtensions.ShouldLogDynamoSql(logCategory);

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
                                    Id = 1,
                                    AnimalId = 1,
                                    BoolField = true
                                }
                            });

                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                });

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.Entity<BinaryKeyDataType>(b => b.Ignore(e => e.Dependents));
            modelBuilder.Entity<StringKeyDataType>(b => b.Ignore(e => e.Dependents));

            modelBuilder.Entity<DynamoAnimal>(b =>
            {
                b.ComplexCollection(e => e.IdentificationMethods);
                b.ComplexProperty(e => e.Details);
            });

            modelBuilder.Ignore<Animal>();
            modelBuilder.Ignore<AnimalDetails>();
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

        public override bool StrictEquality => true;

        public override int IntegerPrecision => 64;

        public override bool SupportsAnsi => false;

        public override bool SupportsUnicodeToAnsiConversion => false;

        public override bool SupportsLargeStringComparisons => true;

        public override bool SupportsBinaryKeys => true;

        public override bool SupportsDecimalComparisons => true;

        public override DateTime DefaultDateTime => new();

        public override bool PreservesDateTimeKind => false;
    }

    protected class DynamoAnimal
    {
        public int Id { get; set; }
        public List<AnimalIdentification> IdentificationMethods { get; set; } = [];
        public required AnimalDetails Details { get; set; }
    }
}
