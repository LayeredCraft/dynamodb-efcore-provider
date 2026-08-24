using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Microsoft.EntityFrameworkCore.DynamoDB;

/// <summary>
/// DynamoDB-specific logger categories. Most DynamoDB events use standard EF Core categories
/// from the <c>Microsoft.EntityFrameworkCore.Diagnostics.DbLoggerCategory</c> class; this class
/// provides additional categories for provider-specific concerns rooted at
/// <c>Microsoft.EntityFrameworkCore.DynamoDB</c>.
/// </summary>
public static class DbLoggerCategory
{
    /// <summary>
    /// Category for DynamoDB capacity-unit consumption and throttling events.
    /// Category name: <c>Microsoft.EntityFrameworkCore.DynamoDB.Capacity</c>.
    /// </summary>
    public sealed class Capacity : LoggerCategory<Capacity> { }
}
