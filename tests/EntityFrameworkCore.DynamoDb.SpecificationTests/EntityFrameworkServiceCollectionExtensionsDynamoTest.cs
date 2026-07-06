#if NET11_0_OR_GREATER
using System.Reflection;
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.TestUtilities;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public sealed class EntityFrameworkServiceCollectionExtensionsDynamoTest()
    : EntityFrameworkServiceCollectionExtensionsTestBase(DynamoTestHelpers.Instance)
{
    [ConditionalFact]
    public void Check_all_tests_overridden()
    {
        var testClass = typeof(EntityFrameworkServiceCollectionExtensionsDynamoTest);
        // The base also has a non-virtual fact; it is shadowed explicitly below.
        var inheritedOverridableTests = testClass
            .GetRuntimeMethods()
            .Where(method => method.DeclaringType != testClass
                && method.IsVirtual
                && !method.IsFinal
                && (Attribute.IsDefined(method, typeof(ConditionalFactAttribute))
                    || Attribute.IsDefined(method, typeof(ConditionalTheoryAttribute))))
            .Select(method => method.Name);

        Assert.Empty(inheritedOverridableTests);
    }

    [ConditionalFact]
    public new void Calling_AddEntityFramework_explicitly_does_not_change_services()
        => base.Calling_AddEntityFramework_explicitly_does_not_change_services();

    public override void Repeated_calls_to_add_do_not_modify_collection()
        => base.Repeated_calls_to_add_do_not_modify_collection();

    public override void Required_services_are_registered_with_expected_lifetimes()
        => base.Required_services_are_registered_with_expected_lifetimes();
}
#endif
