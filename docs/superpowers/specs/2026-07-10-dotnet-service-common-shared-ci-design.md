# .NET 10 Service Template, Common Integration, and Shared CI Design

## Summary

Template.NET will become a controller-based .NET 10 service template with a project-based Aspire AppHost.
The service will consume the public Common packages for Aspire-style service defaults and Serilog integration.
The repository will also contain a separate packable contracts library that illustrates NuGet packaging.
GitHub Actions will delegate validation to `ArkanisCorporation/ci@v1` wherever a matching shared workflow or action exists.
All executable release-related lanes will remain dry-run-only and will be unable to publish releases, images, packages, or deployments.
Documentation will show the explicit future steps required to opt into real image and NuGet publication.

## Goals

- Replace the placeholder `netstandard2.1` library with a controller-based ASP.NET Core service targeting `net10.0`.
- Add a project-based Aspire AppHost targeting `net10.0`.
- Consume appropriate packages from `ArkanisCorporation/Common` through public NuGet.org packages.
- Reuse shared GitHub Actions workflows and actions from `ArkanisCorporation/ci@v1`.
- Preserve repository-specific complexity verification that is not provided by the shared workflows.
- Add a packable contracts library and verify its NuGet artifacts without publishing them.
- Verify the service container image without pushing it.
- Keep semantic-release in verification-only mode.
- Remove obsolete local CI implementations and CitizenId-specific deployment remnants.

## Non-Goals

- The template will not publish GitHub releases.
- The template will not publish container images.
- The template will not publish NuGet packages.
- The template will not deploy to Kubernetes, Docker Compose, Azure, AWS, or another target.
- The AppHost will not include databases, caches, queues, dashboards, or product-specific resources.
- The template will not add `Arkanis.Common.Options` without a real options type and stable configuration section.
- The template will not add `Arkanis.Common.Testing` when its helpers are not used.
- The template will not copy CitizenId-specific gateway, worker, Discord, authentication, certificate, persistence, or deployment topology.

## Project Structure

The solution will contain the following production projects.

```text
src/
  Template.Contracts/
    Template.Contracts.csproj
    README.md
    ServiceStatusResponse.cs
  Template.Service/
    Controllers/
      StatusController.cs
    Properties/
      launchSettings.json
    Template.Service.csproj
    Dockerfile
    Program.cs
    appsettings.json
    appsettings.Development.json
  Template.AppHost/
    Template.AppHost.csproj
    AppHost.cs
```

The solution will contain one integration-test project.

```text
tests/
  Template.Service.IntegrationTests/
    HealthEndpointTests.cs
    StatusEndpointTests.cs
    Template.Service.IntegrationTests.csproj
```

The dependency direction will be `Template.AppHost -> Template.Service -> Template.Contracts`.
`Template.Contracts` will not reference the service or AppHost.
The existing placeholder projects and tests will be replaced rather than retained beside the new structure.

## Contracts Library

`Template.Contracts` will use `Microsoft.NET.Sdk` and target `netstandard2.1` for broad client compatibility.
It will be explicitly packable with package ID `Arkanis.Template.Contracts`.
It will include a package README, portable symbols, and a `.snupkg` symbol package.
Its repository metadata will point to `ArkanisCorporation/Template.NET` until a downstream repository renames it.
The library will expose a documented `ServiceStatusResponse` class used by the service status controller.
The service project will reference the contracts project directly.

The package is an illustration boundary rather than an independently released product.
Executable CI will pack and inspect it with a synthetic prerelease version.
Executable CI will not contain any NuGet publication action, credential, environment, or write permission.

## Service Application

`Template.Service` will use `Microsoft.NET.Sdk.Web` and target `net10.0`.
It will use standard ASP.NET Core controllers and will not use Minimal API application endpoints.
It will directly reference `Arkanis.Common.Aspire.ServiceDefaults` and `Arkanis.Common.Observability.Serilog` at `1.0.0-dev.1` through central package management.
The Common packages are currently available from public NuGet.org, so the base template will not require private package credentials.

Startup will create the Common Serilog bootstrap logger before host construction.
The host builder will register Common service defaults and Common Serilog defaults.
The service will register controllers, Problem Details, and OpenAPI generation.
The HTTP pipeline will use centralized exception handling.
OpenAPI endpoints will be exposed only in the Development environment.
The application will map controllers and Common's default liveness, readiness, and startup endpoints.
Startup failures will be logged as fatal and rethrown.
Serilog will be flushed asynchronously during shutdown.
The .NET 10 Web SDK will generate the public `Program` type used by `WebApplicationFactory<Program>`, so `Program.cs` will not declare a separate partial `Program` type.

`StatusController` will expose `GET /api/status`.
The controller will return a `ServiceStatusResponse` from `Template.Contracts`.
The endpoint exists to demonstrate the approved controller architecture and the service-to-contracts dependency.

Common service defaults will map these health endpoints.

- `/healthz/alive` reports the Common process-liveness check.
- `/healthz/ready` reports checks tagged for readiness.
- `/healthz/startup` reports checks tagged for startup.

The base service has no external dependencies, so readiness and startup will be healthy until downstream projects add tagged checks.
Deployment exposure of the health endpoints must be reviewed by downstream projects because Common maps them whenever `MapDefaultEndpoints` is called.

Common ServiceDefaults owns OpenTelemetry logging, metrics, traces, service discovery, HTTP resilience, and optional OTLP export.
Common Serilog's separate OTLP sink will remain disabled to avoid duplicate log export.
OTLP export will activate only when standard `OTEL_EXPORTER_OTLP_ENDPOINT` configuration is supplied.

## Aspire AppHost

`Template.AppHost` will use `Aspire.AppHost.Sdk/13.4.6` and target `net10.0`, matching the confirmed CitizenId AppHost pattern.
It will directly reference `Arkanis.Common.Hosting` so its health probe uses the shared liveness-path constant rather than a duplicated string.
The AppHost will register only `Template.Service` as an Aspire project resource.
It will expose the service HTTP endpoint for local development.
It will attach a health probe that uses the Common liveness path.
It will not define deployment targets or external infrastructure.

The AppHost is the local orchestration boundary.
The service remains runnable and deployable as a normal ASP.NET Core application without the AppHost.

## Container Image

`Template.Service/Dockerfile` will use .NET 10 SDK and ASP.NET runtime images.
It will restore and publish the service through a multi-stage build.
It will run as the built-in non-root application user.
It will expose the standard ASP.NET Core container HTTP port.
The Dockerfile will not embed package credentials, tokens, or other secrets.

CI will build the image with `wf-verify-publish-container-dotnet.yml@v1` and a synthetic prerelease version.
CI will not log in to a registry or invoke `wf-publish-container-dotnet.yml`.

## Testing

`Template.Service.IntegrationTests` will target `net10.0` and use `Microsoft.AspNetCore.Mvc.Testing` with xUnit.
The test host will exercise the real controller and middleware pipeline.
Integration tests will consume the public `Program` type generated by the .NET 10 Web SDK.
Tests will verify the status controller response contract.
Tests will verify the Common liveness, readiness, and startup endpoints.
The existing placeholder unit and integration tests will be removed.

`Arkanis.Common.Testing` will not be referenced because the approved tests do not use its service-provider fixtures.
If downstream tests need those fixtures, they can adopt the package together with its xUnit v3 requirements.

## GitHub Actions Architecture

The repository will own all event triggers, branch filters, trust decisions, and dependency ordering.
Shared workflows will remain pinned to the stable major reference `ArkanisCorporation/ci@v1`.
Pull requests will run on `ubuntu-latest`.
Trusted pushes and manual runs may use the configured organization runner or fall back to `ubuntu-latest`.
Caller inputs will keep `runs-on-self-hosted` consistent with the effective runner selection.

The primary CI workflow will run on pull requests to `main`, pushes to `main`, `release/*`, and `ci`, manual dispatch, and repository dispatch.
It will keep top-level permissions empty and grant only the scopes required by each job.

The validation graph will contain these lanes.

1. GitHub Actions linting through `wf-lint-github-actions.yml@v1`.
2. .NET formatting and mandatory JetBrains CleanupCode verification through `wf-dotnet-format.yml@v1`.
3. Build, test, coverage report generation, artifact upload, and job-summary reporting through `wf-dotnet-test.yml@v1`.
4. Repository-specific CA1502 and CA1509 complexity verification using the shared `setup-dotnet` action.
5. Semantic-release prediction through `wf-verify-release-semantic.yml@v1`.
6. Container build verification through `wf-verify-publish-container-dotnet.yml@v1`.
7. NuGet package verification through `wf-verify-publish-nuget.yml@v1`.

The format caller will use the Common pattern and set `install-tool: true` for JetBrains CleanupCode.
The coverage caller will set `coverage-pr-comment: false` and `upload-coverage: true` on every event.
Coverage results will remain available through uploaded artifacts and the job summary without granting pull-request write access.
This remains safe if repository administrators enable write tokens for fork pull-request workflows.
The container and NuGet verification lanes will use synthetic versions such as `0.0.0-ci.<run-number>.<run-attempt>` so they run on every CI event without depending on a predicted release.
The NuGet verification lane will set `dotnet-setversion: false`, include sources and symbols, and retain review artifacts for seven days.

No executable workflow will call any of these publication surfaces.

- `wf-release-semantic.yml`
- `wf-publish-container-dotnet.yml`
- `wf-publish-nuget.yml`
- `NuGet/login`
- `dotnet-publish-nuget`
- A deployment workflow

No release-related executable job will receive `id-token: write`, `packages: write`, publication credentials, or a publication environment.
Coverage reporting will receive only `contents: read` and will never comment on pull requests.
A separately reviewed `pull_request_target` or artifact-reporting lane may be considered as a future opt-in, but this design does not add one.

The existing local setup action, duplicated test workflow implementation, duplicated release workflow implementation, semantic-release execution scripts, and CitizenId-specific Kubernetes workflows will be removed.
The semantic-release configuration will retain only metadata-generation plugins accepted by the shared verification workflow.

## Publication Examples

The repository documentation will include non-executable examples for future NuGet.org and GHCR publication.
Examples will live in Markdown rather than `.github/workflows`.
They will omit a complete trigger so they cannot be executed accidentally.

The NuGet example will explain these required future decisions.

- Select the package and versioning policy.
- Select and document the package license before publication.
- Create a protected NuGet publication environment.
- Configure NuGet Trusted Publishing or an explicitly approved token.
- Add only the required permissions.
- Add a new workflow that calls the shared NuGet publisher.
- Verify the package, symbols, metadata, and remote publication.

The container example will explain these required future decisions.

- Select the registry and image naming policy.
- Create a protected container publication environment.
- Grant only the registry and attestation permissions that are required.
- Add a new workflow that calls the shared container publisher.
- Verify image metadata, provenance, SBOM, digest, and remote pull behavior.

Semantic-release publication will be documented as a separate opt-in because release metadata, container publication, and NuGet publication have different trust boundaries.

## Documentation and Template Stewardship

`README.md` will be rewritten around the service, AppHost, controller, health, Docker, testing, and dry-run CI workflows.
`docs/github-actions.md` will document the new shared-workflow graph, runner trust model, dry-run guarantees, and publication examples.
`AGENTS.md` will receive concise durable conventions for the service/AppHost boundary, Common package usage, and dry-run-only release policy.
Generated package lock files will be committed and restored in locked mode.
Repository text files will remain LF-terminated.

## Verification

Local verification will include locked restore, formatting, build, complexity analysis, tests, package creation, Docker build, actionlint, and Git diff checks.
The AppHost will be validated through the Aspire CLI in isolated mode because this work occurs in a linked worktree.
The service resource will be awaited through Aspire before endpoint verification.
Remote GitHub verification will be required for reusable-workflow execution, artifact handling, permission behavior, and pull-request reporting.

Completion requires proving all of the following.

- The solution builds and tests on .NET 10.
- The controller endpoint returns the contracts type.
- All three Common health endpoints respond successfully.
- The AppHost starts and reports the service healthy.
- The Docker image builds without publication.
- The contracts package and symbols pack without publication.
- Workflow linting passes.
- No executable release-related workflow can publish or deploy.
- README, GitHub Actions documentation, and agent guidance describe the implemented state.

## Evidence Sources

- `D:/Git/github/ArkanisCorporation/Common` provides the Common package APIs, NuGet.org consumption model, and shared-workflow caller examples.
- `D:/Git/github/ArkanisCorporation/CitizenId` provides the confirmed .NET 10 AppHost, service composition, controller-host testing, container, and NuGet-package examples.
- `D:/Git/github/ArkanisCorporation/ci` provides the reusable workflow and action contracts used by this design.
- Current GitHub Actions documentation confirms that called workflows can only maintain or reduce caller token permissions.
- NuGet.org currently exposes `Arkanis.Common.Aspire.ServiceDefaults` and `Arkanis.Common.Observability.Serilog` version `1.0.0-dev.1`.
