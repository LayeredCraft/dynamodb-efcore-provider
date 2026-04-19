# Subject: Inline Formatting and Microcontent

Includes formatting, lists, and footnotes.

## Use this when

- improving readability of technical prose
- structuring procedures, requirements, and definitions
- adding supporting references that should not interrupt flow

## Minimum working patterns

### Lists

```markdown
- First item
- Second item

1. Step one
2. Step two
```

### Inline emphasis

```markdown
Use **bold** for emphasis and `inline code` for commands.
```

### Footnote

```markdown
The behavior depends on configuration.[^1]

[^1]: Verify setup before publishing.
```

## Required config / prerequisites

- basic lists and emphasis are markdown-native
- some advanced formatting or footnote rendering can depend on extension support

## Common options the model may need

- ordered vs unordered lists by intent
- definition/task list patterns when supported
- restrained inline emphasis for scanability

## Common mistakes to avoid

- over-formatting simple prose
- mixing list indentation levels incorrectly
- using footnotes for critical instructions that should stay inline
- inconsistent list punctuation/voice within one section

## Interactions / caveats

- footnotes and advanced list behavior may vary by extension setup.
- if behavior is unclear, use simpler markdown and add a note.

## Deeper docs

- [Formatting](https://zensical.org/docs/authoring/formatting/)
- [Lists](https://zensical.org/docs/authoring/lists/)
- [Footnotes](https://zensical.org/docs/authoring/footnotes/)
