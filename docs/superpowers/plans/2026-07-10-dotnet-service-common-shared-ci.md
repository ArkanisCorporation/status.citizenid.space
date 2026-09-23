# .NET 10 Service, Common, and Shared CI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Convert Template.NET into a controller-based .NET 10 service template that consumes Common, includes a project-based Aspire AppHost and packable contracts library, and delegates dry-run validation to `ArkanisCorporation/ci@v1`.

**Architecture:** `Template.AppHost` orchestrates `Template.Service`, and `Template.Service` references the broadly consumable `Template.Contracts` package project.
Common provides service defaults, shared health paths, and Serilog defaults.
GitHub Actions validates formatting, tests, complexity, semantic-release metadata, container construction, and NuGet packing without any executable publication or deployment path.

**Tech Stack:** .NET SDK 10.0.300, ASP.NET Core 10.0.9 controllers, Aspire 13.4.6, Common 1.0.0-dev.1, xUnit v3, GitHub Actions, Docker, semantic-release verification.

## Global Constraints

- Keep all repository text files on LF line endings.
- Keep the service and AppHost on `net10.0`.
- Keep `Template.Contracts` on `netstandard2.1` with package ID `Arkanis.Template.Contracts`.
- Use standard ASP.NET Core controllers and do not add Minimal API application endpoints.
- Reference `Arkanis.Common.Aspire.ServiceDefaults` and `Arkanis.Common.Observability.Serilog` directly from the service at `1.0.0-dev.1`.
- Reference `Arkanis.Common.Hosting` directly from the AppHost at `1.0.0-dev.1` for the liveness-path constant.
- Keep the dependency direction `Template.AppHost -> Template.Service -> Template.Contracts`.
- Keep executable release, image, NuGet, and deployment lanes dry-run-only.
- Do not grant `id-token: write` or `packages: write` to any executable workflow.
- Do not call `wf-release-semantic.yml`, `wf-publish-container-dotnet.yml`, `wf-publish-nuget.yml`, `NuGet/login`, or `dotnet-publish-nuget` from executable workflow files.
- Pin organization-shared workflow and action calls to `ArkanisCorporation/ci@v1`.
- Run untrusted pull requests on `ubuntu-latest`.
- Preserve the CA1502 and CA1509 complexity gate.
- Keep public and internal C# APIs documented with XML comments where their contracts are not self-evident.
- Keep package lock files committed and use locked restore for every project except the Aspire AppHost, whose OS-dependent SDK graph disables lock-file restore.
- Update `README.md`, `docs/github-actions.md`, and `AGENTS.md` to match the implemented service, AppHost, Common, and CI behavior.

---

### Task 1: Create the contracts, controller service, and integration tests

**Files:**

- Delete: `src/Template/Class1.cs`
- Delete: `src/Template/Template.csproj`
- Delete: `src/Template/packages.lock.json`
- Delete: `tests/Template.UnitTests/UnitTest1.cs`
- Delete: `tests/Template.UnitTests/Template.UnitTests.csproj`
- Delete: `tests/Template.UnitTests/packages.lock.json`
- Delete: `tests/Template.IntegrationTests/IntegrationTest1.cs`
- Delete: `tests/Template.IntegrationTests/Template.IntegrationTests.csproj`
- Delete: `tests/Template.IntegrationTests/packages.lock.json`
- Create: `src/Template.Contracts/Template.Contracts.csproj`
- Create: `src/Template.Contracts/README.md`
- Create: `src/Template.Contracts/ServiceStatusResponse.cs`
- Create: `src/Template.Service/Template.Service.csproj`
- Create: `src/Template.Service/Program.cs`
- Create: `src/Template.Service/Controllers/StatusController.cs`
- Create: `src/Template.Service/appsettings.json`
- Create: `src/Template.Service/appsettings.Development.json`
- Create: `src/Template.Service/Properties/launchSettings.json`
- Create: `tests/Template.Service.IntegrationTests/Template.Service.IntegrationTests.csproj`
- Create: `tests/Template.Service.IntegrationTests/StatusEndpointTests.cs`
- Create: `tests/Template.Service.IntegrationTests/HealthEndpointTests.cs`
- Modify: `Directory.Packages.props`
- Modify: `Template.slnx`
- Generate: `src/Template.Contracts/packages.lock.json`
- Generate: `src/Template.Service/packages.lock.json`
- Generate: `tests/Template.Service.IntegrationTests/packages.lock.json`

**Interfaces:**

- Produces: `Arkanis.Template.Contracts.ServiceStatusResponse` with a nonblank `Status` property.
- Produces: `GET /api/status` returning HTTP 200 with `{"status":"Healthy"}`.
- Produces: Common health endpoints `/healthz/alive`, `/healthz/ready`, and `/healthz/startup`.
- Produces: the .NET 10 Web SDK-generated public `Program` type for `WebApplicationFactory<Program>` without an explicit partial declaration.

- [ ] **Step 1: Replace central package versions**

Keep existing tool/package versions that remain used and set these exact package entries in `Directory.Packages.props`.

```xml
<PackageVersion Include="Arkanis.Common.Aspire.ServiceDefaults" Version="1.0.0-dev.1"/>
<PackageVersion Include="Arkanis.Common.Hosting" Version="1.0.0-dev.1"/>
<PackageVersion Include="Arkanis.Common.Observability.Serilog" Version="1.0.0-dev.1"/>
<PackageVersion Include="Aspire.Hosting.AppHost" Version="13.4.6"/>
<PackageVersion Include="coverlet.collector" Version="10.0.1"/>
<PackageVersion Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.9"/>
<PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.9"/>
<PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.7.0"/>
<PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5"/>
<PackageVersion Include="xunit.v3" Version="3.2.2"/>
```

- [ ] **Step 2: Create project scaffolding without application behavior**

Create `Template.Contracts.csproj` as a packable `netstandard2.1` project with nullable and implicit usings enabled, package ID `Arkanis.Template.Contracts`, package README metadata, repository URL `https://github.com/ArkanisCorporation/Template.NET`, portable PDBs, symbol packages, and the project README packed at the package root.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.1</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>true</IsPackable>
    <RootNamespace>Arkanis.Template.Contracts</RootNamespace>
    <PackageId>Arkanis.Template.Contracts</PackageId>
    <Title>Arkanis Template Contracts</Title>
    <Description>Example contracts package for the generated service.</Description>
    <PackageTags>contracts;dotnet;arkanis</PackageTags>
    <PackageReadmeFile>README.md</PackageReadmeFile>
    <RepositoryType>git</RepositoryType>
    <RepositoryUrl>https://github.com/ArkanisCorporation/Template.NET</RepositoryUrl>
    <PublishRepositoryUrl>true</PublishRepositoryUrl>
    <DebugType>portable</DebugType>
    <IncludeSymbols>true</IncludeSymbols>
    <SymbolPackageFormat>snupkg</SymbolPackageFormat>
  </PropertyGroup>
  <ItemGroup>
    <None Include="README.md" Pack="true" PackagePath="\"/>
  </ItemGroup>
</Project>
```

Create `Template.Service.csproj` with `Microsoft.NET.Sdk.Web`, `net10.0`, a project reference to `Template.Contracts`, and package references to the two approved Common packages plus `Microsoft.AspNetCore.OpenApi`.

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>Arkanis.Template.Service</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Arkanis.Common.Aspire.ServiceDefaults"/>
    <PackageReference Include="Arkanis.Common.Observability.Serilog"/>
    <PackageReference Include="Microsoft.AspNetCore.OpenApi"/>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Template.Contracts\Template.Contracts.csproj"/>
  </ItemGroup>
</Project>
```

Create the integration-test project with `net10.0`, `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.NET.Test.Sdk`, `coverlet.collector`, `xunit.v3`, `xunit.runner.visualstudio`, and project references to the service and contracts projects.

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <RootNamespace>Arkanis.Template.Service.IntegrationTests</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="coverlet.collector" PrivateAssets="all"/>
    <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing"/>
    <PackageReference Include="Microsoft.NET.Test.Sdk"/>
    <PackageReference Include="xunit.runner.visualstudio" PrivateAssets="all"/>
    <PackageReference Include="xunit.v3"/>
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit"/>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\Template.Contracts\Template.Contracts.csproj"/>
    <ProjectReference Include="..\..\src\Template.Service\Template.Service.csproj"/>
  </ItemGroup>
</Project>
```

Replace the old project entries in `Template.slnx` with the three new projects.

Create a temporary service startup that registers controllers but maps no controller or Common health endpoints yet, so the behavioral tests can fail with HTTP 404 rather than a compilation error.

- [ ] **Step 3: Write the failing endpoint tests**

Use `WebApplicationFactory<Program>` in both test classes.

`StatusEndpointTests` must request `/api/status`, require success, deserialize `ServiceStatusResponse`, and assert `Status == "Healthy"`.

`HealthEndpointTests` must use a theory with these exact paths and require success for each.

```csharp
[Theory]
[InlineData("/healthz/alive")]
[InlineData("/healthz/ready")]
[InlineData("/healthz/startup")]
public async Task Health_endpoint_returns_success(string path)
```

- [ ] **Step 4: Run the focused tests and verify RED**

Run:

```powershell
rtk dotnet restore Template.slnx
rtk dotnet test tests/Template.Service.IntegrationTests/Template.Service.IntegrationTests.csproj
```

Expected: four endpoint cases fail with HTTP 404 because the controller and Common endpoints are not mapped.

- [ ] **Step 5: Implement the contracts type and controller**

Implement a documented sealed `ServiceStatusResponse` class in the `Arkanis.Template.Contracts` namespace.
Its constructor must reject a null, empty, or whitespace status with `string.IsNullOrWhiteSpace` and an `ArgumentException`.
Its `Status` property must be get-only.

```csharp
namespace Arkanis.Template.Contracts;

/// <summary>
/// Describes the externally visible status of the service.
/// </summary>
public sealed class ServiceStatusResponse
{
    /// <summary>
    /// Creates a service status response.
    /// </summary>
    /// <param name="status">The nonblank service status.</param>
    /// <exception cref="ArgumentException"><paramref name="status"/> is empty or whitespace.</exception>
    public ServiceStatusResponse(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            throw new ArgumentException("Service status is required.", nameof(status));
        }

        Status = status;
    }

    /// <summary>
    /// Gets the service status.
    /// </summary>
    public string Status { get; }
}
```

Implement a documented `StatusController` with `[ApiController]`, `[Route("api/[controller]")]`, and `[HttpGet]`.
Return `ActionResult<ServiceStatusResponse>` containing `new ServiceStatusResponse("Healthy")`.

```csharp
namespace Arkanis.Template.Service.Controllers;

using Arkanis.Template.Contracts;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Reports the service's public status contract.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public sealed class StatusController : ControllerBase
{
    /// <summary>
    /// Gets the current service status.
    /// </summary>
    /// <returns>The healthy service status.</returns>
    [HttpGet]
    [ProducesResponseType<ServiceStatusResponse>(StatusCodes.Status200OK)]
    public ActionResult<ServiceStatusResponse> Get() => Ok(new ServiceStatusResponse("Healthy"));
}
```

- [ ] **Step 6: Implement the service startup and configuration**

Use this startup sequence in `Program.cs`.

```csharp
using Arkanis.Common.Aspire.ServiceDefaults;
using Arkanis.Common.Observability.Serilog;
using Serilog;

_ = SerilogBootstrapLogger.UseBootstrapLogger();

try
{
    Log.Information("Starting service.");

    var builder = WebApplication.CreateBuilder(args);

    _ = builder.AddServiceDefaults();
    _ = builder.AddSerilogDefaults();
    _ = builder.Services.AddControllers();
    _ = builder.Services.AddProblemDetails();
    _ = builder.Services.AddOpenApi();

    var app = builder.Build();

    _ = app.UseExceptionHandler();

    if (app.Environment.IsDevelopment())
    {
        _ = app.MapOpenApi();
    }

    _ = app.MapControllers();
    _ = app.MapDefaultEndpoints();

    await app.RunAsync();
}
catch (Exception exception)
{
    Log.Fatal(exception, "Service terminated during startup.");
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}

```

Rely on the .NET 10 Web SDK-generated public `Program` type for `WebApplicationFactory<Program>`.
Do not add an explicit partial `Program` declaration.

Configure Serilog console output in `appsettings.json` and keep the Serilog OTLP sink disabled because Common ServiceDefaults owns OpenTelemetry export.
Keep `appsettings.Development.json` limited to development log-level overrides.
Configure launch profiles for HTTP and HTTPS without fixed AppHost-owned orchestration dependencies.

`appsettings.json` must contain:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      }
    ],
    "Enrich": [
      "FromLogContext"
    ]
  },
  "AllowedHosts": "*"
}
```

`appsettings.Development.json` must contain:

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Override": {
        "Microsoft.AspNetCore": "Information"
      }
    }
  }
}
```

- [ ] **Step 7: Run GREEN verification and generate lock files**

Run:

```powershell
rtk dotnet restore Template.slnx
rtk dotnet format Template.slnx --verbosity diagnostic --no-restore
rtk dotnet build Template.slnx --configuration Release --no-restore
rtk dotnet test Template.slnx --configuration Release --no-build
```

Expected: locked package files are generated, the solution builds without warnings, and all four endpoint cases pass.

- [ ] **Step 8: Commit Task 1**

```powershell
rtk git add Directory.Packages.props Template.slnx src tests
rtk git commit -m "feat: scaffold controller service and contracts"
```

---

### Task 2: Add the project-based Aspire AppHost and service container

**Files:**

- Create: `src/Template.AppHost/Template.AppHost.csproj`
- Create: `src/Template.AppHost/AppHost.cs`
- Create: `src/Template.Service/Dockerfile`
- Modify: `Template.slnx`

**Interfaces:**

- Consumes: `Template.Service` and Common's `HealthEndpointUrlPaths.Liveness`.
- Produces: Aspire resource name `service` with externally visible HTTP endpoints and an HTTP liveness probe.
- Produces: a non-root .NET 10 service image from repository-root Docker context.

- [ ] **Step 1: Confirm the project-based AppHost exception**

Record in the implementation report that Aspire CLI 13.4.6 currently scaffolds a file-based `apphost.cs`, while the approved design and CitizenId pattern require `src/Template.AppHost/Template.AppHost.csproj`.
Create the approved SDK-style project directly rather than accepting the incompatible generated shape.

- [ ] **Step 2: Verify AppHost APIs before authoring**

Use Aspire documentation API search for `AddProject`, `WithExternalHttpEndpoints`, and `WithHttpHealthCheck`.
If the online documentation command times out, use the compiled Aspire 13.4.6 package surface or the current official templates rather than guessing.

- [ ] **Step 3: Create the AppHost project**

Use `Aspire.AppHost.Sdk/13.4.6`, `net10.0`, executable output, nullable and implicit usings.
Disable package lock generation and locked restore in this project because the Aspire SDK graph is OS-dependent.
Reference `Aspire.Hosting.AppHost`, `Arkanis.Common.Hosting`, and `Template.Service`.

```xml
<Project Sdk="Aspire.AppHost.Sdk/13.4.6">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RestorePackagesWithLockFile>false</RestorePackagesWithLockFile>
    <RestoreLockedMode>false</RestoreLockedMode>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Arkanis.Common.Hosting"/>
    <PackageReference Include="Aspire.Hosting.AppHost"/>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Template.Service\Template.Service.csproj"/>
  </ItemGroup>
</Project>
```

Use this AppHost body.

```csharp
using Arkanis.Common.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

_ = builder.AddProject<Projects.Template_Service>("service")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck(HealthEndpointUrlPaths.Liveness);

builder.Build().Run();
```

Add the AppHost project to `Template.slnx`.

- [ ] **Step 4: Create the non-root Dockerfile**

Use repository root as build context.
Use `mcr.microsoft.com/dotnet/sdk:10.0` for build and `mcr.microsoft.com/dotnet/aspnet:10.0` for runtime.
Restore `Template.slnx` in locked mode, publish `Template.Service.csproj` in Release without another restore, copy the published output, expose port 8080, set `USER $APP_UID`, and run `Template.Service.dll`.

```dockerfile
# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY . .
RUN dotnet restore Template.slnx --locked-mode
RUN dotnet publish src/Template.Service/Template.Service.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
USER $APP_UID
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "Template.Service.dll"]
```

- [ ] **Step 5: Verify AppHost and container construction**

Run:

```powershell
rtk dotnet restore Template.slnx --locked-mode
rtk dotnet build Template.slnx --configuration Release --no-restore
rtk docker build --file src/Template.Service/Dockerfile --tag template-service:dry-run .
```

Expected: solution build and Docker build exit successfully without publishing.

Run the AppHost through Aspire in isolated mode.

```powershell
rtk dotnet aspire start --apphost src/Template.AppHost/Template.AppHost.csproj --isolated --non-interactive
rtk dotnet aspire wait service --apphost src/Template.AppHost/Template.AppHost.csproj --non-interactive
rtk dotnet aspire describe --apphost src/Template.AppHost/Template.AppHost.csproj --format Json --non-interactive
rtk dotnet aspire stop --apphost src/Template.AppHost/Template.AppHost.csproj --non-interactive
```

Expected: the service resource becomes healthy, its external HTTP endpoint is present, and Aspire stops cleanly.

- [ ] **Step 6: Commit Task 2**

```powershell
rtk git add Template.slnx src/Template.AppHost src/Template.Service/Dockerfile
rtk git commit -m "feat: add Aspire AppHost and service image"
```

---

### Task 3: Replace local CI/CD with shared dry-run validation

**Files:**

- Modify: `.github/workflows/main.yaml`
- Replace: `.github/workflows/_test.yaml`
- Create: `.github/workflows/_verify-release.yaml`
- Create: `.github/workflows/_verify-container-image.yaml`
- Create: `.github/workflows/_verify-nuget-package.yaml`
- Delete: `.github/workflows/_release.yaml`
- Delete: `.github/workflows/_deploy-kubernetes.yaml`
- Delete: `.github/workflows/deploy-kubernetes.yaml`
- Delete: `.github/workflows/pipeline-quality.yaml`
- Delete: `.github/actions/setup-dotnet-workspace/action.yaml`
- Modify: `release.config.mjs`
- Delete: `scripts/semantic-release/`

**Interfaces:**

- Produces: shared lint, format, coverage, complexity, release dry-run, container dry-run, and NuGet dry-run jobs.
- Produces: synthetic version `0.0.0-ci.${{ github.run_number }}.${{ github.run_attempt }}` for image and NuGet verification.
- Guarantees: no executable publication, registry login, package push, release creation, backpropagation, or deployment path.

- [ ] **Step 1: Replace semantic-release configuration**

Keep the existing branch and tag policy and repository URL.
Keep only these plugins.

```javascript
plugins: [
    "@semantic-release/commit-analyzer",
    "@semantic-release/release-notes-generator",
    "@semantic-release/github",
]
```

Delete the semantic-release execution scripts because `@semantic-release/exec` is removed and all executable lanes are verification-only.

- [ ] **Step 2: Create the shared test wrapper**

Model `_test.yaml` on Common's wrapper.
Expose `runner`, `test_filter`, `coverage_custom_settings`, and `concurrency_group_prefix` inputs.
Call `wf-dotnet-test.yml@v1` for `Template.slnx`, set `coverage-pr-comment: false` and `upload-coverage: true` on every event, and call `wf-dotnet-format.yml@v1` with `install-tool: true`.

Add one local `complexity` job using `actions/checkout@v7` and `ArkanisCorporation/ci/.github/actions/setup-dotnet@v1`.
Pass `global-json-file: global.json`, `solution: Template.slnx`, and the matching runner contract.
Run:

```bash
dotnet build Template.slnx --no-restore --verbosity normal -warnaserror:CA1502 -warnaserror:CA1509
```

- [ ] **Step 3: Create the release verification wrapper**

Create `_verify-release.yaml` from Common's verified wrapper.
Call only `wf-verify-release-semantic.yml@v1` with `contents: write`, because semantic-release dry-run verifies tag-push access.
Expose the predicted version, tag, channel, last version, last tag, and `release-published` outputs.
Do not call `wf-release-semantic.yml`.

- [ ] **Step 4: Create the container verification wrapper**

Create `_verify-container-image.yaml` with `runner` and `version` inputs.
Call only `wf-verify-publish-container-dotnet.yml@v1` with `contents: read`.
Use image `ghcr.io/arkaniscorporation/template`, context `.`, Dockerfile `src/Template.Service/Dockerfile`, `global-json-file: global.json`, Release build configuration, and OCI source/revision/version metadata.
Do not configure a registry token or call the shared container publisher.

- [ ] **Step 5: Create the NuGet verification wrapper**

Create `_verify-nuget-package.yaml` with `runner` and `version` inputs.
Call only `wf-verify-publish-nuget.yml@v1` with `contents: read`.
Pass `src/Template.Contracts/Template.Contracts.csproj`, `global-json-file: global.json`, `dotnet-setversion: false`, `include-symbols: true`, `include-source: true`, and seven-day artifact retention.
Do not configure NuGet publication credentials or call the shared NuGet publisher.

- [ ] **Step 6: Replace the primary workflow**

Keep workflow dispatch, repository dispatch, pushes to `main`, `release/*`, and `ci`, and pull requests to `main`.
Keep top-level permissions empty.

Add these jobs.

1. `lint` calls `wf-lint-github-actions.yml@v1` directly.
2. `test` calls local `_test.yaml`.
3. `release-dry-run` needs `test` and calls local `_verify-release.yaml`.
4. `container-dry-run` needs `test` and calls local `_verify-container-image.yaml` with the synthetic version.
5. `nuget-dry-run` needs `test` and calls local `_verify-nuget-package.yaml` with the synthetic version.

Use this runner expression for every caller.

```yaml
${{ github.event_name == 'pull_request' && 'ubuntu-latest' || inputs.runner || vars.RUNNER_DEFAULT || 'daedalus' }}
```

Guard `release-dry-run` with this expression so fork pull requests do not request unavailable tag-push access.

```yaml
${{ github.event_name != 'pull_request' || github.event.pull_request.head.repo.full_name == github.repository }}
```

Do not add any publishing, backpropagation, or deployment job.

- [ ] **Step 7: Verify workflow syntax and forbidden surfaces**

Run:

```powershell
rtk dotnet run --file scripts/actionlint.cs
rtk git grep -n -E "wf-release-semantic\.yml|wf-publish-container-dotnet\.yml|wf-publish-nuget\.yml|NuGet/login|dotnet-publish-nuget|id-token: write|packages: write" -- .github/workflows
```

Expected: actionlint exits successfully and the forbidden-surface search returns no matches.

When `act` is installed, list the pull-request jobs without executing them.

```powershell
rtk act -l pull_request -W .github/workflows/main.yaml
```

Expected: `act` lists the validation jobs and no publication or deployment job.

- [ ] **Step 8: Commit Task 3**

```powershell
rtk git add .github release.config.mjs scripts/semantic-release
rtk git commit -m "ci: adopt shared dry-run workflows"
```

---

### Task 4: Update documentation and verify the complete template

**Files:**

- Modify: `README.md`
- Modify: `docs/github-actions.md`
- Modify: `AGENTS.md`

**Interfaces:**

- Documents: service startup, AppHost orchestration, controller endpoint, Common health endpoints, Docker build, contracts pack, and dry-run CI.
- Documents: non-executable opt-in examples for semantic-release, GHCR image publication, and NuGet.org publication.
- Documents: durable agent conventions for Common packages, project dependencies, and dry-run-only workflow safety.

- [ ] **Step 1: Rewrite the README around the service template**

Lead with the project graph and the commands developers run most often.
Document locked restore, format, build, test, direct service run, Aspire start/wait/stop, Docker build, and contracts pack.
Document `/api/status`, `/healthz/alive`, `/healthz/ready`, and `/healthz/startup`.
State that Common packages restore from public NuGet.org and that the pinned prerelease is `1.0.0-dev.1`.
State prominently that all executable release lanes are dry-run-only.

- [ ] **Step 2: Rewrite GitHub Actions documentation**

Document every shared `ci@v1` workflow/action used by the repository.
Document runner trust, read-only pull-request test permissions, coverage artifact and job-summary reporting without pull-request comments, synthetic versions, expected required checks, and GitHub-hosted verification limitations.

Include non-executable YAML fragments for future semantic-release publication, GHCR publication, and NuGet.org Trusted Publishing.
Omit complete triggers from those fragments.
Precede each fragment with an explicit security checklist requiring a protected environment, least privileges, repository-specific naming/versioning, and remote verification.

- [ ] **Step 3: Update durable agent guidance**

Add concise `AGENTS.md` sections stating:

- `Template.Service` is a controller-based `net10.0` service.
- `Template.AppHost` is the local orchestration boundary.
- `Template.Contracts` is the only packable project and remains `netstandard2.1`.
- Common owns service defaults, health paths, and Serilog integration.
- Workflow calls use `ArkanisCorporation/ci@v1`.
- Executable workflows must remain verification-only until the operator explicitly approves publication.
- Documentation publication examples are intentionally non-executable.

- [ ] **Step 4: Run full local verification**

Run:

```powershell
rtk dotnet tool restore
rtk dotnet restore Template.slnx --locked-mode
rtk dotnet format Template.slnx --verify-no-changes --verbosity diagnostic --no-restore
rtk dotnet build Template.slnx --configuration Release --no-restore
rtk dotnet build Template.slnx --configuration Release --no-restore --verbosity normal -warnaserror:CA1502 -warnaserror:CA1509
rtk dotnet test Template.slnx --configuration Release --no-build
rtk dotnet pack src/Template.Contracts/Template.Contracts.csproj --configuration Release --no-restore --include-symbols --include-source -p:PackageVersion=0.0.0-ci.1.1
rtk docker build --file src/Template.Service/Dockerfile --tag template-service:dry-run .
rtk dotnet run --file scripts/actionlint.cs
rtk git diff --check
```

Expected: every command exits successfully, tests pass without warnings, NuGet artifacts exist locally, the image exists only locally, actionlint reports no errors, and Git diff checks are clean.

Validate the AppHost with the Aspire lifecycle commands from Task 2 and stop it afterward.

- [ ] **Step 5: Audit release safety**

Search every executable workflow and confirm there are no publication or deployment surfaces.
Inspect `git diff` against the branch base and match every approved design requirement to an implemented file or verification result.

- [ ] **Step 6: Commit Task 4**

```powershell
rtk git add README.md docs/github-actions.md AGENTS.md
rtk git commit -m "docs: document service template workflows"
```

---

## Final Review

After all four tasks pass their task-scoped review gates, generate a whole-branch review package from the branch merge base to `HEAD`.
Dispatch the final reviewer against the approved design specification and this plan.
Fix every Critical or Important finding in one final fix wave, rerun the covering checks, and request re-review.
Then use the finishing-a-development-branch workflow to present integration options without pushing or opening a pull request unless the operator requests it.
