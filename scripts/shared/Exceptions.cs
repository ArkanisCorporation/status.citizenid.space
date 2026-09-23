namespace Template.Scripting;

/// <summary>
/// Represents a failure that a file-based repository automation script can report to its caller.
/// </summary>
/// <remarks>
/// Script entry points catch this exception type and return <see cref="ExitCode"/> as the process exit code.
/// The message must already contain enough context for humans and CI logs because the entry point writes it directly to standard error.
/// </remarks>
/// <param name="message">Human-readable failure message with operation context and remediation when available.</param>
/// <param name="exitCode">Process exit code returned by the script entry point.</param>
/// <param name="innerException">Original exception that caused the script failure, if any.</param>
public abstract class ScriptException(string message, int exitCode = 2, Exception? innerException = null) : Exception(message, innerException)
{
    /// <summary>
    /// Gets the process exit code that the script entry point should return.
    /// </summary>
    /// <value>
    /// The numeric exit code used by the calling shell or CI runner.
    /// </value>
    public int ExitCode { get; } = exitCode;
}

/// <summary>
/// Represents a missing or invalid script configuration value.
/// </summary>
/// <remarks>
/// Throw this exception for missing environment variables, invalid option values, or unsupported script state that the user can fix without changing source code.
/// </remarks>
/// <param name="message">Human-readable failure message with the missing or invalid configuration value.</param>
/// <param name="innerException">Original exception that caused the configuration failure, if any.</param>
public sealed class ScriptConfigurationException(string message, Exception? innerException = null) : ScriptException(message, innerException: innerException);

/// <summary>
/// Represents a native command that could not be started or completed successfully.
/// </summary>
/// <remarks>
/// The exception includes the executable, arguments, working directory, exit code, standard output, and standard error whenever those values are available.
/// </remarks>
public sealed class NativeCommandException : ScriptException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NativeCommandException"/> class.
    /// </summary>
    /// <param name="message">Human-readable failure message with command context and remediation.</param>
    /// <param name="executable">Executable name or path passed to <c>CliWrap</c>.</param>
    /// <param name="arguments">Arguments passed to the executable.</param>
    /// <param name="workingDirectory">Working directory used for the command.</param>
    /// <param name="nativeExitCode">Exit code returned by the native process, or <see langword="null"/> when the process did not start.</param>
    /// <param name="standardOutput">Captured standard output.</param>
    /// <param name="standardError">Captured standard error.</param>
    /// <param name="innerException">Original exception raised by <c>CliWrap</c>, if any.</param>
    public NativeCommandException(
        string message,
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        int? nativeExitCode,
        string standardOutput,
        string standardError,
        Exception? innerException = null)
        : base(message, nativeExitCode is 127 ? 127 : 2, innerException)
    {
        Executable = executable;
        Arguments = arguments;
        WorkingDirectory = workingDirectory;
        NativeExitCode = nativeExitCode;
        StandardOutput = standardOutput;
        StandardError = standardError;
    }

    /// <summary>
    /// Gets the executable name or path passed to <c>CliWrap</c>.
    /// </summary>
    /// <value>
    /// The command name or absolute path that the native process runner attempted to start.
    /// </value>
    public string Executable { get; }

    /// <summary>
    /// Gets the arguments passed to the executable.
    /// </summary>
    /// <value>
    /// The command-line arguments supplied to the executable without shell concatenation.
    /// </value>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Gets the working directory used for command execution.
    /// </summary>
    /// <value>
    /// The directory where the native process was started.
    /// </value>
    public string WorkingDirectory { get; }

    /// <summary>
    /// Gets the exit code returned by the native process.
    /// </summary>
    /// <value>
    /// <see langword="null"/> when the process could not be started.
    /// </value>
    public int? NativeExitCode { get; }

    /// <summary>
    /// Gets captured standard output.
    /// </summary>
    /// <value>
    /// The text captured from standard output, or an empty string when no output was captured.
    /// </value>
    public string StandardOutput { get; }

    /// <summary>
    /// Gets captured standard error.
    /// </summary>
    /// <value>
    /// The text captured from standard error, or an empty string when no output was captured.
    /// </value>
    public string StandardError { get; }
}

/// <summary>
/// Represents a Git repository or Git index operation failure.
/// </summary>
/// <remarks>
/// Throw this exception when <c>LibGit2Sharp</c> cannot discover a repository, read staged content, or write the index.
/// </remarks>
/// <param name="message">Human-readable failure message with the Git operation and repository path when available.</param>
/// <param name="innerException">Original exception that caused the Git operation failure, if any.</param>
public sealed class GitScriptException(string message, Exception? innerException = null) : ScriptException(message, innerException: innerException);

/// <summary>
/// Represents a release target validation, prepare, or publish failure.
/// </summary>
/// <remarks>
/// Throw this exception when release-specific state is invalid, such as a missing Dockerfile, missing package artifact, or missing Helm chart.
/// </remarks>
/// <param name="message">Human-readable failure message with the release target and remediation when available.</param>
/// <param name="innerException">Original exception that caused the release target failure, if any.</param>
public sealed class ReleaseTargetException(string message, Exception? innerException = null) : ScriptException(message, innerException: innerException);
