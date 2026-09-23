using System.IO;

namespace Template.Scripting;

/// <summary>
/// Builds and executes repository-local <c>act</c> commands for the workflow test job.
/// </summary>
/// <remarks>
/// This helper preserves the existing shell-script contract for <c>scripts/act/test-pr.*</c> and <c>scripts/act/test-ci.*</c>.
/// It uses the <c>ACT_BIN</c> environment variable when it is set to a non-empty value; otherwise it falls back to <c>act</c> and relies on direct process resolution through <c>CliWrap</c>.
/// Each command run ensures <c>.act/artifacts</c> exists under the provided working directory before invoking the native process.
/// </remarks>
internal static class ActRunner
{
    private const string WorkflowPath = ".github/workflows/main.yaml";
    private const string JobName = "test";
    private const string ArtifactServerPath = ".act/artifacts";
    private const string PullRequestEventName = "pull_request";
    private const string PullRequestEventPayloadPath = ".act/events/pull_request.json";
    private const string PushEventName = "push";
    private const string PushEventPayloadPath = ".act/events/push-ci.json";

    /// <summary>
    /// Creates the native command specification for the pull-request workflow test run.
    /// </summary>
    /// <param name="workingDirectory">Repository working directory used to resolve workflow and event paths.</param>
    /// <returns>
    /// A <see cref="NativeCommandSpec" /> that runs <c>act pull_request -W .github/workflows/main.yaml -j test --artifact-server-path .act/artifacts -e .act/events/pull_request.json</c>.
    /// </returns>
    /// <remarks>
    /// The returned specification creates <c>.act/artifacts</c> before execution so artifact uploads have a writable target.
    /// The executable is resolved from <c>ACT_BIN</c> when configured, or defaults to <c>act</c>.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="workingDirectory" /> is empty or whitespace-only.</exception>
    public static NativeCommandSpec CreatePullRequestTestSpec(string workingDirectory)
        => CreateSpec(workingDirectory, PullRequestEventName, PullRequestEventPayloadPath);

    /// <summary>
    /// Creates the native command specification for the CI push workflow test run.
    /// </summary>
    /// <param name="workingDirectory">Repository working directory used to resolve workflow and event paths.</param>
    /// <returns>
    /// A <see cref="NativeCommandSpec" /> that runs <c>act push -W .github/workflows/main.yaml -j test --artifact-server-path .act/artifacts -e .act/events/push-ci.json</c>.
    /// </returns>
    /// <remarks>
    /// The returned specification creates <c>.act/artifacts</c> before execution so artifact uploads have a writable target.
    /// The executable is resolved from <c>ACT_BIN</c> when configured, or defaults to <c>act</c>.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="workingDirectory" /> is empty or whitespace-only.</exception>
    public static NativeCommandSpec CreateCiTestSpec(string workingDirectory)
        => CreateSpec(workingDirectory, PushEventName, PushEventPayloadPath);

    /// <summary>
    /// Runs the pull-request workflow test job through <c>act</c>.
    /// </summary>
    /// <param name="workingDirectory">Repository working directory used to resolve workflow and event paths.</param>
    /// <param name="cancellationToken">Token that cancels native command execution.</param>
    /// <returns>The captured native command result from the <c>act</c> invocation.</returns>
    /// <remarks>
    /// This method preserves the direct native-process execution boundary by delegating to <see cref="NativeCommandRunner.RunAsync(NativeCommandSpec, CancellationToken)" />.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="workingDirectory" /> is empty or whitespace-only.</exception>
    /// <exception cref="NativeCommandException">
    /// Thrown when <c>act</c> cannot be started, exits with a non-zero status, or fails before a normal exit code becomes available.
    /// </exception>
    public static Task<NativeCommandResult> RunPullRequestTestAsync(string workingDirectory, CancellationToken cancellationToken = default)
        => NativeCommandRunner.RunAsync(CreatePullRequestTestSpec(workingDirectory), cancellationToken);

    /// <summary>
    /// Runs the CI push workflow test job through <c>act</c>.
    /// </summary>
    /// <param name="workingDirectory">Repository working directory used to resolve workflow and event paths.</param>
    /// <param name="cancellationToken">Token that cancels native command execution.</param>
    /// <returns>The captured native command result from the <c>act</c> invocation.</returns>
    /// <remarks>
    /// This method preserves the direct native-process execution boundary by delegating to <see cref="NativeCommandRunner.RunAsync(NativeCommandSpec, CancellationToken)" />.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="workingDirectory" /> is empty or whitespace-only.</exception>
    /// <exception cref="NativeCommandException">
    /// Thrown when <c>act</c> cannot be started, exits with a non-zero status, or fails before a normal exit code becomes available.
    /// </exception>
    public static Task<NativeCommandResult> RunCiTestAsync(string workingDirectory, CancellationToken cancellationToken = default)
        => NativeCommandRunner.RunAsync(CreateCiTestSpec(workingDirectory), cancellationToken);

    private static NativeCommandSpec CreateSpec(string workingDirectory, string eventName, string eventPayloadPath)
    {
        var resolvedWorkingDirectory = NormalizeWorkingDirectory(workingDirectory);
        EnsureArtifactDirectory(resolvedWorkingDirectory);

        return new NativeCommandSpec
        {
            Executable = ResolveActExecutable(),
            Arguments =
            [
                eventName,
                "-W",
                WorkflowPath,
                "-j",
                JobName,
                "--artifact-server-path",
                ArtifactServerPath,
                "-e",
                eventPayloadPath,
            ],
            WorkingDirectory = resolvedWorkingDirectory,
        };
    }

    private static string NormalizeWorkingDirectory(string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            throw new ArgumentException("Working directory cannot be empty.", nameof(workingDirectory));
        }

        return Path.GetFullPath(workingDirectory);
    }

    private static void EnsureArtifactDirectory(string workingDirectory)
        => Directory.CreateDirectory(Path.Combine(workingDirectory, ".act", "artifacts"));

    private static string ResolveActExecutable()
        => ScriptEnvironment.GetOrDefault("ACT_BIN", "act");
}
