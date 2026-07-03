#if NET11_0_OR_GREATER
using EntityFrameworkCore.DynamoDb.SpecificationTests.TestUtilities;
using Microsoft.EntityFrameworkCore;

namespace EntityFrameworkCore.DynamoDb.SpecificationTests;

public sealed class EntityFrameworkServiceCollectionExtensionsDynamoTest()
    : EntityFrameworkServiceCollectionExtensionsTestBase(DynamoTestHelpers.Instance)
{
    public override void Repeated_calls_to_add_do_not_modify_collection()
        => base.Repeated_calls_to_add_do_not_modify_collection();

    public override void Required_services_are_registered_with_expected_lifetimes()
        => base.Required_services_are_registered_with_expected_lifetimes();
}
#endif
