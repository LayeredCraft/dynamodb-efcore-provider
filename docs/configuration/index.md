---
title: Configuration
description: Overview of configuration options for the DynamoDB EF Core provider.
icon: lucide/settings
---

# Configuration

The DynamoDB EF Core provider is configured through `DbContextOptionsBuilder` and a set of fluent APIs for mapping entities to tables and keys.

This section covers how to set up the DynamoDB client, configure your `DbContext`, and control how entities map to DynamoDB attributes.

- [Client Setup](client-setup.md) — Configure the DynamoDB client, authentication, and endpoint.
- [DbContext Options](dbcontext.md) — Register the provider and set available options.
- [Table and Key Mapping](table-key-mapping.md) — Map entities to tables and configure partition and sort keys.
- [Attribute Naming](attribute-naming.md) — Control how property names map to DynamoDB attribute names.

## See also

- [Getting Started](../getting-started.md)
- [Data Modeling](../modeling/index.md)
