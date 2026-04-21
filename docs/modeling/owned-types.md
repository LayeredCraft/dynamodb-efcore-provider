---
title: Owned Types and Collections
description: How owned entities and owned collections are stored and queried in DynamoDB.
---

# Owned Types and Collections

_Owned types are stored inline within the owning entity's DynamoDB item as nested maps or lists, with no separate table or key._

## Owned Entities

## Owned Collections

## Nesting Limits and Constraints

!!! warning

    DynamoDB imposes a maximum item size of 400 KB. Deeply nested or large owned collections count against this limit.

## See also

- [Entities and Keys](entities-keys.md)
- [Limitations](../limitations.md)
