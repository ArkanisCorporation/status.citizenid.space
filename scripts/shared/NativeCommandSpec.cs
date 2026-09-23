using System.Collections.Generic;
using CliWrap;

namespace Template.Scripting;

/// <summary>
/// Describes a native command invocation executed through <see cref="NativeCommandRunner" />.
/// </summary>
/// <remarks>
/// This record uses required properties because executable, arguments, and working directory values are easy to swap when passed positionally.
/// The runner does not invoke a shell, so <see cref="Arguments" /> are passed to <c>CliWrap</c> as structured arguments.
/// <see cref="ConfigureCommand" /> runs after the default executable, argument, working directory, validation, and output-pipe configuration has been applied.
/// </remarks>
public sealed record NativeCommandSpec
{
    /// <summary>
    /// Gets the executable name or path.
    /// </summary>
    /// <value>
    /// A command available on <c>PATH</c>, such as <c>dotnet</c>, or an absolute executable path.
    /// </value>
    /// <remarks>
    /// The runner validates that this value is not empty before it attempts to start the process.
    /// </remarks>
    public required string Executable { get; init; }

    /// <summary>
    /// Gets the structured argument list passed to the native process.
    /// </summary>
    /// <value>
    /// A non-null list of arguments that <c>CliWrap</c> forwards without shell concatenation.
    /// </value>
    /// <remarks>
    /// Use one entry per argument so quoting and escaping stay the responsibility of the process runner rather than the caller.
    /// </remarks>
    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>
    /// Gets the working directory used when the process starts.
    /// </summary>
    /// <value>
    /// An absolute or relative directory path resolved by the current process.
    /// </value>
    /// <remarks>
    /// The runner validates that this value is not empty before it executes the command.
    /// </remarks>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Gets the <c>CliWrap</c> result validation mode.
    /// </summary>
    /// <value>
    /// <see cref="CommandResultValidation.None" /> by default so the runner can wrap failures in <see cref="NativeCommandException" /> with script-specific context.
    /// </value>
    /// <remarks>
    /// Set this value when native exit-code validation should happen inside <c>CliWrap</c> before the runner performs additional result checks.
    /// </remarks>
    public CommandResultValidation Validation { get; init; } = CommandResultValidation.None;

    /// <summary>
    /// Gets a value indicating whether a non-zero exit code should be converted to <see cref="NativeCommandException" /> after execution.
    /// </summary>
    /// <value>
    /// <see langword="true" /> by default.
    /// </value>
    /// <remarks>
    /// When this value is <see langword="false" />, callers are responsible for inspecting <see cref="NativeCommandResult.ExitCode" />.
    /// </remarks>
    public bool ThrowOnFailure { get; init; } = true;

    /// <summary>
    /// Gets an optional callback that fluently adjusts the built <c>CliWrap</c> command.
    /// </summary>
    /// <value>
    /// A callback that receives the default command configuration and returns the command instance to execute.
    /// </value>
    /// <remarks>
    /// Use this callback to set additional <c>CliWrap</c> options such as environment variables, input pipes, credentials, or custom output routing.
    /// Replacing the default stdout or stderr pipes can change what is captured in <see cref="NativeCommandResult.StandardOutput" /> and <see cref="NativeCommandResult.StandardError" />.
    /// </remarks>
    public Func<Command, Command>? ConfigureCommand { get; init; }
}
