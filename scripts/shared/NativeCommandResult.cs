namespace Template.Scripting;

/// <summary>
/// Captures the result of a native command invocation.
/// </summary>
/// <remarks>
/// Standard output and standard error are captured even when the runner also mirrors them to the current console streams.
/// Consumers should prefer <see cref="ExitCode" /> for control flow and reserve output parsing for command-specific data.
/// </remarks>
public sealed record NativeCommandResult
{
    /// <summary>
    /// Gets the native process exit code.
    /// </summary>
    /// <value>
    /// The exit code reported by the completed native process.
    /// </value>
    /// <remarks>
    /// A value of <c>0</c> usually indicates success, but callers should interpret it according to the executed tool's contract.
    /// </remarks>
    public required int ExitCode { get; init; }

    /// <summary>
    /// Gets the captured standard output text.
    /// </summary>
    /// <value>
    /// The full standard output stream captured by the runner, or an empty string when no stdout content was produced.
    /// </value>
    /// <remarks>
    /// The returned text reflects the default runner pipes unless <see cref="NativeCommandSpec.ConfigureCommand" /> replaced them.
    /// </remarks>
    public required string StandardOutput { get; init; }

    /// <summary>
    /// Gets the captured standard error text.
    /// </summary>
    /// <value>
    /// The full standard error stream captured by the runner, or an empty string when no stderr content was produced.
    /// </value>
    /// <remarks>
    /// The returned text reflects the default runner pipes unless <see cref="NativeCommandSpec.ConfigureCommand" /> replaced them.
    /// </remarks>
    public required string StandardError { get; init; }
}
