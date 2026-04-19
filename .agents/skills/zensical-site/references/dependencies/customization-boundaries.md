# Dependencies: Customization Boundaries

Use this page to decide when markdown alone is enough and when customization work is required.

## Use this when

- the user asks for behavior that likely needs CSS, JS, templates, or overrides
- visual requirements go beyond built-in authoring features
- theme-level changes are requested

## What markdown usually handles well

- page structure and headings
- links, lists, tables, code blocks, basic admonitions
- standard media insertion

## What often needs customization

- custom component styling beyond built-in classes
- bespoke interactive behavior
- template-level layout changes
- advanced theme overrides and block extensions

## AI behavior rules

- Do not fake custom behavior with invalid markdown syntax.
- Clearly separate content draft from customization recommendations.
- Provide a short "next implementation step" note when customization is required.

## Escalation template

When customization is needed, say:

1. what part is achievable in markdown now
2. what part requires customization
3. which upstream section should be used next

## Deeper docs

- [Customization](https://zensical.org/docs/customization/)
- [Customization: additional CSS](https://zensical.org/docs/customization/#additional-css)
- [Customization: additional JavaScript](https://zensical.org/docs/customization/#additional-javascript)
- [Customization: custom templates](https://zensical.org/docs/customization/#custom-templates)
- [Customization: configuring overrides](https://zensical.org/docs/customization/#configuring-overrides)
- [Customization: template overrides](https://zensical.org/docs/customization/#template-overrides)
- [Customization: overriding blocks](https://zensical.org/docs/customization/#overriding-blocks)
- [Customization: extending the theme](https://zensical.org/docs/customization/#extending-the-theme)
