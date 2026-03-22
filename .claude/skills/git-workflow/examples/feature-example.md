# Example: Feature PR

## Scenario

Current work adds automatic PR template loading and branch creation when running from `main`.

## Expected branch

`feat/core-automate-pr-workflow`

## Expected commit

`feat(core): automate PR workflow from main`

## Expected PR title

`feat(core): automate PR workflow from main`

## Example PR body

# 🚀 Pull Request

## 📋 Summary

> Adds automation for branch preparation and PR generation when opening a pull request from the current repository state. This removes manual branch setup when starting from `main` and keeps PR metadata generation consistent with inferred change intent.

---

## 📝 Changes

- Branch preparation flow
  - Detects `main` and detached `HEAD` before PR creation
  - Creates and switches to a generated branch only when needed
- Metadata and PR drafting
  - Infers PR metadata (`type`, optional `scope`, short description)
  - Loads the local PR template and builds the PR body from current repository state
- Workflow consistency
  - Reuses shared scope and inclusion policy logic so commit and PR behavior stay aligned

---

## 🧪 Validation

- Build/test status: Not explicitly verified by the agent
- Manual verification performed: Reviewed repository status, branch behavior, and generated PR content paths
- Edge cases checked: Existing feature branch path and detached `HEAD` path

---

## 💬 Notes for Reviewers

> Please focus on branch creation guardrails and metadata inference fallbacks, especially when repository state is ambiguous.
