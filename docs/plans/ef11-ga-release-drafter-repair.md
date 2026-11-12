# EF11 GA — Release Drafter repair (PR A) — implementation plan

Status: plan, awaiting maintainer approval. Implementation is a separate step.

Beads: epic `dynamodb-efcore-provider-ar2` · this PR `dynamodb-efcore-provider-ar2.1` · mandatory
post-GA follow-up `dynamodb-efcore-provider-ar2.5` (PR D) · release gate `dynamodb-efcore-provider-ar2.4`.

## 1. Problem statement and confirmed root causes

The EF11 Release Drafter draft is **`v11.0.1 (EF Core 11)` with "No changes"**, unchanged since
2026-07-10. The first stable EF11 release must be `v11.0.0` and must carry the complete EF11
development history.

Confirmed (CI log of run 41867110034, `draft (release-drafter-ef11.yml)` job, and the pinned
release-drafter source):

1. The only EF11 release, `v11.0.0-preview`, is a **GitHub prerelease** (the intended baseline seed).
2. release-drafter v7.7.0 (`34d80673e067bdc0c24568d3af899c216adcfaa9`) drops prereleases from
   baseline selection unless `include-pre-releases` is set
   (`src/actions/drafter/lib/find-previous-releases/find-previous-releases.ts`). Log:
   `No published release found` / `A previous (published) release is required to find changes`.
3. With no baseline it collects **no PRs** and resolves the version from `0.0.0` + `patch` = `0.0.1`;
   `version-template: '11.$MINOR.$PATCH'` renders **`11.0.1`** (log: `RESOLVED_VERSION_MAJOR: 0 … PATCH: 1`).
4. EF10 and EF11 drafts read the same `main` history, so a change specific to one EF line would land
   in both changelogs. There is currently no way to exclude it.

`publish-preview.yaml` resolves preview versions from the same configs in dry-run mode, which is why
EF11 previews were published as `11.0.1-preview.N`.

## 2. Current behaviour (verified against the live repository, origin/main `9961a80`)

| Config | Baseline | Draft updated | PRs | Resolved |
|---|---|---|---|---|
| `release-drafter-ef10.yml` | `v10.1.0` | `v10.2.0` | 39 | `v10.2.0` |
| `release-drafter-ef11.yml` | none | `v11.0.1` | 0 | `v11.0.1` |

## 3. Desired behaviour

| Scenario | Baseline | Resolved | Notes |
|---|---|---|---|
| First v11 GA (now) | `v11.0.0-preview` | **`v11.0.0`** | all eligible PRs in `v11.0.0-preview..main` (55 at planning time) |
| v11 patch after GA | `v11.0.0` | `v11.0.1` | fixes only |
| v11 minor after GA | `v11.0.0` | `v11.1.0` | any `type: feat` / `breaking-change` |
| EF10 maintenance after v11 GA | latest `v10.x` (e.g. `v10.2.0`) | `v10.2.1` / `v10.3.0` | independent of EF11 |
| PR labelled `ef11-only` | — | — | excluded from EF10 notes |
| PR labelled `ef10-only` | — | — | excluded from EF11 notes |
| After PR D (post-GA) | `v11.0.0` | label-driven | any later GitHub prerelease is ignored |

How the pinned release-drafter decides (source-verified):

- **Baseline:** keep releases whose tag, coerced to `X.Y.Z` (prerelease suffix dropped), satisfies
  `filter-by-range`; drop drafts; drop prereleases unless `include-pre-releases`; pick the **highest
  semver** (`compare-versions`), not the newest by date. With several prereleases in range, the
  highest one wins, which is why PR D is mandatory.
- **Version:** `semver.inc(baseline, increment)`. For a prerelease baseline `11.0.0-preview`, node-semver
  7.8.5 returns `11.0.0` for `major`, `minor` **and** `patch`, so the first GA resolves to exactly
  `11.0.0` regardless of labels. For `11.0.0` the normal bump applies.
- **Changelog:** PRs associated with commits in `refs/tags/<baseline>...main`; a PR must carry one
  of `include-labels` and none of `exclude-labels` (exclusion wins).
- **Draft to update:** the first non-prerelease draft whose tag is in range. The existing `v11.0.1`
  draft is updated in place (re-tagged to `v11.0.0`); `v10.2.0` is untouched by the EF11 config.

## 4. Files to change

| File | Change |
|---|---|
| `.github/release-drafter-ef11.yml` | add temporary `include-pre-releases: true`; add `ef10-only` to `exclude-labels` |
| `.github/release-drafter-ef10.yml` | add `ef11-only` to `exclude-labels` |
| `.github/release-drafter.yml` | delete (unused, see below) |
| `docs/plans/ef11-ga-release-drafter-repair.md` | this plan (commit with the PR) |

GitHub repository labels (created once, not files): `ef10-only`, `ef11-only`.

`.github/release-drafter.yml` is unused: every action invocation passes an explicit `config-name`
(`release-drafter.yaml` matrix and autolabeler use `release-drafter-ef10.yml`/`-ef11.yml`;
`publish-preview.yaml` passes `drafterConfig`), and the pinned `LayeredCraft/devops-templates`
workflows used here (`pr-build.yaml`, `nuget-push`) do not invoke release-drafter. Its only mentions
are in the superseded `docs/multi-version-ef-strategy.md` checklist, which already lists removing it.

## 5. Proposed YAML changes

`.github/release-drafter-ef11.yml` (only these two hunks):

```diff
 tag-template: 'v$RESOLVED_VERSION'
 filter-by-range: '>=11.0.0 <12.0.0'
+# TEMPORARY (remove after v11.0.0 is published): the v11.0.0-preview seed is a GitHub
+# prerelease and is the only EF11 baseline until v11.0.0 ships.
+include-pre-releases: true
 version-template: '11.$MINOR.$PATCH'
```

```diff
 exclude-labels:
   - 'skip-changelog'
   - 'internal'
+  - 'ef10-only'
```

`.github/release-drafter-ef10.yml`:

```diff
 exclude-labels:
   - 'skip-changelog'
   - 'internal'
+  - 'ef11-only'
```

Do **not** change `filter-by-range`, `version-template`, `tag-template`, `name-template`,
`commitish`, `version-resolver`, `autolabeler`, `categories`, `include-labels`, or migrate the
deprecated v7 field shapes (the deprecation warnings are pre-existing and out of scope).

## 6. Ordered implementation steps

1. Branch from up-to-date `main` (never commit to `main`), e.g. `ci/ef11-release-drafter-baseline`.
2. `bd update dynamodb-efcore-provider-ar2.1 --claim`.
3. Apply the §5 hunks exactly; `git rm .github/release-drafter.yml`.
4. Run the §7 local validation; all checks must pass.
5. Create the labels (idempotent, no other GitHub writes):
   ```bash
   gh label create ef10-only --color 1D76DB --description "Change applies only to the EF Core 10 line; excluded from EF11 release notes" || true
   gh label create ef11-only --color 5319E7 --description "Change applies only to the EF Core 11 line; excluded from EF10 release notes" || true
   ```
6. Commit (Conventional Commit, bead ID in body), e.g.
   `ci(release): seed EF11 release drafter baseline and add EF-line exclusions`.
7. Push the branch and open the PR using `.github/pull_request_template.md`. Title must pass
   `pr-title-check` (the `ci:` prefix makes the autolabeler add `type: ci`). Do **not** add
   `ef10-only`/`ef11-only` to this PR: it affects both lines. In the template, docs/skill checks:
   no user-facing behaviour change; the provider skill is unaffected.
8. Note the bead in the PR description; stop at review. Do not merge, dispatch workflows, or edit releases.

## 7. Local validation (run before opening the PR)

All commands are read-only against GitHub. Write the Appendix A script to a temp directory
(`$TMPDIR/rd_verify.py`); it is a validation aid and must **not** be committed.

```bash
V="uv run --quiet --no-project --with pyyaml --with jsonschema"
# 7.1 YAML parses and matches the pinned release-drafter schema
curl -sfL https://raw.githubusercontent.com/release-drafter/release-drafter/34d80673e067bdc0c24568d3af899c216adcfaa9/drafter/schema.json -o "$TMPDIR/rd-schema.json"
$V python - "$TMPDIR/rd-schema.json" .github/release-drafter-ef10.yml .github/release-drafter-ef11.yml <<'PY'
import json, sys, yaml, jsonschema
s = json.load(open(sys.argv[1]))
for f in sys.argv[2:]:
    errs = list(jsonschema.validators.validator_for(s)(s).iter_errors(yaml.safe_load(open(f))))
    print(f, "OK" if not errs else errs); sys.exit(1) if errs else None
PY
# 7.2 Required keys/values (the schema allows unknown keys, so a typo like include-pre-release would pass 7.1)
$V python "$TMPDIR/rd_verify.py" config .github/release-drafter-ef10.yml .github/release-drafter-ef11.yml
# 7.3 Future-release behaviour (offline model of the pinned release-drafter logic)
$V python "$TMPDIR/rd_verify.py" scenarios .github/release-drafter-ef10.yml .github/release-drafter-ef11.yml
# 7.4 Live: real releases + real PR labels
git fetch origin
$V python "$TMPDIR/rd_verify.py" live .github/release-drafter-ef11.yml
$V python "$TMPDIR/rd_verify.py" live .github/release-drafter-ef10.yml
# 7.5 Unused file really unused
grep -rnE "release-drafter\.ya?ml\b" .github/ | grep -vE 'release-drafter-ef1[01]\.yml' ; test $? -eq 1
```

Expected results:

- 7.1: both files `OK`. 7.2: all `PASS` (on unmodified `main` exactly three FAIL).
- 7.3: all `PASS`; the `HAZARD` line intentionally shows what happens if PR D is skipped.
- 7.4 EF11: `baseline=v11.0.0-preview draft-to-update=v11.0.1 … resolved=v11.0.0`, eligible set
  non-empty and equal to every merged PR in range carrying an include label and no exclude label
  (55 PRs `#278`–`#357` at planning time; recompute, do not hard-code). EF10:
  `baseline=v10.1.0 draft-to-update=v10.2.0 … resolved=v10.2.0` with the same PR set `main` produces
  today (39 at planning time) unless a PR in range carries `ef11-only`.
- 7.5: no output.

Not required for PR A: .NET restore/build/test, Docker, integration, NativeAOT.

Optional, maintainer approval required (executes third-party code with a GitHub token): run the
pinned release-drafter bundle in dry-run with a local config
(`config-name: file:<absolute path>`, `dry-run: true`). Not needed when 7.1–7.4 pass.

## 8. Changelog completeness verification

- **Before merge (local):** 7.4 lists the expected eligible set and the excluded PRs with their labels.
  Review the excluded list manually; it should contain only `skip-changelog`, `internal`, `ef10-only`
  (EF11) or `ef11-only` (EF10) PRs, or PRs without any `type:` label.
- **After merge (GitHub, by reviewer or implementer when authorised):** the push-triggered
  `Release Drafter` run must show, in `draft (release-drafter-ef11.yml)`:
  `Last release: tag_name: v11.0.0-preview`, `Found N merged pull requests`, `RESOLVED_VERSION: 11.0.0`,
  `Updating existing release...`. Then:
  ```bash
  $V python "$TMPDIR/rd_verify.py" compare .github/release-drafter-ef11.yml v11.0.0   # exit 0 = exact match
  $V python "$TMPDIR/rd_verify.py" compare .github/release-drafter-ef10.yml v10.2.0   # exit 0 = exact match
  ```
  `compare` was validated during planning against the existing `v10.2.0` draft (39/39 exact match).
- Model limitation: PRs are detected from squash-merge subjects `(#N)` in `git log`; release-drafter
  uses GitHub commit/PR association. `compare` is the authoritative check after merge.

## 9. EF10 regression considerations

- The EF10 config change is additive (`ef11-only` exclusion); baseline, range, version template and
  categories are unchanged. 7.4 must reproduce today's EF10 result.
- `include-pre-releases` is EF11-only. There are no EF10 prereleases in range, and adding it to EF10
  is out of scope.
- The autolabeler job reads `release-drafter-ef10.yml`; its `autolabeler` block is unchanged.
- `publish-preview.yaml` is unchanged. After merge, its EF11 leg resolves `11.0.0-preview.<run>`
  instead of `11.0.1-preview.<run>` (§10).

## 10. Risks and mitigations

| Risk | Mitigation |
|---|---|
| PR D forgotten; a later GitHub prerelease in 11.x becomes the baseline | PR D is bead `ar2.5`, blocked only by the v11.0.0 publish (`ar2.4`); scenario `HAZARD` documents the failure mode |
| A one-line PR missing its `ef10-only`/`ef11-only` label leaks into the other line's notes | Labels are applied at PR time; the draft is regenerated on every push, so a late label fixes it before publishing |
| Model diverges from real release-drafter | Model reproduces today's CI results exactly (EF10 39 PRs → v10.2.0, EF11 none → v11.0.1); `compare` gives an authoritative post-merge check |
| Between merge and GA, EF11 previews are `11.0.0-preview.N`, sorting below the already-listed `11.0.1-preview.135–144` | Accepted (previews stay listed); after v11.0.0, previews become `11.0.1-preview.N`/`11.1.0-preview.N` with higher run numbers and sort correctly |
| Testing on the branch rewrites live drafts | Do not `workflow_dispatch` Release Drafter on the branch; drafts only change on merge to `main` |
| v11.0.0 notes repeat PRs already in v10.1.0 (#278–#297) | Intended: the first EF11 stable documents the full EF11 history since the seed |

## 11. PR scope and explicit exclusions

In scope: the two config hunks, deleting `.github/release-drafter.yml`, the two labels, this plan.

Excluded: release publication workflows (`publish-release.yaml`), `publish-preview.yaml`,
`release-drafter.yaml`, package versions, provider code, tests, specification tests, NativeAOT
infrastructure, unrelated docs, release-drafter version bumps or deprecated-field migration, editing
or deleting releases/drafts/tags, unlisting packages.

## 12. Acceptance criteria

1. Diff limited to §4. 2. §7.1–7.5 pass with the expected results. 3. EF11 live model: baseline
`v11.0.0-preview`, resolved exactly `v11.0.0`, non-empty eligible set, complete per §8.
4. `ef10-only`/`skip-changelog`/`internal` excluded from EF11; `ef11-only`/`skip-changelog`/`internal`
excluded from EF10. 5. EF10 baseline and resolved version unchanged from `main`. 6. No manual edit of
release tags/versions needed. 7. After merge: CI log and `compare` checks in §8 pass (verified at the
review/merge gate, not by publishing anything).

## 13. Post-GA cleanup (mandatory, bead `dynamodb-efcore-provider-ar2.5`)

After v11.0.0 is published: remove the `include-pre-releases` line and its comment from
`.github/release-drafter-ef11.yml`, nothing else. Validate with `rd_verify.py config` (expect only the
`include-pre-releases is exactly true` check to FAIL; that check is specific to PR A), `scenarios`
(`post-GA cleanup` lines PASS) and `live` (EF11 baseline `v11.0.0`).

## Appendix A — `rd_verify.py`

```python
"""Model of release-drafter v7.7.0 baseline/version/changelog selection (find-previous-releases.ts,
sort-releases.ts, get-version-info.ts, node-semver 7.8.5 inc). Read-only: uses `git` and `gh api`.

  python rd_verify.py config <ef10.yml> <ef11.yml>             # assert required keys/values
  python rd_verify.py live <config.yml> [head]                 # real releases + PR labels
  python rd_verify.py scenarios <ef10.yml> <ef11.yml>          # offline future-release checks
  python rd_verify.py compare <config.yml> <draft-tag>         # expected PRs vs draft body
"""
import json, re, subprocess, sys
import yaml

def sh(*a): return subprocess.run(a, check=True, capture_output=True, text=True).stdout
def cfg(path):
    c = yaml.safe_load(open(path))
    lo, hi = re.fullmatch(r">=(\S+) <(\S+)", c["filter-by-range"]).groups()
    c["_range"] = (core(lo), core(hi)); return c
def core(t):
    m = re.search(r"(\d+)\.(\d+)\.(\d+)", t); return tuple(map(int, m.groups())) if m else None
def parse(t):
    m = re.fullmatch(r"v?(\d+)\.(\d+)\.(\d+)(?:-([0-9A-Za-z.-]+))?", t)
    return (int(m[1]), int(m[2]), int(m[3]), m[4].split(".") if m[4] else []) if m else None
def order(t):
    M, m, p, pre = parse(t)
    return (M, m, p, 0 if pre else 1, [(0, int(x)) if x.isdigit() else (1, x) for x in pre])

def select(releases, c):
    lo, hi = c["_range"]
    rs = [r for r in releases if (v := core(r["tag_name"])) and lo <= v < hi]
    pub = [r for r in rs if not r["draft"] and (c.get("include-pre-releases") or not r["prerelease"])]
    drafts = [r for r in rs if r["draft"] and not r["prerelease"]]
    return (max(pub, key=lambda r: order(r["tag_name"])) if pub else None), (drafts[0] if drafts else None)

def inc(v, kind):  # node-semver inc() for major/minor/patch
    M, m, p, pre = v
    if kind == "major": return (M + 1 if (m or p or not pre) else M, 0, 0)
    if kind == "minor": return (M, m + 1 if (p or not pre) else m, 0)
    return (M, m, p + 1 if not pre else p)

def increment(c, prs):
    labels = {l for pr in prs for l in pr["labels"]}
    if labels & set(c["version-resolver"]["minor"]["labels"]): return "minor"
    if labels & set(c["version-resolver"]["patch"]["labels"]): return "patch"
    return c["version-resolver"].get("default", "patch")

def render(c, v):
    return c["version-template"].replace("$MAJOR", str(v[0])).replace("$MINOR", str(v[1])).replace("$PATCH", str(v[2]))

def eligible(c, prs):
    inc_l, exc_l = set(c.get("include-labels", [])), set(c.get("exclude-labels", []))
    return [p for p in prs if set(p["labels"]) & inc_l and not set(p["labels"]) & exc_l]

def resolve(c, releases, prs):
    last, draft = select(releases, c)
    base = parse(last["tag_name"]) if last else (0, 0, 0, [])
    elig = eligible(c, prs) if last else []
    kind = increment(c, elig) if elig else c["version-resolver"].get("default", "patch")
    return last, draft, elig, kind, render(c, inc(base, kind))

def repo():
    return json.loads(sh("gh", "repo", "view", "--json", "nameWithOwner"))["nameWithOwner"]

def live_prs(base_tag, head):
    nums = []
    for s in sh("git", "log", "--format=%s", f"{base_tag}..{head}").splitlines():
        m = re.search(r"\(#(\d+)\)$", s)
        if m and int(m[1]) not in nums: nums.append(int(m[1]))
    out = []
    for n in nums:
        pr = json.loads(sh("gh", "api", f"repos/{repo()}/pulls/{n}"))
        if pr.get("merged_at") and pr["base"]["ref"] == "main":
            out.append({"number": n, "title": pr["title"], "labels": [l["name"] for l in pr["labels"]]})
    return out

def cmd_live(path, head="origin/main"):
    c = cfg(path); releases = json.loads(sh("gh", "api", "--paginate", f"repos/{repo()}/releases"))
    last, _ = select(releases, c)
    prs = live_prs(last["tag_name"], head) if last else []
    last, draft, elig, kind, ver = resolve(c, releases, prs)
    print(f"config={path}\n baseline={last and last['tag_name']} draft-to-update={draft and draft['tag_name']}")
    print(f" merged PRs in range={len(prs)} eligible={len(elig)} increment={kind} resolved=v{ver}")
    excluded = [p for p in prs if p not in elig]
    print(" excluded:", ", ".join(f"#{p['number']}{p['labels']}" for p in excluded) or "none")
    print(" eligible:", " ".join(f"#{p['number']}" for p in elig))
    return ver, elig

def cmd_compare(path, tag):
    _, elig = cmd_live(path)
    body = json.loads(sh("gh", "api", "--paginate", f"repos/{repo()}/releases"))
    rel = next(r for r in body if r["tag_name"] == tag)
    got = {int(n) for n in re.findall(r"\(#(\d+)\)", rel["body"] or "")}
    want = {p["number"] for p in elig}
    print(f" draft {tag}: {len(got)} PRs; expected {len(want)}")
    print(" missing from draft:", sorted(want - got) or "none"); print(" unexpected in draft:", sorted(got - want) or "none")
    sys.exit(0 if got == want else 1)

def cmd_scenarios(p10, p11):
    c10, c11 = cfg(p10), cfg(p11)
    c11_post = dict(c11); c11_post.pop("include-pre-releases", None)
    R = lambda t, pre=False, d=False: {"tag_name": t, "prerelease": pre, "draft": d}
    today = [R("v10.0.0"), R("v10.1.0"), R("v11.0.0-preview", pre=True), R("v10.2.0", d=True), R("v11.0.1", d=True)]
    fix = [{"number": 1, "labels": ["type: fix"]}]; feat = [{"number": 2, "labels": ["type: feat"]}]
    ef11only = [{"number": 3, "labels": ["type: fix", "ef11-only"]}]; ef10only = [{"number": 4, "labels": ["type: feat", "ef10-only"]}]
    after_ga = [r for r in today if not r["draft"]] + [R("v10.2.0"), R("v11.0.0")]
    hazard = after_ga + [R("v11.1.0-rc.1", pre=True)]
    cases = [
        ("first v11 GA (feat present)", c11, today, feat, "v11.0.0-preview", "11.0.0"),
        ("first v11 GA (fixes only)", c11, today, fix, "v11.0.0-preview", "11.0.0"),
        ("v11 patch after GA", c11, after_ga, fix, "v11.0.0", "11.0.1"),
        ("v11 minor after GA", c11, after_ga, feat, "v11.0.0", "11.1.0"),
        ("v11 ignores ef10-only PR", c11, after_ga, ef10only + fix, "v11.0.0", "11.0.1"),
        ("EF10 today", c10, today, feat, "v10.1.0", "10.2.0"),
        ("EF10 maintenance after v11 GA", c10, after_ga, fix, "v10.2.0", "10.2.1"),
        ("EF10 ignores ef11-only PR", c10, after_ga, ef11only, "v10.2.0", "10.2.1"),
        ("post-GA cleanup: no include-pre-releases", c11_post, after_ga, fix, "v11.0.0", "11.0.1"),
        ("HAZARD if cleanup skipped + GH prerelease", c11, hazard, fix, "v11.1.0-rc.1", "11.1.0"),
        ("post-GA cleanup ignores GH prerelease", c11_post, hazard, fix, "v11.0.0", "11.0.1"),
    ]
    ok = True
    for name, c, rels, prs, want_base, want_ver in cases:
        last, _, elig, kind, ver = resolve(c, rels, prs)
        good = (last and last["tag_name"]) == want_base and ver == want_ver
        ok &= good or name.startswith("HAZARD")
        print(f" {'PASS' if good else 'FAIL'}  {name}: baseline={last and last['tag_name']} eligible={[p['number'] for p in elig]} -> v{ver}")
    sys.exit(0 if ok else 1)

def cmd_config(p10, p11):
    c10, c11 = yaml.safe_load(open(p10)), yaml.safe_load(open(p11))
    checks = [
        (c11.get("include-pre-releases") is True, "ef11 include-pre-releases is exactly true"),
        (c11.get("filter-by-range") == ">=11.0.0 <12.0.0", "ef11 filter-by-range unchanged"),
        (c11.get("version-template") == "11.$MINOR.$PATCH", "ef11 version-template unchanged"),
        ("ef10-only" in c11.get("exclude-labels", []), "ef11 excludes ef10-only"),
        ("ef11-only" not in c11.get("exclude-labels", []), "ef11 does not exclude ef11-only"),
        ("include-pre-releases" not in c10, "ef10 has no include-pre-releases"),
        (c10.get("filter-by-range") == ">=10.0.0 <11.0.0", "ef10 filter-by-range unchanged"),
        (c10.get("version-template") == "10.$MINOR.$PATCH", "ef10 version-template unchanged"),
        ("ef11-only" in c10.get("exclude-labels", []), "ef10 excludes ef11-only"),
        ("ef10-only" not in c10.get("exclude-labels", []), "ef10 does not exclude ef10-only"),
    ]
    for good, msg in checks: print(f" {'PASS' if good else 'FAIL'}  {msg}")
    sys.exit(0 if all(g for g, _ in checks) else 1)

if __name__ == "__main__":
    cmd, *rest = sys.argv[1:]
    {"config": cmd_config, "live": cmd_live, "scenarios": cmd_scenarios, "compare": cmd_compare}[cmd](*rest)
```
