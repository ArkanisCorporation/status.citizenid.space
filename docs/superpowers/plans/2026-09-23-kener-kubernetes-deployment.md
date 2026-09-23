# Kener Kubernetes Deployment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `Template.AppHost` publish isolated, production-ready Kener Helm charts for `citizenid-status-production` and `citizenid-status-staging`.

**Architecture:** The AppHost selects `Kubernetes-Production` or `Kubernetes-Staging` at publish time and loads a corresponding configuration file before building one Kubernetes resource graph.
Kener runs as a two-replica Deployment, while a namespace-local Redis StatefulSet, CloudNativePG Database, External Secrets, Ingress, PVC, and PodDisruptionBudget are emitted as AppHost-owned manifests.
The publish tests execute the AppHost with the Aspire CLI and assert generated chart artifacts, never AppHost source text.

**Tech Stack:** .NET 10, Aspire 13.5.4, Aspire Kubernetes and Redis hosting, Arkanis Kubernetes, CloudNativePG, External Secrets, and 1Password extensions, xUnit v3.

**Spec:** `docs/superpowers/specs/2026-09-23-kener-production-kubernetes-design.md`

## Global Constraints

- Publish exactly one selected deployment environment per execution.
- Production namespace is `citizenid-status-production` and staging namespace is `citizenid-status-staging`.
- Production origin is `https://status.citizenid.space` and staging origin is `https://status.citizenid.dev`.
- Production database is `citizenid-production-status` and staging database is `citizenid-staging-status` in `postgres-production`.
- Use `rajnandan1/kener:v4.1.5-alpine`, two Kener replicas, one authenticated Redis `8-alpine` StatefulSet, AOF, `2Gi` RWO Longhorn PVC, and no Redis Cluster or operator.
- Use `DATABASE_POOL_MAX=5`, `DATABASE_WORKER_POOL_MAX=3`, `SMTP_HOST=smtp.purelymail.com`, `SMTP_PORT=465`, `SMTP_SECURE=1`, and the environment-specific `ORIGIN`.
- Kener and Redis passwords use ESO `PasswordGenerator` with `CreatedOnce` rotation.
- Kener mail references the existing 1Password item only through `op://` references and `onepassword-connect`.
- Do not resolve, print, create, rotate, or publish secret values.
- Do not apply Helm artifacts or change the live cluster.
- Keep the user’s pre-existing staged and unstaged changes outside this scope intact.

## Review Focus

- A staging publish must not contain production namespace, hostname, database, secret target, or Redis PVC identities.
- A production publish must not contain staging namespace, hostname, database, secret target, or Redis PVC identities.
- The CloudNativePG value must be a `postgresql://` URI with ESO `urlquery` transformations, not the default Npgsql string.
- All Kener Secret inputs must be externalized, and the chart must not contain a resolved 1Password reference or secret value.
- A voluntary eviction must retain one ready Kener pod, while a reduced-zone cluster can still schedule both pods because both anti-affinity terms are preferred only.

## Implementation Outcome

The Kener deployment scope is committed independently on `main`.
The pre-existing staged and unstaged changes in this shared working tree remain outside that commit.

---

### Task 1: Environment-specific chart boundary

**Files:**
- Create: `tests/Template.AppHost.PublishTests/Template.AppHost.PublishTests.csproj`
- Create: `tests/Template.AppHost.PublishTests/AspirePublishFixture.cs`
- Create: `tests/Template.AppHost.PublishTests/KenerPublishTests.cs`
- Modify: `Template.slnx`
- Modify: `Directory.Packages.props`
- Modify: `dotnet-tools.json`
- Modify: `src/Template.AppHost/AppHost.cs`
- Modify: `src/Template.AppHost/Template.AppHost.csproj`
- Create: `src/Template.AppHost/appsettings.Kubernetes.Production.json`
- Create: `src/Template.AppHost/appsettings.Kubernetes.Staging.json`

**Interfaces:**
- Consumes: `dotnet tool run aspire publish --apphost src/Template.AppHost/Template.AppHost.csproj --environment Kubernetes-{Production|Staging}`.
- Produces: `AspirePublishFixture.PublishAsync(string environment, CancellationToken cancellationToken)`, which returns an immutable `PublishedChart` exposing `ExitCode`, `Output`, `ReadAllTemplatesAsync`, and deleting its own temporary output directory.

- [x] **Step 1: Write the test fixture and failing namespace test**

```csharp
[Theory]
[InlineData("Kubernetes-Production", "citizenid-status-production")]
[InlineData("Kubernetes-Staging", "citizenid-status-staging")]
public async Task Publish_uses_the_selected_Kubernetes_namespace(
    string environment,
    string expectedNamespace)
{
    await using var chart = await AspirePublishFixture.PublishAsync(environment, TestContext.Current.CancellationToken);

    Assert.True(chart.ExitCode == 0, chart.Output);

    var artifacts = await chart.ReadAllTemplatesAsync(TestContext.Current.CancellationToken);

    Assert.Contains(expectedNamespace, artifacts, StringComparison.Ordinal);
}
```

- [x] **Step 2: Run the new tests to verify they fail**

Run: `dotnet test tests/Template.AppHost.PublishTests/Template.AppHost.PublishTests.csproj --configuration Release`
Expected: FAIL at `Assert.Equal(0, chart.ExitCode, chart.Output)` because the AppHost cannot yet publish an environment-specific Kubernetes chart.

- [x] **Step 3: Implement the environment boundary and test fixture**

```csharp
var arguments = $"tool run aspire publish --apphost \"{appHostProject}\" --environment {environment} --output-path \"{outputDirectory}\" --non-interactive";
using var process = Process.Start(new ProcessStartInfo("dotnet", arguments)
{
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false,
});
```

The fixture must return exit code and redacted command output rather than throwing for a non-zero publish exit, so the RED test fails as an assertion.
The test project must use `xunit.v3`, `Microsoft.NET.Test.Sdk`, and `xunit.runner.visualstudio` from central package management.
The local Aspire tool version must be aligned to `13.5.4`.
Add `Aspire.Hosting.Kubernetes` `13.5.4-preview.1.26464.4`, `Arkanis.Aspire.Hosting.Extensions.Kubernetes` `1.0.0-dev.20`, and `Arkanis.Aspire.Hosting.Extensions.1Password` `2.0.0`.

```csharp
var isKubernetesDeployment = builder.Environment.EnvironmentName.StartsWith("Kubernetes-", StringComparison.OrdinalIgnoreCase);
if (isKubernetesDeployment)
{
    builder.Configuration.AddJsonFile(
        $"appsettings.{builder.Environment.EnvironmentName.Replace('-', '.')}.json",
        optional: false,
        reloadOnChange: false);

    builder.AddKubernetesEnvironment("kener")
        .WithHelm(helm => helm.WithNamespace(builder.Configuration["Kubernetes:Namespace"]));
}
else
{
    await builder.Use1PasswordAsync("arkaniscorp.1password.com");
}
```

The production and staging JSON files must contain only their exact namespace and non-secret deployment metadata at this stage.

- [x] **Step 4: Run the tests again**

Run: `dotnet test tests/Template.AppHost.PublishTests/Template.AppHost.PublishTests.csproj --configuration Release`
Expected: PASS for both selected namespaces.

- [x] **Step 5: Record the test harness in the Kener deployment commit**

```text
test: add Kener Kubernetes publish boundary
```

### Task 2: Kener, Redis, CloudNativePG, secrets, and ingress

**Files:**
- Modify: `src/Template.AppHost/AppHost.cs`
- Modify: `Directory.Packages.props`
- Modify: `src/Template.AppHost/Template.AppHost.csproj`
- Modify: `src/Template.AppHost/appsettings.Kubernetes.Production.json`
- Modify: `src/Template.AppHost/appsettings.Kubernetes.Staging.json`

**Interfaces:**
- Consumes: the selected `KubernetesEnvironmentResource` from Task 1 and its environment-specific configuration.
- Produces: `kener` Deployment, `kener-redis` StatefulSet, `kener-redis-data` PVC, CloudNativePG Database and `kener-database` ExternalSecret, Kener mail and generated-secret ExternalSecrets, and `kener-ingress`.

- [x] **Step 1: Extend the failing chart tests for isolated application resources**

```csharp
[Theory]
[InlineData("Kubernetes-Production", "status.citizenid.space", "citizenid-production-status", "citizenid-status-staging", "status.citizenid.dev")]
[InlineData("Kubernetes-Staging", "status.citizenid.dev", "citizenid-staging-status", "citizenid-status-production", "status.citizenid.space")]
public async Task Publish_contains_an_environment_isolated_Kener_application(
    string environment,
    string expectedHost,
    string expectedDatabase,
    string excludedNamespace,
    string excludedHost)
{
    await using var chart = await AspirePublishFixture.PublishAsync(environment, TestContext.Current.CancellationToken);
    Assert.True(chart.ExitCode == 0, chart.Output);

    var artifacts = await chart.ReadAllTemplatesAsync(TestContext.Current.CancellationToken);

    Assert.Contains(expectedHost, artifacts, StringComparison.Ordinal);
    Assert.Contains(expectedDatabase, artifacts, StringComparison.Ordinal);
    Assert.Contains("rajnandan1/kener", artifacts, StringComparison.Ordinal);
    Assert.Contains("postgresql://", artifacts, StringComparison.Ordinal);
    Assert.Contains("SMTP_HOST", artifacts, StringComparison.Ordinal);
    Assert.DoesNotContain(excludedNamespace, artifacts, StringComparison.Ordinal);
    Assert.DoesNotContain(excludedHost, artifacts, StringComparison.Ordinal);
}
```

- [x] **Step 2: Run the tests to verify the application assertions fail**

Run: `dotnet test tests/Template.AppHost.PublishTests/Template.AppHost.PublishTests.csproj --configuration Release`
Expected: FAIL because the selected chart lacks Kener, Redis, CloudNativePG, External Secrets, and ingress resources.

- [x] **Step 3: Add the application resource graph and configuration**

```csharp
var database = builder.AddPostgres("database")
    .ExcludeFromManifest()
    .WithCloudNativePostgresDatabase(x => x
        .WithConfigurationFrom(builder.Configuration));

var redisPassword = builder.AddParameter("redis-password", secret: true);
var redis = builder.AddRedis("kener-redis", password: redisPassword)
    .WithImageTag("8-alpine")
    .PublishAsKubernetesService(ConfigureRedis);

var kener = builder.AddContainer("kener", "rajnandan1/kener", "v4.1.5-alpine")
    .WithHttpEndpoint(targetPort: 3000)
    .WithExternalHttpEndpoints()
    .PublishAsKubernetesService(ConfigureKener);
```

Add `Aspire.Hosting.Redis` `13.5.4`, `Arkanis.Aspire.Hosting.Extensions.Kubernetes.CloudNativePostgres` `1.0.0-dev.20`, and `Arkanis.Aspire.Hosting.Extensions.Kubernetes.ExternalSecrets` `1.0.0-dev.20`.
Configure the two JSON files with their exact hosts, database identities, `onepassword-connect`, `postgres-production-credentials`, Longhorn `longhorn-ext4-r2`, and `op://` references using the supplied 1Password vault and item identifiers with only `username` and `password` fields.
Inject the CloudNativePG Secret key as `DATABASE_URL` through `WithKubernetesConnectionString` and a `postgresql://` template using ESO `urlquery` for username and password.
Configure `KENER_SECRET_KEY` and Redis password from `PasswordGenerator` configuration with `CreatedOnce` rotation.
Map the existing Purelymail `username` to `SMTP_USER` and `SMTP_SENDER`, and `password` to `SMTP_PASSWORD`, without resolving either value.
Set non-secret `DATABASE_POOL_MAX=5`, `DATABASE_WORKER_POOL_MAX=3`, `SMTP_HOST=smtp.purelymail.com`, `SMTP_PORT=465`, `SMTP_SECURE=1`, and the selected environment `ORIGIN`.
Set Redis resource requests to `cpu: 100m`, `memory: 256Mi`, limits to `cpu: 500m`, `memory: 512Mi`, AOF `appendonly yes`, `appendfsync everysec`, authenticated probes, and an emitted RWO `2Gi` `longhorn-ext4-r2` PVC mounted at `/data`.

- [x] **Step 4: Run the tests to verify the application resource graph**

Run: `dotnet test tests/Template.AppHost.PublishTests/Template.AppHost.PublishTests.csproj --configuration Release`
Expected: PASS with isolated production and staging application identities.

- [x] **Step 5: Record the configuration boundary in the Kener deployment commit**

```text
feat: add Kener Kubernetes application resources
```

### Task 3: Kener availability and scheduling controls

**Files:**
- Modify: `src/Template.AppHost/AppHost.cs`
- Modify: `docs/superpowers/specs/2026-09-23-kener-production-kubernetes-design.md`

**Interfaces:**
- Consumes: Kubernetes configuration created in Task 2 and the generated `KubernetesResource` callbacks from Aspire.
- Produces: a two-replica `kener` Deployment with probes, explicit resource constraints, rolling-update policy, preferred zone and hostname anti-affinity, and a `policy/v1` PDB.

- [x] **Step 1: Extend the failing artifact tests for workload behavior**

```csharp
[Fact]
public async Task Production_publish_contains_the_documented_availability_controls()
{
    await using var chart = await AspirePublishFixture.PublishAsync("Kubernetes-Production", TestContext.Current.CancellationToken);
    Assert.True(chart.ExitCode == 0, chart.Output);

    var artifacts = await chart.ReadAllTemplatesAsync(TestContext.Current.CancellationToken);

    Assert.Contains("kind: PodDisruptionBudget", artifacts, StringComparison.Ordinal);
    Assert.Contains("minAvailable: 1", artifacts, StringComparison.Ordinal);
    Assert.Contains("topology.kubernetes.io/zone", artifacts, StringComparison.Ordinal);
    Assert.Contains("kubernetes.io/hostname", artifacts, StringComparison.Ordinal);
    Assert.Contains("appendonly", artifacts, StringComparison.Ordinal);
    Assert.Contains("DATABASE_POOL_MAX", artifacts, StringComparison.Ordinal);
    Assert.DoesNotContain("REDIS_CLUSTER", artifacts, StringComparison.Ordinal);
}
```

- [x] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/Template.AppHost.PublishTests/Template.AppHost.PublishTests.csproj --configuration Release`
Expected: FAIL because the generated chart lacks the PDB and the documented Kener deployment controls.

- [x] **Step 3: Implement the resource graph and callbacks**

```csharp
deployment.Spec.Replicas = 2;
deployment.Spec.Strategy.RollingUpdate.MaxUnavailable = "0";
deployment.Spec.Strategy.RollingUpdate.MaxSurge = "1";
deployment.Spec.Template.Metadata.Labels["app.kubernetes.io/component"] = "kener";
deployment.WithPreferredPodAppComponentAntiAffinity("topology.kubernetes.io/zone", 100);
deployment.WithPreferredPodAppComponentAntiAffinity("kubernetes.io/hostname", 50);
```

Set Kener resource requests to `cpu: 1`, `memory: 1Gi`, limits to `cpu: 2`, `memory: 2Gi`, two replicas, zero unavailable rolling updates, `/healthcheck` startup, liveness, and readiness probes, preferred zone anti-affinity weight 100, preferred hostname anti-affinity weight 50, and no required terms.
Add the PDB as an AppHost-owned `BaseKubernetesResource` in the Kener `KubernetesResource.AdditionalResources` collection because the selected C# Kubernetes package does not expose the documented `AddManifest` method.
Update the design specification to name this verified `AdditionalResources` mechanism while retaining its no-custom-pipeline constraint.

- [x] **Step 4: Run the focused tests to verify the complete resource graph**

Run: `dotnet test tests/Template.AppHost.PublishTests/Template.AppHost.PublishTests.csproj --configuration Release`
Expected: PASS with two rendered, environment-isolated charts.

- [x] **Step 5: Record the deployed resource graph in the Kener deployment commit**

```text
feat: add Kener Kubernetes availability controls
```

### Task 4: Operator documentation and whole-change verification

**Files:**
- Modify: `README.md`
- Modify: `docs/superpowers/plans/2026-09-23-kener-kubernetes-deployment.md`

**Interfaces:**
- Consumes: the publication command and environment names implemented in Tasks 1 to 3.
- Produces: operator-safe, non-applying chart-preview commands and a completed implementation record.

- [x] **Step 1: Add non-applying operator documentation**

```powershell
dotnet aspire publish --apphost src/Template.AppHost/Template.AppHost.csproj --environment Kubernetes-Production --output-path artifacts/kener-production --non-interactive
dotnet aspire publish --apphost src/Template.AppHost/Template.AppHost.csproj --environment Kubernetes-Staging --output-path artifacts/kener-staging --non-interactive
```

Explain that each command emits a chart only and does not create 1Password items, apply Helm resources, or alter the cluster.

- [x] **Step 2: Run the full available verification set**

Run: `dotnet test tests/Template.AppHost.PublishTests/Template.AppHost.PublishTests.csproj --configuration Release`
Expected: PASS.

Run: `dotnet build src/Template.AppHost/Template.AppHost.csproj --configuration Release`
Expected: PASS.

Run: `git diff --check`
Expected: PASS.

- [x] **Step 3: Record the pre-existing template-suite limitation**

Run: `dotnet test Template.slnx --configuration Release --no-build`
Observed: the five Kener chart publication tests pass, while the existing Service integration-test target returns `VSTestTask` failure without an error after the staged removal of the Service and Contracts projects.
The broader template suite therefore remains outside this deployment change's passing verification scope.
