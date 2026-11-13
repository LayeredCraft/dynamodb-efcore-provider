# EF11 post-GA — Release Drafter cleanup (PR D) — implementation plan

Status: plan, awaiting maintainer approval. Implementation is a separate step.

Beads: epic `dynamodb-efcore-provider-ar2` · this PR `dynamodb-efcore-provider-ar2.5` · prerequisite
`dynamodb-efcore-provider-ar2.4` (closed) · separate, not in scope: `dynamodb-efcore-provider-ar2.7`
(unlist mis-versioned `11.0.1-preview.*` packages).

Predecessor: `docs/plans/ef11-ga-release-drafter-repair.md` (PR A / #360). Its Appendix A
`rd_verify.py` is reused here.

## 1. Verified current state (2026-11-13, origin/main `1b2fc39`)

| Release | GitHub state | Target |
|---|---|---|
| `v11.0.0` (EF Core 11) | published, not prerelease, **Latest** | `1b2fc39` |
| `v10.2.0` (EF Core 10) | published, not prerelease | `1b2fc39` |
| `v11.0.0-preview` | published, **prerelease** | — |
| `v10.1.0`, `v10.0.0`, `v10.0.0-preview`, `v0.0.2-alpha.*` | published, not prerelease | — |

- **No drafts exist.** Both drafts were published; no push to `main` has happened since
  (last Release Drafter / Publish Preview run #147 was for `1b2fc39`, before publication).
- Both stable tags point at `origin/main` HEAD, so `v11.0.0..main` and `v10.2.0..main` are empty today.
- NuGet: `10.2.0` and `11.0.0` stable present; latest previews `10.2.0-preview.147`,
  `11.0.0-preview.147`; mis-versioned `11.0.1-preview.92–144` still present (ar2.7).

Configs today: EF10 and EF11 are identical apart from range/version/name templates, the EF11
`include-pre-releases: true` (plus a 2-line `TEMPORARY` comment), and the exclusions
(`ef11-only` in EF10, `ef10-only` in EF11).

## 2. Why the change is still needed (even though today's baseline is already correct)

Pinned release-drafter (`34d80673…`, v7.7.0) picks the **highest semver** published release in
`filter-by-range`. `11.0.0-preview < 11.0.0`, so with or without the flag today's EF11 baseline is
`v11.0.0`. The flag is a latent hazard: any future GitHub prerelease in range (e.g. `v11.1.0-rc.1`)
would outrank `v11.0.0` and become the baseline, dropping PRs from notes and mis-resolving the
version (scenario `HAZARD` in §5.3). Removing it restores the EF10-equivalent rule: stable releases only.

Consequence for validation: the `live` result is **identical before and after** the change; the
protective effect is proven only by `scenarios`. That is expected, not a failed test.

## 3. Exact change (only file edited)

`.github/release-drafter-ef11.yml` — remove three lines, nothing else:

```diff
 tag-template: 'v$RESOLVED_VERSION'
 filter-by-range: '>=11.0.0 <12.0.0'
-# TEMPORARY (remove after v11.0.0 is published): the v11.0.0-preview seed is a GitHub
-# prerelease and is the only EF11 baseline until v11.0.0 ships.
-include-pre-releases: true
 version-template: '11.$MINOR.$PATCH'
```

Plus this plan file, committed with the PR.

Do **not** change: `.github/release-drafter-ef10.yml`, any other key in the EF11 file (range,
templates, `commitish`, resolver, autolabeler, categories, include/exclude labels),
`.github/workflows/*` (`release-drafter.yaml`, `publish-preview.yaml`, `publish-release.yaml`), the
action pin, deprecated-field shapes, releases, tags, drafts, labels, or NuGet packages.

After the change, the only differences between the two configs must be: `filter-by-range`,
`version-template`, `name-template`, and the one cross-line exclude label.

## 4. Implementation steps

1. `git fetch origin && git switch -c ci/ef11-release-drafter-post-ga origin/main`
   (never commit to `main`; do not branch from `docs/ef11-ga-documentation`).
2. `bd --sandbox update dynamodb-efcore-provider-ar2.5 --claim`
3. Apply §3. Copy this plan into the branch if it is not already on `main`.
4. Run §5; all checks must match the expected results.
5. Commit, e.g. `ci(release): stop EF11 release drafter from using GitHub prereleases`
   with `Bead: dynamodb-efcore-provider-ar2.5` in the body.
6. Push and open the PR with `.github/pull_request_template.md`. Title must pass `pr-title-check`
   (`ci:` → autolabel `type: ci` → patch). Add label **`ef11-only`** (change touches only the EF11
   config; keeps it out of the EF10 notes). Docs/skill checks: not applicable — no user-facing
   behaviour, configuration, or skill guidance changes.
7. Stop at review. Do not merge, dispatch workflows, or edit/publish releases.

## 5. Local validation (read-only against GitHub)

Write Appendix A of `docs/plans/ef11-ga-release-drafter-repair.md` to `$TMPDIR/rd_verify.py`
(do **not** commit it).

```bash
# shell function (zsh does not word-split a "$V" command string)
rdpy() { uv run --quiet --no-project --with pyyaml --with jsonschema python "$@"; }
git fetch origin

# 5.1 Diff scope: exactly 3 deleted lines in one config (+ this plan)
git diff --stat origin/main -- .github
git diff origin/main -- .github | grep -E '^[-+][^-+]'
#   expect only the three '-' lines from §3

# 5.2 Schema (pinned release-drafter)
curl -sfL https://raw.githubusercontent.com/release-drafter/release-drafter/34d80673e067bdc0c24568d3af899c216adcfaa9/drafter/schema.json -o "$TMPDIR/rd-schema.json"
rdpy - "$TMPDIR/rd-schema.json" .github/release-drafter-ef10.yml .github/release-drafter-ef11.yml <<'PY'
import json, sys, yaml, jsonschema
s = json.load(open(sys.argv[1]))
for f in sys.argv[2:]:
    errs = list(jsonschema.validators.validator_for(s)(s).iter_errors(yaml.safe_load(open(f))))
    print(f, "OK" if not errs else errs); sys.exit(1) if errs else None
PY

# 5.3 Post-GA config invariants (replaces rd_verify `config`, whose first check is PR-A-specific)
rdpy - .github/release-drafter-ef10.yml .github/release-drafter-ef11.yml <<'PY'
import sys, yaml
c10, c11 = (yaml.safe_load(open(p)) for p in sys.argv[1:])
diff = {k for k in c10.keys() | c11.keys() if c10.get(k) != c11.get(k)}
checks = [
  ("include-pre-releases" not in c11, "ef11 has no include-pre-releases"),
  ("include-pre-releases" not in c10, "ef10 has no include-pre-releases"),
  (c11["filter-by-range"] == ">=11.0.0 <12.0.0" and c11["version-template"] == "11.$MINOR.$PATCH", "ef11 range/version unchanged"),
  (c10["filter-by-range"] == ">=10.0.0 <11.0.0" and c10["version-template"] == "10.$MINOR.$PATCH", "ef10 range/version unchanged"),
  ("ef10-only" in c11["exclude-labels"] and "ef11-only" not in c11["exclude-labels"], "ef11 exclusions"),
  ("ef11-only" in c10["exclude-labels"] and "ef10-only" not in c10["exclude-labels"], "ef10 exclusions"),
  (diff == {"filter-by-range", "version-template", "name-template", "exclude-labels"}, f"configs differ only in expected keys: {sorted(diff)}"),
]
for ok, m in checks: print(" PASS" if ok else " FAIL", m)
sys.exit(0 if all(ok for ok, _ in checks) else 1)
PY

# 5.4 Future-release model: new config, then the pre-change config to show the hazard it removes
rdpy "$TMPDIR/rd_verify.py" scenarios .github/release-drafter-ef10.yml .github/release-drafter-ef11.yml
git show origin/main:.github/release-drafter-ef11.yml > "$TMPDIR/ef11-before.yml"
rdpy "$TMPDIR/rd_verify.py" scenarios .github/release-drafter-ef10.yml "$TMPDIR/ef11-before.yml" | grep HAZARD

# 5.5 Live (real releases + labels), against main and against the branch head
rdpy "$TMPDIR/rd_verify.py" live .github/release-drafter-ef11.yml
rdpy "$TMPDIR/rd_verify.py" live .github/release-drafter-ef10.yml
rdpy "$TMPDIR/rd_verify.py" live .github/release-drafter-ef11.yml HEAD
```

Expected:

- 5.1: only the three removed lines. 5.2: both `OK`. 5.3: all `PASS`.
- 5.4 (new config): script exits **1 by design** with exactly three FAIL lines —
  `first v11 GA (feat present)`, `first v11 GA (fixes only)` (pre-GA scenarios that need the removed
  flag) and `HAZARD …` (the hazard no longer reproduces). Every other line PASS, notably:
  `v11 patch after GA` → `baseline=v11.0.0 … -> v11.0.1`; `v11 minor after GA` → `v11.1.0`;
  `EF10 maintenance after v11 GA` → `baseline=v10.2.0 … -> v10.2.1`; `EF10/v11 ignores ef1x-only PR`;
  `post-GA cleanup ignores GH prerelease` → `baseline=v11.0.0 … -> v11.0.1`.
  Pre-change config: `PASS  HAZARD …: baseline=v11.1.0-rc.1 … -> v11.1.0` (the removed risk).
- 5.5 EF11: `baseline=v11.0.0 draft-to-update=None merged PRs in range=0 … resolved=v11.0.1`.
  EF10: `baseline=v10.2.0 draft-to-update=None merged PRs in range=0 … resolved=v10.2.1`.
  If other PRs merge first, recompute: resolved is `11.0.1`/`10.2.1` for patch labels, `11.1.0`/`10.3.0`
  if any eligible PR has `type: feat` or `breaking-change`; baseline must stay `v11.0.0` / `v10.2.0`.

Not required: .NET restore/build/test, Docker, integration, NativeAOT, `task docs:build`
(no `docs/` content change besides this plan, which is outside the published nav).

## 6. Post-merge validation (by reviewer/maintainer; observe only)

The merge is the first push to `main` since GA, so both drafts are **created fresh** (nothing to update).

1. `Release Drafter` run for the merge commit:
   - `draft (release-drafter-ef11.yml)`: last release `v11.0.0`, 1 merged PR (#PR-D), `RESOLVED_VERSION: 11.0.1`,
     creating a new draft `v11.0.1 (EF Core 11)`; no `v11.0.0-preview` reference.
   - `draft (release-drafter-ef10.yml)`: last release `v10.2.0`, PR D excluded by `ef11-only`,
     `RESOLVED_VERSION: 10.2.1`, new draft `v10.2.1 (EF Core 10)` with no changes.
2. `gh release list --limit 5` → drafts `v11.0.1 (EF Core 11)` and `v10.2.1 (EF Core 10)`;
   `v11.0.0` still Latest; `v10.2.0`/`v11.0.0`/`v11.0.0-preview` unchanged.
3. `rdpy "$TMPDIR/rd_verify.py" compare .github/release-drafter-ef11.yml v11.0.1` and
   `compare … ef10.yml v10.2.1` both exit 0.
4. `Publish Preview` run (`.github/**` is not path-ignored): `package_version=11.0.1-preview.<N>` and
   `10.2.1-preview.<N>` with `N ≥ 148`. No version collision with the old `11.0.1-preview.92–144`
   (run numbers are monotonic) and `11.0.1-preview.148+` correctly sorts above stable `11.0.0`.

Safety: never `workflow_dispatch` Release Drafter from the PR branch, never publish the new drafts
as part of this work, never edit/delete `v11.0.0-preview`.

## 7. Post-GA edge cases reviewed

| Case | Finding |
|---|---|
| No existing drafts | First post-merge push creates new drafts; expected, no stale draft to retag. |
| Both stable tags on the same commit | Both lines share future `main` history; separation relies on `ef10-only`/`ef11-only` labels. Unchanged risk from PR A. |
| `v11.0.0-preview` (GitHub prerelease) | Ignored after cleanup; and lower than `v11.0.0` anyway. Leave as is. |
| `v10.0.0-preview`, `v0.0.2-alpha.*` are **not** flagged prerelease | `v10.0.0-preview` coerces into the EF10 range but sorts below `v10.2.0`; alphas out of both ranges. No effect. |
| Name/version/tag templates | `version-template` pins major; `semver.inc(11.0.0)` gives `11.0.1`/`11.1.0`, rendered unchanged. No edge case. |
| Future EF11 GitHub prerelease (e.g. `v11.1.0-rc.1`) | Ignored after cleanup (scenario). Note: `publish-preview.yaml` never creates GitHub releases, so this only arises from manual prereleases. |
| **"Latest" badge on future EF10 releases** | Release-drafter default `latest: true` (key exists in the pinned schema). Publishing a future `v10.2.x` draft may move GitHub's Latest from `v11.0.x` to the EF10 release. Not caused or fixed by PR D. **Maintainer decision:** follow-up bead to set `latest: 'false'` (or `legacy`) in the EF10 config, or untick "Set as latest" when publishing EF10. Out of scope here. |
| Mis-versioned `11.0.1-preview.92–144` (ar2.7) | Independent. New `11.0.1-preview.148+` previews coexist and sort higher; unlisting can proceed in either order. Do not touch packages here. |
| Deprecated v7 field-shape warnings | Pre-existing; out of scope. |

## 8. Acceptance criteria

1. Diff is exactly the three removed lines in `.github/release-drafter-ef11.yml` plus this plan.
2. §5.1–5.3 pass; §5.4 output exactly as stated; §5.5 shows EF11 baseline `v11.0.0` → `v11.0.1`
   (label-driven) and EF10 baseline `v10.2.0` → `v10.2.1` (label-driven).
3. EF10 config byte-identical to `origin/main`; both cross-line exclusions intact.
4. No workflow, release, tag, draft, label, or NuGet change.
5. PR carries `type: ci` and `ef11-only`; template docs/skill checks marked N/A with reason.
6. After merge (§6): CI logs show baselines `v11.0.0`/`v10.2.0`, new drafts `v11.0.1`/`v10.2.1`,
   `compare` exits 0 for both, previews `11.0.1-preview.≥148`/`10.2.1-preview.≥148`. Then close ar2.5.

## 9. Boundaries

In scope: §3 edit, this plan, ar2.5 bead status. Out of scope: ar2.7 unlisting, EF10 `latest`
behaviour (§7, maintainer decision), workflows, action version bumps, releases/drafts/tags,
NuGet packages, provider code, tests, docs content.
