# Dependencies: Navigation and Runtime Caveats

Use this page when rendering behavior may change due to navigation/runtime configuration.

## Use this when

- output includes JS-backed features (tabs, diagrams, math, sortable tables)
- behavior differs between first load and in-site navigation
- page metadata interacts with layout/navigation controls

## Key caveats to remember

- Instant navigation can change how some client-side features initialize.
- Sidebar and navigation settings can influence layout expectations.
- URL mode changes internal link output behavior.

## Authoring implications

- Keep link syntax compatible with docs-relative navigation.
- Avoid relying on fragile runtime-only behavior unless confirmed.
- Mention caveats when the user asks for advanced interactive behavior.
- If the user needs the exact TOML flags involved, pair this page with [Configuration Reference](configuration-reference.md).

## Fallback policy

- Prefer stable, static markdown patterns when runtime behavior is uncertain.
- If interactive behavior is required, call out dependency assumptions clearly.

## Deeper docs

- [Navigation: instant navigation](https://zensical.org/docs/setup/navigation/#instant-navigation)
- [Navigation: hide the sidebars](https://zensical.org/docs/setup/navigation/#hide-the-sidebars)
- [Basics: use_directory_urls](https://zensical.org/docs/setup/basics/#use_directory_urls)
