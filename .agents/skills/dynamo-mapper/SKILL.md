---
name: dynamo-mapper
description: Use this skill when you need to write or explain DynamoMapper mappings for DynamoDB `AttributeValue` items in C#. It covers how to declare mapper classes, how `DynamoMapper`, `DynamoField`, `DynamoIgnore`, and `DynamoMapperConstructor` behave, how lifecycle hooks work, what types and nested shapes are supported, how custom conversion really works, and how to troubleshoot DynamoMapper diagnostics and common gotchas without relying on stale docs.
---

# DynamoMapper

Use this skill when generating or explaining DynamoMapper code.

## Core truths

- DynamoMapper is a C# incremental source generator for `T <-> Dictionary<string, AttributeValue>`.
- Configure mapping on a `static partial` mapper class marked with `[DynamoMapper]`.
- The generator recognizes unimplemented partial methods whose names start with `To` or `From`
  and use the expected model/dictionary signatures.
- One-way mappers are valid: `To*` only or `From*` only.
- Domain models usually stay clean except for optional `[DynamoMapperConstructor]` on a constructor.
- Nested object mapping is implemented and tested.
- Lifecycle hooks are implemented and validated (`BeforeToItem`, `AfterToItem`,
  `BeforeFromItem`, `AfterFromItem`).
- Some public docs are stale; use `references/gotchas.md` when behavior seems surprising.

## Choose a path

- Read `references/core-usage.md` for mapper shape, attribute behavior, defaults, constructor rules,
  and common implementation patterns.
- Read `references/type-matrix.md` for supported types, collection rules, nested shapes, and hard
  limits.
- Read `references/diagnostics.md` for generator diagnostics and the most likely fixes.
- Read `references/hooks.md` for hook signatures, call order, generation behavior, and
  hook-specific diagnostics.
- Read `references/gotchas.md` for stale-doc traps and the non-obvious rules most likely to cause
  bad guidance.

## Default workflow

1. Identify whether the task is mapper authoring, supported-type lookup, or diagnostics.
2. Read the matching reference file before making assumptions.
3. If the task touches hooks, read `references/hooks.md` first.
4. If the task touches nested mapping or converters, check `references/gotchas.md` before
   answering.
5. Keep answers concrete and code-oriented.

## High-risk misunderstandings

- Do not tell the user to decorate every POCO property; configuration belongs on the mapper class.
- Do not assume methods must be named exactly `ToItem` and `FromItem`; the `To`/`From` prefix
  matters, but the generator also expects the recognized model/dictionary signatures.
- Do not invent hook signatures; all hooks must be `static partial void` with exact parameter
  shapes.
- `AfterFromItem` requires `ref` on the entity parameter.
- Do not assume every unsupported converter setup becomes a DynamoMapper diagnostic; some become normal C# compile errors.

## Reference map

- `references/core-usage.md`
- `references/type-matrix.md`
- `references/diagnostics.md`
- `references/hooks.md`
- `references/gotchas.md`
