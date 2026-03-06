using System.Linq.Expressions;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Metadata.Builders;
using LayeredCraft.EntityFrameworkCore.DynamoDb.Utilities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

// ReSharper disable CheckNamespace

namespace Microsoft.EntityFrameworkCore;

public static class DynamoEntityTypeBuilderExtensions
{
    extension(EntityTypeBuilder entityTypeBuilder)
    {
        /// <summary>Configures the table that the entity type maps to when targeting AWS DynamoDB.</summary>
        /// <param name="entityTypeBuilder">The builder for the entity type being configured.</param>
        /// <param name="name">
        ///     The table name. Pass <see langword="null" /> to clear the explicit table mapping and
        ///     fall back to convention behavior.
        /// </param>
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
        ///     Only needed when the partition key property is not the first property in the EF primary
        ///     key. By default the physical attribute name is derived from the first EF primary key property
        ///     via <c>HasAttributeName</c>, falling back to the CLR property name.
        /// </remarks>
        /// <param name="propertyName">
        ///     The EF property name whose attribute name maps to the DynamoDB partition
        ///     key.
        /// </param>
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
        ///     Only needed when the sort key property is not the second property in the EF primary key.
        ///     By default the physical attribute name is derived from the second EF primary key property via
        ///     <c>HasAttributeName</c>, falling back to the CLR property name.
        /// </remarks>
        /// <param name="propertyName">The EF property name whose attribute name maps to the DynamoDB sort key.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public EntityTypeBuilder HasSortKey(string propertyName)
        {
            propertyName.NotEmpty();
            entityTypeBuilder.Metadata.SetSortKeyPropertyName(propertyName);
            return entityTypeBuilder;
        }

        /// <summary>Configures a DynamoDB global secondary index that uses only a partition key.</summary>
        /// <param name="name">The DynamoDB global secondary index name.</param>
        /// <param name="partitionKeyPropertyName">The EF property name that maps to the GSI partition key.</param>
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
            indexBuilder.Metadata.SetSecondaryIndexProjectionType(DynamoSecondaryIndexProjectionType.All);

            return new DynamoSecondaryIndexBuilder(indexBuilder);
        }

        /// <summary>Configures a DynamoDB global secondary index that uses partition and sort keys.</summary>
        /// <param name="name">The DynamoDB global secondary index name.</param>
        /// <param name="partitionKeyPropertyName">The EF property name that maps to the GSI partition key.</param>
        /// <param name="sortKeyPropertyName">The EF property name that maps to the GSI sort key.</param>
        /// <returns>A builder for chaining DynamoDB secondary index configuration.</returns>
        public DynamoSecondaryIndexBuilder HasGlobalSecondaryIndex(
            string name,
            string partitionKeyPropertyName,
            string sortKeyPropertyName)
        {
            name.NotEmpty();
            partitionKeyPropertyName.NotEmpty();
            sortKeyPropertyName.NotEmpty();

            var indexBuilder = entityTypeBuilder.HasIndex([partitionKeyPropertyName, sortKeyPropertyName], name);
            indexBuilder.Metadata.SetSecondaryIndexName(name);
            indexBuilder.Metadata.SetSecondaryIndexKind(DynamoSecondaryIndexKind.Global);
            indexBuilder.Metadata.SetSecondaryIndexProjectionType(DynamoSecondaryIndexProjectionType.All);

            return new DynamoSecondaryIndexBuilder(indexBuilder);
        }

        /// <summary>Configures a DynamoDB local secondary index that uses the table partition key and a new sort key.</summary>
        /// <param name="name">The DynamoDB local secondary index name.</param>
        /// <param name="sortKeyPropertyName">The EF property name that maps to the LSI sort key.</param>
        /// <returns>A builder for chaining DynamoDB secondary index configuration.</returns>
        public DynamoSecondaryIndexBuilder HasLocalSecondaryIndex(
            string name,
            string sortKeyPropertyName)
        {
            name.NotEmpty();
            sortKeyPropertyName.NotEmpty();

            var partitionKeyPropertyName = entityTypeBuilder.Metadata.GetPartitionKeyPropertyName();
            if (partitionKeyPropertyName is null)
                throw new InvalidOperationException(
                    $"Entity '{entityTypeBuilder.Metadata.DisplayName()}' must configure a partition key before configuring a local secondary index.");

            var indexBuilder = entityTypeBuilder.HasIndex([partitionKeyPropertyName, sortKeyPropertyName], name);
            indexBuilder.Metadata.SetSecondaryIndexName(name);
            indexBuilder.Metadata.SetSecondaryIndexKind(DynamoSecondaryIndexKind.Local);
            indexBuilder.Metadata.SetSecondaryIndexProjectionType(DynamoSecondaryIndexProjectionType.All);

            return new DynamoSecondaryIndexBuilder(indexBuilder);
        }
    }

    extension<TEntity>(EntityTypeBuilder<TEntity> entityTypeBuilder) where TEntity : class
    {
        /// <summary>Configures the table that the entity type maps to when targeting AWS DynamoDB.</summary>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <param name="entityTypeBuilder">The builder for the entity type being configured.</param>
        /// <param name="name">
        ///     The table name. Pass <see langword="null" /> to clear the explicit table mapping and
        ///     fall back to convention behavior.
        /// </param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public EntityTypeBuilder<TEntity> ToTable(string? name)
            => (EntityTypeBuilder<TEntity>)((EntityTypeBuilder)entityTypeBuilder).ToTable(name);

        /// <summary>
        ///     Configures which property provides the DynamoDB partition key attribute name for this
        ///     entity type.
        /// </summary>
        /// <remarks>
        ///     Only needed when the partition key property is not the first property in the EF primary
        ///     key. By default the physical attribute name is derived from the first EF primary key property
        ///     via <c>HasAttributeName</c>, falling back to the CLR property name.
        /// </remarks>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <param name="keyExpression">
        ///     A lambda expression selecting the property that maps to the DynamoDB
        ///     partition key.
        /// </param>
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
        ///     Only needed when the sort key property is not the second property in the EF primary key.
        ///     By default the physical attribute name is derived from the second EF primary key property via
        ///     <c>HasAttributeName</c>, falling back to the CLR property name.
        /// </remarks>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <param name="keyExpression">
        ///     A lambda expression selecting the property that maps to the DynamoDB
        ///     sort key.
        /// </param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public EntityTypeBuilder<TEntity> HasSortKey(
            Expression<Func<TEntity, object?>> keyExpression)
            => (EntityTypeBuilder<TEntity>)entityTypeBuilder.HasSortKey(
                EntityTypeBuilder<TEntity>.GetPropertyName(keyExpression));

        /// <summary>Configures a DynamoDB global secondary index that uses only a partition key.</summary>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <param name="name">The DynamoDB global secondary index name.</param>
        /// <param name="partitionKeyExpression">A lambda selecting the property that maps to the GSI partition key.</param>
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
            indexBuilder.Metadata.SetSecondaryIndexProjectionType(DynamoSecondaryIndexProjectionType.All);

            return new DynamoSecondaryIndexBuilder<TEntity>(indexBuilder);
        }

        /// <summary>Configures a DynamoDB global secondary index that uses partition and sort keys.</summary>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <param name="name">The DynamoDB global secondary index name.</param>
        /// <param name="partitionKeyExpression">A lambda selecting the property that maps to the GSI partition key.</param>
        /// <param name="sortKeyExpression">A lambda selecting the property that maps to the GSI sort key.</param>
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
            indexBuilder.Metadata.SetSecondaryIndexProjectionType(DynamoSecondaryIndexProjectionType.All);

            return new DynamoSecondaryIndexBuilder<TEntity>(indexBuilder);
        }

        /// <summary>Configures a DynamoDB local secondary index that uses the table partition key and a new sort key.</summary>
        /// <typeparam name="TEntity">The entity type being configured.</typeparam>
        /// <param name="name">The DynamoDB local secondary index name.</param>
        /// <param name="sortKeyExpression">A lambda selecting the property that maps to the LSI sort key.</param>
        /// <returns>A builder for chaining DynamoDB secondary index configuration.</returns>
        public DynamoSecondaryIndexBuilder<TEntity> HasLocalSecondaryIndex(
            string name,
            Expression<Func<TEntity, object?>> sortKeyExpression)
        {
            name.NotEmpty();

            var sortKeyPropertyName = GetPropertyName(sortKeyExpression);
            var partitionKeyPropertyName = entityTypeBuilder.Metadata.GetPartitionKeyPropertyName();
            if (partitionKeyPropertyName is null)
                throw new InvalidOperationException(
                    $"Entity '{entityTypeBuilder.Metadata.DisplayName()}' must configure a partition key before configuring a local secondary index.");

            var indexBuilder = entityTypeBuilder.HasIndex([partitionKeyPropertyName, sortKeyPropertyName], name);
            indexBuilder.Metadata.SetSecondaryIndexName(name);
            indexBuilder.Metadata.SetSecondaryIndexKind(DynamoSecondaryIndexKind.Local);
            indexBuilder.Metadata.SetSecondaryIndexProjectionType(DynamoSecondaryIndexProjectionType.All);

            return new DynamoSecondaryIndexBuilder<TEntity>(indexBuilder);
        }

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
        /// <param name="ownedNavigationBuilder">The owned navigation builder being configured.</param>
        /// <param name="name">The top-level attribute name.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public OwnedNavigationBuilder HasAttributeName(string? name)
        {
            ownedNavigationBuilder.OwnedEntityType.SetContainingAttributeName(name);
            return ownedNavigationBuilder;
        }
    }

    extension<TOwnerEntity, TDependentEntity>(
        OwnedNavigationBuilder<TOwnerEntity, TDependentEntity> ownedNavigationBuilder)
        where TOwnerEntity : class where TDependentEntity : class
    {
        /// <summary>Configures the top-level DynamoDB attribute name used to store this owned navigation.</summary>
        /// <typeparam name="TOwnerEntity">The owner CLR type.</typeparam>
        /// <typeparam name="TDependentEntity">The dependent CLR type.</typeparam>
        /// <param name="ownedNavigationBuilder">The owned navigation builder being configured.</param>
        /// <param name="name">The top-level attribute name.</param>
        /// <returns>The same builder instance so that multiple calls can be chained.</returns>
        public OwnedNavigationBuilder<TOwnerEntity, TDependentEntity> HasAttributeName(string? name)
            => (OwnedNavigationBuilder<TOwnerEntity, TDependentEntity>)
                ((OwnedNavigationBuilder)ownedNavigationBuilder).HasAttributeName(name);
    }
}
