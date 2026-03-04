---
name: dynamodb-efcore-docs-update
description: Update documentation for behavior changes in the DynamoDB EF Core provider. Use when LINQ translation, PartiQL generation, pagination/projection semantics, diagnostics, configuration, or supported limitations change.
---

# DynamoDB EF Core Docs Update

## Overview

Use this skill whenever provider behavior changes so docs stay accurate in the same story. Follow this checklist to update canonical docs pages, keep examples aligned with supported translation, and verify docs build.

## Trigger Conditions

- New or changed LINQ operator translation
- PartiQL generation behavior changes
- Pagination/result limit/token semantics changes
- Projection/materialization behavior changes
- Provider option/configuration changes
- New diagnostics, warnings, or logging changes
- Newly supported or unsupported query shapes
- Query pipeline architecture flow changes

## Workflow

1. Classify the behavior change
   - Operator translation
   - Pagination semantics
   - Projection/materialization
   - Configuration/options
   - Diagnostics/warnings
   - Limitations/support matrix
   - Architecture/pipeline

2. Update canonical docs first
   - `docs/operators.md` for operator behavior and examples
   - `docs/limitations.md` for unsupported or constrained shapes

3. Update topical docs only where impacted
   - `docs/pagination.md`
   - `docs/projections.md`
   - `docs/configuration.md`
   - `docs/diagnostics.md`
   - `docs/architecture.md`

4. Check example correctness and scope
   - Keep examples aligned with currently supported translation
   - Avoid method calls in `Where` unless explicitly supported
   - Keep content user-facing and concise
   - Do not add internal test/code links in published docs

5. Add DynamoDB/PartiQL semantic context when needed
   - If behavior depends on AWS semantics, include a relevant AWS reference
   - Typical references: ExecuteStatement API, PartiQL SELECT/operators, AttributeValue

6. Update docs site config only if necessary
   - Edit `zensical.toml` only when navigation or docs config must change

7. Verify docs build
   - Preferred: `uv run zensical build`
   - Alternative: `task docs:build`

## Output Checklist

- Changed behavior is documented in all relevant canonical/topical pages
- Examples reflect currently supported LINQ translation
- AWS semantic references are added where behavior depends on DynamoDB/PartiQL rules
- No internal implementation/test links were introduced in docs
- Docs build succeeds

## Reference

See `references/doc-pages.md` for a quick page-impact matrix and an operator entry template.
