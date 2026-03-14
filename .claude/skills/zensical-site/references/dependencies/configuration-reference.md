# Dependencies: Configuration Reference

Use this page when the task depends on how Zensical is configured, not just on markdown syntax.

## Default configuration model

- Prefer `zensical.toml` examples and guidance.
- Zensical can read `mkdocs.yml`, but this skill should default to TOML unless the user is explicitly migrating or maintaining YAML.
- Most settings live under `[project]`.
- `site_name` is required in the project config.
- Theme flags usually live under `[project.theme]`.
- Markdown features usually live under `[project.markdown_extensions...]`.
- Template and behavior overrides often use `[project.extra]`, `extra_css`, `extra_javascript`, and `custom_dir`.

## Core TOML patterns to recognize

### Base project settings

```toml
[project]
site_name = "Example Docs"
site_url = "https://docs.example.com"
docs_dir = "docs"
site_dir = "site"
use_directory_urls = true
```

### Theme features

```toml
[project.theme]
features = ["navigation.instant", "search.highlight"]
```

### Navigation structure

```toml
[project]
nav = [
  { Home = "index.md" },
  { Guides = [{ Getting_started = "guides/getting-started.md" }] }
]
```

### Markdown extension blocks

```toml
[project.markdown_extensions.admonition]

[project.markdown_extensions.pymdownx.details]

[project.markdown_extensions.pymdownx.superfences]
```

### Asset registration

```toml
[project]
extra_css = ["stylesheets/extra.css"]
extra_javascript = ["javascripts/extra.js"]
```

### Structured JavaScript entries

```toml
[[project.extra_javascript]]
path = "javascripts/extra.mjs"
type = "module"
```

### Extra metadata for templates/features

```toml
[project.extra.status]
new = "Recently added"
deprecated = "Deprecated"
```

## Path and scope rules

- `docs_dir` and `site_dir` are relative to the config file.
- `docs_dir` cannot currently be `.`.
- `extra_css` and `extra_javascript` paths point to files inside the docs directory.
- `custom_dir` is resolved relative to the config file, not relative to `docs_dir`.
- Use `document$.subscribe(...)` in custom JavaScript when behavior must survive instant navigation.

## Feature-to-config map

### Navigation and previews

- `navigation.instant` requires `site_url` to be set.
- Instant previews also require `site_url`.
- Instant preview automation uses `[project.markdown_extensions.zensical.extensions.preview]` with nested `configurations`.
- `navigation.prune` is incompatible with `navigation.expand`.
- `navigation.indexes` is incompatible with `toc.integrate`.

### Front matter behaviors

- Page status badges require `[project.extra.status]` before `status:` in front matter will mean anything.
- Page templates require `[project.theme] custom_dir = "overrides"` and a template file in that overrides directory.
- Search exclusion for an entire page works with front matter alone.
- Search exclusion for sections or blocks requires `attr_list`.

### Admonitions

- Base admonitions need `[project.markdown_extensions.admonition]`.
- Collapsible admonitions need `[project.markdown_extensions.pymdownx.details]`.
- Nested admonitions or nested rich blocks need `[project.markdown_extensions.pymdownx.superfences]`.
- Admonition icon changes use `[project.theme.icon.admonition]`.

### Buttons and attribute-driven styling

- Buttons require `[project.markdown_extensions.attr_list]`.
- Any advice that uses classes like `.md-button`, `.copy`, `.select`, or `data-search-exclude` depends on `attr_list` support.

### Content tabs

- Content tabs need `[project.markdown_extensions.pymdownx.superfences]` and `[project.markdown_extensions.pymdownx.tabbed]` with `alternate_style = true`.
- Linked tabs across pages use `[project.theme] features = ["content.tabs.link"]`.
- Better tab anchors can use `[project.markdown_extensions.pymdownx.tabbed.slugify]`.

### Code blocks

- Recommended code-block setup uses `pymdownx.highlight`, `pymdownx.inlinehilite`, `pymdownx.snippets`, and `pymdownx.superfences`.
- Good defaults for advanced code blocks include `anchor_linenums = true`, `line_spans = "__span"`, and `pygments_lang_class = true`.
- Global copy/select/annotate controls use theme features such as `content.code.copy`, `content.code.select`, and `content.code.annotate`.
- Code annotations depend on Pygments-based highlighting, not generic JavaScript highlighters.
- Extra annotation selectors use `[project.extra.annotate]`.

### Diagrams

- Mermaid diagrams require `pymdownx.superfences` with a `custom_fences` entry for `mermaid`.
- Standard Mermaid support needs no extra JavaScript beyond that fence config.
- Advanced Mermaid customization can add `extra_javascript`, and any runtime code should work with `document$.subscribe(...)` if needed.

### Math

- Math support needs `[project.markdown_extensions.pymdownx.arithmatex] generic = true`.
- MathJax also needs `extra_javascript` entries for the local setup file and the MathJax CDN.
- KaTeX also needs `extra_javascript`, `extra_css`, and a small runtime script.
- Math runtime scripts should subscribe to `document$` so rendering works with instant navigation.

### Tooltips and glossary patterns

- Abbreviations need `[project.markdown_extensions.abbr]`.
- Non-link tooltips and block-level tooltip attributes need `attr_list`.
- Shared glossary snippets need `pymdownx.snippets`, often with `auto_append`.
- Enhanced UI tooltips use the theme feature `content.tooltips`.

### Tables

- Basic table support uses `[project.markdown_extensions.tables]`.
- Sortable tables are not built-in config flags; they require `extra_javascript` plus a runtime helper script.

## Safe authoring rules for this skill

- When the user asks for config help, show TOML-first examples unless their repo already uses YAML.
- Do not imply that a markdown feature works automatically if the docs say it needs extension or theme setup.
- When a feature works only with additional CSS, JS, icons, or overrides, separate the content draft from the configuration snippet.
- When a feature has compatibility caveats, mention them instead of presenting the setup as universal.

## Common gotchas

- forgetting `[project]` or `[project.theme]` scopes in TOML examples
- assuming `site_url` is optional for instant navigation or instant previews
- using `status:` in front matter without defining statuses in `[project.extra.status]`
- recommending `template:` without configuring `custom_dir`
- suggesting section/block search exclusions without `attr_list`
- combining `navigation.prune` with `navigation.expand`
- combining `navigation.indexes` with `toc.integrate`
- placing extra CSS/JS paths as if they were relative to the repo root instead of the docs directory

## Deeper docs

- [Basics](https://zensical.org/docs/setup/basics/)
- [Navigation](https://zensical.org/docs/setup/navigation/)
- [Search](https://zensical.org/docs/setup/search/)
- [Python Markdown](https://zensical.org/docs/setup/extensions/python-markdown/)
- [Python Markdown Extensions](https://zensical.org/docs/setup/extensions/python-markdown-extensions/)
- [Customization](https://zensical.org/docs/customization/)
