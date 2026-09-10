# Setup and Configuration

Install the provider package, define a `DbContext`, and register it with `UseDynamo(...)`. Create
and own the AWS DynamoDB client through normal AWS SDK configuration; the provider uses that client
for its requests.

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
