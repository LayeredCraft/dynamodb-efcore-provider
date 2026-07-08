using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.TestUtilities;
using Xunit;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public abstract class CustomConvertersDynamoTest(
    CustomConvertersDynamoTest.CustomConvertersDynamoFixture fixture)
    : CustomConvertersTestBase<CustomConvertersDynamoTest.CustomConvertersDynamoFixture>(fixture)
{
    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(CustomConvertersDynamoTest));

    public override Task Can_filter_projection_with_captured_enum_variable(bool async)
        => async
            ? base.Can_filter_projection_with_captured_enum_variable(async)
            : DynamoTestHelpers.Instance.NoSyncTest(
                async,
                base.Can_filter_projection_with_captured_enum_variable);

    public override Task Can_filter_projection_with_inline_enum_variable(bool async)
        => async
            ? base.Can_filter_projection_with_inline_enum_variable(async)
            : DynamoTestHelpers.Instance.NoSyncTest(
                async,
                base.Can_filter_projection_with_inline_enum_variable);

#if NET10_0
    public override Task Can_query_using_any_data_type() => base.Can_query_using_any_data_type();

    public override Task Can_query_using_any_data_type_shadow()
        => base.Can_query_using_any_data_type_shadow();

    // Nullable converted enum parameters currently hit provider-value expression rewriting for
    // nullable-to-non-nullable conversions.
    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_query_using_any_nullable_data_type()
        => base.Can_query_using_any_nullable_data_type();

    public override Task Can_query_using_any_data_type_nullable_shadow()
        => base.Can_query_using_any_data_type_nullable_shadow();

    // Nullable converted literals currently hit provider-value expression rewriting for
    // nullable-to-non-nullable conversions.
    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
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

    [ConditionalFact(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task Can_query_and_update_with_nullable_converter_on_unique_index()
        => base.Can_query_and_update_with_nullable_converter_on_unique_index();

    // Relational test relies on foreign-key relationship fixup; DynamoDB provider ignores these
    // navigations.
    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task Can_query_and_update_with_nullable_converter_on_primary_key()
        => base.Can_query_and_update_with_nullable_converter_on_primary_key();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_query_and_update_with_conversion_for_custom_type()
        => base.Can_query_and_update_with_conversion_for_custom_type();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_query_and_update_with_conversion_for_custom_struct()
        => base.Can_query_and_update_with_conversion_for_custom_struct();

    // Upstream test validates case-insensitive FK relationship fixup and Include. DynamoDB has no
    // FK/navigation support, so scalar key converter coverage lives in provider-specific test
    // below.
    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task Can_insert_and_read_back_with_case_insensitive_string_key()
        => base.Can_insert_and_read_back_with_case_insensitive_string_key();

    [ConditionalFact]
    public async Task Can_insert_and_read_back_with_case_insensitive_string_key_scalar_dynamo()
    {
        await using (var context = CreateContext())
        {
            context.Set<StringKeyDataType>().Add(new StringKeyDataType { Id = "Gumball!!" });

            Assert.Equal(1, await context.SaveChangesAsync());
        }

        await using (var context = CreateContext())
        {
            var entity = (await context
                .Set<StringKeyDataType>()
                .Where(e => e.Id == "Gumball!!")
                .ToListAsync()).Single();

            Assert.Equal("Gumball!!", entity.Id);
        }
    }

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_insert_and_read_back_with_string_list()
        => base.Can_insert_and_read_back_with_string_list();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_insert_and_query_struct_to_string_converter_for_pk()
        => base.Can_insert_and_query_struct_to_string_converter_for_pk();

    [ConditionalTheory(Skip = SkipReason.CustomTypeEqualityIssue241)]
    public override Task Can_query_custom_type_not_mapped_by_default_equality(bool async)
        => async
            ? base.Can_query_custom_type_not_mapped_by_default_equality(async)
            : DynamoTestHelpers.Instance.NoSyncTest(
                async,
                base.Can_query_custom_type_not_mapped_by_default_equality);

    public override Task Field_on_derived_type_retrieved_via_cast_applies_value_converter()
        => base.Field_on_derived_type_retrieved_via_cast_applies_value_converter();

    [ConditionalFact(Skip = SkipReason.JoinsNotSupported)]
    public override Task Value_conversion_is_appropriately_used_for_join_condition()
        => base.Value_conversion_is_appropriately_used_for_join_condition();

    [ConditionalFact(Skip = SkipReason.JoinsNotSupported)]
    public override Task Value_conversion_is_appropriately_used_for_left_join_condition()
        => base.Value_conversion_is_appropriately_used_for_left_join_condition();

    public override Task Where_bool_gets_converted_to_equality_when_value_conversion_is_used()
        => base.Where_bool_gets_converted_to_equality_when_value_conversion_is_used();

    public override Task
        Where_negated_bool_gets_converted_to_equality_when_value_conversion_is_used()
        => base.Where_negated_bool_gets_converted_to_equality_when_value_conversion_is_used();

    public override Task
        Where_bool_with_value_conversion_inside_comparison_doesnt_get_converted_twice()
        => base.Where_bool_with_value_conversion_inside_comparison_doesnt_get_converted_twice();

    public override Task Select_bool_with_value_conversion_is_used()
        => base.Select_bool_with_value_conversion_is_used();

    [ConditionalFact(Skip = SkipReason.ConditionalBoolValueConversionIssue243)]
    public override Task Where_conditional_bool_with_value_conversion_is_used()
        => base.Where_conditional_bool_with_value_conversion_is_used();

    [ConditionalFact(Skip = SkipReason.ConditionalBoolValueConversionIssue243)]
    public override Task Select_conditional_bool_with_value_conversion_is_used()
        => base.Select_conditional_bool_with_value_conversion_is_used();

    public override Task
        Where_bool_gets_converted_to_equality_when_value_conversion_is_used_using_EFProperty()
        => base
            .Where_bool_gets_converted_to_equality_when_value_conversion_is_used_using_EFProperty();

    public override Task
        Where_bool_gets_converted_to_equality_when_value_conversion_is_used_using_indexer()
        => base.Where_bool_gets_converted_to_equality_when_value_conversion_is_used_using_indexer();

    public override void Value_conversion_with_property_named_value()
        => base.Value_conversion_with_property_named_value();

    public override void Value_conversion_on_enum_collection_contains()
        => Assert.Contains(
            CoreStrings.TranslationFailed("")[47..],
            Assert.Throws<InvalidOperationException>(()
                    => base.Value_conversion_on_enum_collection_contains())
                .Message);

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override void Collection_property_as_scalar_Any()
        => base.Collection_property_as_scalar_Any();

    [ConditionalFact(Skip = SkipReason.CountAggregatesNotSupported)]
    public override void Collection_property_as_scalar_Count_member()
        => base.Collection_property_as_scalar_Count_member();

    public override void Collection_enum_as_string_Contains()
        => base.Collection_enum_as_string_Contains();

    [ConditionalFact(Skip = SkipReason.OwnedEntityTypesNotSupported)]
    public override void Optional_owned_with_converter_reading_non_nullable_column()
        => base.Optional_owned_with_converter_reading_non_nullable_column();

    public override Task Id_object_as_entity_key() => base.Id_object_as_entity_key();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override void Composition_over_collection_of_complex_mapped_as_scalar()
        => base.Composition_over_collection_of_complex_mapped_as_scalar();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override void GroupBy_converted_enum() => base.GroupBy_converted_enum();

    [ConditionalFact(Skip = SkipReason.SubqueryContainsNotSupported)]
    public override void Infer_type_mapping_from_in_subquery_to_item()
        => base.Infer_type_mapping_from_in_subquery_to_item();

    [Collection(DynamoSpecificationCollection.Name)]
    public sealed class CustomConvertersDynamoTestDefault : CustomConvertersDynamoTest
    {
        public CustomConvertersDynamoTestDefault(
            CustomConvertersDynamoFixture fixture,
            DynamoSpecificationContainerFixture containerFixture) : base(fixture)
            => _ = containerFixture;
    }

    public class CustomConvertersDynamoFixture
        : CustomConvertersFixtureBase, IDynamoSpecificationFixture
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
                    var hasChanges = false;

                    if (await context
                        .FindAsync<DynamoAnimal>([1], cancellationToken)
                        .ConfigureAwait(false) is null)
                    {
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

                        hasChanges = true;
                    }

                    if (!await context
                        .Set<RssBlog>()
                        .AsAsyncEnumerable()
                        .AnyAsync(b => b.BlogId == 2, cancellationToken)
                        .ConfigureAwait(false))
                    {
                        context
                            .Set<RssBlog>()
                            .Add(
                                new RssBlog
                                {
                                    BlogId = 2,
                                    Url = "http://rssblog.com",
                                    RssUrl = "http://rssblog.com/rss",
                                    IsVisible = false,
                                    ["IndexerVisible"] = true
                                });

                        hasChanges = true;
                    }

                    if (hasChanges)
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
            modelBuilder.Ignore<OwnedWithConverter>();

            modelBuilder.Entity<Parent>(b =>
            {
                var ownedWithConverter = b.ComplexProperty(e => e.OwnedWithConverter);
                ownedWithConverter.IsRequired(false);
                ownedWithConverter.Property(e => e.Value).HasConversion<string>();
            });

            modelBuilder.Entity<NullablePrincipal>(b => b.Ignore(e => e.Dependents));
            modelBuilder.Entity<NonNullableDependent>(b => b.Ignore(e => e.Principal));
            modelBuilder.Entity<Load>(b => b.HasPartitionKey(e => e.LoadId));
            modelBuilder.Entity<Blog>(b =>
            {
                b.Ignore(e => e.Posts);
                b.HasPartitionKey(e => e.BlogId);
            });
            modelBuilder.Entity<Post>(b =>
            {
                b.Ignore(e => e.Blog);
                b.Property(e => e.BlogId).HasConversion<int?>();
                b.HasPartitionKey(e => e.PostId);
            });
            modelBuilder.Entity<Order>(b
                => ReconfigureExplicitPrimaryKeyAsPartitionKey(b, nameof(Order.Id)));
            modelBuilder.Entity<SimpleCounter>(b
                => ReconfigureExplicitPrimaryKeyAsPartitionKey(b, nameof(SimpleCounter.CounterId)));
            modelBuilder.Entity<Book>(b
                => ReconfigureExplicitPrimaryKeyAsPartitionKey(b, nameof(Book.Id)));

            // TODO: remove and add better discriminator support
#if NET10_0
            modelBuilder.Entity<BuiltInDataTypesShadow>(b =>
            {
                b.Ignore("$type");
            });
#endif
        }

        private static void ReconfigureExplicitPrimaryKeyAsPartitionKey<TEntity>(
            EntityTypeBuilder<TEntity> builder,
            string propertyName) where TEntity : class
        {
            var primaryKey = builder.Metadata.FindPrimaryKey();
            if (primaryKey is not null)
            {
                var mutableEntityType = builder.Metadata;
                mutableEntityType.SetPrimaryKey((IReadOnlyList<IMutableProperty>?)null);
                mutableEntityType.RemoveKey(primaryKey.Properties);
            }

            builder.HasPartitionKey(propertyName);
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
