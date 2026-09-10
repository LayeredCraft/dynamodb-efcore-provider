# NativeAOT Smoke App — EF10 ONLY

- This smoke app and its CI leg (`.github/workflows/pr-build.yaml` `native-aot`
  job) run on **EF10 configurations only** (`Release EF10`). Do NOT add an EF11
  (`Release EF11`) smoke leg to CI.
- EF11 NativeAOT smoke publishing is broken upstream: the EF11
  `Microsoft.EntityFrameworkCore.Tasks` precompile step cannot resolve EF types
  (`DbContext`, `DbSet`, `KeyType`, `TableStatus`, `SaveChangesAsync`, ...) when
  compiling generated interceptors against the pinned EF 11 preview package
  (`11.0.0-preview.5.26302.115`).
- Aligning EF Core to the Tasks preview (11.0.0-preview.6.26359.118) also
  fails: the provider no longer compiles (`TranslateFullJoin` abstract member
  added, `Create` override signature changed).
- Revisit only when a newer EF 11 preview both fixes interceptor compilation
  and includes those provider API breaks, or with an EF11 API migration.
- `task test:aot-generation` and the full test suites DO run on both EF10 and
  EF11; only native publish/execution here is EF10-only.
