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
        processStartInfo.ArgumentList.Add("publish");
        processStartInfo.ArgumentList.Add("--apphost");
        processStartInfo.ArgumentList.Add(appHostProject);
        processStartInfo.ArgumentList.Add("--environment");
        processStartInfo.ArgumentList.Add(environment);
        processStartInfo.ArgumentList.Add("--output-path");
        processStartInfo.ArgumentList.Add(outputDirectory);
        processStartInfo.ArgumentList.Add("--non-interactive");

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

        throw new DirectoryNotFoundException(
            "Could not locate the repository root containing Arkanis.Infra.Deployment.CitizenId.Status.slnx."
        );
    }

    private static string Redact(string output) =>
        OnePasswordReference().Replace(output, "op://[redacted]");

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
        var artifacts = await Task.WhenAll(
            artifactPaths.Select(path => File.ReadAllTextAsync(path, cancellationToken))
        );

        return string.Join(Environment.NewLine, artifacts);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(outputDirectory))
        {
            Directory.Delete(outputDirectory, recursive: true);
        }

        return ValueTask.CompletedTask;
    }
}
