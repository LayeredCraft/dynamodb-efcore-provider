--- BEGIN AGENTS.MD CONTENT ---

## Issue Tracking

This project uses **bd (beads)** for issue tracking.
Run `bd prime` for workflow context, or install hooks (`bd hooks install`) for auto-injection.

**Quick reference:**

- `bd ready` - Find unblocked work
- `bd create "Title" --type task --priority 2` - Create issue
- `bd close <id>` - Complete work
- `bd dolt push` - Push beads to remote

For full workflow details: `bd prime`
--- END AGENTS.MD CONTENT ---

For GitHub Copilot users:
Add the same content to .github/copilot-instructions.md

How it works:
• bd prime provides dynamic workflow context (~80 lines)
• bd hooks install auto-injects bd prime at session start
• AGENTS.md only needs this minimal pointer, not full instructions

This keeps AGENTS.md lean while bd prime provides up-to-date workflow details.

