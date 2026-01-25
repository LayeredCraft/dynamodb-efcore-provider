// Portions of this file are derived from efcore
// Source:
// https://github.com/dotnet/efcore/blob/v10.0.2/src/Shared/Check.cs
// Copyright (c) .NET Foundation and Contributors.
// Licensed under the MIT License
// See THIRD-PARTY-LICENSES.txt file in the project root or visit
// https://github.com/dotnet/efcore/blob/v10.0.2/LICENSE.txt

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Utilities;

[DebuggerStepThrough]
internal static class Check
{
    [return: NotNull]
    public static T NotNull<T>(
        [AllowNull] [NotNull] this T value,
        [CallerArgumentExpression(nameof(value))] string parameterName = "")
    {
        if (value is null)
            ThrowArgumentNull(parameterName);

        return value;
    }

    public static IReadOnlyList<T> NotEmpty<T>(
        [NotNull] this IReadOnlyList<T>? value,
        [CallerArgumentExpression(nameof(value))] string parameterName = "")
    {
        NotNull(value, parameterName);

        if (value.Count == 0)
            ThrowNotEmpty(parameterName);

        return value;
    }

    public static string NotEmpty(
        [NotNull] this string? value,
        [CallerArgumentExpression(nameof(value))] string parameterName = "")
    {
        NotNull(value, parameterName);

        if (value.AsSpan().Trim().Length == 0)
            ThrowStringArgumentEmpty(parameterName);

        return value;
    }

    public static string NullButNotEmpty(
        this string? value,
        [CallerArgumentExpression(nameof(value))] string parameterName = "")
    {
        if (value is not null && value.Length == 0)
            ThrowStringArgumentEmpty(parameterName);

        return value!;
    }

    public static IReadOnlyList<T> HasNoNulls<T>(
        [NotNull] this IReadOnlyList<T>? value,
        [CallerArgumentExpression(nameof(value))] string parameterName = "") where T : class
    {
        value.NotNull(parameterName);

        // ReSharper disable once ForCanBeConvertedToForeach
        for (var i = 0; i < value.Count; i++)
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (value[i] is null)
                ThrowArgumentException(parameterName, parameterName);

        return value;
    }

    public static IReadOnlyList<string> HasNoEmptyElements(
        [NotNull] this IReadOnlyList<string>? value,
        [CallerArgumentExpression(nameof(value))] string parameterName = "")
    {
        value.NotNull(parameterName);

        for (var i = 0; i < value.Count; i++)
            if (string.IsNullOrWhiteSpace(value[i]))
                ThrowCollectionHasEmptyElements(parameterName);

        return value;
    }

    [Conditional("DEBUG")]
    public static void DebugAssert(
        [DoesNotReturnIf(false)] bool condition,
        [CallerArgumentExpression(nameof(condition))] string message = "")
    {
        if (!condition)
            throw new UnreachableException($"Check.DebugAssert failed: {message}");
    }

    [Conditional("DEBUG")]
    [DoesNotReturn]
    public static void DebugFail(string message) =>
        throw new UnreachableException($"Check.DebugFail failed: {message}");

    [DoesNotReturn]
    private static void ThrowArgumentNull(string parameterName) =>
        throw new ArgumentNullException(parameterName);

    [DoesNotReturn]
    private static void ThrowNotEmpty(string parameterName) =>
        throw new ArgumentException(AbstractionsStrings.CollectionArgumentIsEmpty, parameterName);

    [DoesNotReturn]
    private static void ThrowStringArgumentEmpty(string parameterName) =>
        throw new ArgumentException(AbstractionsStrings.ArgumentIsEmpty, parameterName);

    [DoesNotReturn]
    private static void ThrowCollectionHasEmptyElements(string parameterName) =>
        throw new ArgumentException(
            AbstractionsStrings.CollectionArgumentHasEmptyElements,
            parameterName);

    [DoesNotReturn]
    private static void ThrowArgumentException(string message, string parameterName) =>
        throw new ArgumentException(message, parameterName);
}
