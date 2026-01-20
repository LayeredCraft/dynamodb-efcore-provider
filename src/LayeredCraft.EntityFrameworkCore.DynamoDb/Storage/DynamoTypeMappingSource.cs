using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;

public class DynamoTypeMappingSource : ITypeMappingSource
{
    public CoreTypeMapping? FindMapping(IProperty property) => throw new NotImplementedException();

    public CoreTypeMapping? FindMapping(IElementType elementType) =>
        throw new NotImplementedException();

    public CoreTypeMapping? FindMapping(MemberInfo member) => throw new NotImplementedException();

    public CoreTypeMapping? FindMapping(MemberInfo member, IModel model, bool useAttributes) =>
        throw new NotImplementedException();

    public CoreTypeMapping? FindMapping(Type type) => throw new NotImplementedException();

    public CoreTypeMapping? FindMapping(
        Type type,
        IModel model,
        CoreTypeMapping? elementMapping = null
    ) => throw new NotImplementedException();
}
