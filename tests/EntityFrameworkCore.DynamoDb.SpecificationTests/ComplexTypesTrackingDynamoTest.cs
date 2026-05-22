using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

/// <summary>Complex type tracking specification tests for the DynamoDB provider.</summary>
[Collection(DynamoSpecificationCollection.Name)]
public class ComplexTypesTrackingDynamoTest
    : ComplexTypesTrackingTestBase<ComplexTypesTrackingDynamoTest.ComplexTypesTrackingDynamoFixture>
{
    // DynamoDB SDK write APIs are async-only. Sync rows inherited from EF Core specification tests
    // intentionally no-op here; sync SaveChanges behavior is covered by provider-specific tests.

    /// <summary>Creates complex type tracking specification tests.</summary>
    public ComplexTypesTrackingDynamoTest(
        ComplexTypesTrackingDynamoFixture fixture,
        DynamoSpecificationContainerFixture containerFixture) : base(fixture)
        => _ = containerFixture;

    /// <summary>Ensures all inherited specification tests are reviewed by this provider.</summary>
    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(ComplexTypesTrackingDynamoTest));

    public override Task Can_track_entity_with_complex_objects(EntityState state, bool async)
        => async ? base.Can_track_entity_with_complex_objects(state, async) : Task.CompletedTask;

    public override Task Can_track_entity_with_complex_structs(EntityState state, bool async)
        => async ? base.Can_track_entity_with_complex_structs(state, async) : Task.CompletedTask;

    public override Task
        Can_track_entity_with_complex_readonly_structs(EntityState state, bool async)
        => async
            ? base.Can_track_entity_with_complex_readonly_structs(state, async)
            : Task.CompletedTask;

    public override Task Can_track_entity_with_complex_record_objects(EntityState state, bool async)
        => async
            ? base.Can_track_entity_with_complex_record_objects(state, async)
            : Task.CompletedTask;

    public override Task
        Can_track_entity_with_complex_objects_with_fields(EntityState state, bool async)
        => async
            ? base.Can_track_entity_with_complex_objects_with_fields(state, async)
            : Task.CompletedTask;

    public override Task
        Can_track_entity_with_complex_structs_with_fields(EntityState state, bool async)
        => async
            ? base.Can_track_entity_with_complex_structs_with_fields(state, async)
            : Task.CompletedTask;

    public override Task
        Can_track_entity_with_complex_readonly_structs_with_fields(EntityState state, bool async)
        => async
            ? base.Can_track_entity_with_complex_readonly_structs_with_fields(state, async)
            : Task.CompletedTask;

    public override Task
        Can_track_entity_with_complex_record_objects_with_fields(EntityState state, bool async)
        => async
            ? base.Can_track_entity_with_complex_record_objects_with_fields(state, async)
            : Task.CompletedTask;

    public override Task
        Can_track_entity_with_complex_type_collections(EntityState state, bool async)
        => async
            ? base.Can_track_entity_with_complex_type_collections(state, async)
            : Task.CompletedTask;

    public override Task Can_change_state_from_Deleted_with_complex_collection(
        EntityState newState,
        bool async)
        => async
            ? base.Can_change_state_from_Deleted_with_complex_collection(newState, async)
            : Task.CompletedTask;

    public override Task Can_change_state_from_Deleted_with_complex_record_collection(
        EntityState newState,
        bool async)
        => async
            ? base.Can_change_state_from_Deleted_with_complex_record_collection(newState, async)
            : Task.CompletedTask;

    public override Task Can_change_state_from_Deleted_with_complex_field_collection(
        EntityState newState,
        bool async)
        => async
            ? base.Can_change_state_from_Deleted_with_complex_field_collection(newState, async)
            : Task.CompletedTask;

    public override Task Can_change_state_from_Deleted_with_complex_field_record_collection(
        EntityState newState,
        bool async)
        => async
            ? base.Can_change_state_from_Deleted_with_complex_field_record_collection(
                newState,
                async)
            : Task.CompletedTask;

    public override Task
        Can_track_entity_with_complex_record_collections(EntityState state, bool async)
        => async
            ? base.Can_track_entity_with_complex_record_collections(state, async)
            : Task.CompletedTask;

    public override Task
        Can_track_entity_with_complex_field_collections(EntityState state, bool async)
        => async
            ? base.Can_track_entity_with_complex_field_collections(state, async)
            : Task.CompletedTask;

    public override Task
        Can_track_entity_with_complex_record_collections_with_fields(EntityState state, bool async)
        => async
            ? base.Can_track_entity_with_complex_record_collections_with_fields(state, async)
            : Task.CompletedTask;

    public override Task Can_track_entity_with_complex_type_array_collections(
        EntityState state,
        bool async)
        => async
            ? base.Can_track_entity_with_complex_type_array_collections(state, async)
            : Task.CompletedTask;

    public override Task Can_track_entity_with_complex_struct_array_collections(
        EntityState state,
        bool async)
        => async
            ? base.Can_track_entity_with_complex_struct_array_collections(state, async)
            : Task.CompletedTask;

    public override Task Can_track_entity_with_complex_readonly_struct_array_collections(
        EntityState state,
        bool async)
        => async
            ? base.Can_track_entity_with_complex_readonly_struct_array_collections(state, async)
            : Task.CompletedTask;

    public override Task Can_track_entity_with_complex_record_array_collections(
        EntityState state,
        bool async)
        => async
            ? base.Can_track_entity_with_complex_record_array_collections(state, async)
            : Task.CompletedTask;

    public override Task
        Can_track_entity_with_complex_property_bag_collections(EntityState state, bool async)
        => async
            ? base.Can_track_entity_with_complex_property_bag_collections(state, async)
            : Task.CompletedTask;

    public override Task
        Can_save_null_second_level_complex_property_with_required_properties(bool async)
        => async
            ? ExecuteWithStrategyInTransactionAsync(async context =>
            {
                var yogurt = CreateYogurt(context, nullLicense: true);
                await context.AddAsync(yogurt).ConfigureAwait(false);
                await context.SaveChangesAsync().ConfigureAwait(false);
            })
            : Task.CompletedTask;

    public override Task
        Can_save_null_third_level_complex_property_with_all_optional_properties(bool async)
        => async
            ? ExecuteWithStrategyInTransactionAsync(async context =>
            {
                var yogurt = CreateYogurt(context, nullTag: true);
                await context.AddAsync(yogurt).ConfigureAwait(false);
                await context.SaveChangesAsync().ConfigureAwait(false);
            })
            : Task.CompletedTask;

    public override Task
        Can_save_default_values_in_optional_complex_property_with_multiple_properties(bool async)
    {
        if (!async)
            return Task.CompletedTask;

        var id = Guid.NewGuid();
        return ExecuteWithStrategyInTransactionAsync(
            async context =>
            {
                var entity = Fixture.UseProxies
                    ? context.CreateProxy<EntityWithOptionalMultiPropComplex>()
                    : new EntityWithOptionalMultiPropComplex();

                entity.Id = id;
                entity.ComplexProp = null;

                await context.AddAsync(entity).ConfigureAwait(false);
                await context.SaveChangesAsync().ConfigureAwait(false);

                Assert.Null(entity.ComplexProp);
            },
            async context =>
            {
                var entity =
                    await context
                        .Set<EntityWithOptionalMultiPropComplex>()
                        .AsAsyncEnumerable()
                        .SingleAsync(e => e.Id == id)
                        .ConfigureAwait(false);

                Assert.Null(entity.ComplexProp);

                // Set the complex property with default values
                entity.ComplexProp = new MultiPropComplex
                {
                    IntValue = 0, BoolValue = false, DateValue = default
                };

                await context.SaveChangesAsync().ConfigureAwait(false);

                Assert.NotNull(entity.ComplexProp);
                Assert.Equal(0, entity.ComplexProp.IntValue);
                Assert.False(entity.ComplexProp.BoolValue);
                Assert.Equal(default, entity.ComplexProp.DateValue);
            },
            async context =>
            {
                var entity =
                    await context
                        .Set<EntityWithOptionalMultiPropComplex>()
                        .AsAsyncEnumerable()
                        .SingleAsync(e => e.Id == id)
                        .ConfigureAwait(false);

                // Complex types with more than one property should materialize even with
                // default values
                Assert.NotNull(entity.ComplexProp);
                Assert.Equal(0, entity.ComplexProp.IntValue);
                Assert.False(entity.ComplexProp.BoolValue);
                Assert.Equal(default, entity.ComplexProp.DateValue);
            });
    }

    public override Task Can_null_complex_property_with_default_values_and_multiple_properties(
        bool async)
    {
        if (!async)
            return Task.CompletedTask;

        var id = Guid.NewGuid();
        return ExecuteWithStrategyInTransactionAsync(
            async context =>
            {
                var entity = Fixture.UseProxies
                    ? context.CreateProxy<EntityWithOptionalMultiPropComplex>()
                    : new EntityWithOptionalMultiPropComplex();

                entity.Id = id;
                // Set the complex property with default values
                entity.ComplexProp = new MultiPropComplex
                {
                    IntValue = 0, BoolValue = false, DateValue = default
                };

                await context.AddAsync(entity).ConfigureAwait(false);
                await context.SaveChangesAsync().ConfigureAwait(false);

                Assert.NotNull(entity.ComplexProp);
            },
            async context =>
            {
                var entity =
                    await context
                        .Set<EntityWithOptionalMultiPropComplex>()
                        .AsAsyncEnumerable()
                        .SingleAsync(e => e.Id == id)
                        .ConfigureAwait(false);

                Assert.NotNull(entity.ComplexProp);

                entity.ComplexProp = null;

                await context.SaveChangesAsync().ConfigureAwait(false);

                Assert.Null(entity.ComplexProp);
            },
            async context =>
            {
                var entity =
                    await context
                        .Set<EntityWithOptionalMultiPropComplex>()
                        .AsAsyncEnumerable()
                        .SingleAsync(e => e.Id == id)
                        .ConfigureAwait(false);

                Assert.Null(entity.ComplexProp);
            });
    }

    public override void Can_mark_complex_type_properties_modified(bool trackFromQuery)
        => base.Can_mark_complex_type_properties_modified(trackFromQuery);

    public override void
        Can_read_original_values_for_properties_of_complex_types(bool trackFromQuery)
        => base.Can_read_original_values_for_properties_of_complex_types(trackFromQuery);

    public override void
        Can_write_original_values_for_properties_of_complex_types(bool trackFromQuery)
        => base.Can_write_original_values_for_properties_of_complex_types(trackFromQuery);

    public override void Can_mark_complex_readonly_struct_properties_modified(bool trackFromQuery)
        => base.Can_mark_complex_readonly_struct_properties_modified(trackFromQuery);

    public override void Can_read_original_values_for_properties_of_structs(bool trackFromQuery)
        => base.Can_read_original_values_for_properties_of_structs(trackFromQuery);

    public override void Can_write_original_values_for_properties_of_structs(bool trackFromQuery)
        => base.Can_write_original_values_for_properties_of_structs(trackFromQuery);

    public override void
        Can_mark_complex_readonly_readonly_struct_properties_modified(bool trackFromQuery)
        => base.Can_mark_complex_readonly_readonly_struct_properties_modified(trackFromQuery);

    public override void
        Can_read_original_values_for_properties_of_readonly_structs(bool trackFromQuery)
        => base.Can_read_original_values_for_properties_of_readonly_structs(trackFromQuery);

    public override void
        Can_write_original_values_for_properties_of_readonly_structs(bool trackFromQuery)
        => base.Can_write_original_values_for_properties_of_readonly_structs(trackFromQuery);

    public override void Can_mark_complex_record_type_properties_modified(bool trackFromQuery)
        => base.Can_mark_complex_record_type_properties_modified(trackFromQuery);

    public override void
        Can_read_original_values_for_properties_of_record_complex_types(bool trackFromQuery)
        => base.Can_read_original_values_for_properties_of_record_complex_types(trackFromQuery);

    public override void
        Can_write_original_values_for_properties_of_record_complex_types(bool trackFromQuery)
        => base.Can_write_original_values_for_properties_of_record_complex_types(trackFromQuery);

    public override void Can_mark_complex_type_properties_modified_with_fields(bool trackFromQuery)
        => base.Can_mark_complex_type_properties_modified_with_fields(trackFromQuery);

    public override void
        Can_read_original_values_for_properties_of_complex_types_with_fields(bool trackFromQuery)
        => base.Can_read_original_values_for_properties_of_complex_types_with_fields(
            trackFromQuery);

    public override void
        Can_write_original_values_for_properties_of_complex_types_with_fields(bool trackFromQuery)
        => base.Can_write_original_values_for_properties_of_complex_types_with_fields(
            trackFromQuery);

    public override void
        Can_mark_complex_readonly_struct_properties_modified_with_fields(bool trackFromQuery)
        => base.Can_mark_complex_readonly_struct_properties_modified_with_fields(trackFromQuery);

    public override void
        Can_read_original_values_for_properties_of_structs_with_fields(bool trackFromQuery)
        => base.Can_read_original_values_for_properties_of_structs_with_fields(trackFromQuery);

    public override void
        Can_write_original_values_for_properties_of_structs_with_fields(bool trackFromQuery)
        => base.Can_write_original_values_for_properties_of_structs_with_fields(trackFromQuery);

    public override void
        Can_mark_complex_readonly_readonly_struct_properties_modified_with_fields(
            bool trackFromQuery)
        => base.Can_mark_complex_readonly_readonly_struct_properties_modified_with_fields(
            trackFromQuery);

    public override void
        Can_read_original_values_for_properties_of_readonly_structs_with_fields(bool trackFromQuery)
        => base.Can_read_original_values_for_properties_of_readonly_structs_with_fields(
            trackFromQuery);

    public override void
        Can_write_original_values_for_properties_of_readonly_structs_with_fields(
            bool trackFromQuery)
        => base.Can_write_original_values_for_properties_of_readonly_structs_with_fields(
            trackFromQuery);

    public override void
        Can_mark_complex_record_type_properties_modified_with_fields(bool trackFromQuery)
        => base.Can_mark_complex_record_type_properties_modified_with_fields(trackFromQuery);

    public override void
        Can_read_original_values_for_properties_of_record_complex_types_with_fields(
            bool trackFromQuery)
        => base.Can_read_original_values_for_properties_of_record_complex_types_with_fields(
            trackFromQuery);

    public override void
        Can_write_original_values_for_properties_of_record_complex_types_with_fields(
            bool trackFromQuery)
        => base.Can_write_original_values_for_properties_of_record_complex_types_with_fields(
            trackFromQuery);

    public override void Can_mark_complex_type_collection_properties_modified(bool trackFromQuery)
        => base.Can_mark_complex_type_collection_properties_modified(trackFromQuery);

    public override void
        Can_read_original_values_for_properties_of_complex_type_collections(bool trackFromQuery)
        => base.Can_read_original_values_for_properties_of_complex_type_collections(trackFromQuery);

    public override void
        Can_write_original_values_for_properties_of_complex_type_collections(bool trackFromQuery)
        => base.Can_write_original_values_for_properties_of_complex_type_collections(
            trackFromQuery);

    public override Task
        Can_change_state_from_Deleted_with_complex_struct_collection(
            EntityState newState,
            bool async)
        => async
            ? base.Can_change_state_from_Deleted_with_complex_struct_collection(newState, async)
            : Task.CompletedTask;

    public override Task
        Can_change_state_from_Deleted_with_complex_readonly_struct_collection(
            EntityState newState,
            bool async)
        => async
            ? base.Can_change_state_from_Deleted_with_complex_readonly_struct_collection(
                newState,
                async)
            : Task.CompletedTask;

    public override Task
        Can_change_state_from_Deleted_with_complex_field_struct_collection(
            EntityState newState,
            bool async)
        => async
            ? base.Can_change_state_from_Deleted_with_complex_field_struct_collection(
                newState,
                async)
            : Task.CompletedTask;

    public override Task
        Can_change_state_from_Deleted_with_complex_field_readonly_struct_collection(
            EntityState newState,
            bool async)
        => async
            ? base.Can_change_state_from_Deleted_with_complex_field_readonly_struct_collection(
                newState,
                async)
            : Task.CompletedTask;

    public override Task
        Can_track_entity_with_complex_struct_collections(EntityState state, bool async)
        => async
            ? base.Can_track_entity_with_complex_struct_collections(state, async)
            : Task.CompletedTask;

    public override void Can_mark_complex_struct_collection_properties_modified(bool trackFromQuery)
        => base.Can_mark_complex_struct_collection_properties_modified(trackFromQuery);

    public override void
        Can_read_original_values_for_properties_of_complex_struct_collections(bool trackFromQuery)
        => base.Can_read_original_values_for_properties_of_complex_struct_collections(
            trackFromQuery);

    public override void
        Can_write_original_values_for_properties_of_complex_struct_collections(bool trackFromQuery)
        => base.Can_write_original_values_for_properties_of_complex_struct_collections(
            trackFromQuery);

    public override Task
        Can_track_entity_with_complex_readonly_struct_collections(EntityState state, bool async)
        => async
            ? base.Can_track_entity_with_complex_readonly_struct_collections(state, async)
            : Task.CompletedTask;

    public override void
        Can_mark_complex_readonly_struct_collection_properties_modified(bool trackFromQuery)
        => base.Can_mark_complex_readonly_struct_collection_properties_modified(trackFromQuery);

    public override void
        Can_read_original_values_for_properties_of_readonly_struct_collections(bool trackFromQuery)
        => base.Can_read_original_values_for_properties_of_readonly_struct_collections(
            trackFromQuery);

    public override void
        Can_write_original_values_for_properties_of_readonly_struct_collections(bool trackFromQuery)
        => base.Can_write_original_values_for_properties_of_readonly_struct_collections(
            trackFromQuery);

    public override void Can_mark_complex_record_collection_properties_modified(bool trackFromQuery)
        => base.Can_mark_complex_record_collection_properties_modified(trackFromQuery);

    public override void
        Can_read_original_values_for_properties_of_complex_record_collections(bool trackFromQuery)
        => base.Can_read_original_values_for_properties_of_complex_record_collections(
            trackFromQuery);

    public override void
        Can_write_original_values_for_properties_of_complex_record_collections(bool trackFromQuery)
        => base.Can_write_original_values_for_properties_of_complex_record_collections(
            trackFromQuery);

    public override void Can_mark_complex_field_collection_properties_modified(bool trackFromQuery)
        => base.Can_mark_complex_field_collection_properties_modified(trackFromQuery);

    public override void
        Can_read_original_values_for_properties_of_complex_field_collections(bool trackFromQuery)
        => base.Can_read_original_values_for_properties_of_complex_field_collections(
            trackFromQuery);

    public override void
        Can_write_original_values_for_properties_of_complex_field_collections(bool trackFromQuery)
        => base.Can_write_original_values_for_properties_of_complex_field_collections(
            trackFromQuery);

    public override Task
        Can_track_entity_with_complex_struct_collections_with_fields(EntityState state, bool async)
        => async
            ? base.Can_track_entity_with_complex_struct_collections_with_fields(state, async)
            : Task.CompletedTask;

    public override void
        Can_mark_complex_struct_collections_with_fields_properties_modified(bool trackFromQuery)
        => base.Can_mark_complex_struct_collections_with_fields_properties_modified(trackFromQuery);

    public override void
        Can_read_original_values_for_properties_of_complex_struct_collections_with_fields(
            bool trackFromQuery)
        => base.Can_read_original_values_for_properties_of_complex_struct_collections_with_fields(
            trackFromQuery);

    public override void
        Can_write_original_values_for_properties_of_complex_struct_collections_with_fields(
            bool trackFromQuery)
        => base.Can_write_original_values_for_properties_of_complex_struct_collections_with_fields(
            trackFromQuery);

    public override void
        Can_mark_complex_record_collections_with_fields_properties_modified(bool trackFromQuery)
        => base.Can_mark_complex_record_collections_with_fields_properties_modified(trackFromQuery);

    public override void
        Can_read_original_values_for_properties_of_complex_record_collections_with_fields(
            bool trackFromQuery)
        => base.Can_read_original_values_for_properties_of_complex_record_collections_with_fields(
            trackFromQuery);

    public override void
        Can_write_original_values_for_properties_of_complex_record_collections_with_fields(
            bool trackFromQuery)
        => base.Can_write_original_values_for_properties_of_complex_record_collections_with_fields(
            trackFromQuery);

    public override Task
        Can_track_entity_with_complex_readonly_struct_collections_with_fields(
            EntityState state,
            bool async)
        => async
            ? base.Can_track_entity_with_complex_readonly_struct_collections_with_fields(
                state,
                async)
            : Task.CompletedTask;

    public override void
        Can_mark_complex_readonly_struct_collections_with_fields_properties_modified(
            bool trackFromQuery)
        => base.Can_mark_complex_readonly_struct_collections_with_fields_properties_modified(
            trackFromQuery);

    public override void
        Can_read_original_values_for_properties_of_complex_readonly_struct_collections_with_fields(
            bool trackFromQuery)
        => base
            .Can_read_original_values_for_properties_of_complex_readonly_struct_collections_with_fields(
                trackFromQuery);

    public override void
        Can_write_original_values_for_properties_of_complex_readonly_struct_collections_with_fields(
            bool trackFromQuery)
        => base
            .Can_write_original_values_for_properties_of_complex_readonly_struct_collections_with_fields(
                trackFromQuery);

    public override void
        Can_mark_complex_type_array_collection_properties_modified(bool trackFromQuery)
        => base.Can_mark_complex_type_array_collection_properties_modified(trackFromQuery);

    public override void
        Can_read_original_values_for_properties_of_complex_type_array_collections(
            bool trackFromQuery)
        => base.Can_read_original_values_for_properties_of_complex_type_array_collections(
            trackFromQuery);

    public override void
        Can_write_original_values_for_properties_of_complex_type_array_collections(
            bool trackFromQuery)
        => base.Can_write_original_values_for_properties_of_complex_type_array_collections(
            trackFromQuery);

    public override void
        Can_mark_complex_struct_array_collection_properties_modified(bool trackFromQuery)
        => base.Can_mark_complex_struct_array_collection_properties_modified(trackFromQuery);

    public override void
        Can_read_original_values_for_properties_of_complex_struct_array_collections(
            bool trackFromQuery)
        => base.Can_read_original_values_for_properties_of_complex_struct_array_collections(
            trackFromQuery);

    public override void
        Can_write_original_values_for_properties_of_complex_struct_array_collections(
            bool trackFromQuery)
        => base.Can_write_original_values_for_properties_of_complex_struct_array_collections(
            trackFromQuery);

    public override void
        Can_mark_complex_readonly_struct_array_collection_properties_modified(bool trackFromQuery)
        => base.Can_mark_complex_readonly_struct_array_collection_properties_modified(
            trackFromQuery);

    public override void
        Can_read_original_values_for_properties_of_complex_readonly_struct_array_collections(
            bool trackFromQuery)
        => base
            .Can_read_original_values_for_properties_of_complex_readonly_struct_array_collections(
                trackFromQuery);

    public override void
        Can_write_original_values_for_properties_of_complex_readonly_struct_array_collections(
            bool trackFromQuery)
        => base
            .Can_write_original_values_for_properties_of_complex_readonly_struct_array_collections(
                trackFromQuery);

    public override void
        Can_mark_complex_record_array_collection_properties_modified(bool trackFromQuery)
        => base.Can_mark_complex_record_array_collection_properties_modified(trackFromQuery);

    public override void
        Can_read_original_values_for_properties_of_complex_record_array_collections(
            bool trackFromQuery)
        => base.Can_read_original_values_for_properties_of_complex_record_array_collections(
            trackFromQuery);

    public override void
        Can_write_original_values_for_properties_of_complex_record_array_collections(
            bool trackFromQuery)
        => base.Can_write_original_values_for_properties_of_complex_record_array_collections(
            trackFromQuery);

    public override void
        Can_mark_complex_property_bag_collection_properties_modified(bool trackFromQuery)
        => base.Can_mark_complex_property_bag_collection_properties_modified(trackFromQuery);

    public override void
        Can_read_original_values_for_properties_of_complex_property_bag_collections(
            bool trackFromQuery)
        => base.Can_read_original_values_for_properties_of_complex_property_bag_collections(
            trackFromQuery);

    public override void
        Can_write_original_values_for_properties_of_complex_property_bag_collections(
            bool trackFromQuery)
        => base.Can_write_original_values_for_properties_of_complex_property_bag_collections(
            trackFromQuery);

    public override void Detect_changes_in_complex_type_properties(bool trackFromQuery)
        => base.Detect_changes_in_complex_type_properties(trackFromQuery);

    public override Task Throws_only_when_saving_with_null_top_level_complex_property(bool async)
        => async
            ? base.Throws_only_when_saving_with_null_top_level_complex_property(async)
            : Task.CompletedTask;

    public override Task Throws_only_when_saving_with_null_second_level_complex_property(bool async)
        => async
            ? base.Throws_only_when_saving_with_null_second_level_complex_property(async)
            : Task.CompletedTask;

    public override void Detect_changes_in_complex_struct_type_properties(bool trackFromQuery)
        => base.Detect_changes_in_complex_struct_type_properties(trackFromQuery);

    public override void
        Detects_changes_in_complex_readonly_struct_type_properties(bool trackFromQuery)
        => base.Detects_changes_in_complex_readonly_struct_type_properties(trackFromQuery);

    public override void Detects_changes_in_complex_record_type_properties(bool trackFromQuery)
        => base.Detects_changes_in_complex_record_type_properties(trackFromQuery);

    public override void
        Can_detect_reordered_elements_in_complex_type_collections(bool trackFromQuery)
        => base.Can_detect_reordered_elements_in_complex_type_collections(trackFromQuery);

    public override void Can_detect_added_elements_in_complex_type_collections(bool trackFromQuery)
        => base.Can_detect_added_elements_in_complex_type_collections(trackFromQuery);

    public override void
        Can_detect_removed_elements_in_complex_type_collections(bool trackFromQuery)
        => base.Can_detect_removed_elements_in_complex_type_collections(trackFromQuery);

    public override void
        Can_detect_replaced_elements_in_complex_type_collections(bool trackFromQuery)
        => base.Can_detect_replaced_elements_in_complex_type_collections(trackFromQuery);

    public override void Can_detect_duplicates_in_complex_type_collections(bool trackFromQuery)
        => base.Can_detect_duplicates_in_complex_type_collections(trackFromQuery);

    public override void
        Can_remove_from_complex_collection_with_nested_complex_collection(bool trackFromQuery)
        => base.Can_remove_from_complex_collection_with_nested_complex_collection(trackFromQuery);

    public override void
        Can_remove_from_complex_struct_collection_with_nested_complex_collection(
            bool trackFromQuery)
        => base.Can_remove_from_complex_struct_collection_with_nested_complex_collection(
            trackFromQuery);

    public override void
        Can_remove_from_complex_readonly_struct_collection_with_nested_complex_collection(
            bool trackFromQuery)
        => base.Can_remove_from_complex_readonly_struct_collection_with_nested_complex_collection(
            trackFromQuery);

    public override void
        Can_remove_from_complex_record_collection_with_nested_complex_collection(
            bool trackFromQuery)
        => base.Can_remove_from_complex_record_collection_with_nested_complex_collection(
            trackFromQuery);

    public override void
        Can_remove_from_complex_field_collection_with_nested_complex_collection(bool trackFromQuery)
        => base.Can_remove_from_complex_field_collection_with_nested_complex_collection(
            trackFromQuery);

    public override void
        Can_remove_from_complex_struct_field_collection_with_nested_complex_collection(
            bool trackFromQuery)
        => base.Can_remove_from_complex_struct_field_collection_with_nested_complex_collection(
            trackFromQuery);

    public override void
        Can_remove_from_complex_readonly_struct_field_collection_with_nested_complex_collection(
            bool trackFromQuery)
        => base
            .Can_remove_from_complex_readonly_struct_field_collection_with_nested_complex_collection(
                trackFromQuery);

    public override void
        Can_remove_from_complex_record_field_collection_with_nested_complex_collection(
            bool trackFromQuery)
        => base.Can_remove_from_complex_record_field_collection_with_nested_complex_collection(
            trackFromQuery);

    public override void Can_handle_null_elements_in_complex_type_collections(bool trackFromQuery)
        => base.Can_handle_null_elements_in_complex_type_collections(trackFromQuery);

    public override void Can_detect_swapped_complex_objects_in_collections(bool trackFromQuery)
        => base.Can_detect_swapped_complex_objects_in_collections(trackFromQuery);

    public override void Can_detect_changes_to_struct_collection_elements(bool trackFromQuery)
        => base.Can_detect_changes_to_struct_collection_elements(trackFromQuery);

    public override void
        Can_detect_changes_to_readonly_struct_collection_elements(bool trackFromQuery)
        => base.Can_detect_changes_to_readonly_struct_collection_elements(trackFromQuery);

    public override void
        Can_handle_collection_with_mixed_null_and_duplicate_elements(bool trackFromQuery)
        => base.Can_handle_collection_with_mixed_null_and_duplicate_elements(trackFromQuery);

    public override void Can_detect_changes_to_record_collection_elements(bool trackFromQuery)
        => base.Can_detect_changes_to_record_collection_elements(trackFromQuery);

    public override void
        Can_detect_nested_collection_changes_in_complex_type_collections(bool trackFromQuery)
        => base.Can_detect_nested_collection_changes_in_complex_type_collections(trackFromQuery);

    public override void
        Can_detect_changes_to_nested_teams_members_in_complex_type_collections(bool trackFromQuery)
        => base.Can_detect_changes_to_nested_teams_members_in_complex_type_collections(
            trackFromQuery);

    public override void
        Can_detect_changes_to_nested_struct_teams_in_complex_type_collections(bool trackFromQuery)
        => base.Can_detect_changes_to_nested_struct_teams_in_complex_type_collections(
            trackFromQuery);

    public override void
        Can_detect_changes_to_nested_readonly_struct_teams_in_complex_type_collections(
            bool trackFromQuery)
        => base.Can_detect_changes_to_nested_readonly_struct_teams_in_complex_type_collections(
            trackFromQuery);

    public override void
        Can_detect_changes_to_record_teams_in_complex_type_collections(bool trackFromQuery)
        => base.Can_detect_changes_to_record_teams_in_complex_type_collections(trackFromQuery);

    public override void
        Can_handle_empty_nested_teams_in_complex_type_collections(bool trackFromQuery)
        => base.Can_handle_empty_nested_teams_in_complex_type_collections(trackFromQuery);

    public override void Throws_when_accessing_complex_entries_using_incorrect_cardinality()
        => base.Throws_when_accessing_complex_entries_using_incorrect_cardinality();

    protected override async Task ExecuteWithStrategyInTransactionAsync(
        Func<DbContext, Task> testOperation,
        Func<DbContext, Task>? nestedTestOperation1 = null,
        Func<DbContext, Task>? nestedTestOperation2 = null,
        Func<DbContext, Task>? nestedTestOperation3 = null)
    {
        await using var context = CreateContext();

        try
        {
            await context
                .Database
                .CreateExecutionStrategy()
                .ExecuteAsync(
                    context,
                    async _ =>
                    {
                        await using (var cleanupContext = CreateContext())
                        {
                            await Fixture
                                .TestStore
                                .CleanAsync(cleanupContext)
                                .ConfigureAwait(false);
                        }

                        await using (var innerContext = CreateContext())
                        {
                            innerContext.Database.AutoTransactionBehavior =
                                AutoTransactionBehavior.Never;
                            await testOperation(innerContext).ConfigureAwait(false);
                        }

                        if (nestedTestOperation1 is null)
                            return;

                        await using (var innerContext = CreateContext())
                        {
                            innerContext.Database.AutoTransactionBehavior =
                                AutoTransactionBehavior.Never;
                            await nestedTestOperation1(innerContext).ConfigureAwait(false);
                        }

                        if (nestedTestOperation2 is null)
                            return;

                        await using (var innerContext = CreateContext())
                        {
                            innerContext.Database.AutoTransactionBehavior =
                                AutoTransactionBehavior.Never;
                            await nestedTestOperation2(innerContext).ConfigureAwait(false);
                        }

                        if (nestedTestOperation3 is null)
                            return;

                        await using (var innerContext = CreateContext())
                        {
                            innerContext.Database.AutoTransactionBehavior =
                                AutoTransactionBehavior.Never;
                            await nestedTestOperation3(innerContext).ConfigureAwait(false);
                        }
                    });
        }
        finally
        {
            await Fixture.TestStore.CleanAsync(context).ConfigureAwait(false);
        }
    }

    /// <summary>Fixture for DynamoDB complex type tracking specification tests.</summary>
    public class ComplexTypesTrackingDynamoFixture : FixtureBase, IDynamoSpecificationFixture
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
                    CoreEventId.MappedEntityTypeIgnoredWarning,
                    DynamoEventId.NoCompatibleSecondaryIndexFound,
                    DynamoEventId.ScanLikeQueryDetected))
                .UseDynamo(options =>
                {
                    options.DynamoDbClient(DynamoTestStoreFactory.Instance.Client);
                    options.AllowUnsafeFilteredQueries();
                });

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            base.OnModelCreating(modelBuilder, context);

            // DynamoDB complex collections currently require List<T>/IList<T>; ignore array-backed
            // entities so the rest of the complex type tracking model can be validated and tested.
            modelBuilder.Ignore<PubWithArrayCollections>();
            modelBuilder.Ignore<PubWithRecordArrayCollections>();
        }
    }
}
