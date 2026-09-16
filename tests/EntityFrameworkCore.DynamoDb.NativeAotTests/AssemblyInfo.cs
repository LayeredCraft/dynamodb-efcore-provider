using Xunit;
using Xunit.Sdk;
using Xunit.v3;

[assembly: AssemblyFixture<EntityFrameworkCore.DynamoDb.NativeAotTests.DynamoFixture>]
// xunit.v3.aot.mtp-v2 currently emits an incompatible factory for this fixture's parameterless
// constructor, so retain the context-aware constructor and register the runtime factory explicitly.
[assembly: EntityFrameworkCore.DynamoDb.NativeAotTests.DynamoFixtureRegistration]
[assembly: Parallelization(Mode = ParallelMode.None)]
