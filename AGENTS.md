# Agent Context

## Instruction Stewardship

Keep shared repo context current without turning `AGENTS.md` into a component catalogue.
While implementing, testing, debugging, or reviewing, ask:

> Did I learn a durable convention, setup step, repeated pitfall, design expectation, or local pattern that applies across multiple components, interfaces, workflows, or subsystems?

If yes, update the nearest applicable `AGENTS.md`; also update `README.md` when the context helps human developers or operators.
Keep updates concise and factual; exclude transient output, speculation, secrets, one-off task history, and component-only advice.
If no documentation update is needed, say so in the final handoff.

For durable contracts on public types, members, or model invariants, add XML docs there: `<summary>` for meaning, `<remarks>` for caveats, constraints, lifecycle, and side effects, and `<exception>` for thrown exceptions.
Keep `AGENTS.md` focused on workflow, repo conventions, cross-file patterns, design expectations, and pointers to code owning broader contracts.

## GitHub Actions

Keep reusable workflow files directly under `.github/workflows`.
GitHub does not support reusable workflow files in subdirectories.

Keep GitHub Actions changes reflected in [`README.md`](README.md) or a dedicated workflow operations document when local validation commands, runner expectations, secrets, or deployment behavior change.

Workflow and action calls use `ArkanisCorporation/ci@v1` where a shared contract exists.
Keep every executable workflow verification-only until the operator explicitly approves publication.
Keep publication examples in documentation intentionally non-executable and omit a complete trigger.
Keep pull-request validation jobs read-only and publish coverage through artifacts and job summaries.
Any future workflow that writes coverage results to pull requests requires a separate security review and explicit operator approval.

## Service Architecture

`Template.Service` is a controller-based `net10.0` service.
`Template.AppHost` is the local orchestration boundary.
`Template.Contracts` is the only packable project and remains on `netstandard2.1` for client compatibility.
Keep the dependency direction `Template.AppHost -> Template.Service -> Template.Contracts`.

Common packages own service defaults, the liveness, readiness, and startup health paths, and Serilog integration.
Consume Common through its public NuGet.org packages instead of duplicating those cross-cutting behaviors locally.

## Line Endings

Keep repository text files on LF line endings.
`.editorconfig` defines the editor expectation, and `.gitattributes` keeps fresh Git checkouts consistent even when a developer has `core.autocrlf=true`.

## Repository Automation Scripts

Keep repository automation scripts as C# file-based apps run with `dotnet run --file`.
Executable file-based script entries start with `#!/usr/bin/env -S dotnet --`, followed by the shared `#:` property directives used under `scripts`.
Use `#:include` for shared script logic.
Use `CliWrap` through the shared native-command helpers for native command execution.
Use `LibGit2Sharp` through the shared repository helpers for Git repository, status, blob, and index operations.
Run workflow linting through `dotnet run --file scripts/actionlint.cs` so the repository downloads and checksum-verifies its pinned cross-platform Actionlint binary.
When a file-based script name contains characters that are awkward for generated assembly names, set an explicit `#:property AssemblyName=...`.
Public and internal script helper APIs and custom exceptions require XML docs that state behavior, preconditions, side effects, and thrown exceptions.

## Post-Init Tasks

Run `dotnet husky run --group init` and `dotnet husky install` after creating a downstream project or worktree from this template.
Keep essential post-init tasks in the Husky `init` group, including `dotnet-aspire-agent-init`, so README initialization examples can point at one stable command.
