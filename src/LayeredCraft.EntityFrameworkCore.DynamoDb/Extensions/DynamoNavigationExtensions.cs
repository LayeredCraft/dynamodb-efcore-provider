// ReSharper disable CheckNamespace

using Microsoft.EntityFrameworkCore.Metadata;

namespace Microsoft.EntityFrameworkCore;

public static class DynamoNavigationExtensions
{
    extension(IReadOnlyNavigation navigation)
    {
        /// <summary>Determines whether this navigation is embedded in the same DynamoDB item as its owner.</summary>
        public bool IsEmbedded()
            => !navigation.IsOnDependent && navigation.TargetEntityType.IsOwned();
    }
}
