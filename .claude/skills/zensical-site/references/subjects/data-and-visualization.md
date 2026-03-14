# Subject: Data and Visualization

Includes data tables and structured visual data presentation.

## Use this when

- presenting comparable values in table form
- creating compact structured summaries
- adding sortable or interactive table patterns when supported

## Minimum working pattern

```markdown
| Metric | Value |
| --- | --- |
| Build time | 42s |
| Pages | 120 |
```

## Required config / prerequisites

- basic tables work in markdown
- sortable/interactive behavior may require extra setup
- use [Configuration Reference](../dependencies/configuration-reference.md) when the answer needs `extra_javascript` examples or search-related caveats

Check [Extension Prerequisites](../dependencies/extension-prereqs.md) and [Navigation and Runtime Caveats](../dependencies/navigation-runtime-caveats.md) for setup-sensitive behavior.

## Common options the model may need

- alignment and readability choices
- compact column labels for small screens
- simple grouping by section before large tables

## Common mistakes to avoid

- very wide tables without considering readability
- dense numeric tables without context labels
- assuming sorting is available without support confirmation

## Interactions / caveats

- large tables often need surrounding explanation text.
- for complex visualizations, use diagrams guidance in [Code and Technical Content](code-and-technical-content.md).

## Deeper docs

- [Data tables](https://zensical.org/docs/authoring/data-tables/)
- [Diagrams](https://zensical.org/docs/authoring/diagrams/)
