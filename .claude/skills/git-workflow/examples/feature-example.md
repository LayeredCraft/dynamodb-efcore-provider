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

> Adds automation for branch creation, committing, and PR generation when preparing a pull request from the current repository state.

---

## 📝 Changes

- Added logic to detect `main` and detached `HEAD`
- Added branch name generation based on inferred PR metadata
- Added PR template loading from the local skill templates folder

---

## 🧪 Validation

- Build/test status: Not explicitly verified by the agent
- Manual verification performed: Reviewed repository status and diff
- Deployment impact: None
- Breaking changes: None known

---

## ✅ Checklist

- [ ] My changes build cleanly
- [ ] I've added or updated relevant tests
- [ ] I've added or updated documentation or README content
- [ ] I've followed the coding style for this project
- [ ] I've tested the changes locally when applicable

---

## 🧩 Related Issues or PRs

Closes #...

---

## 📦 Release Notes

- Added automation for branch creation and PR preparation

---

## 💬 Notes for Reviewers

> Please review the inferred branch naming and commit message behavior for edge cases.
