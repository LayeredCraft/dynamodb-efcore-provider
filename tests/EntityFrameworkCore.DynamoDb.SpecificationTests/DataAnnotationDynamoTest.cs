using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public abstract class DataAnnotationDynamoTest(
    DataAnnotationDynamoTest.DataAnnotationDynamoFixture fixture)
    : DataAnnotationTestBase<DataAnnotationDynamoTest.DataAnnotationDynamoFixture>(fixture)
{
    protected override TestHelpers TestHelpers => DynamoTestHelpers.Instance;

    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(DataAnnotationDynamoTest));

    public override void
        Explicit_configuration_on_derived_type_overrides_annotation_on_unmapped_base_type()
        => base.Explicit_configuration_on_derived_type_overrides_annotation_on_unmapped_base_type();

    public override void
        Explicit_configuration_on_derived_type_overrides_annotation_on_mapped_base_type()
        => base.Explicit_configuration_on_derived_type_overrides_annotation_on_mapped_base_type();

    public override void Explicit_configuration_on_derived_type_or_base_type_is_last_one_wins()
        => base.Explicit_configuration_on_derived_type_or_base_type_is_last_one_wins();

    public override void Duplicate_column_order_is_ignored()
        => base.Duplicate_column_order_is_ignored();

    public override IModel Non_public_annotations_are_enabled()
        => base.Non_public_annotations_are_enabled();

    public override IModel Field_annotations_are_enabled() => base.Field_annotations_are_enabled();

    public override void NotMapped_should_propagate_down_inheritance_hierarchy()
        => base.NotMapped_should_propagate_down_inheritance_hierarchy();

    public override void NotMapped_on_base_class_property_ignores_it()
        => base.NotMapped_on_base_class_property_ignores_it();

    public override void NotMapped_on_base_class_property_and_overridden_property_ignores_them()
        => base.NotMapped_on_base_class_property_and_overridden_property_ignores_them();

    public override void NotMapped_on_base_class_property_discovered_through_navigation_ignores_it()
        => base.NotMapped_on_base_class_property_discovered_through_navigation_ignores_it();

    public override void NotMapped_on_overridden_property_is_ignored()
        => base.NotMapped_on_overridden_property_is_ignored();

    public override void NotMapped_on_unmapped_derived_property_ignores_it()
        => base.NotMapped_on_unmapped_derived_property_ignores_it();

    public override void NotMapped_on_abstract_base_class_property_ignores_it()
        => base.NotMapped_on_abstract_base_class_property_ignores_it();

    public override void
        NotMapped_on_unmapped_base_class_property_and_overridden_property_ignores_it()
        => base.NotMapped_on_unmapped_base_class_property_and_overridden_property_ignores_it();

    public override void NotMapped_on_unmapped_base_class_property_ignores_it()
        => base.NotMapped_on_unmapped_base_class_property_ignores_it();

    public override void
        NotMapped_on_new_property_with_same_name_as_in_unmapped_base_class_ignores_it()
        => base.NotMapped_on_new_property_with_same_name_as_in_unmapped_base_class_ignores_it();

    public override void StringLength_with_value_takes_precedence_over_MaxLength()
        => base.StringLength_with_value_takes_precedence_over_MaxLength();

    public override void MaxLength_with_length_takes_precedence_over_StringLength()
        => base.MaxLength_with_length_takes_precedence_over_StringLength();

    public override ModelBuilder Default_length_for_key_string_column()
        => base.Default_length_for_key_string_column();

    public override IModel Key_and_column_work_together() => base.Key_and_column_work_together();

    public override IModel Key_and_MaxLength_64_produce_nvarchar_64()
        => base.Key_and_MaxLength_64_produce_nvarchar_64();

    public override void Key_from_base_type_is_recognized()
        => base.Key_from_base_type_is_recognized();

    public override void Key_from_base_type_is_recognized_if_base_discovered_first()
        => base.Key_from_base_type_is_recognized_if_base_discovered_first();

    public override void Key_from_base_type_is_recognized_if_discovered_through_relationship()
        => base.Key_from_base_type_is_recognized_if_discovered_through_relationship();

    public override void Key_on_nav_prop_is_ignored() => base.Key_on_nav_prop_is_ignored();

    public override ModelBuilder Key_property_is_not_used_for_FK_when_set_by_annotation()
        => base.Key_property_is_not_used_for_FK_when_set_by_annotation();

    public override ModelBuilder Key_specified_on_multiple_properties_can_be_overridden()
        => base.Key_specified_on_multiple_properties_can_be_overridden();

    public override void Keyless_and_key_attributes_which_conflict_cause_warning()
        => base.Keyless_and_key_attributes_which_conflict_cause_warning();

    public override void Keyless_fluent_api_and_key_attribute_do_not_cause_warning()
        => base.Keyless_fluent_api_and_key_attribute_do_not_cause_warning();

    public override void Key_fluent_api_and_keyless_attribute_do_not_cause_warning()
        => base.Key_fluent_api_and_keyless_attribute_do_not_cause_warning();

    public override void Fluent_API_relationship_throws_for_Keyless_attribute()
        => base.Fluent_API_relationship_throws_for_Keyless_attribute();

    public override IModel DatabaseGeneratedOption_configures_the_property_correctly()
        => base.DatabaseGeneratedOption_configures_the_property_correctly();

    public override IModel
        DatabaseGeneratedOption_Identity_does_not_throw_on_noninteger_properties()
        => base.DatabaseGeneratedOption_Identity_does_not_throw_on_noninteger_properties();

    public override IModel Timestamp_takes_precedence_over_MaxLength()
        => base.Timestamp_takes_precedence_over_MaxLength();

    public override void Annotation_in_derived_class_when_base_class_processed_after_derived_class()
        => base.Annotation_in_derived_class_when_base_class_processed_after_derived_class();

    public override void Required_and_ForeignKey_to_Required()
        => base.Required_and_ForeignKey_to_Required();

    public override void Required_to_Required_and_ForeignKey()
        => base.Required_to_Required_and_ForeignKey();

    public override void Required_and_ForeignKey_to_Required_and_ForeignKey()
        => base.Required_and_ForeignKey_to_Required_and_ForeignKey();

    public override void Required_and_ForeignKey_to_Required_and_ForeignKey_can_be_overridden()
        => base.Required_and_ForeignKey_to_Required_and_ForeignKey_can_be_overridden();

    public override void Required_and_ForeignKey_to_ForeignKey_can_be_overridden()
        => base.Required_and_ForeignKey_to_ForeignKey_can_be_overridden();

    public override void ForeignKey_to_nothing() => base.ForeignKey_to_nothing();

    public override void Required_and_ForeignKey_to_nothing()
        => base.Required_and_ForeignKey_to_nothing();

    public override void Nothing_to_ForeignKey() => base.Nothing_to_ForeignKey();

    public override void Nothing_to_Required_and_ForeignKey()
        => base.Nothing_to_Required_and_ForeignKey();

    public override void ForeignKey_to_ForeignKey() => base.ForeignKey_to_ForeignKey();

    public override void ForeignKey_to_ForeignKey_same_name()
        => base.ForeignKey_to_ForeignKey_same_name();

    public override void ForeignKey_to_ForeignKey_same_name_one_shadow()
        => base.ForeignKey_to_ForeignKey_same_name_one_shadow();

    public override void Required_to_Nothing() => base.Required_to_Nothing();

    public override void Required_to_Nothing_inverted() => base.Required_to_Nothing_inverted();

    public override void Shared_ForeignKey_to_different_principals()
        => base.Shared_ForeignKey_to_different_principals();

    public override void Inverse_and_self_ref_ForeignKey()
        => base.Inverse_and_self_ref_ForeignKey();

    public override void Multiple_self_ref_ForeignKeys_on_navigations()
        => base.Multiple_self_ref_ForeignKeys_on_navigations();

    public override void Multiple_self_ref_ForeignKeys_on_properties()
        => base.Multiple_self_ref_ForeignKeys_on_properties();

    public override void Multiple_self_ref_ForeignKey_and_Inverse()
        => base.Multiple_self_ref_ForeignKey_and_Inverse();

    public override void ForeignKeyAttribute_configures_relationships_when_inverse_on_derived()
        => base.ForeignKeyAttribute_configures_relationships_when_inverse_on_derived();

    public override void ForeignKeyAttribute_configures_two_self_referencing_relationships()
        => base.ForeignKeyAttribute_configures_two_self_referencing_relationships();

    public override IModel TableNameAttribute_affects_table_name_in_TPH()
        => base.TableNameAttribute_affects_table_name_in_TPH();

    public override Task ConcurrencyCheckAttribute_throws_if_value_in_database_changed()
        => base.ConcurrencyCheckAttribute_throws_if_value_in_database_changed();

    public override Task DatabaseGeneratedAttribute_autogenerates_values_when_set_to_identity()
        => base.DatabaseGeneratedAttribute_autogenerates_values_when_set_to_identity();

    public override async Task
        MaxLengthAttribute_throws_while_inserting_value_longer_than_max_length()
        => await base.MaxLengthAttribute_throws_while_inserting_value_longer_than_max_length();

    public override void NotMappedAttribute_ignores_entityType()
        => base.NotMappedAttribute_ignores_entityType();

    public override void NotMappedAttribute_ignores_navigation()
        => base.NotMappedAttribute_ignores_navigation();

    public override void NotMappedAttribute_ignores_property()
        => base.NotMappedAttribute_ignores_property();

    public override void NotMappedAttribute_ignores_explicit_interface_implementation_property()
        => base.NotMappedAttribute_ignores_explicit_interface_implementation_property();

    public override void NotMappedAttribute_removes_ambiguity_in_relationship_building()
        => base.NotMappedAttribute_removes_ambiguity_in_relationship_building();

    public override void NotMappedAttribute_removes_ambiguity_in_relationship_building_with_base()
        => base.NotMappedAttribute_removes_ambiguity_in_relationship_building_with_base();

    public override void InversePropertyAttribute_removes_ambiguity()
        => base.InversePropertyAttribute_removes_ambiguity();

    public override void InversePropertyAttribute_removes_ambiguity_with_base_type()
        => base.InversePropertyAttribute_removes_ambiguity_with_base_type();

    public override void InversePropertyAttribute_removes_ambiguity_with_base_type_ignored()
        => base.InversePropertyAttribute_removes_ambiguity_with_base_type_ignored();

    public override void InversePropertyAttribute_from_ignored_base_causes_ambiguity()
        => base.InversePropertyAttribute_from_ignored_base_causes_ambiguity();

    public override void
        InversePropertyAttribute_from_ignored_base_can_be_ignored_to_remove_ambiguity()
        => base.InversePropertyAttribute_from_ignored_base_can_be_ignored_to_remove_ambiguity();

    public override void InversePropertyAttribute_removes_ambiguity_from_the_ambiguous_end()
        => base.InversePropertyAttribute_removes_ambiguity_from_the_ambiguous_end();

    public override void
        InversePropertyAttribute_removes_ambiguity_when_combined_with_other_attributes()
        => base.InversePropertyAttribute_removes_ambiguity_when_combined_with_other_attributes();

    public override void InversePropertyAttribute_removes_ambiguity_with_base_type_bidirectional()
        => base.InversePropertyAttribute_removes_ambiguity_with_base_type_bidirectional();

    public override void InversePropertyAttribute_is_noop_in_unambiguous_models()
        => base.InversePropertyAttribute_is_noop_in_unambiguous_models();

    public override void InversePropertyAttribute_pointing_to_same_nav_on_base_causes_ambiguity()
        => base.InversePropertyAttribute_pointing_to_same_nav_on_base_causes_ambiguity();

    public override void InversePropertyAttribute_pointing_to_same_nav_on_base_with_one_ignored()
        => base.InversePropertyAttribute_pointing_to_same_nav_on_base_with_one_ignored();

    public override void
        InversePropertyAttribute_pointing_to_same_skip_nav_on_base_causes_ambiguity()
        => base.InversePropertyAttribute_pointing_to_same_skip_nav_on_base_causes_ambiguity();

    public override void
        ForeignKeyAttribute_creates_two_relationships_if_applied_on_property_on_both_side()
        => base.ForeignKeyAttribute_creates_two_relationships_if_applied_on_property_on_both_side();

    public override void
        ForeignKeyAttribute_creates_two_relationships_if_applied_on_navigations_on_both_sides_and_values_do_not_match()
        => base
            .ForeignKeyAttribute_creates_two_relationships_if_applied_on_navigations_on_both_sides_and_values_do_not_match();

    public override void
        ForeignKeyAttribute_throws_if_applied_on_two_relationships_targetting_the_same_property()
        => base
            .ForeignKeyAttribute_throws_if_applied_on_two_relationships_targetting_the_same_property();

    public override void Attribute_set_shadow_FK_name_is_preserved_with_HasPrincipalKey()
        => base.Attribute_set_shadow_FK_name_is_preserved_with_HasPrincipalKey();

    public override async Task RequiredAttribute_for_navigation_throws_while_inserting_null_value()
        => await base.RequiredAttribute_for_navigation_throws_while_inserting_null_value();

    public override async Task RequiredAttribute_for_property_throws_while_inserting_null_value()
        => await base.RequiredAttribute_for_property_throws_while_inserting_null_value();

    public override async Task
        StringLengthAttribute_throws_while_inserting_value_longer_than_max_length()
        => await base.StringLengthAttribute_throws_while_inserting_value_longer_than_max_length();

    public override Task TimestampAttribute_throws_if_value_in_database_changed()
        => base.TimestampAttribute_throws_if_value_in_database_changed();

    public override void UnicodeAttribute_sets_unicode_for_properties_and_fields()
        => base.UnicodeAttribute_sets_unicode_for_properties_and_fields();

    public override void PrecisionAttribute_sets_precision_for_properties_and_fields()
        => base.PrecisionAttribute_sets_precision_for_properties_and_fields();

    public override void OwnedEntityTypeAttribute_configures_one_reference_as_owned()
        => base.OwnedEntityTypeAttribute_configures_one_reference_as_owned();

    public override void OwnedEntityTypeAttribute_configures_all_references_as_owned()
        => base.OwnedEntityTypeAttribute_configures_all_references_as_owned();

    public override void InverseProperty_with_case_sensitive_clr_property()
        => base.InverseProperty_with_case_sensitive_clr_property();

    public override void InverseProperty_with_potentially_ambigous_derived_types()
        => base.InverseProperty_with_potentially_ambigous_derived_types();

    public class DataAnnotationDynamoFixture
        : DataAnnotationFixtureBase, IDynamoSpecificationFixture
    {
        public TestSqlLoggerFactory TestSqlLoggerFactory => (TestSqlLoggerFactory)ListLoggerFactory;

        protected override ITestStoreFactory TestStoreFactory => DynamoTestStoreFactory.Instance;

        public override DbContextOptionsBuilder AddOptions(DbContextOptionsBuilder builder)
            => base
                .AddOptions(builder)
                .ConfigureWarnings(w => w.Log(CoreEventId.MappedEntityTypeIgnoredWarning))
                .UseDynamo(o => o.DynamoDbClient(DynamoTestStoreFactory.Instance.Client));

        protected override bool ShouldLogCategory(string logCategory)
            => base.ShouldLogCategory(logCategory)
                || DynamoSpecificationFixtureExtensions.ShouldLogDynamoSql(logCategory);

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            modelBuilder.Entity<Book>().Ignore(e => e.Label);
            modelBuilder.Entity<Book>().Ignore(e => e.AlternateLabel);
            modelBuilder.Entity<Book>().Ignore(e => e.Details);

            modelBuilder.Ignore<BookDetails>();
            modelBuilder.Ignore<BookLabel>();
            modelBuilder.Ignore<SpecialBookLabel>();
            modelBuilder.Ignore<ExtraSpecialBookLabel>();
            modelBuilder.Ignore<AnotherBookLabel>();
            modelBuilder.Ignore<AdditionalBookDetails>();
            modelBuilder.Ignore<KeylessAndKeyAttributes>();
            modelBuilder.Ignore<KeylessFluentApiAndKeyAttribute>();
            modelBuilder.Ignore<KeyFluentApiAndKeylessAttribute>();
            modelBuilder.Ignore<One>();
            modelBuilder.Ignore<Two>();
            modelBuilder.Ignore<Book>();
        }

        protected override Task SeedAsync(PoolableDbContext context) => Task.CompletedTask;
    }

    [Collection(DynamoSpecificationCollection.Name)]
    public class DataAnnotationDynamoTestDefault(
        DataAnnotationDynamoFixture fixture,
        DynamoSpecificationContainerFixture containerFixture) : DataAnnotationDynamoTest(fixture)
    {
        private readonly DynamoSpecificationContainerFixture _containerFixture = containerFixture;
    }
}
