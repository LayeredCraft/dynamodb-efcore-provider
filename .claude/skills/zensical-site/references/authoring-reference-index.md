# Authoring Reference Index

Use this page to decide which reference file to load for a task.

## How to use this index

1. Identify the user task type.
2. Load the matching core or subject reference.
3. Load dependency references whenever output depends on setup, feature flags, runtime behavior, or TOML snippets.
4. If the user asks for advanced behavior, link out to upstream docs.

## Task routing

| User need | Open this reference first | Also check |
| --- | --- | --- |
| Write or rewrite a standard docs page | [Markdown and Links](core/markdown-and-links.md) | [Shared Patterns](shared-patterns.md), [Front Matter](core/front-matter.md) |
| Set page metadata | [Front Matter](core/front-matter.md) | [Configuration Reference](dependencies/configuration-reference.md), [Navigation and Runtime Caveats](dependencies/navigation-runtime-caveats.md) |
| Explain or draft `zensical.toml` configuration | [Configuration Reference](dependencies/configuration-reference.md) | [Navigation and Runtime Caveats](dependencies/navigation-runtime-caveats.md), [Customization Boundaries](dependencies/customization-boundaries.md) |
| Use admonitions, tabs, buttons, or tooltips | [Callouts and Interactive Elements](subjects/callouts-and-interactive-elements.md) | [Extension Prerequisites](dependencies/extension-prereqs.md), [Configuration Reference](dependencies/configuration-reference.md) |
| Add code snippets, diagrams, or equations | [Code and Technical Content](subjects/code-and-technical-content.md) | [Extension Prerequisites](dependencies/extension-prereqs.md), [Configuration Reference](dependencies/configuration-reference.md), [Navigation and Runtime Caveats](dependencies/navigation-runtime-caveats.md) |
| Build media-rich layouts | [Layout and Media](subjects/layout-and-media.md) | [Customization Boundaries](dependencies/customization-boundaries.md) |
| Create tables and structured data sections | [Data and Visualization](subjects/data-and-visualization.md) | [Extension Prerequisites](dependencies/extension-prereqs.md), [Configuration Reference](dependencies/configuration-reference.md) |
| Improve text-level formatting and list structure | [Inline Formatting and Microcontent](subjects/inline-formatting-and-microcontent.md) | [Shared Patterns](shared-patterns.md) |
| Figure out whether custom CSS/JS/templates are needed | [Customization Boundaries](dependencies/customization-boundaries.md) | [Docs Reference Outline](docs-reference-outline.md) |

## When to escalate to upstream docs

Escalate (link out) when the task asks for:

- complex extension options not covered in these references
- deep theme customization or override internals
- unusual runtime behavior across multiple plugins
- edge-case compatibility behavior that affects production output

Use upstream links from [Docs Reference Outline](docs-reference-outline.md).
