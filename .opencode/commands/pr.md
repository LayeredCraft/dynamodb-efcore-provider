---
description: Open a pull request for the current branch
---

Open a GitHub pull request for the current Git branch.

Additional instructions from the user (optional):
$ARGUMENTS

Use GitHub CLI (`gh`). Be autonomous and do not ask questions unless you are blocked.

Context (use this to drive decisions):
- Branch/status: !`git status -sb`
- Recent commits: !`git log --oneline -20`

Pre-flight checks (do these in parallel where possible):
1) Determine base branch:
   - Prefer `main` if it exists, else `master`.
2) Fail fast if the current branch is the base branch.
3) Ensure there are commits to propose (compared to base branch).
4) Check if a PR already exists for this branch:
   - `gh pr list --head <branch> --json number,url,title,state`
   - If one exists, return the PR URL and stop.
5) Ensure the branch is pushed:
   - If no upstream is configured, push with `git push -u origin <branch>`.

PR title selection (strict conventional commits):
- Prefer using the most recent commit subject if it matches the format.
- Otherwise, infer from `git diff <base>...HEAD` and propose a title.
- Only ask the user if the change spans multiple scopes and you cannot pick one.

Title format (validate before creating the PR):
- `<type>(<scope>): <description>` or `<type>: <description>`
- `type` must be lowercase and one of: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `ci`
- `scope` is optional; if provided, it must be one of:
  - `dynamodb`, `query`, `storage`, `extensions`, `infrastructure`, `tests`, `docs`, `solution`, `example.simple`, `build`, `ci`, `deps`
- `description` is required and uses imperative mood ("add", "fix", "refactor").

PR body:
- Use the template format below (this is the expected PR body layout).
- Fill in the sections based on the diff; remove any section that truly doesn't apply.
- Include how it was tested (or explicitly say "Not run" with a reason).

Create the PR (use heredoc, no temp files). Use `--draft` if the user indicates WIP/draft in $ARGUMENTS.

Use a command like:

```bash
gh pr create \
  --title "<validated-title>" \
  --base "<base-branch>" \
  --body "$(cat <<'EOF'
# 🚀 Pull Request

## 📋 Summary

[Your analysis of what changed and why]

---

## ✅ Checklist

- [x] My changes build cleanly
- [x] I've added/updated relevant tests
- [ ] I've added/updated documentation or README
- [x] I've followed the coding style for this project
- [x] I've tested the changes locally (if applicable)

---

## 🧪 Related Issues or PRs

[If applicable: Closes #...]

---

## 💬 Notes for Reviewers

[Any specific areas to look at, or remove this section]
EOF
)"
```

Additional template sections (only add for complex PRs; place after Summary and before Checklist):

```markdown
## 🔄 Changes

- Change 1
- Change 2
- Change 3

---

## 🧑‍💻 Testing

Describe how the changes were tested, specific test cases run, or manual testing performed.
```

After creation, print the PR URL.
