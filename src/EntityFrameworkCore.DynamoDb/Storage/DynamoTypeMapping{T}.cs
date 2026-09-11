using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.Json;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EntityFrameworkCore.DynamoDb.Storage;

/// <summary>
///     Represents a <see cref="DynamoTypeMapping" /> whose CLR type is statically known.
/// </summary>
/// <remarks>
///     Mirrors EF Core's own <c>InMemoryTypeMapping&lt;T&gt;</c> pattern (the reference
///     implementation for a non-relational provider migrating to EF Core 11's NativeAOT-oriented
///     type-mapping contract, see
///     <see href="https://github.com/dotnet/efcore/pull/38440" />). EF Core 11's compiled-model
///     code generator reconstructs a type mapping by cloning its runtime type's public static
///     <c>Default</c> instance; it no longer supports overriding <c>ClrType</c> on that clone
///     (EF10 did, via <c>Clone(clrType: ...)</c>). A single, shared, <see cref="object" />-typed
///     <see cref="DynamoTypeMapping.Default" /> singleton can therefore no longer represent every
///     CLR type in generated code. Each closed generic instantiation of this type
///     (<c>DynamoTypeMapping&lt;string&gt;</c>, <c>DynamoTypeMapping&lt;decimal&gt;</c>, ...) gets
///     its own distinct static <see cref="Default" />, already correctly typed for
///     <typeparamref name="T" /> — because static members on closed generic types are
///     per-instantiation in .NET — so no <c>ClrType</c> override is needed at all. All
///     DynamoDB-specific behavior (reader/writer, PartiQL literal/parameter generation, collection
///     codec priming) remains on the non-generic <see cref="DynamoTypeMapping" /> base, unchanged.
/// </remarks>
/// <typeparam name="T">The .NET type used in the EF model.</typeparam>
public class DynamoTypeMapping<
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicMethods
        | DynamicallyAccessedMemberTypes.PublicProperties)]
    T> : DynamoTypeMapping
{
    /// <summary>The default mapping instance for <typeparamref name="T" />.</summary>
    public static new DynamoTypeMapping<T> Default { get; } = new();

#if NET11_0
    /// <summary>Creates a mapping for <typeparamref name="T" />.</summary>
    public DynamoTypeMapping(ValueComparer? comparer = null, ValueComparer? keyComparer = null)
        : base(typeof(T), comparer, keyComparer) { }
#else
    // EF10's CoreTypeMapping.Comparer/KeyComparer lazily fall back to the reflection-based
    // ValueComparer.CreateDefault(Type, bool) whenever no comparer was supplied — unsafe under
    // NativeAOT. EF10 also has no CreateDefaultComparer override point to intercept that (see the
    // #else branch below), so the comparers must be supplied eagerly here instead, using the
    // generic, reflection-free ValueComparer.CreateDefault<T>(bool) overload (present on EF10
    // too) so the unsafe lazy path is never reached.
    /// <summary>Creates a mapping for <typeparamref name="T" />.</summary>
    public DynamoTypeMapping(ValueComparer? comparer = null, ValueComparer? keyComparer = null)
        : base(
            typeof(T),
            comparer ?? ValueComparer.CreateDefault<T>(favorStructuralComparisons: false),
            keyComparer ?? ValueComparer.CreateDefault<T>(favorStructuralComparisons: true)) { }
#endif

    private DynamoTypeMapping(CoreTypeMappingParameters parameters) : base(parameters) { }

#if NET11_0
    // EF10's CoreTypeMapping has no CreateDefaultComparer override point at all — its Comparer/
    // KeyComparer/ProviderValueComparer properties call the reflection-based
    // ValueComparer.CreateDefault(Type, bool) directly and cannot be intercepted per-type. That's
    // fine on EF10, which doesn't need this AOT-safe path. EF11 added CreateDefaultComparer
    // specifically so it could be overridden with the generic, reflection-free
    // ValueComparer.CreateDefault<T>(bool) overload (see dotnet/efcore#38440).
    /// <inheritdoc />
    protected override ValueComparer CreateDefaultComparer(bool favorStructuralComparisons)
        => ClrType == typeof(T)
            ? ValueComparer.CreateDefault<T>(favorStructuralComparisons)
            : base.CreateDefaultComparer(favorStructuralComparisons);
#endif

    /// <inheritdoc />
    public override CoreTypeMapping WithComposedConverter(
        ValueConverter? converter,
        ValueComparer? comparer = null,
        ValueComparer? keyComparer = null,
        CoreTypeMapping? elementMapping = null,
        JsonValueReaderWriter? jsonValueReaderWriter = null)
        => new DynamoTypeMapping<T>(
            Parameters.WithComposedConverter(
                converter,
                comparer,
                keyComparer,
                elementMapping,
                jsonValueReaderWriter));

    /// <inheritdoc />
    protected override CoreTypeMapping Clone(CoreTypeMappingParameters parameters)
        => new DynamoTypeMapping<T>(parameters);
}
