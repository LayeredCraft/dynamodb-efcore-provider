# Contributing

Thanks for helping improve EntityFrameworkCore.DynamoDb. Start with the [README](README.md) for project scope and usage examples.

## Prerequisites

- .NET SDK 10.0.x. `global.json` pins `10.0.103` with latest-minor roll-forward.
- [Go](https://go.dev/doc/install), used to install the `format` CLI.
- [uv](https://docs.astral.sh/uv/), used by Markdown formatting and docs commands. No separate Python install is required.
- Docker or another Testcontainers-compatible runtime for integration tests.
- Optional: [Task](https://taskfile.dev/) for `Taskfile.yml` shortcuts.

## Setup

Restore .NET packages and local tools:

```sh
dotnet restore
dotnet tool restore
```

Install `format` with Go:

```sh
go install github.com/j-d-ha/format/cmd/format@latest
```

Make sure Go's bin directory is on `PATH`:

```sh
export PATH="$(go env GOPATH)/bin:$PATH"
format --help
```

`dotnet tool restore` installs the local JetBrains cleanup tool used by `format.json`.

## Build and test

```sh
dotnet build
```

Run focused tests while developing:

```sh
dotnet test tests/EntityFrameworkCore.DynamoDb.Tests/EntityFrameworkCore.DynamoDb.Tests.csproj
dotnet test tests/EntityFrameworkCore.DynamoDb.IntegrationTests/EntityFrameworkCore.DynamoDb.IntegrationTests.csproj
dotnet test tests/EntityFrameworkCore.DynamoDb.SpecificationTests/EntityFrameworkCore.DynamoDb.SpecificationTests.csproj
```

Task shortcuts:

```sh
task build
task docs:build
```

## Formatting

Formatting is configured by `.editorconfig`, `EntityFrameworkCore.DynamoDb.sln.DotSettings`, and `format.json`.

Format explicit files with:

```sh
format README.md src/EntityFrameworkCore.DynamoDb/Infrastructure/DynamoDbContextOptionsBuilder.cs
```

Markdown/docs formatting uses `uvx`; C# formatting uses the restored `jb` .NET tool.

## Git hooks

This repo already includes `.githooks/pre-commit`. Enable it for your clone:

```sh
git config core.hooksPath .githooks
```

`format` can also install Git hooks and set `core.hooksPath`:

```sh
format hook git install
```

That command writes `.githooks/pre-commit`, makes it executable, and configures Git to use `.githooks`. It refuses to overwrite an existing hook unless `--force` is passed. Use `--force` only when intentionally regenerating the checked-in hook script.

The pre-commit hook formats staged files and stages formatter output. It fails on partially staged files to avoid committing unstaged changes.

The checked-in hook runs:

```sh
format hook git pre-commit --log-level warn
```

## Documentation

For behavior changes, update the relevant docs under `docs/` and verify the site:

```sh
task docs:build
```

If Task is unavailable, run:

```sh
uv run zensical build -f zensical.toml
```
