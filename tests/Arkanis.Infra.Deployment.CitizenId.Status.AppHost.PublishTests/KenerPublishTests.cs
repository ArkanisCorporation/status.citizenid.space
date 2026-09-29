namespace Arkanis.Infra.Deployment.CitizenId.Status.AppHost.PublishTests;

using System.Text.Json;

/// <summary>
/// Verifies Kener artifacts from the synthetic publish fixture.
/// </summary>
public sealed class KenerPublishTests
{
    /// <summary>
    /// Verifies that an inherited deployment setting cannot alter a synthetic fixture.
    /// </summary>
    [Fact]
    public async Task Test_AppHost_rejects_inherited_deployment_settings()
    {
        await using var chart = await AspirePublishFixture.PublishWithInheritedSettingAsync(
            TestContext.Current.CancellationToken
        );

        Assert.NotEqual(0, chart.ExitCode);
        Assert.Contains(
            "rejects inherited deployment settings",
            chart.Output,
            StringComparison.Ordinal
        );
        Assert.Empty(await chart.ReadAllTemplatesAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Verifies that External Secrets emission resolves generated password parameters.
    /// </summary>
    [Fact]
    public async Task External_Secrets_emission_materializes_generated_password_parameters()
    {
        await using var chart = await AspirePublishFixture.EmitExternalSecretsAsync(
            TestContext.Current.CancellationToken
        );

        Assert.True(chart.ExitCode == 0, chart.Output);
        Assert.DoesNotContain(
            "Failed to save parameter redis-auth",
            chart.Output,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "Failed to save parameter web-secret-key",
            chart.Output,
            StringComparison.Ordinal
        );

        var artifacts = await chart.ReadAllTemplatesAsync(TestContext.Current.CancellationToken);

        Assert.Contains("apiVersion: external-secrets.io/v1", artifacts, StringComparison.Ordinal);
        Assert.Contains("kind: ExternalSecret", artifacts, StringComparison.Ordinal);
        Assert.Contains("KENER_SECRET_KEY", artifacts, StringComparison.Ordinal);
        Assert.Contains("REDIS_PASSWORD", artifacts, StringComparison.Ordinal);
        Assert.Contains("generatorRef", artifacts, StringComparison.Ordinal);
        Assert.Contains("secretKeyRef", artifacts, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the published chart contains only the synthetic test identity.
    /// </summary>
    [Fact]
    public async Task Publish_contains_only_the_synthetic_application()
    {
        await using var chart = await AspirePublishFixture.PublishAsync(
            TestContext.Current.CancellationToken
        );

        Assert.True(chart.ExitCode == 0, chart.Output);

        var artifacts = await chart.ReadAllTemplatesAsync(TestContext.Current.CancellationToken);
        var databaseExternalSecret = await chart.ReadArtifactAsync(
            Path.Combine("templates", "database", "externalsecret.yaml"),
            TestContext.Current.CancellationToken
        );

        Assert.Contains("status.example.test", artifacts, StringComparison.Ordinal);
        Assert.Contains("citizenid-publish-test-status", artifacts, StringComparison.Ordinal);
        Assert.Contains("owner: citizenid-status-publish-test", artifacts, StringComparison.Ordinal);
        Assert.Contains("name: fixture-cluster", artifacts, StringComparison.Ordinal);
        Assert.Contains("key: citizenid-status-publish-test-credentials", artifacts, StringComparison.Ordinal);
        Assert.Contains("name: web-database", databaseExternalSecret, StringComparison.Ordinal);
        Assert.Contains("refreshInterval: 1m", databaseExternalSecret, StringComparison.Ordinal);
        Assert.Contains("rajnandan1/kener", artifacts, StringComparison.Ordinal);
        Assert.Contains("postgresql://", artifacts, StringComparison.Ordinal);
        Assert.Contains("SMTP_HOST", artifacts, StringComparison.Ordinal);
        Assert.DoesNotContain("status.citizenid.space", artifacts, StringComparison.Ordinal);
        Assert.DoesNotContain("status.citizenid.dev", artifacts, StringComparison.Ordinal);
        Assert.DoesNotContain("citizenid-status-production", artifacts, StringComparison.Ordinal);
        Assert.DoesNotContain("citizenid-status-staging", artifacts, StringComparison.Ordinal);
        Assert.DoesNotContain("kener-kubernetes-dashboard", artifacts, StringComparison.Ordinal);
        Assert.DoesNotContain("op://", artifacts, StringComparison.Ordinal);

        var ownership = await chart.ReadArtifactAsync(
            "secret-ownership.json",
            TestContext.Current.CancellationToken
        );
        using var report = JsonDocument.Parse(ownership);
        var bindings = report.RootElement.GetProperty("Bindings").EnumerateArray().ToArray();
        Assert.Contains(
            bindings,
            binding =>
                binding.GetProperty("ResourceName").GetString() == "web"
                && binding.GetProperty("Key").GetString() == "REDIS_PASSWORD"
                && binding.GetProperty("SourceKind").GetString() == "PasswordGenerator"
                && binding.GetProperty("Owner").GetString() == "ExternalSecretsOperator"
        );
        Assert.Contains(
            bindings,
            binding =>
                binding.GetProperty("ResourceName").GetString() == "web"
                && binding.GetProperty("Key").GetString() == "KENER_SECRET_KEY"
                && binding.GetProperty("SourceKind").GetString() == "PasswordGenerator"
        );
    }

    /// <summary>
    /// Verifies that the test chart retains availability and persistence controls.
    /// </summary>
    [Fact]
    public async Task Publish_contains_the_documented_availability_controls()
    {
        await using var chart = await AspirePublishFixture.PublishAsync(
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
        var redisStatefulSet = await chart.ReadArtifactAsync(
            Path.Combine("templates", "redis", "statefulset.yaml"),
            TestContext.Current.CancellationToken
        );
        Assert.Contains(
            "exec redis-server --requirepass",
            redisStatefulSet,
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(
            "        - \"--requirepass\"",
            redisStatefulSet,
            StringComparison.Ordinal
        );
        Assert.Contains("redis-data", artifacts, StringComparison.Ordinal);
        Assert.Contains("longhorn-ext4-r2", artifacts, StringComparison.Ordinal);
        Assert.Contains("automountServiceAccountToken: false", artifacts, StringComparison.Ordinal);
        Assert.Contains("DATABASE_POOL_MAX", artifacts, StringComparison.Ordinal);
        Assert.Contains("DATABASE_URL", artifacts, StringComparison.Ordinal);
        Assert.Contains("refreshPolicy: CreatedOnce", artifacts, StringComparison.Ordinal);
        Assert.Contains("/healthcheck", artifacts, StringComparison.Ordinal);
        Assert.Contains(
            "redis://:$(REDIS_PASSWORD)@redis:6379",
            artifacts,
            StringComparison.Ordinal
        );
        var webDeployment = await chart.ReadArtifactAsync(
            Path.Combine("templates", "web", "deployment.yaml"),
            TestContext.Current.CancellationToken
        );
        var passwordSource = webDeployment.IndexOf(
            "name: redis-auth-generated-secrets",
            StringComparison.Ordinal
        );
        var redisUrl = webDeployment.IndexOf("name: \"REDIS_URL\"", StringComparison.Ordinal);
        Assert.True(passwordSource >= 0 && redisUrl > passwordSource, webDeployment);
        Assert.DoesNotContain("/healthz/", artifacts, StringComparison.Ordinal);
        Assert.DoesNotContain("REDIS_CLUSTER", artifacts, StringComparison.Ordinal);
    }
}
