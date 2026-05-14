---
title: Data Modeling
description: Overview of how to model entities for the DynamoDB EF Core provider.
icon: lucide/database
---

# Data Modeling

Entity modeling in the DynamoDB EF Core provider extends standard EF Core conventions with DynamoDB-specific configuration for partition keys, sort keys, secondary indexes, and complex properties.

This section covers how to define your model and how the provider maps it to DynamoDB's data structures. EF Core foreign-key and navigation relationships are not supported: use complex types for embedded data, and separate root entities for separate DynamoDB items or tables.

- [Entities and Keys](entities-keys.md) — Define entities and configure partition and sort keys.
- [Secondary Indexes](secondary-indexes.md) — Configure and query Global and Local Secondary Indexes.
- [Complex Properties and Collections](complex-types.md) — Store nested objects and collections within a single DynamoDB item.
- [Single-Table Design and Discriminators](single-table-design.md) — Map multiple entity types to one table with discriminator-driven filtering.

## See also

- [Configuration](../configuration/index.md)
- [Querying](../querying/index.md)
