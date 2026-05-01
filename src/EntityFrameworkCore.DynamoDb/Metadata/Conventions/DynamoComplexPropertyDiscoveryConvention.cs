using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using EntityFrameworkCore.DynamoDb.Storage;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Infrastructure;

namespace EntityFrameworkCore.DynamoDb.Metadata.Conventions;

/// <summary>
///     Discovers nested CLR types as DynamoDB complex properties without requiring pre-registration
///     via <c>[ComplexType]</c> or <c>ModelBuilder.ComplexType&lt;T&gt;()</c>.
/// </summary>
public sealed class DynamoComplexPropertyDiscoveryConvention(
    ProviderConventionSetBuilderDependencies dependencies,
    bool useAttributes = true) : ComplexPropertyDiscoveryConvention(dependencies, useAttributes)
{
    /// <summary>
    ///     Returns whether the given member can be configured as a complex property for DynamoDB.
    /// </summary>
    /// <param name="memberInfo">The candidate CLR member.</param>
    /// <param name="structuralType">The declaring structural type being discovered.</param>
    /// <param name="targetClrType">The discovered complex CLR type when the candidate is valid.</param>
    /// <param name="isCollection">Whether the candidate is a complex collection.</param>
    /// <returns><see langword="true" /> when the member should be configured as complex.</returns>
    protected override bool IsCandidateComplexProperty(
        MemberInfo memberInfo,
        IConventionTypeBase structuralType,
        [NotNullWhen(true)] out Type? targetClrType,
        out bool isCollection)
    {
        var memberType = GetMemberType(memberInfo);

        if (!structuralType.IsInModel
            || structuralType.IsIgnored(memberInfo.Name)
            || structuralType.FindMember(memberInfo.Name) != null
            || (memberInfo is PropertyInfo propertyInfo
                && propertyInfo.GetIndexParameters().Length != 0))
        {
            isCollection = false;
            targetClrType = null;
            return false;
        }

        var isComplexCandidate = Dependencies.MemberClassifier.IsCandidateComplexProperty(
            memberInfo,
            structuralType.Model,
            UseAttributes,
            out var elementType,
            out _);

        if (!isComplexCandidate
            && (memberInfo is not PropertyInfo
                || !DynamoTypeMappingSource.TryGetComplexCollectionElementType(
                    memberType,
                    out elementType)
                || Dependencies.MemberClassifier.IsCandidatePrimitiveProperty(
                    memberInfo,
                    structuralType.Model,
                    UseAttributes,
                    out _)))
        {
            isCollection = false;
            targetClrType = null;
            return false;
        }

        isCollection = elementType != null;
        targetClrType = UnwrapNullableType(elementType ?? memberType);

        if (DynamoTypeMappingSource.IsPrimitiveType(targetClrType)
            || DynamoTypeMappingSource.IsSupportedPrimitiveCollectionShape(targetClrType)
            || structuralType.Model.Builder.IsIgnored(targetClrType)
            // Entity-typed CLR types are not complex types even when they look like POCOs.
            // Allowing them here would shadow navigation discovery and corrupt model building.
            || structuralType.Model.FindEntityType(targetClrType) != null
            || (structuralType is IReadOnlyComplexType complexType
                && complexType.IsContainedBy(targetClrType)))
        {
            isCollection = false;
            targetClrType = null;
            return false;
        }

        return true;
    }

    /// <summary>Returns the CLR type represented by the supplied member.</summary>
    /// <param name="memberInfo">The member to inspect.</param>
    /// <returns>The member CLR type.</returns>
    private static Type GetMemberType(MemberInfo memberInfo)
        => memberInfo switch
        {
            PropertyInfo property => property.PropertyType,
            FieldInfo field => field.FieldType,
            _ => throw new NotSupportedException(
                $"Member '{memberInfo.Name}' on '{memberInfo.DeclaringType?.Name}' is not supported.")
        };

    /// <summary>Unwraps nullable value types to their underlying CLR type.</summary>
    /// <param name="type">The CLR type that may be nullable.</param>
    /// <returns>The unwrapped CLR type, or the original type when not nullable.</returns>
    private static Type UnwrapNullableType(Type type) => Nullable.GetUnderlyingType(type) ?? type;
}
