using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public class CustomConvertersDynamoTest(
    CustomConvertersDynamoTest.CustomConvertersDynamoFixture fixture)
    : CustomConvertersTestBase<CustomConvertersDynamoTest.CustomConvertersDynamoFixture>(fixture)
{
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

    public override Task Can_perform_query_with_max_length()
        => base.Can_perform_query_with_max_length();

    public override Task Can_perform_query_with_ansi_strings_test()
        => base.Can_perform_query_with_ansi_strings_test();

    public override Task Can_insert_and_read_with_max_length_set()
        => base.Can_insert_and_read_with_max_length_set();

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

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task Can_insert_and_read_back_with_null_binary_foreign_key()
        => base.Can_insert_and_read_back_with_null_binary_foreign_key();

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

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task Can_insert_and_read_back_with_null_string_foreign_key()
        => base.Can_insert_and_read_back_with_null_string_foreign_key();

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

    public override async Task Can_read_back_mapped_enum_from_collection_first_or_default()
    {
        await using var context = CreateContext();
        var query =
            from animal in context.Set<DynamoAnimal>()
            select new { animal.Id, animal.IdentificationMethods.FirstOrDefault()!.Method };

        var result = await query.AsAsyncEnumerable().FirstOrDefaultAsync();
        Assert.Equal(IdentificationMethod.EarTag, result?.Method);
    }

    public override async Task Can_read_back_bool_mapped_as_int_through_navigation()
    {
        await using var context = CreateContext();
        var query =
            from animal in context.Set<DynamoAnimal>()
            where animal.Details != null
            select new { animal.Details.BoolField };

        var result = Assert.Single(await query.ToListAsync());
        Assert.True(result.BoolField);
    }

    public override Task Can_compare_enum_to_constant() => base.Can_compare_enum_to_constant();

    public override Task Can_compare_enum_to_parameter() => base.Can_compare_enum_to_parameter();

    public override Task Object_to_string_conversion() => base.Object_to_string_conversion();

    public override Task Optional_datetime_reading_null_from_database()
        => base.Optional_datetime_reading_null_from_database();

    public override Task Can_insert_query_multiline_string()
        => base.Can_insert_query_multiline_string();

    public override async Task Can_query_and_update_with_nullable_converter_on_unique_index()
    {
        await using (var context = CreateContext())
        {
            context.AddRange(
                new Person { Id = 1, Name = "Lewis" },
                new Person
                {
                    Id = 2, Name = "Seb", SSN = new SocialSecurityNumber { Number = 111111111 },
                },
                new Person
                {
                    Id = 3,
                    Name = "Kimi",
                    SSN = new SocialSecurityNumber { Number = 222222222 },
                },
                new Person { Id = 4, Name = "Valtteri" });

            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext())
        {
            var drivers = (await context.Set<Person>().ToListAsync()).OrderBy(p => p.Name).ToList();

            Assert.Equal(4, drivers.Count);

            Assert.Equal("Kimi", drivers[0].Name);
            Assert.Equal(222222222, drivers[0].SSN!.Value.Number);

            Assert.Equal("Lewis", drivers[1].Name);
            Assert.False(drivers[1].SSN.HasValue);

            Assert.Equal("Seb", drivers[2].Name);
            Assert.Equal(111111111, drivers[2].SSN!.Value.Number);

            Assert.Equal("Valtteri", drivers[3].Name);
            Assert.False(drivers[3].SSN.HasValue);

            context.Remove(drivers[0]);

            context.Add(
                new Person
                {
                    Id = 5,
                    Name = "Charles",
                    SSN = new SocialSecurityNumber { Number = 222222222 },
                });

            await context.SaveChangesAsync();
        }

        await using (var context = CreateContext())
        {
            var drivers = (await context.Set<Person>().ToListAsync()).OrderBy(p => p.Name).ToList();

            Assert.Equal(4, drivers.Count);

            Assert.Equal("Charles", drivers[0].Name);
            Assert.Equal(222222222, drivers[0].SSN!.Value.Number);

            Assert.Equal("Lewis", drivers[1].Name);
            Assert.False(drivers[1].SSN.HasValue);

            Assert.Equal("Seb", drivers[2].Name);
            Assert.Equal(111111111, drivers[2].SSN!.Value.Number);

            Assert.Equal("Valtteri", drivers[3].Name);
            Assert.False(drivers[3].SSN.HasValue);

            context.Remove(drivers[0]);
        }
    }

    // Relational test relies on foreign-key relationship fixup; DynamoDB provider ignores these
    // navigations.
    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task Can_query_and_update_with_nullable_converter_on_primary_key()
        => base.Can_query_and_update_with_nullable_converter_on_primary_key();

    public override async Task Can_query_and_update_with_conversion_for_custom_type()
    {
        Guid id;
        await using (var context = CreateContext())
        {
            var user =
                context.Set<User>().Add(new User(Email.Create("eeky_bear@example.com"))).Entity;

            Assert.Equal(1, await context.SaveChangesAsync());

            id = user.Id;
        }

        await using (var context = CreateContext())
        {
            var user =
                await context
                    .Set<User>()
                    .AsAsyncEnumerable()
                    .SingleAsync(e => e.Id == id && e.Email == "eeky_bear@example.com");

            Assert.Equal(id, user.Id);
            Assert.Equal("eeky_bear@example.com", user.Email);
        }
    }

    public override async Task Can_query_and_update_with_conversion_for_custom_struct()
    {
        await using (var context = CreateContext())
        {
            context.Set<Load>().Add(new Load { LoadId = 1, Fuel = new Fuel(1.1) });

            Assert.Equal(1, await context.SaveChangesAsync());
        }

        await using (var context = CreateContext())
        {
            var load = await context
                .Set<Load>()
                .Where(e => e.LoadId == 1 && e.Fuel.Equals(new Fuel(1.1)))
                .AsAsyncEnumerable()
                .SingleAsync();

            Assert.Equal(1, load.LoadId);
            Assert.Equal(1.1, load.Fuel.Volume);
        }
    }

    public override async Task Can_insert_and_read_back_with_case_insensitive_string_key()
    {
        await using (var context = CreateContext())
        {
            var principal =
                context
                    .Set<StringKeyDataType>()
                    .Add(new StringKeyDataType { Id = "Gumball!!" })
                    .Entity;

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

    public override async Task Can_insert_and_read_back_with_string_list()
    {
        using (var context = CreateContext())
        {
            context
                .Set<StringListDataType>()
                .Add(
                    new StringListDataType
                    {
                        Id = 1, Strings = new List<string> { "Gum", "Taffy" },
                    });

            Assert.Equal(1, await context.SaveChangesAsync());
        }

        using (var context = CreateContext())
        {
            var entity = await context.Set<StringListDataType>().AsAsyncEnumerable().SingleAsync();

            Assert.Equal(["Gum", "Taffy"], entity.Strings);
        }
    }

    public override async Task Can_insert_and_query_struct_to_string_converter_for_pk()
    {
        await using (var context = CreateContext())
        {
            context.Set<Order>().Add(new Order { Id = OrderId.Parse("Id1") });

            Assert.Equal(1, await context.SaveChangesAsync());
        }

        await using (var context = CreateContext())
        {
            // Inline
            var entity =
                await context
                    .Set<Order>()
                    .Where(o => (string)o.Id == "Id1")
                    .AsAsyncEnumerable()
                    .SingleAsync();

            // constant from closure
            const string idAsStringConstant = "Id1";
            entity = await context
                .Set<Order>()
                .Where(o => (string)o.Id == idAsStringConstant)
                .AsAsyncEnumerable()
                .SingleAsync();

            // Variable from closure
            var idAsStringVariable = "Id1";
            entity = await context
                .Set<Order>()
                .Where(o => (string)o.Id == idAsStringVariable)
                .AsAsyncEnumerable()
                .SingleAsync();

            // Inline parsing function
            entity = await context
                .Set<Order>()
                .Where(o => (string)o.Id == OrderId.Parse("Id1").StringValue)
                .AsAsyncEnumerable()
                .SingleAsync();
        }
    }

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported + " #241")]
    public override Task Can_query_custom_type_not_mapped_by_default_equality(bool async)
        => async
            ? base.Can_query_custom_type_not_mapped_by_default_equality(async)
            : DynamoTestHelpers.Instance.NoSyncTest(
                async,
                base.Can_query_custom_type_not_mapped_by_default_equality);

    public override Task Field_on_derived_type_retrieved_via_cast_applies_value_converter()
        => base.Field_on_derived_type_retrieved_via_cast_applies_value_converter();

    // DynamoDB PartiQL does not support joins.
    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Value_conversion_is_appropriately_used_for_join_condition()
        => base.Value_conversion_is_appropriately_used_for_join_condition();

    // DynamoDB PartiQL does not support joins.
    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
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

    public override Task Where_conditional_bool_with_value_conversion_is_used()
        => base.Where_conditional_bool_with_value_conversion_is_used();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported + " #243")]
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

    public override void Collection_property_as_scalar_Count_member()
        => base.Collection_property_as_scalar_Count_member();

    public override void Collection_enum_as_string_Contains()
        => base.Collection_enum_as_string_Contains();

    public override void Optional_owned_with_converter_reading_non_nullable_column()
    {
        using var context = CreateContext();
        Assert.Equal(
            "Nullable object must have a value.",
            Assert.Throws<InvalidOperationException>(()
                    => context
                        .Set<Parent>()
                        .Select(e => new { e.OwnedWithConverter.Value })
                        .ToListAsync()
                        .GetAwaiter()
                        .GetResult())
                .Message);
    }

    public override Task Id_object_as_entity_key() => base.Id_object_as_entity_key();

    public override void Composition_over_collection_of_complex_mapped_as_scalar()
    {
        using var context = CreateContext();
        Assert.Equal(
            CoreStrings.TranslationFailed(@"l => new {     H = l.Height,     W = l.Width }"),
            Assert
                .Throws<InvalidOperationException>(()
                    => context
                        .Set<Dashboard>()
                        .AsNoTracking()
                        .Select(d => new
                        {
                            d.Id,
                            d.Name,
                            Layouts =
                                d
                                    .Layouts
                                    .Select(l => new { H = l.Height, W = l.Width })
                                    .ToList(),
                        })
                        .ToListAsync()
                        .GetAwaiter()
                        .GetResult())
                .Message
                .Replace("\r", "")
                .Replace("\n", ""));
    }

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override void GroupBy_converted_enum() => base.GroupBy_converted_enum();

    [ConditionalFact(Skip = SkipReason.SubqueryContainsNotSupported)]
    public override void Infer_type_mapping_from_in_subquery_to_item()
        => base.Infer_type_mapping_from_in_subquery_to_item();

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
                                            Method = IdentificationMethod.EarTag,
                                        },
                                    ],
                                    Details = new AnimalDetails
                                    {
                                        Id = 1, AnimalId = 1, BoolField = true,
                                    },
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
                                    ["IndexerVisible"] = true,
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
            modelBuilder.Entity<BuiltInDataTypesShadow>(b =>
            {
                b.Ignore("$type");
            });
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
