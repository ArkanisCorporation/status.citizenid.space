using Arkanis.Aspire.Hosting.Extensions._1Password;
using Arkanis.Aspire.Hosting.Extensions.Kubernetes;
using Arkanis.Aspire.Hosting.Extensions.Kubernetes.CloudNativePostgres;
using Arkanis.Aspire.Hosting.Extensions.Kubernetes.ExternalSecrets;
using Arkanis.Aspire.Hosting.Extensions.Kubernetes.KubernetesConnections;
using Arkanis.Aspire.Hosting.Extensions.Kubernetes.KubernetesIngresses;
using Arkanis.Aspire.Hosting.Extensions.Kubernetes.PersistentVolumeClaims;
using Aspire.Hosting.Kubernetes;
using Aspire.Hosting.Kubernetes.Resources;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);
if (builder.Environment.IsKubernetesDeployment())
{
    DeploymentConfiguration.ConfigureTarget(builder);

    var configuration = builder.Configuration;
    var kubernetesNamespace = Require(configuration, "Kubernetes:Namespace");
    var originValue = Require(configuration, "Kener:Origin");
    if (
        !Uri.TryCreate(originValue, UriKind.Absolute, out var origin)
        || origin.Scheme != Uri.UriSchemeHttps
        || origin.AbsolutePath != "/"
        || !string.IsNullOrEmpty(origin.Query)
        || !string.IsNullOrEmpty(origin.Fragment)
    )
    {
        throw new InvalidOperationException(
            "Kener:Origin must be an HTTPS origin without a path, query, or fragment."
        );
    }

    Require(configuration, "Kubernetes:CloudNativePostgres:Resources:database:ClusterName");
    Require(configuration, "Kubernetes:CloudNativePostgres:Resources:database:DatabaseName");
    Require(configuration, "Kubernetes:CloudNativePostgres:Resources:database:Owner");
    Require(
        configuration,
        "Kubernetes:CloudNativePostgres:Resources:database:CredentialsSecret:SourceSecretName"
    );
    Require(configuration, "Kubernetes:Secrets:SecretStore:Name");
    Require(configuration, "Parameters:smtp-username");
    Require(configuration, "Parameters:smtp-password");

    var kubernetes = builder
        .AddKubernetesEnvironment("kener-kubernetes")
        .WithDashboard(false)
        .WithHelm(helm => helm.WithNamespace(kubernetesNamespace));
    var database = builder
        .AddPostgres("database")
        .ExcludeFromManifest()
        .WithCloudNativePostgresDatabase(database =>
            database
                .WithConfigurationFrom(configuration)
                .WithCredentialsConnectionStringTemplate(annotation =>
                    $"{{{{ `postgresql://{{{{ .username | urlquery }}}}:{{{{ .password | urlquery }}}}@{{{{ .host }}}}:{{{{ .port }}}}/{annotation.Credentials.DatabaseName}` }}}}"
                )
        );
    // Aspire persists all parameters before ESO materializes generated credentials.
    // These defaults satisfy only that deployment-state contract and are never published into Kubernetes.
    var redisPassword = builder.AddParameter("redis-auth", string.Empty, secret: true);
    var webSecretKey = builder.AddParameter("web-secret-key", string.Empty, secret: true);
    var smtpUsername = builder.AddParameter("smtp-username", true);
    var smtpPassword = builder.AddParameter("smtp-password", true);

    var redis = builder
        .AddRedis("redis", password: redisPassword)
        .WithImageTag("8-alpine")
        .WithEnvironment("REDIS_PASSWORD", redisPassword)
        .WithNewKubernetesPersistentVolumeClaim(
            "data",
            "redis-data",
            "/data",
            claim =>
                claim
                    .WithReadWriteOnce()
                    .WithStorageRequest("2Gi")
                    .WithStorageClass("longhorn-ext4-r2")
        )
        .PublishAsKubernetesService(resource =>
        {
            resource.Service!.Metadata.Name = "redis";
            ConfigureRedis(resource);
        })
        .WithComputeEnvironment(kubernetes);

    builder
        .AddContainer("web", "rajnandan1/kener", "v4.1.5-alpine")
        .WithHttpEndpoint(name: "http", targetPort: 3000)
        .WithExternalHttpEndpoints()
        .WithEnvironment("DATABASE_POOL_MAX", "5")
        .WithEnvironment("DATABASE_WORKER_POOL_MAX", "3")
        .WithEnvironment("SMTP_HOST", "smtp.purelymail.com")
        .WithEnvironment("SMTP_PORT", "465")
        .WithEnvironment("SMTP_SECURE", "1")
        .WithEnvironment("SMTP_USER", smtpUsername)
        .WithEnvironment("SMTP_SENDER", smtpUsername)
        .WithEnvironment("SMTP_PASSWORD", smtpPassword)
        .WithEnvironment("KENER_SECRET_KEY", webSecretKey)
        .WithEnvironment("REDIS_PASSWORD", redisPassword)
        .WithEnvironment("ORIGIN", originValue)
        .WithHttpHealthCheck("/healthcheck")
        .AddAllHealthCheckProbes(
            new ResourceHealthCheckProbeOptions
            {
                PeriodSeconds = 5,
                TimeoutSeconds = 3,
                FailureThreshold = 30,
            },
            new ResourceHealthCheckProbeOptions
            {
                PeriodSeconds = 10,
                TimeoutSeconds = 3,
                FailureThreshold = 3,
            },
            new ResourceHealthCheckProbeOptions
            {
                PeriodSeconds = 10,
                TimeoutSeconds = 3,
                FailureThreshold = 3,
            }
        )
        .PublishAsKubernetesService(ConfigureWeb)
        .WithKubernetesConnectionString(
            database,
            "WebDatabase",
            secretReference =>
            {
                secretReference.SecretName = "web-database";
                secretReference.SecretKey = "DATABASE_URL";
                secretReference.EnvironmentVariableName = "DATABASE_URL";
            }
        )
        .WithKubernetesIngress(
            "web-ingress",
            ingress => ingress.WithConfigurationFrom(configuration).AtHost(origin.Host)
        )
        .WithComputeEnvironment(kubernetes)
        .WithKubernetesEnvironmentVariables(environment =>
            environment.WithVariable(
                "REDIS_URL",
                value =>
                    value
                        .AsUri("redis")
                        .WithRawPasswordEnvironment("REDIS_PASSWORD")
                        .WithServiceEndpoint(redis)
            )
        );

    var externalSecretsOptions = ExternalSecretsOptions.FromConfiguration(configuration);
    kubernetes.WithExternalSecrets(secrets =>
        secrets
            .WithSecretStore(externalSecretsOptions.SecretStore)
            .WithParameterSource(
                webSecretKey,
                source =>
                    source.UsePasswordGenerator(password =>
                        password.WithLength(64).WithDigits(8).WithSymbols(8).CreatedOnce()
                    )
            )
            .WithParameterSource(
                redisPassword,
                source =>
                    source.UsePasswordGenerator(password =>
                        password
                            .WithLength(32)
                            .WithDigits(4)
                            .WithSymbols(4)
                            .WithSymbolCharacters("-._~")
                            .CreatedOnce()
                    )
            )
    );
}
else
{
    if (builder.Environment.EnvironmentName.StartsWith("Kubernetes", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Malformed Kubernetes deployment environment '{builder.Environment.EnvironmentName}'.");
    }

    await builder.Use1PasswordAsync("arkaniscorp.1password.com");
}

await builder.Build().RunAsync();

static string Require(IConfiguration configuration, string key) =>
    string.IsNullOrWhiteSpace(configuration[key])
        ? throw new InvalidOperationException(
            $"Kubernetes deployment configuration must define {key}."
        )
        : configuration[key]!;

static void ConfigureRedis(KubernetesResource resource)
{
    if (resource.Workload is not StatefulSet statefulSet)
    {
        throw new InvalidOperationException(
            "The Kener Redis resource must publish as a StatefulSet."
        );
    }

    var container = statefulSet.Spec.Template.Spec.Containers.Single();
    statefulSet.Spec.Template.Spec.AutomountServiceAccountToken = false;
    container.Args.Clear();
    container.Args.Add("-c");
    container.Args.Add(
        "exec redis-server --requirepass \"$REDIS_PASSWORD\" --appendonly yes --appendfsync everysec"
    );
    container.Resources = new ResourceRequirementsV1
    {
        Requests = { ["cpu"] = "100m", ["memory"] = "256Mi" },
        Limits = { ["cpu"] = "500m", ["memory"] = "512Mi" },
    };
    container.LivenessProbe = CreateRedisProbe(10, 3);
    container.ReadinessProbe = CreateRedisProbe(0, 3);
}

static void ConfigureWeb(KubernetesResource resource)
{
    if (resource.Workload is not Deployment deployment)
    {
        throw new InvalidOperationException(
            "The web application must publish as a Deployment."
        );
    }

    deployment.Spec.Replicas = 2;
    deployment.Spec.Strategy.Type = "RollingUpdate";
    deployment.Spec.Strategy.RollingUpdate.MaxUnavailable = 0;
    deployment.Spec.Strategy.RollingUpdate.MaxSurge = 1;
    deployment.Spec.Template.Metadata.Labels["app.kubernetes.io/component"] = "web";
    deployment.Spec.Template.Spec.AutomountServiceAccountToken = false;
    deployment.WithPreferredPodAppComponentAntiAffinity("topology.kubernetes.io/zone", 100);
    deployment.WithPreferredPodAppComponentAntiAffinity("kubernetes.io/hostname", 50);

    var container = deployment.Spec.Template.Spec.Containers.Single();
    container.Resources = new ResourceRequirementsV1
    {
        Requests = { ["cpu"] = "1", ["memory"] = "1Gi" },
        Limits = { ["cpu"] = "2", ["memory"] = "2Gi" },
    };
    SetHealthCheckPath(container.LivenessProbe);
    SetHealthCheckPath(container.ReadinessProbe);
    SetHealthCheckPath(container.StartupProbe);

    var selector = new LabelSelectorV1();
    selector.MatchLabels["app.kubernetes.io/component"] = "web";
    resource.AdditionalResources.Add(
        new PodDisruptionBudget
        {
            Metadata = new ObjectMetaV1 { Name = "web-pdb" },
            Spec = new PodDisruptionBudgetSpec { MinAvailable = 1, Selector = selector },
        }
    );
}

static ProbeV1 CreateRedisProbe(int initialDelaySeconds, int failureThreshold) =>
    new()
    {
        Exec = new ExecActionV1
        {
            Command = { "sh", "-c", "redis-cli --no-auth-warning -a \"$REDIS_PASSWORD\" ping" },
        },
        InitialDelaySeconds = initialDelaySeconds,
        PeriodSeconds = 10,
        TimeoutSeconds = 3,
        FailureThreshold = failureThreshold,
    };

static void SetHealthCheckPath(ProbeV1? probe)
{
    if (probe?.HttpGet is not { } httpGet)
    {
        throw new InvalidOperationException("Kener health probes must use HTTP GET actions.");
    }

    httpGet.Path = "/healthcheck";
    httpGet.Scheme = "HTTP";
}

/// <summary>
/// Selects and validates the settings for a Kubernetes publish target.
/// </summary>
internal static class DeploymentConfiguration
{
    /// <summary>
    /// Loads the selected target's settings and rejects inherited deployment settings in test publishes.
    /// </summary>
    /// <param name="builder">The AppHost builder whose configuration is updated.</param>
    /// <exception cref="InvalidOperationException">The target or its configuration is unsupported.</exception>
    internal static void ConfigureTarget(IDistributedApplicationBuilder builder)
    {
        var environmentName = builder.Environment.EnvironmentName;
        var target = builder.Environment.GetDeploymentEnvironment();
        if (target?.EnvironmentType is not ("Staging" or "Production" or "PublishTest"))
        {
            throw new InvalidOperationException($"Unsupported Kubernetes deployment environment '{environmentName}'.");
        }

        if (target.EnvironmentType == "PublishTest")
        {
            var fixturePath = Environment.GetEnvironmentVariable("KENER_PUBLISH_TEST_FIXTURE");
            if (string.IsNullOrWhiteSpace(fixturePath) || !Path.IsPathFullyQualified(fixturePath))
            {
                throw new InvalidOperationException("Kubernetes-PublishTest requires an absolute KENER_PUBLISH_TEST_FIXTURE path.");
            }

            var inheritedDeploymentKeys = builder.Configuration
                .AsEnumerable()
                .Where(static entry =>
                    entry.Key.StartsWith("Kubernetes:", StringComparison.OrdinalIgnoreCase)
                    || entry.Key.StartsWith("Kener:", StringComparison.OrdinalIgnoreCase)
                    || entry.Key.StartsWith("Parameters:", StringComparison.OrdinalIgnoreCase)
                )
                .Select(static entry => entry.Key)
                .ToArray();
            if (inheritedDeploymentKeys.Length > 0)
            {
                throw new InvalidOperationException(
                    $"Kubernetes-PublishTest rejects inherited deployment settings: {string.Join(", ", inheritedDeploymentKeys)}."
                );
            }

            builder.Configuration.AddJsonFile(fixturePath, optional: false, reloadOnChange: false);
            ValidateTargetSetting(
                builder.Configuration,
                environmentName,
                "Kubernetes:Namespace",
                "citizenid-status-publish-test"
            );
            ValidateTargetSetting(
                builder.Configuration,
                environmentName,
                "Kener:Origin",
                "https://status.example.test"
            );
        }
        else
        {
            builder.AddDeploymentEnvironmentConfiguration(includeLocalSettings: false);
            var suffix = target.EnvironmentType.ToLowerInvariant();
            var expectedNamespace = $"citizenid-status-{suffix}";
            ValidateTargetSetting(builder.Configuration, environmentName, "Kubernetes:Namespace", expectedNamespace);
            ValidateTargetSetting(
                builder.Configuration,
                environmentName,
                "Kener:Origin",
                target.EnvironmentType == "Staging" ? "https://status.citizenid.dev" : "https://status.citizenid.space"
            );
            ValidateTargetSetting(
                builder.Configuration,
                environmentName,
                "Kubernetes:CloudNativePostgres:Resources:database:DatabaseName",
                $"citizenid-{suffix}-status"
            );
            ValidateTargetSetting(
                builder.Configuration,
                environmentName,
                "Kubernetes:CloudNativePostgres:Resources:database:Owner",
                expectedNamespace
            );
            ValidateTargetSetting(
                builder.Configuration,
                environmentName,
                "Kubernetes:CloudNativePostgres:Resources:database:CredentialsSecret:SourceSecretName",
                $"{expectedNamespace}-credentials"
            );
        }
    }

    private static void ValidateTargetSetting(
        ConfigurationManager configuration,
        string environmentName,
        string key,
        string expectedValue
    )
    {
        if (!string.Equals(configuration[key], expectedValue, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{environmentName} requires {key} '{expectedValue}', but configuration supplied '{configuration[key]}'."
            );
        }
    }
}
