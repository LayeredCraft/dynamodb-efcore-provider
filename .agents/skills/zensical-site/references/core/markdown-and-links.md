# Core: Markdown and Links

Use this reference for baseline page authoring, linking, and title behavior.

## Use this when

- drafting or rewriting standard docs pages
- creating or updating internal links
- deciding how titles should be derived

## Minimum working pattern

```markdown
# Page Title

Intro paragraph with context.

## Section Heading

Use relative links like [Front matter](../frontmatter/).
```

## Required config / prerequisites

- No special extension needed for basic markdown.
- Link output behavior depends on URL mode (`use_directory_urls`).

## Key rules

- Write links to docs pages using relative paths.
- Do not hardcode output `.html` links for internal docs pages.
- Keep one `#` heading per page body when title is content-driven.
- If navigation/front matter sets title, ensure content heading stays consistent.

## Page title precedence

In practice, title resolution follows this order:

1. navigation-defined title
2. front matter title
3. first-level markdown heading
4. filename fallback

## Common mistakes to avoid

- linking to built HTML instead of docs-relative page paths
- mixing absolute and relative links inconsistently
- adding multiple top-level headings in one page
- assuming content `#` always controls title when nav/front matter overrides it

## Interactions / caveats

- front matter can override visible title behavior ([Front Matter](front-matter.md)).
- URL mode impacts generated paths ([Navigation and Runtime Caveats](../dependencies/navigation-runtime-caveats.md)).

## Deeper docs

- [Markdown](https://zensical.org/docs/authoring/markdown/)
- [Basics: use_directory_urls](https://zensical.org/docs/setup/basics/#use_directory_urls)
