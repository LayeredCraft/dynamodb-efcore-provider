---
title: Data Modeling
description: Overview of how to model entities for the DynamoDB EF Core provider.
icon: lucide/database
---

# Data Modeling

Entity modeling in the DynamoDB EF Core provider extends standard EF Core conventions with DynamoDB-specific configuration for partition keys, sort keys, secondary indexes, and owned types.

This section covers how to define your model and how the provider maps it to DynamoDB's data structures.

- [Entities and Keys](entities-keys.md) — Define entities and configure partition and sort keys.
- [Secondary Indexes](secondary-indexes.md) — Configure and query Global and Local Secondary Indexes.
- [Owned Types and Collections](owned-types.md) — Store nested objects and collections within a single DynamoDB item.
- [Inheritance and Discriminators](inheritance.md) — Model entity inheritance with discriminator attributes.

## See also

- [Configuration](../configuration/index.md)
- [Querying](../querying/index.md)
