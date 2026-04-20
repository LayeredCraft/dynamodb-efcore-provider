# Zensical Docs Reference Outline

Goal: define which docs to reference later when expanding the `zensical-site` skill, and what to extract from each.

## 1) Authoring docs to reference

- [Markdown](https://zensical.org/docs/authoring/markdown/)
  - Pull later: baseline markdown rules, internal linking guidance, page-title precedence.
- [Front matter](https://zensical.org/docs/authoring/frontmatter/)
  - Pull later: supported front matter keys, precedence/override behavior, layout/search-related keys.
- [Admonitions](https://zensical.org/docs/authoring/admonitions/)
  - Pull later: syntax variants, supported admonition types, required extension/config.
- [Buttons](https://zensical.org/docs/authoring/buttons/)
  - Pull later: button syntax/classes, icon usage in buttons, extension requirements.
- [Code blocks](https://zensical.org/docs/authoring/code-blocks/)
  - Pull later: fenced code options (titles/line numbers/highlights/annotations/snippets), toggles, feature flags.
- [Content tabs](https://zensical.org/docs/authoring/content-tabs/)
  - Pull later: tab syntax, linked-tab behavior, nesting/anchor constraints.
- [Data tables](https://zensical.org/docs/authoring/data-tables/)
  - Pull later: table syntax conventions, sorting integration, compatibility constraints.
- [Diagrams](https://zensical.org/docs/authoring/diagrams/)
  - Pull later: Mermaid setup, supported diagram families, customization hooks.
- [Footnotes](https://zensical.org/docs/authoring/footnotes/)
  - Pull later: reference/definition syntax, tooltip integration behavior.
- [Formatting](https://zensical.org/docs/authoring/formatting/)
  - Pull later: inline formatting patterns, required extensions.
- [Grids](https://zensical.org/docs/authoring/grids/)
  - Pull later: card/grid container syntax, nesting rules, extension dependencies.
- [Icons, Emojis](https://zensical.org/docs/authoring/icons-emojis/)
  - Pull later: shortcode conventions, icon naming rules, template usage.
- [Images](https://zensical.org/docs/authoring/images/)
  - Pull later: alignment/captions/lazy-load patterns, light/dark asset behavior.
- [Lists](https://zensical.org/docs/authoring/lists/)
  - Pull later: list-type syntax (unordered/ordered/definition/task), extension requirements.
- [Math](https://zensical.org/docs/authoring/math/)
  - Pull later: MathJax vs KaTeX options, delimiter conventions, navigation integration caveats.
- [Tooltips](https://zensical.org/docs/authoring/tooltips/)
  - Pull later: tooltip and abbreviation syntax, glossary automation behavior.

## 2) Cross-reference docs required for authoring correctness

- [Basics: use_directory_urls](https://zensical.org/docs/setup/basics/#use_directory_urls)
  - Pull later: how URL mode changes link generation/resolution.
- [Navigation: instant navigation](https://zensical.org/docs/setup/navigation/#instant-navigation)
  - Pull later: runtime navigation behavior impacting JS-backed features (math/diagrams/tables).
- [Navigation: hide the sidebars](https://zensical.org/docs/setup/navigation/#hide-the-sidebars)
  - Pull later: sidebar/layout controls that affect page composition and front matter.
- [Site search](https://zensical.org/docs/setup/search/)
  - Pull later: metadata/indexing behavior tied to front matter and discoverability.
- [Colors](https://zensical.org/docs/setup/colors/)
  - Pull later: theme/palette behavior affecting rendering choices.
- [Logo and icons: additional icons](https://zensical.org/docs/setup/logo-and-icons/#additional-icons)
  - Pull later: custom icon registration and usage flow.
- [Python Markdown](https://zensical.org/docs/setup/extensions/python-markdown/)
  - Pull later: base Python Markdown extension setup required by authoring features.
- [Python Markdown Extensions](https://zensical.org/docs/setup/extensions/python-markdown-extensions/)
  - Pull later: pymdown extension setup/options used across many authoring features.
- [Customization](https://zensical.org/docs/customization/)
  - Pull later: CSS/JS/template override mechanisms for behavior and styling adjustments.
- [Customization: additional CSS](https://zensical.org/docs/customization/#additional-css)
  - Pull later: custom stylesheet integration points.
- [Customization: additional JavaScript](https://zensical.org/docs/customization/#additional-javascript)
  - Pull later: custom JS integration points.
- [Customization: custom templates](https://zensical.org/docs/customization/#custom-templates)
  - Pull later: template customization entry points.
- [Customization: configuring overrides](https://zensical.org/docs/customization/#configuring-overrides)
  - Pull later: override config structure and placement.
- [Customization: template overrides](https://zensical.org/docs/customization/#template-overrides)
  - Pull later: template override behavior and scope.
- [Customization: overriding blocks](https://zensical.org/docs/customization/#overriding-blocks)
  - Pull later: block-level extension points.
- [Customization: extending the theme](https://zensical.org/docs/customization/#extending-the-theme)
  - Pull later: theme extension strategy and boundaries.

## 3) Extraction template for the next pass

Use these fields when we return to pull details:

- `doc_url`
- `doc_title`
- `feature_area`
- `authoring_purpose`
- `required_config`
- `syntax_patterns`
- `options_modifiers`
- `dependencies`
- `behavioral_rules`
- `limitations_edge_cases`
- `cross_reference_urls`
- `minimal_examples_to_capture`
