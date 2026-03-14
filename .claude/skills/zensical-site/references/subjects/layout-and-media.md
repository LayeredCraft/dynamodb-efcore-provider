# Subject: Layout and Media

Includes grids, images, and icons/emojis.

## Use this when

- building card layouts or visual content blocks
- adding screenshots, diagrams, or illustrative assets
- using iconography to improve scanability

## Minimum working patterns

### Image

```markdown
![Setup screen](../assets/setup-screen.png)
```

### Grid/card block

Use the simplest supported grid/card pattern from upstream docs for the current setup.

### Icon/emoji

Use supported shortcode or icon notation from the active icon set.

## Required config / prerequisites

- grid and icon behavior can depend on extension support
- some icon sets require explicit setup
- light/dark image swaps may rely on theme conventions

See [Extension Prerequisites](../dependencies/extension-prereqs.md) and [Customization Boundaries](../dependencies/customization-boundaries.md).

## Common options the model may need

- image caption/alignment/lazy-load behavior
- card layout variants with simple consistent structure
- icon naming conventions for available packs

## Common mistakes to avoid

- large unoptimized images without context
- relying on icons not registered in the site setup
- using complex grid syntax when plain sections would communicate better
- mixing visual patterns that reduce readability on small screens

## Interactions / caveats

- palette/theme choices can affect icon and image visibility.
- if task requires custom styling tweaks, escalate to customization boundaries.

## Deeper docs

- [Grids](https://zensical.org/docs/authoring/grids/)
- [Images](https://zensical.org/docs/authoring/images/)
- [Icons and emojis](https://zensical.org/docs/authoring/icons-emojis/)
- [Logo and icons: additional icons](https://zensical.org/docs/setup/logo-and-icons/#additional-icons)
