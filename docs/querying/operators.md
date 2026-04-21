---
title: Supported Operators
description: Reference table of supported LINQ operators and their PartiQL translation.
---

# Supported Operators

_This page is the authoritative reference for which LINQ operators translate to PartiQL server-side, which are evaluated client-side, and which are unsupported._

## Filtering Operators

| LINQ Operator | PartiQL Translation | Evaluation | Notes |
| ------------- | ------------------- | ---------- | ----- |
| `Where`       | `WHERE`             | Server     |       |

## Projection Operators

| LINQ Operator | PartiQL Translation | Evaluation | Notes |
| ------------- | ------------------- | ---------- | ----- |
| `Select`      |                     |            |       |

## Ordering Operators

| LINQ Operator       | PartiQL Translation | Evaluation | Notes |
| ------------------- | ------------------- | ---------- | ----- |
| `OrderBy`           |                     |            |       |
| `OrderByDescending` |                     |            |       |

## Element Operators

| LINQ Operator                | PartiQL Translation | Evaluation | Notes |
| ---------------------------- | ------------------- | ---------- | ----- |
| `First` / `FirstOrDefault`   |                     |            |       |
| `Single` / `SingleOrDefault` |                     |            |       |
| `Any`                        |                     |            |       |
| `Count`                      |                     |            |       |

## Scalar Type Support

| .NET Type                | DynamoDB Attribute Type | Notes |
| ------------------------ | ----------------------- | ----- |
| `string`                 | `S`                     |       |
| `int`, `long`, `decimal` | `N`                     |       |
| `bool`                   | `BOOL`                  |       |
| `byte[]`                 | `B`                     |       |
| `Guid`                   |                         |       |
| `DateTime`               |                         |       |
| `DateTimeOffset`         |                         |       |
| `DateOnly`               |                         |       |
| `TimeOnly`               |                         |       |
| `TimeSpan`               |                         |       |

## See also

- [How Queries Execute](how-queries-execute.md)
- [Filtering](filtering.md)
- [Limitations](../limitations.md)
