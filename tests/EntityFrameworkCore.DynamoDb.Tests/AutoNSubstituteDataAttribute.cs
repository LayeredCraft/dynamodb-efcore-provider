using AutoFixture;
using AutoFixture.AutoNSubstitute;
using AutoFixture.Xunit3;

namespace EntityFrameworkCore.DynamoDb.Tests;

/// <summary>Represents the AutoNSubstituteDataAttribute type.</summary>
public class AutoNSubstituteDataAttribute() : AutoDataAttribute(()
    => new Fixture().Customize(new AutoNSubstituteCustomization { ConfigureMembers = true }));

/// <summary>Represents the InlineAutoNSubstituteDataAttribute type.</summary>
public class InlineAutoNSubstituteDataAttribute : InlineAutoDataAttribute
{
    /// <summary>Provides functionality for this member.</summary>
    public InlineAutoNSubstituteDataAttribute(params object[] args) : base(
        new AutoNSubstituteDataAttribute(),
        args) { }
}
