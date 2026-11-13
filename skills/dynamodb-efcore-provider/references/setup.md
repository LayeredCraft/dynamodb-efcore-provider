# Setup and Configuration

Install the provider package, define a `DbContext`, and register it with `UseDynamo(...)`. Create
and own the AWS DynamoDB client through normal AWS SDK configuration; the provider uses that client
for its requests.

## Choose the package version

| Provider package                    | Target framework | EF Core | .NET    |
| ----------------------------------- | ---------------- | ------- | ------- |
| `EntityFrameworkCore.DynamoDb` 10.x | `net10.0`        | 10.0.x  | .NET 10 |
| `EntityFrameworkCore.DynamoDb` 11.x | `net11.0`        | 11.0.x  | .NET 11 |

Each package targets one framework and one EF Core major version. Match the provider's major
version to your EF Core major version: 11.x packages cannot be installed in a `net10.0` project,
and 10.x packages do not support EF Core 11. The exact EF Core range is listed on each package's
NuGet page — for example, 10.2.0 requires EF Core 10.0.14 or later 10.0.x, and 11.0.0 requires
EF Core 11.0.0 or later 11.0.x.

```bash
# .NET 10 / EF Core 10
dotnet add package EntityFrameworkCore.DynamoDb --version 10.2.0

# .NET 11 / EF Core 11
dotnet add package EntityFrameworkCore.DynamoDb --version 11.0.0
```

Use the latest release of the line that matches your EF Core version. Always pass `--version` on
.NET 10: without it, NuGet selects the newest stable package, which is an 11.x package that cannot
be installed in a `net10.0` project. Packages with a `-preview` suffix are CI builds from `main`;
do not use them in production.

To move from 10.x to 11.x, retarget the project to `net11.0`, update every
`Microsoft.EntityFrameworkCore.*` package (including `Tasks` and `Design` if you use precompiled
queries) to 11.0.x, and then update the provider to 11.x.

Choose configuration based on the application:

- ASP.NET Core: register the client and context in DI.
- Console or worker application: construct the client with its region and credentials, then pass it
  to the context options.
- Local development and tests: configure an explicit DynamoDB Local endpoint and credentials that
  DynamoDB Local accepts.

## Important options to check

- Region, credentials, endpoint, retry behavior, and client lifetime belong to the AWS client.
- Read consistency, scan-like query policy, automatic index selection, logging, and capacity
  reporting belong to provider/context options.
- Build one options configuration per application role. Do not create a DynamoDB client per query.

## Before production

- Give the runtime identity only the required DynamoDB permissions.
- Confirm region and table names in the deployed environment.
- Decide whether strongly consistent reads are needed; they affect cost and availability choices.
- Test the same key and index layout the application will use in DynamoDB.
