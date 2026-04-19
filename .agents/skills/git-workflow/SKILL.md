---
name: git-workflow
description: >-
  Git workflow automation for committing, branching, and opening pull requests.
  Use this whenever the user asks to commit their work, create a branch, or
  create/open/draft a PR.
---

# Git Workflow

Use this skill whenever the user asks to:

- commit these changes
- create a commit
- save my work
- stage and commit
- commit current work
- create a branch
- start a feature branch
- make a branch for this work
- start working on a change
- create a PR
- open a PR
- draft a PR
- prepare a pull request
- commit and open a PR
- create a branch and PR
- submit the current work

______________________________________________________________________

## Shared references

Before executing any workflow, load all four shared references:

- [Scope Detection](shared/scope-detection.md)
- [File Inclusion Policy](shared/file-inclusion-policy.md)
- [Safety Rules](shared/safety-rules.md)
- [Conventional Types](shared/conventional-types.md)

______________________________________________________________________

## Intent routing

Based on the user's request, load exactly one workflow doc:

| User intent                                     | Load                             |
|-------------------------------------------------|----------------------------------|
| Commit work, save changes, stage and commit     | [docs/commit.md](docs/commit.md) |
| Create a branch, start a feature branch         | [docs/branch.md](docs/branch.md) |
| Create/open/draft a PR, submit the current work | [docs/pr.md](docs/pr.md)         |

When intent is ambiguous, prefer the more complete workflow. If the user says "commit and open a PR", load `docs/pr.md` — it covers the full lifecycle including commit and branch.
