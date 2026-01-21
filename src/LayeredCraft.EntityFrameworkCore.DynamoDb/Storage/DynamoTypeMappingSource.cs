using System.Reflection;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

namespace LayeredCraft.EntityFrameworkCore.DynamoDb.Storage;

public class DynamoTypeMappingSource : TypeMappingSource
{
    public DynamoTypeMappingSource(TypeMappingSourceDependencies dependencies)
        : base(dependencies) { }

    protected override CoreTypeMapping? FindMapping(in TypeMappingInfo mappingInfo)
    {
        if (mappingInfo.ClrType == typeof(string))
        {
            return new DynamoTypeMapping(mappingInfo.ClrType);
        }

        return null;
    }
}
