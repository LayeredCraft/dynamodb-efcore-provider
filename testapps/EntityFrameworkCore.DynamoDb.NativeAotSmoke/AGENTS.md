# NativeAOT Smoke App

- This smoke app and its CI leg (`.github/workflows/pr-build.yaml` `native-aot`
  job) are CI-validated for **both** EF10 (`net10.0`) and EF11 (`net11.0`).
- The project is standard-multi-targeted (`net10.0;net11.0`). NativeAOT
  publish/execution requires the project to evaluate as genuinely
  single-targeted, which is not achievable via an externally supplied
  `-p:TargetFramework`/`-p:TargetFrameworks` property — EF's own NativeAOT
  tooling loses such external build context during internal project
  reevaluation (see `docs/internal/ef10-ef11-build-configuration-strategy-research.md`,
  upstream issues [dotnet/efcore#38951](https://github.com/dotnet/efcore/issues/38951)
  and [dotnet/efcore#38955](https://github.com/dotnet/efcore/issues/38955)).
  Generate `scripts/write-target-framework-override.sh <net10.0|net11.0>`
  first — a physical, gitignored props file that forces single-TFM evaluation
  and survives EF's internal reopens/re-invocations because it's part of the
  project's own import graph, not an external property. Clear it afterward
  with `scripts/write-target-framework-override.sh --clear`
  (`task test:aot-publish FRAMEWORK=net10.0`/`net11.0` does both automatically).
- EF11 requires the exact-pinned `11.0.0-rc.1.26425.128` build of
  `Microsoft.EntityFrameworkCore`/`.Design`/`.Tasks` (see `Directory.Packages.props`
  and the exempted `Microsoft.EntityFrameworkCore.Specification.Tests` pin).
- `#if NET11_0` locations relevant to this app's compiled model/query
  interceptors are listed in `docs/internal/ef11-native-aot-implementation-plan.md`
  — review them when EF Core ships further breaking changes.
- `task test:aot-generation` and the full test suites run on both `net10.0`
  and `net11.0` without needing the override (they don't trigger EF Tasks'
  publish-time generation path).
