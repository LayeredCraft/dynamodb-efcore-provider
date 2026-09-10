using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Internal;
using System.Diagnostics.CodeAnalysis;
using static System.Linq.Expressions.Expression;

#pragma warning disable EF1001 // Internal EF Core API: StructuralTypeMaterializerSource

namespace EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>
///     Overrides complex type materialization so that complex properties are handled by
///     <see cref="DynamoShapedQueryCompilingExpressionVisitor.AddStructuralTypeInitialization" />
///     rather than being inlined via <c>ValueBufferTryReadValue</c>.
///     This lets the projection-binding removing visitor inject the correct nested
///     <c>Dictionary&lt;string, AttributeValue&gt;</c> context per complex property.
/// </summary>
internal sealed class DynamoStructuralTypeMaterializerSource(
    StructuralTypeMaterializerSourceDependencies dependencies)
    : StructuralTypeMaterializerSource(dependencies)
{
    private static readonly MethodInfo PopulateCollectionMethod =
        typeof(DynamoStructuralTypeMaterializerSource).GetMethod(
            nameof(PopulateCollection),
            BindingFlags.NonPublic | BindingFlags.Static)!;

    /// <summary>
    ///     Returns <see langword="false" /> so that complex type properties are skipped during
    ///     the initial inline scalar materialization pass and are instead emitted as
    ///     <see cref="DynamoComplexPropertyInitializationExpression" /> /
    ///     <see cref="DynamoComplexCollectionInitializationExpression" /> markers by
    ///     <c>AddStructuralTypeInitialization</c>.
    /// </summary>
    protected override bool ReadComplexTypeDirectly(IComplexType complexType) => false;

    /// <summary>
    ///     EF Core's populate branch for primitive collections casts the existing instance to
    ///     <see cref="IList{T}" /> of the element type to call <c>PopulateList</c>. Types that
    ///     implement <see cref="ICollection{T}" /> but not <see cref="IList{T}" /> — such as
    ///     <see cref="Dictionary{TKey, TValue}" /> and <see cref="HashSet{T}" /> — throw
    ///     <see cref="InvalidCastException" /> at materialization time. This override mirrors the
    ///     base populate logic for those types but populates through
    ///     <see cref="ICollection{T}" /> (<c>Clear</c> plus <c>Add</c>), preserving the existing
    ///     collection instance the same way the base <c>PopulateList</c> path does for
    ///     <see cref="List{T}" />.
    /// </summary>
    protected override void AddInitializeExpression(
        IPropertyBase property,
        ParameterBindingInfo bindingInfo,
        Expression instanceVariable,
        MethodCallExpression getValueBufferExpression,
        List<Expression> blockExpressions,
        bool nullable)
    {
        if (property is IProperty
            {
                IsPrimitiveCollection: true, ClrType.IsArray: false
            } primitiveCollection
            && TryGetEnumerableElementType(primitiveCollection.ClrType) is { } elementType
            && typeof(ICollection<>)
                .MakeGenericType(elementType)
                .IsAssignableFrom(primitiveCollection.ClrType)
            && !typeof(IList<>)
                .MakeGenericType(elementType)
                .IsAssignableFrom(primitiveCollection.ClrType))
        {
            var memberInfo = property.GetMemberInfo(forMaterialization: true, forSet: true);
            // GetMemberType is internal to EF Core; ClrType matches it for non-indexer properties,
            // which are the only shapes this provider maps as primitive collections.
            var valueExpression = getValueBufferExpression.CreateValueBufferReadValueExpression(
                primitiveCollection.ClrType,
                primitiveCollection.GetIndex(),
                primitiveCollection);

            var iCollectionInterface = typeof(ICollection<>).MakeGenericType(elementType);
            var populateMethod = PopulateCollectionMethod.MakeGenericMethod(elementType);
            var currentVariable = Variable(primitiveCollection.ClrType);
            var convertedVariable =
                populateMethod.GetParameters()[1]
                    .ParameterType
                    .IsAssignableFrom(currentVariable.Type)
                    ? (Expression)currentVariable
                    : Convert(currentVariable, populateMethod.GetParameters()[1].ParameterType);

            blockExpressions.Add(
                Block(
                    [currentVariable],
                    Assign(
                        currentVariable,
                        MakeMemberAccess(
                            instanceVariable,
                            property.GetMemberInfo(forMaterialization: true, forSet: false))),
                    IfThenElse(
                        OrElse(
                            OrElse(
                                ReferenceEqual(currentVariable, Constant(null)),
                                ReferenceEqual(valueExpression, Constant(null))),
                            MakeMemberAccess(
                                currentVariable,
                                iCollectionInterface.GetProperty(
                                    nameof(ICollection<>.IsReadOnly))!)),
                        property.IsIndexerProperty()
                            ? Assign(
                                MakeIndex(
                                    instanceVariable,
                                    (PropertyInfo)memberInfo,
                                    [Constant(property.Name)]),
                                valueExpression)
                            : MakeMemberAccess(instanceVariable, memberInfo)
                                .Assign(valueExpression),
                        Call(populateMethod, valueExpression, convertedVariable))));

            return;
        }

        base.AddInitializeExpression(
            property,
            bindingInfo,
            instanceVariable,
            getValueBufferExpression,
            blockExpressions,
            nullable);

        // EF Core keeps this helper internal (System.SharedTypeExtensions), so replicate the
        // minimal lookup needed here.
        static Type? TryGetEnumerableElementType(
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type)
            => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                ? type.GetGenericArguments()[0]
                : type
                    .GetInterfaces()
                    .Where(interfaceType => interfaceType.IsGenericType
                        && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    .Select(interfaceType => interfaceType.GetGenericArguments()[0])
                    .FirstOrDefault();
    }

    // Mirrors EF Core's PopulateList<T> but for any ICollection<T> — Dictionary<K,V> and
    // HashSet<T> implement ICollection<KeyValuePair<K,V>> / ICollection<T> Add respectively.
    private static ICollection<T> PopulateCollection<T>(
        IEnumerable<T> source,
        ICollection<T> target)
    {
        target.Clear();
        foreach (var value in source)
            target.Add(value);

        return target;
    }
}
