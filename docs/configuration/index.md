---
title: Configuration
description: Overview of configuration options for the DynamoDB EF Core provider.
icon: lucide/settings
---

# Configuration

The DynamoDB EF Core provider is configured through `DbContextOptionsBuilder` and a set of fluent
APIs for mapping entities to tables and keys.

## In This Section

### [Client Setup](client-setup.md)

Configure the `IAmazonDynamoDB` client the provider uses for all SDK calls. You can supply a
pre-built client instance, a base `AmazonDynamoDBConfig`, or an inline callback to set the
endpoint, region, and credentials. This page also covers pointing at DynamoDB Local for local
development and testing.

### [DbContext Options](dbcontext.md)

Register the provider via `UseDynamo` and tune the runtime behavior: transaction overflow
strategy, maximum transaction and batch sizes, automatic index selection mode, and per-context
overrides through the `DatabaseFacade` extension methods.

### [Table and Key Mapping](table-key-mapping.md)

Map each entity type to a DynamoDB table and declare which properties serve as the partition key
and optional sort key. Covers both convention-based key discovery and explicit fluent
configuration, the key property requirements, and the model validation errors the provider raises
at finalization.

### [Attribute Naming](attribute-naming.md)

Control how CLR property names translate to DynamoDB attribute names. The provider applies
CamelCase by default; you can switch to a different built-in convention, supply a custom naming
function, or override individual properties with an explicit attribute name.

## See also

- [Getting Started](../getting-started.md)
- [Data Modeling](../modeling/index.md)
