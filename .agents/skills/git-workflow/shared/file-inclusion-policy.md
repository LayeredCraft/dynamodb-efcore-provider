# File Inclusion Policy

Treat the working tree as the user's intent.

## Default behavior

Include **all user-modified files** in the commit:

- modified files
- staged files
- unstaged files
- untracked files
- deleted files

If the user changed a file, assume the change is intentional.

Do **not** exclude files merely because they appear unrelated to the inferred task.

## Allowed automatic exclusions

Files may only be excluded if they are clearly not intended for source control:

- `.DS_Store`
- editor swap files
- temporary files
- build output folders
- cache folders
- machine-local configuration files
- secret files that should never be committed

Example patterns:

```
.DS_Store
*.swp
*.tmp
bin/
obj/
node_modules/
.vscode/*
```

## Ambiguity rule

If there is **any uncertainty** about whether a file should be committed:

**Include the file.**

Never silently omit a user-modified file.

## Transparency rule

If any files are excluded automatically, explicitly report:

- which files were excluded
- the reason they were excluded
