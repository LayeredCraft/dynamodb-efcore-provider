# Core: Front Matter

Use this reference for page metadata and title/visibility controls.

## Use this when

- creating pages that need metadata
- setting page title behavior explicitly
- controlling discoverability or layout behavior per page

## Minimum working pattern

```markdown
---
title: Example Page
description: One-sentence summary for readers.
---

# Example Page

Page content starts here.
```

## Required config / prerequisites

- Front matter support is expected in docs authoring flow.
- Some keys influence behavior only when related setup features are enabled (search, navigation, templates).
- Use [Configuration Reference](../dependencies/configuration-reference.md) when status labels, template overrides, or search behavior need matching `zensical.toml` setup.

## What to capture in output

- page identity: title/description
- optional discoverability controls
- optional layout controls when explicitly requested
- metadata consistency with page body headings

## Working rules

- Keep front matter minimal; include only fields needed for the task.
- Align metadata with page purpose and heading text.
- If uncertain about advanced keys, call it out and provide a safe default.

## Common mistakes to avoid

- stuffing many optional keys without user need
- title mismatch between front matter and body heading
- using fields that depend on unknown site configuration without a caveat

## Interactions / caveats

- title precedence is affected by navigation and markdown heading ([Markdown and Links](markdown-and-links.md)).
- search/layout behavior can depend on setup configuration ([Navigation and Runtime Caveats](../dependencies/navigation-runtime-caveats.md)).

## Deeper docs

- [Front matter](https://zensical.org/docs/authoring/frontmatter/)
- [Search](https://zensical.org/docs/setup/search/)
- [Navigation](https://zensical.org/docs/setup/navigation/)
