# Subject: Callouts and Interactive Elements

Includes admonitions, buttons, content tabs, and tooltips.

## Use this when

- emphasizing important notes/warnings
- creating action-style links with button presentation
- organizing alternative content paths with tabs
- adding inline clarifications with tooltips/abbreviations

## Minimum working patterns

### Admonition

```markdown
!!! note
    Key context for the reader.
```

### Button-style link

```markdown
[Get Started](../get-started/){ .md-button }
```

### Content tab

```markdown
=== "CLI"
    Use this command.

=== "UI"
    Use this interface path.
```

### Tooltip/abbreviation

```markdown
The API uses HTML.

*[HTML]: HyperText Markup Language
```

## Required config / prerequisites

- Most patterns depend on specific markdown extensions.
- Verify extension availability in [Extension Prerequisites](../dependencies/extension-prereqs.md) when behavior is uncertain.
- Use [Configuration Reference](../dependencies/configuration-reference.md) when the answer should include TOML examples for tabs, admonitions, buttons, tooltips, or linked tabs.

## Common options the model may need

- admonition type (`note`, `warning`, `tip`, etc.)
- button styling classes (if supported)
- tab labels that map to reader context (for example, platform names)

## Common mistakes to avoid

- incorrect indentation inside admonitions or tab bodies
- mixing tab syntaxes in one block
- using button classes without ensuring style support
- turning critical instructions into tooltips that hide required info

## Interactions / caveats

- Tabs and tooltips may rely on extension support and theme behavior.
- If interaction behavior is runtime-sensitive, check [Navigation and Runtime Caveats](../dependencies/navigation-runtime-caveats.md).

## Deeper docs

- [Admonitions](https://zensical.org/docs/authoring/admonitions/)
- [Buttons](https://zensical.org/docs/authoring/buttons/)
- [Content tabs](https://zensical.org/docs/authoring/content-tabs/)
- [Tooltips](https://zensical.org/docs/authoring/tooltips/)
