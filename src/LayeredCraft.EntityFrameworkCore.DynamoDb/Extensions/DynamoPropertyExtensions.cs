// ReSharper disable CheckNamespace

using Microsoft.EntityFrameworkCore.Metadata;

namespace Microsoft.EntityFrameworkCore;

public static class DynamoPropertyExtensions
{
    extension(IReadOnlyProperty property)
    {
        /// <summary>Determines whether this property is the ordinal key part for an owned collection element.</summary>
        public bool IsOwnedOrdinalKeyProperty()
        {
            if (property.DeclaringType is not IReadOnlyEntityType entityType)
                return false;

            var ownership = entityType.FindOwnership();
            if (ownership == null || ownership.IsUnique)
                return false;

            if (property.ClrType != typeof(int) || !property.IsPrimaryKey())
                return false;

            var fkProperties = ownership.Properties;
            return !fkProperties.Contains(property);
        }
    }
}
