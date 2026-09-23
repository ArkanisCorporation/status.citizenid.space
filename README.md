# Template.NET

Template.NET is a GitHub template for a controller-based .NET 10 service with local Aspire orchestration, Common service defaults, and dry-run-only CI release lanes.

> [!IMPORTANT]
> Every executable release lane in this repository is verification-only.
> The workflows do not publish GitHub releases, container images, NuGet packages, or deployments.

## Project Graph

```text
Template.AppHost (net10.0)
└── Template.Service (net10.0)
    └── Template.Contracts (netstandard2.1, packable)
```

`Template.Service` is the controller-based ASP.NET Core application.
`Template.AppHost` is the local orchestration boundary and runs the service as the Aspire resource named `service`.
`Template.Contracts` is the only packable project and demonstrates a client-compatible contracts package.

The service consumes `Arkanis.Common.Aspire.ServiceDefaults`, `Arkanis.Common.Observability.Serilog`, and the AppHost consumes `Arkanis.Common.Hosting`.
These packages restore from public NuGet.org without private-feed credentials.
The centrally pinned Common prerelease is `1.0.0-dev.1`.

## Initialize A Checkout

Use the .NET SDK selected by [`global.json`](global.json).
Restore local tools, run the template initialization tasks, and install the Git hooks after creating a downstream project or worktree.

```powershell
dotnet tool restore
dotnet husky run --group init
dotnet husky install
dotnet restore Template.slnx --locked-mode
```

The init group prepares the local Aspire agent guidance.

## Build And Test

Run the repository's standard local verification sequence from the repository root.

```powershell
dotnet restore Template.slnx --locked-mode
dotnet format Template.slnx --verify-no-changes --verbosity diagnostic --no-restore
dotnet build Template.slnx --configuration Release --no-restore
dotnet build Template.slnx --configuration Release --no-restore --verbosity normal -warnaserror:CA1502 -warnaserror:CA1509
dotnet test Template.slnx --configuration Release --no-build
```

Locked restore uses the committed `packages.lock.json` files.
The AppHost intentionally opts out of solution-wide package lock generation because Aspire's SDK and package graph are OS-dependent.

## Run The Service Directly

Run the service as a normal ASP.NET Core application when orchestration is unnecessary.

```powershell
dotnet run --project src/Template.Service/Template.Service.csproj
```

Use the HTTP URL printed by ASP.NET Core.
The development launch profile uses `http://localhost:5000` by default.

The service exposes these endpoints.

- `GET /api/status` returns HTTP 200 with `{"status":"Healthy"}`.
- `GET /healthz/alive` reports Common process liveness.
- `GET /healthz/ready` reports Common readiness checks.
- `GET /healthz/startup` reports Common startup checks.

Common owns the health endpoint paths, OpenTelemetry setup, service discovery, HTTP resilience, and optional OTLP export.
Common Serilog integration owns the application logging defaults.

## Run With Aspire

Use the restored local Aspire CLI for the AppHost lifecycle.
Use `--isolated` in a worktree so the AppHost does not share state with another checkout.

```powershell
dotnet aspire start --apphost src/Template.AppHost/Template.AppHost.csproj --isolated --non-interactive
dotnet aspire wait service --apphost src/Template.AppHost/Template.AppHost.csproj --non-interactive
dotnet aspire describe --apphost src/Template.AppHost/Template.AppHost.csproj --format Json --non-interactive
dotnet aspire stop --apphost src/Template.AppHost/Template.AppHost.csproj --non-interactive
```

Always stop the AppHost when the local session is complete.
Use `dotnet aspire ps --format Json --non-interactive` to confirm that no AppHost remains running.

## Build The Container Locally

Build the service image from the repository-root context.

```powershell
docker build --file src/Template.Service/Dockerfile --tag template-service:dry-run .
docker image inspect template-service:dry-run
```

The multi-stage Dockerfile restores in locked mode, publishes the service in Release, exposes port 8080, and runs as the .NET base image's non-root application user.
The root `.dockerignore` keeps repository metadata, local tools, automation, documentation, and generated output out of the build context.
This command creates only a local image and does not authenticate to or push to a registry.

## Pack The Contracts Locally

Pack the contracts project with a synthetic prerelease version.

```powershell
dotnet pack src/Template.Contracts/Template.Contracts.csproj --configuration Release --no-restore --include-symbols --include-source -p:PackageVersion=0.0.0-ci.1.1
```

The `.nupkg` and `.snupkg` files are written beneath the repository's shared `artifacts/package/release` directory.
Packing does not publish either artifact.

## GitHub Actions

The main workflow validates GitHub Actions, formatting, complexity, tests and coverage, semantic-release prediction, the container image, and the contracts package.
The repository delegates shared behavior to `ArkanisCorporation/ci@v1` and keeps only repository-specific orchestration and complexity checks locally.
Container and NuGet verification use synthetic versions of the form `0.0.0-ci.<run-number>.<run-attempt>`.

Pull requests run on GitHub-hosted runners.
Trusted pushes and manual runs may use organization runners.
CI uploads generated coverage reports as workflow artifacts and includes the Markdown coverage summary in the job summary.
CI deliberately does not comment on pull requests, so the test lanes remain read-only even when repository administrators enable write tokens for fork pull-request workflows.
Fork pull requests also skip semantic-release prediction that needs write access.

See [GitHub Actions](docs/github-actions.md) for the complete workflow map, trust and permission rules, expected checks, GitHub-hosted verification limits, and intentionally non-executable publication examples.

## Local Workflow Linting

Run the repository-local Actionlint wrapper before committing workflow changes.
The wrapper downloads Actionlint `1.7.12` directly from its official GitHub release, verifies the pinned archive checksum, and caches the executable beneath ignored `.tools/actionlint`.
It supports Windows, Linux, and macOS on x64 and ARM64 without a global Actionlint or package-manager installation.

```powershell
dotnet run --file scripts/actionlint.cs
```

Arguments after `--` replace the default `-config-file .github/actionlint.yaml` arguments when an advanced Actionlint invocation is required.

Run the pull-request job listing through `act` when `act` and Docker are installed.

```powershell
act -l pull_request -W .github/workflows/main.yaml
```

`act` is useful for workflow discovery, but GitHub-hosted Actions remain the required verification environment for reusable workflow resolution, permissions, pull-request comments, summaries, and artifact upload behavior.

## Husky Tasks

The configured tasks live in [`.husky/task-runner.json`](.husky/task-runner.json).

```powershell
dotnet husky run --name prepare-shell-scripts
dotnet husky run --name update-shell-script-permissions
dotnet husky run --name dotnet-format
dotnet husky run --name dotnet-format-check
dotnet husky run --name dotnet-aspire-agent-init
```

The pre-commit group prepares staged shell scripts and verifies .NET formatting without modifying source files.
