using Arkanis.Aspire.Hosting.Extensions._1Password;
using Arkanis.Aspire.Hosting.Extensions.Kubernetes;
using Arkanis.Aspire.Hosting.Extensions.Kubernetes.CloudNativePostgres;
using Arkanis.Aspire.Hosting.Extensions.Kubernetes.ExternalSecrets;
using Arkanis.Aspire.Hosting.Extensions.Kubernetes.KubernetesIngresses;
using Arkanis.Aspire.Hosting.Extensions.Kubernetes.PersistentVolumeClaims;
using Aspire.Hosting.Kubernetes;
using Aspire.Hosting.Kubernetes.Resources;
using Microsoft.Extensions.Configuration;

var builder = DistributedApplication.CreateBuilder(args);

var isKubernetesDeployment = builder.Environment.EnvironmentName.StartsWith(
    "Kubernetes-",
    StringComparison.OrdinalIgnoreCase
);

if (isKubernetesDeployment)
{
    builder.Configuration.AddJsonFile(
        $"appsettings.{builder.Environment.EnvironmentName.Replace('-', '.')}.json",
        false,
        false
    );

    var kubernetesNamespace =
        builder.Configuration["Kubernetes:Namespace"]
        ?? throw new InvalidOperationException("Kubernetes deployment configuration must define Kubernetes:Namespace.");

    var kubernetes = builder
        .AddKubernetesEnvironment("kener-kubernetes")
        .WithDashboard(false)
        .WithHelm(helm => helm.WithNamespace(kubernetesNamespace));
    var database = builder
        .AddPostgres("database")
        .ExcludeFromManifest()
        .WithCloudNativePostgresDatabase(database =>
            database
                .WithConfigurationFrom(builder.Configuration)
                .WithCredentialsConnectionStringTemplate(annotation =>
                    $"{{{{ `postgresql://{{{{ .username | urlquery }}}}:{{{{ .password | urlquery }}}}@{{{{ .host }}}}:{{{{ .port }}}}/{annotation.Credentials.DatabaseName}` }}}}"
                )
        );
    // Aspire persists all parameters before ESO materializes generated credentials.
    // These defaults satisfy only that deployment-state contract and are never published into Kubernetes.
    var redisPassword = builder.AddParameter("redis-password", string.Empty, secret: true);
    var kenerSecretKey = builder.AddParameter("kener-secret-key", string.Empty, secret: true);
    var smtpUsername = builder.AddParameter("smtp-username", true);
    var smtpPassword = builder.AddParameter("smtp-password", true);

    var redis = builder
        .AddRedis("kener-redis")
        .WithImageTag("8-alpine")
        .WithNewKubernetesPersistentVolumeClaim(
            "data",
            "kener-redis-data",
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
        .WithComputeEnvironment(kubernetes)
        .WithKubernetesExternalSecretProjection(projection => projection
            .MapParameter("REDIS_PASSWORD", redisPassword)
        );

    var web = builder
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
        .WithEnvironment(
            "ORIGIN",
            builder.Configuration["Kener:Origin"]
            ?? throw new InvalidOperationException("Kubernetes deployment configuration must define Kener:Origin.")
        )
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
            "KenerDatabase",
            credentials =>
                credentials.WithConnectionStringTemplate(annotation =>
                    $"{{{{ `postgresql://{{{{ .username | urlquery }}}}:{{{{ .password | urlquery }}}}@{{{{ .host }}}}:{{{{ .port }}}}/{annotation.Credentials.DatabaseName}` }}}}"
                ),
            secretReference =>
            {
                secretReference.SecretName = "kener-database";
                secretReference.SecretKey = "DATABASE_URL";
                secretReference.EnvironmentVariableName = "DATABASE_URL";
            }
        )
        .WithKubernetesIngress(
            "kener-ingress",
            ingress => ingress.WithConfigurationFrom(builder.Configuration)
        )
        .WithComputeEnvironment(kubernetes)
        .WithKubernetesExternalSecretProjection(projection => projection
            .MapParameter("KENER_SECRET_KEY", kenerSecretKey)
            .MapParameter("REDIS_PASSWORD", redisPassword)
        );

    var externalSecretsOptions = ExternalSecretsOptions.FromConfiguration(builder.Configuration);
    kubernetes.WithExternalSecrets(secrets => secrets
        .WithSecretStore(externalSecretsOptions.SecretStore)
        .WithParameterSource(
            kenerSecretKey,
            source => source
                .UsePasswordGenerator(password => password
                    .WithLength(64)
                    .WithDigits(8)
                    .WithSymbols(8)
                    .CreatedOnce()
                )
        )
        .WithParameterSource(
            redisPassword,
            source => source
                .UsePasswordGenerator(password => password
                    .WithLength(32)
                    .WithDigits(4)
                    .WithSymbols(4)
                    .CreatedOnce()
                )
        )
    );
}
else
{
    await builder.Use1PasswordAsync("arkaniscorp.1password.com");
}

await builder.Build().RunAsync();

static void ConfigureRedis(KubernetesResource resource)
{
    if (resource.Workload is not StatefulSet statefulSet)
    {
        throw new InvalidOperationException("The Kener Redis resource must publish as a StatefulSet.");
    }

    var container = statefulSet.Spec.Template.Spec.Containers.Single();
    statefulSet.Spec.Template.Spec.AutomountServiceAccountToken = false;
    container.Env.Add(new EnvVarV1 { Name = "REDIS_PASSWORD" });
    container.Args.Add("--requirepass");
    container.Args.Add("$(REDIS_PASSWORD)");
    container.Args.Add("--appendonly");
    container.Args.Add("yes");
    container.Args.Add("--appendfsync");
    container.Args.Add("everysec");
    container.Resources = new ResourceRequirementsV1
    {
        Requests =
        {
            ["cpu"] = "100m",
            ["memory"] = "256Mi",
        },
        Limits =
        {
            ["cpu"] = "500m",
            ["memory"] = "512Mi",
        },
    };
    container.LivenessProbe = CreateRedisProbe(10, 3);
    container.ReadinessProbe = CreateRedisProbe(0, 3);
}

static void ConfigureWeb(KubernetesResource resource)
{
    if (resource.Workload is not Deployment deployment)
    {
        throw new InvalidOperationException("The web application must publish as a Deployment.");
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
    container.Env.Add(new EnvVarV1 { Name = "KENER_SECRET_KEY" });
    container.Env.Add(new EnvVarV1 { Name = "REDIS_PASSWORD" });
    container.Env.Add(
        new EnvVarV1
        {
            Name = "REDIS_URL",
            Value = "redis://:$(REDIS_PASSWORD)@redis:6379",
        }
    );
    container.Resources = new ResourceRequirementsV1
    {
        Requests =
        {
            ["cpu"] = "1",
            ["memory"] = "1Gi",
        },
        Limits =
        {
            ["cpu"] = "2",
            ["memory"] = "2Gi",
        },
    };
    SetHealthCheckPath(container.LivenessProbe);
    SetHealthCheckPath(container.ReadinessProbe);
    SetHealthCheckPath(container.StartupProbe);

    var selector = new LabelSelectorV1();
    selector.MatchLabels["app.kubernetes.io/component"] = "web";
    resource.AdditionalResources.Add(
        new PodDisruptionBudget
        {
            Metadata = new ObjectMetaV1 { Name = "kener-pdb" },
            Spec = new PodDisruptionBudgetSpec
            {
                MinAvailable = 1,
                Selector = selector,
            },
        }
    );
}

static ProbeV1 CreateRedisProbe(int initialDelaySeconds, int failureThreshold)
    => new()
    {
        Exec = new ExecActionV1
        {
            Command =
            {
                "sh",
                "-c",
                "redis-cli --no-auth-warning -a \"$REDIS_PASSWORD\" ping",
            },
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
