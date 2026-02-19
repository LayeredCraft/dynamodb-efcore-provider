using System.Reflection;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Query.Internal;

/// <summary>Cached Enumerable method infos used by query expression rewrites.</summary>
internal static class EnumerableMethods
{
    static EnumerableMethods()
    {
        var methodGroups = typeof(Enumerable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .GroupBy(method => method.Name)
            .ToDictionary(group => group.Key, group => group.ToList());

        Select = GetMethod(
            nameof(Enumerable.Select),
            2,
            types =>
            [
                typeof(IEnumerable<>).MakeGenericType(types[0]),
                typeof(Func<,>).MakeGenericType(types[0], types[1]),
            ]);

        SelectWithOrdinal = GetMethod(
            nameof(Enumerable.Select),
            2,
            types =>
            [
                typeof(IEnumerable<>).MakeGenericType(types[0]),
                typeof(Func<,,>).MakeGenericType(types[0], typeof(int), types[1]),
            ]);

        ToList = GetMethod(
            nameof(Enumerable.ToList),
            1,
            types => [typeof(IEnumerable<>).MakeGenericType(types[0])]);

        ToArray = GetMethod(
            nameof(Enumerable.ToArray),
            1,
            types => [typeof(IEnumerable<>).MakeGenericType(types[0])]);

        MethodInfo GetMethod(
            string name,
            int genericParameterCount,
            Func<Type[], Type[]> parameterGenerator)
            => methodGroups[name]
                .Single(method =>
                    // Match both generic arity and exact delegate signature so we select the
                    // intended overload (e.g. Select(source, selector) vs Select(source,
                    // indexSelector)).
                    ((genericParameterCount == 0 && !method.IsGenericMethod)
                        || (method.IsGenericMethod
                            && method.GetGenericArguments().Length == genericParameterCount))
                    && method
                        .GetParameters()
                        .Select(parameter => parameter.ParameterType)
                        .SequenceEqual(
                            parameterGenerator(
                                method.IsGenericMethod ? method.GetGenericArguments() : [])));
    }

    public static MethodInfo Select { get; }

    public static MethodInfo SelectWithOrdinal { get; }

    public static MethodInfo ToList { get; }

    public static MethodInfo ToArray { get; }
}
