# Shared Patterns

Use this page for rules that apply across multiple subject references.

## Link handling

- Use relative links for internal docs navigation.
- Link to markdown sources/pages, not built HTML artifacts.
- Use meaningful anchor text.
- Avoid absolute links unless the destination is external.

## Title and structure defaults

- Use one top-level heading per page.
- Keep heading hierarchy consistent (`#`, `##`, `###`).
- Keep paragraphs short and scannable.

## Minimal example standard

For each feature example:

- show the smallest valid syntax pattern
- include only one meaningful option at a time
- avoid stacking unrelated options in one block

## Failure prevention checklist

Before final output, quickly check:

- internal links are relative and valid for docs context
- front matter and body title do not conflict
- feature syntax matches the relevant subject reference
- required prerequisites are called out when needed
- unresolved assumptions are listed in `Open Questions`

## Escalation rule

When deeper behavior is needed, do not guess.
Link to the corresponding upstream docs listed in [Docs Reference Outline](docs-reference-outline.md).
