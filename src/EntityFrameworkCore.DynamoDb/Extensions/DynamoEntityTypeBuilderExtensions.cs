using System.Linq.Expressions;
using EntityFrameworkCore.DynamoDb.Metadata;
using EntityFrameworkCore.DynamoDb.Metadata.Builders;
using EntityFrameworkCore.DynamoDb.Metadata.Internal;
using EntityFrameworkCore.DynamoDb.Utilities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// ReSharper disable CheckNamespace

namespace Microsoft.EntityFrameworkCore;

/// <summary>Represents the DynamoEntityTypeBuilderExtensions type.</summary>
public static class DynamoEntityTypeBuilderExtensions
{
    extension(EntityTypeBuilder entityTypeBuilder)
    {
        /// <summary>Configures the table that the entity type maps to when targeting AWS DynamoDB.</summary>
        ///     The table name. Pass  to clear the explicit table mapping and
        ///     fall back to convention behavior.
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public EntityTypeBuilder ToTable(string? name)
        {
            name.NullButNotEmpty();

            entityTypeBuilder.Metadata.SetTableName(name);

            return entityTypeBuilder;
        }

        /// <summary>
        ///     Configures which property provides the DynamoDB partition key attribute name for this
        ///     entity type.
        /// </summary>
        /// <remarks>
        ///     This is the authoritative API for configuring the DynamoDB partition key on a root entity.
        ///     The provider derives the EF primary key automatically from the configured partition key and
        ///     optional sort key, so root entities should not configure <c>HasKey(...)</c> directly.
        /// </remarks>
        ///     The EF property name whose attribute name maps to the DynamoDB partition
        ///     key.
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public EntityTypeBuilder HasPartitionKey(string propertyName)
        {
            propertyName.NotEmpty();
            entityTypeBuilder.Metadata.SetPartitionKeyPropertyName(propertyName);
            return entityTypeBuilder;
        }

        /// <summary>
        ///     Configures which property provides the DynamoDB sort key attribute name for this entity
        ///     type.
        /// </summary>
        /// <remarks>
        ///     This is the authoritative API for configuring the DynamoDB sort key on a root entity.
        ///     When present, the provider derives the EF primary key automatically as
        ///     <c>[partitionKey, sortKey]</c>, so root entities should not configure <c>HasKey(...)</c>
        ///     directly.
        /// </remarks>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public EntityTypeBuilder HasSortKey(string propertyName)
        {
            propertyName.NotEmpty();
            entityTypeBuilder.Metadata.SetSortKeyPropertyName(propertyName);
            return entityTypeBuilder;
        }

        /// <summary>Configures a DynamoDB global secondary index that uses only a partition key.</summary>
        /// <returns>A builder for chaining DynamoDB secondary index configuration.</returns>
        public DynamoSecondaryIndexBuilder HasGlobalSecondaryIndex(
            string name,
            string partitionKeyPropertyName)
        {
            name.NotEmpty();
            partitionKeyPropertyName.NotEmpty();

            var indexBuilder = entityTypeBuilder.HasIndex([partitionKeyPropertyName], name);
            indexBuilder.Metadata.SetSecondaryIndexName(name);
            indexBuilder.Metadata.SetSecondaryIndexKind(DynamoSecondaryIndexKind.Global);
            indexBuilder.Metadata.SetSecondaryIndexProjectionType(
                DynamoSecondaryIndexProjectionType.All);

            return new DynamoSecondaryIndexBuilder(indexBuilder);
        }

        /// <summary>Configures a DynamoDB global secondary index that uses partition and sort keys.</summary>
        /// <returns>A builder for chaining DynamoDB secondary index configuration.</returns>
        public DynamoSecondaryIndexBuilder HasGlobalSecondaryIndex(
            string name,
            string partitionKeyPropertyName,
            string sortKeyPropertyName)
        {
            name.NotEmpty();
            partitionKeyPropertyName.NotEmpty();
            sortKeyPropertyName.NotEmpty();

            var indexBuilder =
                entityTypeBuilder.HasIndex([partitionKeyPropertyName, sortKeyPropertyName], name);
            indexBuilder.Metadata.SetSecondaryIndexName(name);
            indexBuilder.Metadata.SetSecondaryIndexKind(DynamoSecondaryIndexKind.Global);
            indexBuilder.Metadata.SetSecondaryIndexProjectionType(
                DynamoSecondaryIndexProjectionType.All);

            return new DynamoSecondaryIndexBuilder(indexBuilder);
        }

        /// <summary>Configures a DynamoDB local secondary index that uses the table partition key and a new sort key.</summary>
        /// <returns>A builder for chaining DynamoDB secondary index configuration.</returns>
        public DynamoSecondaryIndexBuilder HasLocalSecondaryIndex(
            string name,
            string sortKeyPropertyName)
        {
            name.NotEmpty();
            sortKeyPropertyName.NotEmpty();

            var indexBuilder = entityTypeBuilder.HasIndex([sortKeyPropertyName], name);
            indexBuilder.Metadata.SetSecondaryIndexName(name);
            indexBuilder.Metadata.SetSecondaryIndexKind(DynamoSecondaryIndexKind.Local);
            indexBuilder.Metadata.SetSecondaryIndexProjectionType(
                DynamoSecondaryIndexProjectionType.All);

            return new DynamoSecondaryIndexBuilder(indexBuilder);
        }

        /// <summary>
        ///     Configures an automatic attribute naming convention for all scalar properties on this
        ///     entity type.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         At model finalization, the convention is applied to every declared scalar property that
        ///         does not already have an explicit <c>HasAttributeName(...)</c> override. Shadow properties
        ///         (provider-internal) are not affected.
        ///     </para>
        ///     <para>
        ///         Owned entity types without their own convention configured inherit this entity's
        ///         convention automatically.
        ///     </para>
        /// </remarks>
        /// <param name="convention">The naming convention to apply.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public EntityTypeBuilder HasAttributeNamingConvention(
            DynamoAttributeNamingConvention convention = DynamoAttributeNamingConvention.CamelCase)
        {
            entityTypeBuilder.Metadata.SetAttributeNamingConvention(
                DynamoNamingConventionDescriptor.Named(convention));
            return entityTypeBuilder;
        }

        /// <summary>
        ///     Configures a custom attribute naming function for all scalar properties on this entity
        ///     type.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         At model finalization, <paramref name="translator" /> is called with each declared scalar
        ///         property's CLR name and the return value is used as the DynamoDB attribute name. Properties
        ///         with an explicit <c>HasAttributeName(...)</c> override are not affected. Shadow properties
        ///         (provider-internal) are not affected.
        ///     </para>
        ///     <para>
        ///         Owned entity types without their own convention configured inherit this entity's
        ///         convention automatically.
        ///     </para>
        /// </remarks>
        /// <param name="translator">
        ///     A function that receives the CLR property name and returns the desired
        ///     DynamoDB attribute name.
        /// </param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public EntityTypeBuilder HasAttributeNamingConvention(Func<string, string> translator)
        {
            translator.NotNull();
            entityTypeBuilder.Metadata.SetAttributeNamingConvention(
                DynamoNamingConventionDescriptor.Custom(translator));
            return entityTypeBuilder;
        }
    }

    extension<TEntity>(EntityTypeBuilder<TEntity> entityTypeBuilder) where TEntity : class
    {
        /// <summary>Configures the table that the entity type maps to when targeting AWS DynamoDB.</summary>
        ///     The table name. Pass  to clear the explicit table mapping and
        ///     fall back to convention behavior.
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public EntityTypeBuilder<TEntity> ToTable(string? name)
            => (EntityTypeBuilder<TEntity>)((EntityTypeBuilder)entityTypeBuilder).ToTable(name);

        /// <summary>
        ///     Configures which property provides the DynamoDB partition key attribute name for this
        ///     entity type.
        /// </summary>
        /// <remarks>
        ///     This is the authoritative API for configuring the DynamoDB partition key on a root entity.
        ///     The provider derives the EF primary key automatically from the configured partition key and
        ///     optional sort key, so root entities should not configure <c>HasKey(...)</c> directly.
        /// </remarks>
        ///     A lambda expression selecting the property that maps to the DynamoDB
        ///     partition key.
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public EntityTypeBuilder<TEntity> HasPartitionKey(
            Expression<Func<TEntity, object?>> keyExpression)
            => (EntityTypeBuilder<TEntity>)entityTypeBuilder.HasPartitionKey(
                EntityTypeBuilder<TEntity>.GetPropertyName(keyExpression));

        /// <summary>
        ///     Configures which property provides the DynamoDB sort key attribute name for this entity
        ///     type.
        /// </summary>
        /// <remarks>
        ///     This is the authoritative API for configuring the DynamoDB sort key on a root entity.
        ///     When present, the provider derives the EF primary key automatically as
        ///     <c>[partitionKey, sortKey]</c>, so root entities should not configure <c>HasKey(...)</c>
        ///     directly.
        /// </remarks>
        ///     A lambda expression selecting the property that maps to the DynamoDB
        ///     sort key.
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public EntityTypeBuilder<TEntity> HasSortKey(
            Expression<Func<TEntity, object?>> keyExpression)
            => (EntityTypeBuilder<TEntity>)entityTypeBuilder.HasSortKey(
                EntityTypeBuilder<TEntity>.GetPropertyName(keyExpression));

        /// <summary>Configures a DynamoDB global secondary index that uses only a partition key.</summary>
        /// <returns>A builder for chaining DynamoDB secondary index configuration.</returns>
        public DynamoSecondaryIndexBuilder<TEntity> HasGlobalSecondaryIndex(
            string name,
            Expression<Func<TEntity, object?>> partitionKeyExpression)
        {
            name.NotEmpty();

            var partitionKeyPropertyName = GetPropertyName(partitionKeyExpression);
            var indexBuilder = entityTypeBuilder.HasIndex([partitionKeyPropertyName], name);
            indexBuilder.Metadata.SetSecondaryIndexName(name);
            indexBuilder.Metadata.SetSecondaryIndexKind(DynamoSecondaryIndexKind.Global);
            indexBuilder.Metadata.SetSecondaryIndexProjectionType(
                DynamoSecondaryIndexProjectionType.All);

            return new DynamoSecondaryIndexBuilder<TEntity>(indexBuilder);
        }

        /// <summary>Configures a DynamoDB global secondary index that uses partition and sort keys.</summary>
        /// <returns>A builder for chaining DynamoDB secondary index configuration.</returns>
        public DynamoSecondaryIndexBuilder<TEntity> HasGlobalSecondaryIndex(
            string name,
            Expression<Func<TEntity, object?>> partitionKeyExpression,
            Expression<Func<TEntity, object?>> sortKeyExpression)
        {
            name.NotEmpty();

            var partitionKeyPropertyName = GetPropertyName(partitionKeyExpression);
            var sortKeyPropertyName = GetPropertyName(sortKeyExpression);
            var indexBuilder = entityTypeBuilder.HasIndex(
                [partitionKeyPropertyName, sortKeyPropertyName],
                name);
            indexBuilder.Metadata.SetSecondaryIndexName(name);
            indexBuilder.Metadata.SetSecondaryIndexKind(DynamoSecondaryIndexKind.Global);
            indexBuilder.Metadata.SetSecondaryIndexProjectionType(
                DynamoSecondaryIndexProjectionType.All);

            return new DynamoSecondaryIndexBuilder<TEntity>(indexBuilder);
        }

        /// <summary>Configures a DynamoDB local secondary index that uses the table partition key and a new sort key.</summary>
        /// <returns>A builder for chaining DynamoDB secondary index configuration.</returns>
        public DynamoSecondaryIndexBuilder<TEntity> HasLocalSecondaryIndex(
            string name,
            Expression<Func<TEntity, object?>> sortKeyExpression)
        {
            name.NotEmpty();

            var sortKeyPropertyName = GetPropertyName(sortKeyExpression);
            var indexBuilder = entityTypeBuilder.HasIndex([sortKeyPropertyName], name);
            indexBuilder.Metadata.SetSecondaryIndexName(name);
            indexBuilder.Metadata.SetSecondaryIndexKind(DynamoSecondaryIndexKind.Local);
            indexBuilder.Metadata.SetSecondaryIndexProjectionType(
                DynamoSecondaryIndexProjectionType.All);

            return new DynamoSecondaryIndexBuilder<TEntity>(indexBuilder);
        }

        /// <summary>
        ///     Configures an automatic attribute naming convention for all scalar properties on this
        ///     entity type.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         At model finalization, the convention is applied to every declared scalar property that
        ///         does not already have an explicit <c>HasAttributeName(...)</c> override. Shadow properties
        ///         (provider-internal) are not affected.
        ///     </para>
        ///     <para>
        ///         Owned entity types without their own convention configured inherit this entity's
        ///         convention automatically.
        ///     </para>
        /// </remarks>
        /// <param name="convention">The naming convention to apply.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public EntityTypeBuilder<TEntity> HasAttributeNamingConvention(
            DynamoAttributeNamingConvention convention = DynamoAttributeNamingConvention.CamelCase)
            => (EntityTypeBuilder<TEntity>)((EntityTypeBuilder)entityTypeBuilder)
                .HasAttributeNamingConvention(convention);

        /// <summary>
        ///     Configures a custom attribute naming function for all scalar properties on this entity
        ///     type.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         At model finalization, <paramref name="translator" /> is called with each declared scalar
        ///         property's CLR name and the return value is used as the DynamoDB attribute name. Properties
        ///         with an explicit <c>HasAttributeName(...)</c> override are not affected. Shadow properties
        ///         (provider-internal) are not affected.
        ///     </para>
        ///     <para>
        ///         Owned entity types without their own convention configured inherit this entity's
        ///         convention automatically.
        ///     </para>
        /// </remarks>
        /// <param name="translator">
        ///     A function that receives the CLR property name and returns the desired
        ///     DynamoDB attribute name.
        /// </param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public EntityTypeBuilder<TEntity>
            HasAttributeNamingConvention(Func<string, string> translator)
            => (EntityTypeBuilder<TEntity>)((EntityTypeBuilder)entityTypeBuilder)
                .HasAttributeNamingConvention(translator);

        private static string GetPropertyName(Expression<Func<TEntity, object?>> expression)
        {
            var body = expression.Body;
            if (body is UnaryExpression { NodeType: ExpressionType.Convert } unary)
                body = unary.Operand;
            if (body is MemberExpression member)
                return member.Member.Name;
            throw new ArgumentException(
                $"Expression '{expression}' must be a simple property access.",
                nameof(expression));
        }
    }

    extension(OwnedNavigationBuilder ownedNavigationBuilder)
    {
        /// <summary>Configures the top-level DynamoDB attribute name used to store this owned navigation.</summary>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public OwnedNavigationBuilder HasAttributeName(string? name)
        {
            ownedNavigationBuilder.OwnedEntityType.SetContainingAttributeName(name);
            return ownedNavigationBuilder;
        }

        /// <summary>
        ///     Configures an automatic attribute naming convention for all scalar properties of this
        ///     owned entity type.
        /// </summary>
        /// <remarks>
        ///     Overrides any convention inherited from the root entity for this owned type's properties.
        ///     Properties with an explicit <c>HasAttributeName(...)</c> override are not affected.
        /// </remarks>
        /// <param name="convention">The naming convention to apply.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public OwnedNavigationBuilder HasAttributeNamingConvention(
            DynamoAttributeNamingConvention convention = DynamoAttributeNamingConvention.CamelCase)
        {
            ownedNavigationBuilder.OwnedEntityType.SetAttributeNamingConvention(
                DynamoNamingConventionDescriptor.Named(convention));
            return ownedNavigationBuilder;
        }

        /// <summary>
        ///     Configures a custom attribute naming function for all scalar properties of this owned
        ///     entity type.
        /// </summary>
        /// <remarks>
        ///     Overrides any convention inherited from the root entity for this owned type's properties.
        ///     Properties with an explicit <c>HasAttributeName(...)</c> override are not affected.
        /// </remarks>
        /// <param name="translator">
        ///     A function that receives the CLR property name and returns the desired
        ///     DynamoDB attribute name.
        /// </param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public OwnedNavigationBuilder HasAttributeNamingConvention(Func<string, string> translator)
        {
            translator.NotNull();
            ownedNavigationBuilder.OwnedEntityType.SetAttributeNamingConvention(
                DynamoNamingConventionDescriptor.Custom(translator));
            return ownedNavigationBuilder;
        }
    }

    extension<TOwnerEntity, TDependentEntity>(
        OwnedNavigationBuilder<TOwnerEntity, TDependentEntity> ownedNavigationBuilder)
        where TOwnerEntity : class where TDependentEntity : class
    {
        /// <summary>Configures the top-level DynamoDB attribute name used to store this owned navigation.</summary>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public OwnedNavigationBuilder<TOwnerEntity, TDependentEntity> HasAttributeName(string? name)
            => (OwnedNavigationBuilder<TOwnerEntity, TDependentEntity>)
                ((OwnedNavigationBuilder)ownedNavigationBuilder).HasAttributeName(name);

        /// <summary>
        ///     Configures an automatic attribute naming convention for all scalar properties of this
        ///     owned entity type.
        /// </summary>
        /// <remarks>
        ///     Overrides any convention inherited from the root entity for this owned type's properties.
        ///     Properties with an explicit <c>HasAttributeName(...)</c> override are not affected.
        /// </remarks>
        /// <param name="convention">The naming convention to apply.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public OwnedNavigationBuilder<TOwnerEntity, TDependentEntity> HasAttributeNamingConvention(
            DynamoAttributeNamingConvention convention = DynamoAttributeNamingConvention.CamelCase)
            => (OwnedNavigationBuilder<TOwnerEntity, TDependentEntity>)
                ((OwnedNavigationBuilder)ownedNavigationBuilder).HasAttributeNamingConvention(
                    convention);

        /// <summary>
        ///     Configures a custom attribute naming function for all scalar properties of this owned
        ///     entity type.
        /// </summary>
        /// <remarks>
        ///     Overrides any convention inherited from the root entity for this owned type's properties.
        ///     Properties with an explicit <c>HasAttributeName(...)</c> override are not affected.
        /// </remarks>
        /// <param name="translator">
        ///     A function that receives the CLR property name and returns the desired
        ///     DynamoDB attribute name.
        /// </param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public OwnedNavigationBuilder<TOwnerEntity, TDependentEntity> HasAttributeNamingConvention(
            Func<string, string> translator)
            => (OwnedNavigationBuilder<TOwnerEntity, TDependentEntity>)
                ((OwnedNavigationBuilder)ownedNavigationBuilder).HasAttributeNamingConvention(
                    translator);
    }
}
