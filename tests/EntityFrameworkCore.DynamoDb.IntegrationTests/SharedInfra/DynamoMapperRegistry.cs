using System.Reflection;
using Amazon.DynamoDBv2.Model;

namespace EntityFrameworkCore.DynamoDb.IntegrationTests.SharedInfra;

public sealed class DynamoMapperRegistry
{
    private DynamoMapperRegistry(IReadOnlyDictionary<Type, DynamoMapperRegistration> mappers)
        => Mappers = mappers;

    public IReadOnlyDictionary<Type, DynamoMapperRegistration> Mappers { get; }

    public static DynamoMapperRegistry FromAssembly(Assembly assembly)
    {
        var mapperInterfaceType = typeof(IDynamoMapper<>);
        var discovered = assembly
            .GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .SelectMany(type
                => type
                    .GetInterfaces()
                    .Where(@interface => @interface.IsGenericType
                        && @interface.GetGenericTypeDefinition() == mapperInterfaceType)
                    .Select(@interface => new
                    {
                        ModelType = @interface.GetGenericArguments()[0], MapperType = type,
                    }))
            .ToArray();

        var duplicates = discovered
            .GroupBy(mapper => mapper.ModelType)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.FullName ?? group.Key.Name)
            .ToArray();

        if (duplicates.Length > 0)
            throw new InvalidOperationException(
                $"Multiple DynamoMapper classes found for model type(s): {string.Join(", ", duplicates)}.");

        return new DynamoMapperRegistry(
            discovered.ToDictionary(
                mapper => mapper.ModelType,
                mapper => new DynamoMapperRegistration(mapper.ModelType, mapper.MapperType)));
    }

    public DynamoMapperRegistration Get(Type modelType)
    {
        if (!Mappers.TryGetValue(modelType, out var mapper))
            throw new InvalidOperationException($"No mapper for type '{modelType}' found.");

        return mapper;
    }

    public DynamoMapperRegistration Get<T>() => Get(typeof(T));

    public bool TryGet(Type modelType, out DynamoMapperRegistration mapper)
        => Mappers.TryGetValue(modelType, out mapper!);

    public bool TryGet<T>(out DynamoMapperRegistration mapper) => TryGet(typeof(T), out mapper);
}

public sealed class DynamoMapperRegistration
{
    private readonly MethodInfo _toItemMethod;
    private readonly MethodInfo _fromItemMethod;

    public DynamoMapperRegistration(Type modelType, Type mapperType)
    {
        ModelType = modelType;
        MapperType = mapperType;
        _toItemMethod = GetStaticMapperMethod(mapperType, nameof(IDynamoMapper<object>.ToItem));
        _fromItemMethod = GetStaticMapperMethod(mapperType, nameof(IDynamoMapper<object>.FromItem));
    }

    public Type ModelType { get; }

    public Type MapperType { get; }

    public Dictionary<string, AttributeValue> ToItem(object source)
        => (Dictionary<string, AttributeValue>)_toItemMethod.Invoke(null, [source])!;

    public object FromItem(Dictionary<string, AttributeValue> item)
        => _fromItemMethod.Invoke(null, [item])!;

    public T FromItem<T>(Dictionary<string, AttributeValue> item) => (T)FromItem(item);

    private static MethodInfo GetStaticMapperMethod(Type mapperType, string methodName)
        => mapperType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"Mapper {mapperType.FullName} does not define public static {methodName} method.");
}
