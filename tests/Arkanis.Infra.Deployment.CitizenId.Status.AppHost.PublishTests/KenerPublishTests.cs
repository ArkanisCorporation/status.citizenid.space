namespace Arkanis.Infra.Deployment.CitizenId.Status.AppHost.PublishTests;

using System.Text.Json;

/// <summary>
/// Verifies the environment-specific Kener publish artifacts.
/// </summary>
public sealed class KenerPublishTests
{
    /// <summary>
    /// Verifies that External Secrets emission resolves the generated password parameters.
    /// </summary>
    /// <param name="environment">The AppHost deployment environment.</param>
    [Theory]
    [InlineData("Kubernetes-Production")]
    [InlineData("Kubernetes-Staging")]
    public async Task External_Secrets_emission_materializes_generated_password_parameters(string environment)
    {
        await using var chart = await AspirePublishFixture.EmitExternalSecretsAsync(
            environment,
            TestContext.Current.CancellationToken
        );

        Assert.True(chart.ExitCode == 0, chart.Output);
        Assert.DoesNotContain("Failed to save parameter redis-password", chart.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Failed to save parameter web-secret-key", chart.Output, StringComparison.Ordinal);

        var artifacts = await chart.ReadAllTemplatesAsync(TestContext.Current.CancellationToken);

        Assert.Contains("apiVersion: external-secrets.io/v1", artifacts, StringComparison.Ordinal);
        Assert.Contains("kind: ExternalSecret", artifacts, StringComparison.Ordinal);
        Assert.Contains("KENER_SECRET_KEY", artifacts, StringComparison.Ordinal);
        Assert.Contains("REDIS_PASSWORD", artifacts, StringComparison.Ordinal);
        Assert.Contains("generatorRef", artifacts, StringComparison.Ordinal);
        Assert.Contains("secretKeyRef", artifacts, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that publishing selects the requested Kubernetes namespace.
    /// </summary>
    /// <param name="environment">The AppHost deployment environment.</param>
    /// <param name="expectedNamespace">The namespace expected for the Helm release.</param>
    [Theory]
    [InlineData("Kubernetes-Production", "citizenid-status-production")]
    [InlineData("Kubernetes-Staging", "citizenid-status-staging")]
    public async Task Publish_uses_the_selected_Kubernetes_namespace(
        string environment,
        string expectedNamespace
    )
    {
        await using var chart = await AspirePublishFixture.PublishAsync(
            environment,
            TestContext.Current.CancellationToken
        );

        Assert.True(chart.ExitCode == 0, chart.Output);

        Assert.Equal(expectedNamespace, ReadHelmReleaseNamespace(environment));
    }

    /// <summary>
    /// Verifies that each published chart contains only the selected Kener application identity.
    /// </summary>
    /// <param name="environment">The AppHost deployment environment.</param>
    /// <param name="expectedHost">The expected public status hostname.</param>
    /// <param name="expectedDatabase">The expected CloudNativePG database name.</param>
    /// <param name="expectedOwner">The provisioned CloudNativePG role that owns the database.</param>
    /// <param name="expectedCredentialsSecret">The provisioned CloudNativePG role credential source.</param>
    /// <param name="excludedHost">The hostname belonging to the other environment.</param>
    [Theory]
    [InlineData(
        "Kubernetes-Production",
        "status.citizenid.space",
        "citizenid-production-status",
        "citizenid-status-production",
        "citizenid-status-production-credentials",
        "status.citizenid.dev"
    )]
    [InlineData(
        "Kubernetes-Staging",
        "status.citizenid.dev",
        "citizenid-staging-status",
        "citizenid-status-staging",
        "citizenid-status-staging-credentials",
        "status.citizenid.space"
    )]
    public async Task Publish_contains_an_environment_isolated_Kener_application(
        string environment,
        string expectedHost,
        string expectedDatabase,
        string expectedOwner,
        string expectedCredentialsSecret,
        string excludedHost
    )
    {
        await using var chart = await AspirePublishFixture.PublishAsync(
            environment,
            TestContext.Current.CancellationToken
        );

        Assert.True(chart.ExitCode == 0, chart.Output);

        var artifacts = await chart.ReadAllTemplatesAsync(TestContext.Current.CancellationToken);
        var databaseExternalSecret = await chart.ReadArtifactAsync(
            Path.Combine("templates", "database", "externalsecret.yaml"),
            TestContext.Current.CancellationToken
        );

        Assert.Contains(expectedHost, artifacts, StringComparison.Ordinal);
        Assert.Contains(expectedDatabase, artifacts, StringComparison.Ordinal);
        Assert.Contains($"owner: {expectedOwner}", artifacts, StringComparison.Ordinal);
        Assert.Contains("name: production", artifacts, StringComparison.Ordinal);
        Assert.Contains($"key: {expectedCredentialsSecret}", artifacts, StringComparison.Ordinal);
        Assert.Contains("name: web-database", databaseExternalSecret, StringComparison.Ordinal);
        Assert.Contains("refreshInterval: 1m", databaseExternalSecret, StringComparison.Ordinal);
        Assert.Contains("rajnandan1/kener", artifacts, StringComparison.Ordinal);
        Assert.Contains("postgresql://", artifacts, StringComparison.Ordinal);
        Assert.Contains("SMTP_HOST", artifacts, StringComparison.Ordinal);
        Assert.DoesNotContain(excludedHost, artifacts, StringComparison.Ordinal);
        Assert.DoesNotContain("kener-kubernetes-dashboard", artifacts, StringComparison.Ordinal);
        Assert.DoesNotContain("op://", artifacts, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that production output includes the documented availability and persistence controls.
    /// </summary>
    [Fact]
    public async Task Production_publish_contains_the_documented_availability_controls()
    {
        await using var chart = await AspirePublishFixture.PublishAsync(
            "Kubernetes-Production",
            TestContext.Current.CancellationToken
        );

        Assert.True(chart.ExitCode == 0, chart.Output);

        var artifacts = await chart.ReadAllTemplatesAsync(TestContext.Current.CancellationToken);

        Assert.Contains("kind: \"PodDisruptionBudget\"", artifacts, StringComparison.Ordinal);
        Assert.Contains("minAvailable: 1", artifacts, StringComparison.Ordinal);
        Assert.Contains("topology.kubernetes.io/zone", artifacts, StringComparison.Ordinal);
        Assert.Contains("kubernetes.io/hostname", artifacts, StringComparison.Ordinal);
        Assert.Contains("replicas: 2", artifacts, StringComparison.Ordinal);
        Assert.Contains("maxUnavailable: 0", artifacts, StringComparison.Ordinal);
        Assert.Contains("appendonly", artifacts, StringComparison.Ordinal);
        Assert.Contains("appendfsync", artifacts, StringComparison.Ordinal);
        Assert.Contains("redis-data", artifacts, StringComparison.Ordinal);
        Assert.Contains("longhorn-ext4-r2", artifacts, StringComparison.Ordinal);
        Assert.Contains("automountServiceAccountToken: false", artifacts, StringComparison.Ordinal);
        Assert.Contains("DATABASE_POOL_MAX", artifacts, StringComparison.Ordinal);
        Assert.Contains("DATABASE_URL", artifacts, StringComparison.Ordinal);
        Assert.Contains("refreshPolicy: CreatedOnce", artifacts, StringComparison.Ordinal);
        Assert.Contains("/healthcheck", artifacts, StringComparison.Ordinal);
        Assert.DoesNotContain("/healthz/", artifacts, StringComparison.Ordinal);
        Assert.DoesNotContain("REDIS_CLUSTER", artifacts, StringComparison.Ordinal);
    }

    private static string ReadHelmReleaseNamespace(string environment)
    {
        var repositoryRoot = FindRepositoryRoot();
        var configurationPath = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Arkanis.Infra.Deployment.CitizenId.Status.AppHost",
            $"appsettings.{environment.Replace('-', '.')}.json"
        );
        using var document = JsonDocument.Parse(File.ReadAllText(configurationPath));

        return document.RootElement.GetProperty("Kubernetes").GetProperty("Namespace").GetString()
               ?? throw new InvalidOperationException($"The deployment configuration '{configurationPath}' does not define Kubernetes:Namespace.");
    }

    private static DirectoryInfo FindRepositoryRoot()
    {
        for (
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent
        )
        {
            if (
                File.Exists(
                    Path.Combine(
                        directory.FullName,
                        "Arkanis.Infra.Deployment.CitizenId.Status.slnx"
                    )
                )
            )
            {
                return directory;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root containing Arkanis.Infra.Deployment.CitizenId.Status.slnx.");
    }
}
