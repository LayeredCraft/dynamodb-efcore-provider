using Amazon.DynamoDBv2.Model;
using System.Linq.Expressions;
using EntityFrameworkCore.DynamoDb.Infrastructure;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using EntityFrameworkCore.DynamoDb.Query.Internal;
using EntityFrameworkCore.DynamoDb.Query.Internal.Expressions;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;

namespace EntityFrameworkCore.DynamoDb.Tests.Query;

#pragma warning disable EF9100

public class PrecompiledQueryTemplateTests
{
    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Scalar_parameter_renders_without_reconstructing_query_tree()
    {
        var mapping = new DynamoTypeMapping(typeof(string));
        var select = CreateSelect(mapping);
        select.ApplyPredicate(
            new SqlBinaryExpression(
                ExpressionType.Equal,
                new SqlPropertyExpression("pk", typeof(string), mapping, true),
                new SqlParameterExpression("pk", typeof(string), mapping),
                typeof(bool),
                new DynamoTypeMapping(typeof(bool))));

        var template = new DynamoQuerySqlGenerator().GeneratePrecompiledTemplate(select);
        var query = template.Render(new Dictionary<string, object?> { ["pk"] = "tenant-1" });

        query.Sql.Should().Be("SELECT \"pk\"\nFROM \"Items\"\nWHERE \"pk\" = ?");
        query.Parameters.Should().ContainSingle().Which.S.Should().Be("tenant-1");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Runtime_model_table_name_replaces_generated_from_clause()
    {
        using var context = new ConverterContext();
        var generatedTemplate =
            new DynamoQuerySqlGenerator().GeneratePrecompiledTemplate(
                CreateSelect(new DynamoTypeMapping(typeof(string))));
        var template = DynamoGeneratedQueryRuntime.CreateQueryTemplate(
            [.. generatedTemplate.Segments],
            generatedTemplate.TableName,
            context.Model,
            generatedTemplate.QueryEntityTypeName,
            generatedTemplate.IndexName,
            generatedTemplate.IndexModelName,
            generatedTemplate.IsGlobalSecondaryIndex,
            generatedTemplate.IsScanLike,
            generatedTemplate.ScanMessage,
            generatedTemplate.ScanAllowed,
            generatedTemplate.Limit,
            generatedTemplate.LimitParameterName,
            generatedTemplate.SeedNextToken,
            generatedTemplate.SeedNextTokenParameterName,
            generatedTemplate.ConsistentRead,
            generatedTemplate.ConsistentReadParameterName,
            generatedTemplate.HasUserLimit,
            generatedTemplate.IsFirstTerminal,
            generatedTemplate.IsSingleTerminal);

        template
            .Render(new Dictionary<string, object?>())
            .Sql
            .Should()
            .Be("SELECT \"pk\"\nFROM \"RuntimeItems\"");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Runtime_model_table_name_resolves_mapped_root_for_derived_entity()
    {
        using var context = new InheritanceContext();
        var select = new SelectExpression("DesignTimeItems", typeof(DerivedEntity).FullName);
        select.AddToProjection(
            new SqlPropertyExpression(
                "pk",
                typeof(string),
                new DynamoTypeMapping(typeof(string)),
                true),
            "pk");
        var generatedTemplate = new DynamoQuerySqlGenerator().GeneratePrecompiledTemplate(select);
        var template = DynamoGeneratedQueryRuntime.CreateQueryTemplate(
            [.. generatedTemplate.Segments],
            generatedTemplate.TableName,
            context.Model,
            generatedTemplate.QueryEntityTypeName,
            generatedTemplate.IndexName,
            generatedTemplate.IndexModelName,
            generatedTemplate.IsGlobalSecondaryIndex,
            generatedTemplate.IsScanLike,
            generatedTemplate.ScanMessage,
            generatedTemplate.ScanAllowed,
            generatedTemplate.Limit,
            generatedTemplate.LimitParameterName,
            generatedTemplate.SeedNextToken,
            generatedTemplate.SeedNextTokenParameterName,
            generatedTemplate.ConsistentRead,
            generatedTemplate.ConsistentReadParameterName,
            generatedTemplate.HasUserLimit,
            generatedTemplate.IsFirstTerminal,
            generatedTemplate.IsSingleTerminal);

        template
            .Render(new Dictionary<string, object?>())
            .Sql
            .Should()
            .Be("SELECT \"pk\"\nFROM \"RuntimeItems\"");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Runtime_model_index_name_replaces_generated_from_clause()
    {
        using var context = new ConverterContext();
        var select = CreateSelect(new DynamoTypeMapping(typeof(string)));
        select.ApplyIndexName("DesignTimeStatus");
        select.ApplyIndexModelName(
            context.Model.FindEntityType(typeof(ConvertedEntity))!.GetIndexes().Single().Name);
        var generatedTemplate = new DynamoQuerySqlGenerator().GeneratePrecompiledTemplate(select);
        generatedTemplate
            .IndexModelName
            .Should()
            .Be(context.Model.FindEntityType(typeof(ConvertedEntity))!.GetIndexes().Single().Name);
        var template = DynamoGeneratedQueryRuntime.CreateQueryTemplate(
            [.. generatedTemplate.Segments],
            generatedTemplate.TableName,
            context.Model,
            generatedTemplate.QueryEntityTypeName,
            generatedTemplate.IndexName,
            generatedTemplate.IndexModelName,
            generatedTemplate.IsGlobalSecondaryIndex,
            generatedTemplate.IsScanLike,
            generatedTemplate.ScanMessage,
            generatedTemplate.ScanAllowed,
            generatedTemplate.Limit,
            generatedTemplate.LimitParameterName,
            generatedTemplate.SeedNextToken,
            generatedTemplate.SeedNextTokenParameterName,
            generatedTemplate.ConsistentRead,
            generatedTemplate.ConsistentReadParameterName,
            generatedTemplate.HasUserLimit,
            generatedTemplate.IsFirstTerminal,
            generatedTemplate.IsSingleTerminal);

        template.IndexName.Should().Be("RuntimeStatus");
        template
            .Render(new Dictionary<string, object?>())
            .Sql
            .Should()
            .Be("SELECT \"pk\"\nFROM \"RuntimeItems\".\"RuntimeStatus\"");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Collection_parameter_expands_and_empty_collection_uses_false_predicate()
    {
        var mapping = new DynamoTypeMapping(typeof(string));
        var select = CreateSelect(mapping);
        select.ApplyEffectivePartitionKeyPropertyNames(new HashSet<string> { "pk" });
        select.ApplyPredicate(
            new SqlInExpression(
                new SqlPropertyExpression("pk", typeof(string), mapping, true),
                null,
                new SqlParameterExpression("keys", typeof(string[]), mapping),
                true,
                new DynamoTypeMapping(typeof(bool))));

        var template = new DynamoQuerySqlGenerator().GeneratePrecompiledTemplate(select);
        var populated = template.Render(
            new Dictionary<string, object?> { ["keys"] = new[] { "a", "b" } });
        var empty = template.Render(
            new Dictionary<string, object?> { ["keys"] = Array.Empty<string>() });

        populated.Sql.Should().EndWith("WHERE \"pk\" IN [?, ?]");
        populated.Parameters.Select(parameter => parameter.S).Should().Equal("a", "b");
        empty.Sql.Should().EndWith("WHERE 1 = 0");
        empty.Parameters.Should().BeEmpty();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Collection_parameter_handles_null_and_rebinds_captured_values()
    {
        var mapping = new DynamoTypeMapping(typeof(string));
        var select = CreateSelect(mapping);
        select.ApplyPredicate(
            new SqlInExpression(
                new SqlPropertyExpression("pk", typeof(string), mapping, true),
                null,
                new SqlParameterExpression("keys", typeof(IEnumerable<string>), mapping),
                true,
                new DynamoTypeMapping(typeof(bool))));

        var template = new DynamoQuerySqlGenerator().GeneratePrecompiledTemplate(select);
        var nullValues = template.Render(new Dictionary<string, object?> { ["keys"] = null });
        var firstExecution = template.Render(
            new Dictionary<string, object?> { ["keys"] = new[] { "first" } });
        var secondExecution = template.Render(
            new Dictionary<string, object?> { ["keys"] = new[] { "second" } });

        nullValues.Sql.Should().EndWith("WHERE 1 = 0");
        nullValues.Parameters.Should().BeEmpty();
        firstExecution.Parameters.Single().S.Should().Be("first");
        secondExecution.Parameters.Single().S.Should().Be("second");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Collection_parameter_stops_reading_after_the_supported_limit()
    {
        var mapping = new DynamoTypeMapping(typeof(string));
        var select = CreateSelect(mapping);
        select.ApplyPredicate(
            new SqlInExpression(
                new SqlPropertyExpression("pk", typeof(string), mapping, true),
                null,
                new SqlParameterExpression("keys", typeof(IEnumerable<string>), mapping),
                true,
                new DynamoTypeMapping(typeof(bool))));

        var template = new DynamoQuerySqlGenerator().GeneratePrecompiledTemplate(select);
        var valuesRead = 0;

        IEnumerable<string> Values()
        {
            while (true)
            {
                valuesRead++;
                yield return valuesRead.ToString();
            }
        }

        var action = () => template.Render(new Dictionary<string, object?> { ["keys"] = Values() });

        action.Should().Throw<InvalidOperationException>();
        valuesRead.Should().Be(51);
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Collection_parameter_allows_up_to_50_values_for_partition_keys()
    {
        var mapping = new DynamoTypeMapping(typeof(string));
        var select = CreateSelect(mapping);
        select.ApplyEffectivePartitionKeyPropertyNames(new HashSet<string> { "pk" });
        select.ApplyPredicate(
            new SqlInExpression(
                new SqlPropertyExpression("pk", typeof(string), mapping, true),
                null,
                new SqlParameterExpression("keys", typeof(IEnumerable<string>), mapping),
                true,
                new DynamoTypeMapping(typeof(bool))));

        var template = new DynamoQuerySqlGenerator().GeneratePrecompiledTemplate(select);

        var atLimit = template.Render(
            new Dictionary<string, object?>
            {
                ["keys"] = Enumerable.Range(0, 50).Select(static i => i.ToString())
            });
        atLimit.Parameters.Should().HaveCount(50);
        atLimit.Sql.Should().Contain("IN [?, ?, ?, ?, ?, ?, ?, ?, ?, ?");

        var render = () => template.Render(
            new Dictionary<string, object?>
            {
                ["keys"] = Enumerable.Range(0, 51).Select(static i => i.ToString())
            });
        render.Should().Throw<InvalidOperationException>();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Collection_parameter_allows_up_to_100_values_for_non_partition_keys()
    {
        var mapping = new DynamoTypeMapping(typeof(string));
        var select = CreateSelect(mapping);
        select.ApplyPredicate(
            new SqlInExpression(
                new SqlPropertyExpression("status", typeof(string), mapping, true),
                null,
                new SqlParameterExpression("keys", typeof(IEnumerable<string>), mapping),
                false,
                new DynamoTypeMapping(typeof(bool))));

        var template = new DynamoQuerySqlGenerator().GeneratePrecompiledTemplate(select);

        var atLimit = template.Render(
            new Dictionary<string, object?>
            {
                ["keys"] = Enumerable.Range(0, 100).Select(static i => i.ToString())
            });
        atLimit.Parameters.Should().HaveCount(100);

        var render = () => template.Render(
            new Dictionary<string, object?>
            {
                ["keys"] = Enumerable.Range(0, 101).Select(static i => i.ToString())
            });
        render.Should().Throw<InvalidOperationException>();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Collection_parameter_enumerates_one_shot_enumerables_exactly_once()
    {
        var mapping = new DynamoTypeMapping(typeof(string));
        var select = CreateSelect(mapping);
        select.ApplyPredicate(
            new SqlInExpression(
                new SqlPropertyExpression("pk", typeof(string), mapping, true),
                null,
                new SqlParameterExpression("keys", typeof(IEnumerable<string>), mapping),
                true,
                new DynamoTypeMapping(typeof(bool))));

        var template = new DynamoQuerySqlGenerator().GeneratePrecompiledTemplate(select);
        var enumerations = 0;

        IEnumerable<string> OneShot()
        {
            enumerations++;
            yield return "only";
        }

        var query = template.Render(new Dictionary<string, object?> { ["keys"] = OneShot() });

        enumerations.Should().Be(1);
        query.Sql.Should().EndWith("IN [?]");
        query.Parameters.Select(parameter => parameter.S).Should().Equal("only");
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Execution_metadata_round_trips_without_the_translated_query_tree()
    {
        var mapping = new DynamoTypeMapping(typeof(string));
        var select = CreateSelect(mapping);
        select.ApplyIndexName("by-status");
        select.ApplyIndexSourceKind(DynamoIndexSourceKind.GlobalSecondaryIndex);
        select.ApplyScanQueryClassification(
            new DynamoScanQueryClassification(true, "index 'by-status'", "test", "scan required"));
        select.AllowScan();
        select.ApplyUserLimitExpression(new QueryParameterExpression("limit", typeof(int)));
        select.ApplySeedNextTokenExpression(new QueryParameterExpression("token", typeof(string)));
        select.ApplyConsistentReadExpression(
            new QueryParameterExpression("consistent", typeof(bool)));
        select.MarkAsFirstTerminal();
        select.MarkAsSingleTerminal();

        var executionExpression = new DynamoQuerySqlGenerator()
            .GeneratePrecompiledTemplate(select)
            .CreateExecutionExpression();

        executionExpression.TableName.Should().Be("Items");
        executionExpression.IndexName.Should().Be("by-status");
        executionExpression.IndexSourceKind.Should().Be(DynamoIndexSourceKind.GlobalSecondaryIndex);
        executionExpression.ScanQueryClassification!.Message.Should().Be("scan required");
        executionExpression.ScanAllowed.Should().BeTrue();
        executionExpression
            .LimitExpression
            .Should()
            .BeOfType<QueryParameterExpression>()
            .Which
            .Name
            .Should()
            .Be("limit");
        executionExpression
            .SeedNextTokenExpression
            .Should()
            .BeOfType<QueryParameterExpression>()
            .Which
            .Name
            .Should()
            .Be("token");
        executionExpression
            .ConsistentReadExpression
            .Should()
            .BeOfType<QueryParameterExpression>()
            .Which
            .Name
            .Should()
            .Be("consistent");
        executionExpression.IsFirstTerminal.Should().BeTrue();
        executionExpression.IsSingleTerminal.Should().BeTrue();
    }

    [Fact(Timeout = TestConfiguration.DefaultTimeout)]
    public void Property_reader_preserves_value_converter()
    {
        using var context = new ConverterContext();
        var property =
            context.Model.FindEntityType(typeof(ConvertedEntity))!.FindProperty(
                nameof(ConvertedEntity.Status))!;
        var reader = DynamoGeneratedQueryRuntime.CreateValueReader<ConvertedStatus>(
            (DynamoTypeMapping)property.GetTypeMapping(),
            property,
            "status",
            "ConvertedEntity.status",
            true);

        var value = reader(
            new Dictionary<string, AttributeValue>
            {
                ["status"] = new() { S = nameof(ConvertedStatus.Active) }
            });

        value.Should().Be(ConvertedStatus.Active);
    }

    private static SelectExpression CreateSelect(DynamoTypeMapping mapping)
    {
        var select = new SelectExpression("Items", typeof(ConvertedEntity).FullName);
        select.AddToProjection(
            new SqlPropertyExpression("pk", typeof(string), mapping, true),
            "pk");
        return select;
    }

    private sealed class ConverterContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseDynamo()
                .ConfigureWarnings(warnings
                    => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
            => modelBuilder.Entity<ConvertedEntity>(entity =>
            {
                entity.ToTable("RuntimeItems");
                entity.HasPartitionKey(item => item.Pk);
                entity.HasGlobalSecondaryIndex("RuntimeStatus", item => item.Status);
                entity.Property(item => item.Status).HasConversion<string>();
            });
    }

    private sealed class InheritanceContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            => optionsBuilder
                .UseDynamo()
                .ConfigureWarnings(warnings
                    => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<BaseEntity>(entity =>
            {
                entity.ToTable("RuntimeItems");
                entity.HasPartitionKey(item => item.Pk);
            });
            modelBuilder.Entity<DerivedEntity>();
        }
    }

    private sealed class ConvertedEntity
    {
        public string Pk { get; set; } = null!;
        public ConvertedStatus Status { get; set; }
    }

    private class BaseEntity
    {
        public string Pk { get; set; } = null!;
    }

    private sealed class DerivedEntity : BaseEntity { }

    private enum ConvertedStatus
    {
        Active
    }
}

#pragma warning restore EF9100
