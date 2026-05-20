using EntityFrameworkCore.DynamoDb.Diagnostics;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public class CustomConvertersDynamoTest(
    CustomConvertersDynamoTest.CustomConvertersDynamoFixture fixture)
    : CustomConvertersTestBase<CustomConvertersDynamoTest.CustomConvertersDynamoFixture>(fixture)
{
    [ConditionalFact]
    public virtual void Check_all_tests_overridden()
        => DynamoTestHelpers.AssertAllTestMethodsOverridden(typeof(CustomConvertersDynamoTest));

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override async Task Can_filter_projection_with_captured_enum_variable(bool async)
    {
        if (!async)
        {
            await DynamoTestHelpers.Instance.NoSyncTest(
                async,
                a => base.Can_filter_projection_with_captured_enum_variable(a));
            return;
        }

        await base.Can_filter_projection_with_captured_enum_variable(async);
    }

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override async Task Can_filter_projection_with_inline_enum_variable(bool async)
    {
        if (!async)
        {
            await DynamoTestHelpers.Instance.NoSyncTest(
                async,
                a => base.Can_filter_projection_with_inline_enum_variable(a));
            return;
        }

        await base.Can_filter_projection_with_inline_enum_variable(async);
    }

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_query_using_any_data_type() => base.Can_query_using_any_data_type();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_query_using_any_data_type_shadow()
        => base.Can_query_using_any_data_type_shadow();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_query_using_any_nullable_data_type()
        => base.Can_query_using_any_nullable_data_type();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_query_using_any_data_type_nullable_shadow()
        => base.Can_query_using_any_data_type_nullable_shadow();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_query_using_any_nullable_data_type_as_literal()
        => base.Can_query_using_any_nullable_data_type_as_literal();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_query_with_null_parameters_using_any_nullable_data_type()
        => base.Can_query_with_null_parameters_using_any_nullable_data_type();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_insert_and_read_back_all_non_nullable_data_types()
        => base.Can_insert_and_read_back_all_non_nullable_data_types();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_perform_query_with_max_length()
        => base.Can_perform_query_with_max_length();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_perform_query_with_ansi_strings_test()
        => base.Can_perform_query_with_ansi_strings_test();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
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

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_insert_and_read_back_all_nullable_data_types_with_values_set_to_null()
        => base.Can_insert_and_read_back_all_nullable_data_types_with_values_set_to_null();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Can_insert_and_read_back_all_nullable_data_types_with_values_set_to_non_null()
        => base.Can_insert_and_read_back_all_nullable_data_types_with_values_set_to_non_null();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_insert_and_read_back_object_backed_data_types()
        => base.Can_insert_and_read_back_object_backed_data_types();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_insert_and_read_back_nullable_backed_data_types()
        => base.Can_insert_and_read_back_nullable_backed_data_types();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
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

    [ConditionalFact(Skip = SkipReason.NavigationPropertiesNotSupported)]
    public override Task Can_read_back_bool_mapped_as_int_through_navigation()
        => base.Can_read_back_bool_mapped_as_int_through_navigation();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_compare_enum_to_constant() => base.Can_compare_enum_to_constant();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_compare_enum_to_parameter() => base.Can_compare_enum_to_parameter();

    public override Task Object_to_string_conversion() => base.Object_to_string_conversion();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Optional_datetime_reading_null_from_database()
        => base.Optional_datetime_reading_null_from_database();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_insert_query_multiline_string()
        => base.Can_insert_query_multiline_string();

    [ConditionalFact(Skip = SkipReason.OrderedResultSetNotSupported)]
    public override Task Can_query_and_update_with_nullable_converter_on_unique_index()
        => base.Can_query_and_update_with_nullable_converter_on_unique_index();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task Can_query_and_update_with_nullable_converter_on_primary_key()
        => base.Can_query_and_update_with_nullable_converter_on_primary_key();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_query_and_update_with_conversion_for_custom_type()
        => base.Can_query_and_update_with_conversion_for_custom_type();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_query_and_update_with_conversion_for_custom_struct()
        => base.Can_query_and_update_with_conversion_for_custom_struct();

    [ConditionalFact(Skip = SkipReason.ForeignKeysNotSupported)]
    public override Task Can_insert_and_read_back_with_case_insensitive_string_key()
        => base.Can_insert_and_read_back_with_case_insensitive_string_key();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_insert_and_read_back_with_string_list()
        => base.Can_insert_and_read_back_with_string_list();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_insert_and_query_struct_to_string_converter_for_pk()
        => base.Can_insert_and_query_struct_to_string_converter_for_pk();

    [ConditionalTheory(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Can_query_custom_type_not_mapped_by_default_equality(bool async)
        => DynamoTestHelpers.Instance.NoSyncTest(
            async,
            a => base.Can_query_custom_type_not_mapped_by_default_equality(a));

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Field_on_derived_type_retrieved_via_cast_applies_value_converter()
        => base.Field_on_derived_type_retrieved_via_cast_applies_value_converter();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Value_conversion_is_appropriately_used_for_join_condition()
        => base.Value_conversion_is_appropriately_used_for_join_condition();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Value_conversion_is_appropriately_used_for_left_join_condition()
        => base.Value_conversion_is_appropriately_used_for_left_join_condition();

    public override Task Where_bool_gets_converted_to_equality_when_value_conversion_is_used()
        => base.Where_bool_gets_converted_to_equality_when_value_conversion_is_used();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Where_negated_bool_gets_converted_to_equality_when_value_conversion_is_used()
        => base.Where_negated_bool_gets_converted_to_equality_when_value_conversion_is_used();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Where_bool_with_value_conversion_inside_comparison_doesnt_get_converted_twice()
        => base.Where_bool_with_value_conversion_inside_comparison_doesnt_get_converted_twice();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_bool_with_value_conversion_is_used()
        => base.Select_bool_with_value_conversion_is_used();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Where_conditional_bool_with_value_conversion_is_used()
        => base.Where_conditional_bool_with_value_conversion_is_used();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Select_conditional_bool_with_value_conversion_is_used()
        => base.Select_conditional_bool_with_value_conversion_is_used();

    public override Task
        Where_bool_gets_converted_to_equality_when_value_conversion_is_used_using_EFProperty()
        => base
            .Where_bool_gets_converted_to_equality_when_value_conversion_is_used_using_EFProperty();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task
        Where_bool_gets_converted_to_equality_when_value_conversion_is_used_using_indexer()
        => base.Where_bool_gets_converted_to_equality_when_value_conversion_is_used_using_indexer();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override void Value_conversion_with_property_named_value()
        => DynamoTestHelpers.Instance.NoSyncTest(()
            => base.Value_conversion_with_property_named_value());

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override void Value_conversion_on_enum_collection_contains()
        => DynamoTestHelpers.Instance.NoSyncTest(()
            => base.Value_conversion_on_enum_collection_contains());

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override void Collection_property_as_scalar_Any()
        => DynamoTestHelpers.Instance.NoSyncTest(() => base.Collection_property_as_scalar_Any());

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override void Collection_property_as_scalar_Count_member()
        => DynamoTestHelpers.Instance.NoSyncTest(()
            => base.Collection_property_as_scalar_Count_member());

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override void Collection_enum_as_string_Contains()
        => DynamoTestHelpers.Instance.NoSyncTest(() => base.Collection_enum_as_string_Contains());

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override void Optional_owned_with_converter_reading_non_nullable_column()
        => DynamoTestHelpers.Instance.NoSyncTest(()
            => base.Optional_owned_with_converter_reading_non_nullable_column());

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override Task Id_object_as_entity_key() => base.Id_object_as_entity_key();

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override void Composition_over_collection_of_complex_mapped_as_scalar()
        => DynamoTestHelpers.Instance.NoSyncTest(()
            => base.Composition_over_collection_of_complex_mapped_as_scalar());

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override void GroupBy_converted_enum()
        => DynamoTestHelpers.Instance.NoSyncTest(() => base.GroupBy_converted_enum());

    [ConditionalFact(Skip = SkipReason.QueryShapeNotSupported)]
    public override void Infer_type_mapping_from_in_subquery_to_item()
        => DynamoTestHelpers.Instance.NoSyncTest(()
            => base.Infer_type_mapping_from_in_subquery_to_item());

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
                                        Method = IdentificationMethod.EarTag,
                                    },
                                ],
                                Details = new AnimalDetails
                                {
                                    Id = 1, AnimalId = 1, BoolField = true,
                                },
                            });

                    await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                });

        protected override void OnModelCreating(ModelBuilder modelBuilder, DbContext context)
        {
            ConfigureCustomConverterModel(modelBuilder);

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

            modelBuilder.Entity<BuiltInDataTypesShadow>(b =>
            {
                b.Ignore("$type");
            });
        }

        private static void ConfigureCustomConverterModel(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Person>(b =>
            {
                b
                    .Property(p => p.SSN)
                    .HasConversion(
                        ssn => ssn.HasValue ? ssn.Value.Number : new int?(),
                        i => i.HasValue
                            ? new SocialSecurityNumber { Number = i.Value }
                            : new SocialSecurityNumber?());
                b.Property(p => p.Id).ValueGeneratedNever();
                b.HasIndex(p => p.SSN).IsUnique();
            });

            modelBuilder.Entity<NullablePrincipal>(b =>
            {
                b.Ignore(e => e.Dependents);
                b.Property(e => e.Id).ValueGeneratedNever();
                b.Property(e => e.Id).HasConversion(v => v ?? 0, v => v);
            });

            modelBuilder.Entity<NonNullableDependent>(b =>
            {
                b.Ignore(e => e.Principal);
                b.Property(e => e.Id).ValueGeneratedNever();
                b.Property(e => e.PrincipalId).HasConversion(v => v, v => v);
            });

            modelBuilder.Entity<User>(b =>
            {
                b
                    .Property(x => x.Email)
                    .HasConversion(email => (string)email, value => Email.Create(value));
                b.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<Load>(b =>
            {
                b.HasPartitionKey(e => e.LoadId);
                b.Property(x => x.Fuel).HasConversion(f => f.Volume, v => new Fuel(v));
                b.Property(e => e.LoadId).ValueGeneratedNever();
            });

            modelBuilder.Entity<StringListDataType>(b =>
            {
                b
                    .Property(e => e.Strings)
                    .HasConversion(
                        v => string.Join(",", v),
                        v => v.Split(new[] { ',' }).ToList(),
                        new ValueComparer<IList<string>>(
                            (v1, v2) => v1!.SequenceEqual(v2!),
                            v => v.GetHashCode()));
                b.Property(e => e.Id).ValueGeneratedNever();
            });

            modelBuilder.Entity<Order>(b =>
            {
                b
                    .Property(o => o.Id)
                    .HasConversion(
                        orderId => orderId.StringValue,
                        stringValue => OrderId.Parse(stringValue));
            });

            modelBuilder.Entity<SimpleCounter>(b =>
            {
                b.HasPartitionKey(e => e.CounterId);
                b.Property(e => e.CounterId).ValueGeneratedNever();
                b
                    .Property(c => c.Discriminator)
                    .HasConversion(
                        d => StringToDictionarySerializer.Serialize(d),
                        json => StringToDictionarySerializer.Deserialize(json),
                        new ValueComparer<IDictionary<string, string>>(
                            (v1, v2) => v1!.SequenceEqual(v2!),
                            v => v.GetHashCode(),
                            v => new Dictionary<string, string>(v)));
            });

            var urlConverter = new UrlSchemeRemover();
            modelBuilder.Entity<Blog>(b =>
            {
                b.HasPartitionKey(e => e.BlogId);
                b.Ignore(e => e.Posts);
                b.Property(e => e.Url).HasConversion(urlConverter);
                b.Property(e => e.IsVisible).HasConversion(new BoolToStringConverter("N", "Y"));
                b
                    .IndexerProperty(typeof(bool), "IndexerVisible")
                    .HasConversion(new BoolToStringConverter("Nay", "Aye"));
                b.HasData(
                    new
                    {
                        BlogId = 1,
                        Url = "http://blog.com",
                        IsVisible = true,
                        IndexerVisible = false,
                    });
            });

            modelBuilder.Entity<RssBlog>(b =>
            {
                b.Property(e => e.RssUrl).HasConversion(urlConverter);
                b.HasData(
                    new
                    {
                        BlogId = 2,
                        Url = "http://rssblog.com",
                        RssUrl = "http://rssblog.com/rss",
                        IsVisible = false,
                        IndexerVisible = true,
                    });
            });

            modelBuilder.Entity<Post>(b =>
            {
                b.HasPartitionKey(e => e.PostId);
                b.Ignore(e => e.Blog);
                b.HasData(
                    new Post { PostId = 1, BlogId = 1 },
                    new Post { PostId = 2, BlogId = null });
            });

            modelBuilder.Entity<EntityWithValueWrapper>(e =>
            {
                e
                    .Property(e => e.Wrapper)
                    .HasConversion(w => w.Value, v => new ValueWrapper { Value = v });
                e.HasData(
                    new EntityWithValueWrapper
                    {
                        Id = 1, Wrapper = new ValueWrapper { Value = "foo" },
                    });
            });

            modelBuilder.Entity<CollectionScalar>(b =>
            {
                b
                    .Property(e => e.Tags)
                    .HasConversion(
                        c => string.Join(",", c),
                        s => s.Split(',', StringSplitOptions.None).ToList(),
                        new ValueComparer<List<string>>(true));
                b.HasData(new CollectionScalar { Id = 1, Tags = ["A", "B", "C"] });
            });

            modelBuilder.Entity<CollectionEnum>(b =>
            {
                b
                    .Property(e => e.Roles)
                    .HasConversion(
                        new RolesToStringConveter(),
                        new ValueComparer<ICollection<Roles>>(true));
                b.HasData(new CollectionEnum { Id = 1, Roles = new List<Roles> { Roles.Seller } });
            });

            modelBuilder.Entity<Book>(b =>
            {
                b.Property(e => e.Id).HasConversion(e => e.Id, e => new BookId(e));
                b.HasData(new Book(new BookId(1)) { Value = "Book1" });
            });

            modelBuilder.Entity<User23059>(b =>
            {
                b
                    .Property(e => e.MessageGroups)
                    .HasConversion(
                        v => string.Join(',', v),
                        v => v
                            .Split(',', StringSplitOptions.RemoveEmptyEntries)
                            .Select(x => Enum.Parse<MessageGroup>(x))
                            .ToList(),
                        new ValueComparer<List<MessageGroup>>(true));
                b.HasData(
                    new User23059
                    {
                        Id = 1, IsSoftDeleted = true, MessageGroups = [MessageGroup.SomeGroup],
                    },
                    new User23059
                    {
                        Id = 2, IsSoftDeleted = false, MessageGroups = [MessageGroup.SomeGroup],
                    });
            });

            modelBuilder
                .Entity<Dashboard>()
                .Property(e => e.Layouts)
                .HasConversion(
                    v => LayoutsToStringSerializer.Serialize(v),
                    v => LayoutsToStringSerializer.Deserialize(v),
                    new ValueComparer<List<Layout>>(
                        (v1, v2) => v1!.SequenceEqual(v2!),
                        v => v.GetHashCode(),
                        v => new List<Layout>(v)));

            modelBuilder
                .Entity<HolderClass>()
                .HasData(new HolderClass { Id = 1, HoldingEnum = HoldingEnum.Value2 });
            modelBuilder
                .Entity<Entity>()
                .Property(e => e.SomeEnum)
                .HasConversion(e => e.ToString(), e => Enum.Parse<SomeEnum>(e));
            modelBuilder
                .Entity<Entity>()
                .HasData(
                    new Entity { Id = 1, SomeEnum = SomeEnum.Yes },
                    new Entity { Id = 2, SomeEnum = SomeEnum.No },
                    new Entity { Id = 3, SomeEnum = SomeEnum.Yes });
        }

        private static class StringToDictionarySerializer
        {
            public static string Serialize(IDictionary<string, string> dictionary)
                => string.Join(
                    Environment.NewLine,
                    dictionary.Select(kvp => $"{{{kvp.Key},{kvp.Value}}}"));

            public static IDictionary<string, string> Deserialize(string s)
            {
                var dictionary = new Dictionary<string, string>();
                foreach (var keyValuePair in s.Split(
                    Environment.NewLine,
                    StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = keyValuePair[1..^1].Split(",");
                    dictionary[parts[0]] = parts[1];
                }

                return dictionary;
            }
        }

        private static class LayoutsToStringSerializer
        {
            public static string Serialize(List<Layout> layouts)
                => string.Join(
                    Environment.NewLine,
                    layouts.Select(layout => $"({layout.Height},{layout.Width})"));

            public static List<Layout> Deserialize(string s)
            {
                var list = new List<Layout>();
                foreach (var keyValuePair in s.Split(
                    Environment.NewLine,
                    StringSplitOptions.RemoveEmptyEntries))
                {
                    var parts = keyValuePair[1..^1].Split(",");
                    list.Add(
                        new Layout { Height = int.Parse(parts[0]), Width = int.Parse(parts[1]) });
                }

                return list;
            }
        }

        private class UrlSchemeRemover() : ValueConverter<string, string>(
            x => x.Remove(0, 7),
            x => "http://" + x);

        private class RolesToStringConveter() : ValueConverter<ICollection<Roles>, string>(
            v => string.Join(";", v.Select(f => f.ToString())),
            v => v.Length > 0
                ? v.Split(new[] { ';' }).Select(f => (Roles)Enum.Parse(typeof(Roles), f)).ToList()
                : new List<Roles>());

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
