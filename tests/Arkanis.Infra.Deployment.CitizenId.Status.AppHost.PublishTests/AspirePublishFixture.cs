namespace Arkanis.Infra.Deployment.CitizenId.Status.AppHost.PublishTests;

using System.Diagnostics;
using System.Text.RegularExpressions;

/// <summary>
/// Publishes the AppHost into an isolated temporary directory for artifact assertions.
/// </summary>
internal static partial class AspirePublishFixture
{
    /// <summary>
    /// Publishes the AppHost for the supplied deployment environment.
    /// </summary>
    /// <param name="environment">The AppHost deployment environment.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>The generated chart, including non-secret diagnostic output.</returns>
    public static async Task<PublishedChart> PublishAsync(
        string environment,
        CancellationToken cancellationToken
    )
        => await ExecuteAsync("publish", null, false, environment, cancellationToken);

    /// <summary>
    /// Executes the non-mutating External Secrets emission step for the supplied deployment environment.
    /// </summary>
    /// <param name="environment">The AppHost deployment environment.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>The generated chart, including non-secret diagnostic output.</returns>
    public static async Task<PublishedChart> EmitExternalSecretsAsync(
        string environment,
        CancellationToken cancellationToken
    )
        => await ExecuteAsync("do", "emit-externalsecrets-kener-kubernetes", true, environment, cancellationToken);

    /// <summary>
    /// Executes an Aspire operation that renders deployment artifacts without invoking Helm deployment.
    /// </summary>
    /// <param name="command">The Aspire command to execute.</param>
    /// <param name="step">The optional named pipeline step for the command.</param>
    /// <param name="clearDeploymentState">Whether the pipeline must ignore persisted deployment state.</param>
    /// <param name="environment">The AppHost deployment environment.</param>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>The generated chart, including non-secret diagnostic output.</returns>
    private static async Task<PublishedChart> ExecuteAsync(
        string command,
        string? step,
        bool clearDeploymentState,
        string environment,
        CancellationToken cancellationToken
    )
    {
        var repositoryRoot = FindRepositoryRoot();
        var outputDirectory = Path.Combine(
            Path.GetTempPath(),
            "kener-apphost-publish",
            Guid.NewGuid().ToString("N")
        );
        var appHostProject = Path.Combine(
            repositoryRoot.FullName,
            "src",
            "Arkanis.Infra.Deployment.CitizenId.Status.AppHost",
            "Arkanis.Infra.Deployment.CitizenId.Status.AppHost.csproj"
        );
        var processStartInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = repositoryRoot.FullName,
        };

        processStartInfo.ArgumentList.Add("tool");
        processStartInfo.ArgumentList.Add("run");
        processStartInfo.ArgumentList.Add("aspire");
        processStartInfo.ArgumentList.Add(command);
        if (step is not null)
        {
            processStartInfo.ArgumentList.Add(step);
        }

        processStartInfo.ArgumentList.Add("--apphost");
        processStartInfo.ArgumentList.Add(appHostProject);
        processStartInfo.ArgumentList.Add("--environment");
        processStartInfo.ArgumentList.Add(environment);
        processStartInfo.ArgumentList.Add("--output-path");
        processStartInfo.ArgumentList.Add(outputDirectory);
        processStartInfo.ArgumentList.Add("--non-interactive");
        if (clearDeploymentState)
        {
            processStartInfo.ArgumentList.Add("--clear-cache");
            processStartInfo.ArgumentList.Add("true");
        }

        using var process =
            Process.Start(processStartInfo)
            ?? throw new InvalidOperationException("Could not start the Aspire publish process.");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = Redact(string.Concat(await standardOutputTask, "\n", await standardErrorTask));
        return new PublishedChart(outputDirectory, process.ExitCode, output);
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

    private static string Redact(string output)
        => OnePasswordReference().Replace(output, "op://[redacted]");

    [GeneratedRegex("op://[^\\s]+", RegexOptions.CultureInvariant)]
    private static partial Regex OnePasswordReference();
}

/// <summary>
/// Represents a temporary Aspire publish output directory.
/// </summary>
internal sealed class PublishedChart(string outputDirectory, int exitCode, string output)
    : IAsyncDisposable
{
    /// <summary>
    /// Gets the Aspire publish exit code.
    /// </summary>
    public int ExitCode { get; } = exitCode;

    /// <summary>
    /// Gets redacted publish diagnostics.
    /// </summary>
    public string Output { get; } = output;

    /// <summary>
    /// Reads all generated YAML artifacts into one string.
    /// </summary>
    /// <param name="cancellationToken">The test cancellation token.</param>
    /// <returns>The concatenated chart YAML artifacts.</returns>
    public async Task<string> ReadAllTemplatesAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(outputDirectory))
        {
            return string.Empty;
        }

        var artifactPaths = Directory
            .EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories)
            .Where(static path => Path.GetExtension(path) is ".yaml" or ".yml")
            .Order(StringComparer.Ordinal)
            .ToArray();
        var artifacts = await Task.WhenAll(artifactPaths.Select(path => File.ReadAllTextAsync(path, cancellationToken)));

        return string.Join(Environment.NewLine, artifacts);
    }

    /// <summary>
    /// Reads a generated artifact relative to the published chart output directory.
    /// </summary>
    /// <param name="relativePath">The artifact path relative to the chart output directory.</param>
    /// <param name="cancellationToken">The cancellation token for file I/O.</param>
    /// <returns>The artifact content, or an empty string when it was not emitted.</returns>
    public async Task<string> ReadArtifactAsync(string relativePath, CancellationToken cancellationToken)
    {
        var artifactPath = Path.Combine(outputDirectory, relativePath);
        return File.Exists(artifactPath)
            ? await File.ReadAllTextAsync(artifactPath, cancellationToken)
            : string.Empty;
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, true);
        }

        return ValueTask.CompletedTask;
    }
}
