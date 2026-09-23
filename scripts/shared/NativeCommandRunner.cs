using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using CliWrap;
using CliWrap.Exceptions;

namespace Template.Scripting;

/// <summary>
/// Runs native commands using <c>CliWrap</c>.
/// </summary>
/// <remarks>
/// Commands are executed directly without a shell.
/// The default output pipes capture stdout and stderr while mirroring each line to the current process console streams.
/// <see cref="NativeCommandSpec.ConfigureCommand" /> can extend or replace the default command configuration before execution begins.
/// </remarks>
public static class NativeCommandRunner
{
    /// <summary>
    /// Executes a native command asynchronously.
    /// </summary>
    /// <param name="spec">
    /// Command specification that provides the executable, structured arguments, working directory, validation behavior, and optional <c>CliWrap</c> customization.
    /// </param>
    /// <param name="cancellationToken">Token that cancels command execution.</param>
    /// <returns>The captured native command result.</returns>
    /// <remarks>
    /// The default command construction uses <c>Cli.Wrap(spec.Executable).WithArguments(spec.Arguments).WithWorkingDirectory(spec.WorkingDirectory).WithValidation(spec.Validation)</c>.
    /// The runner captures stdout and stderr in memory and mirrors each line to <see cref="Console.Out" /> and <see cref="Console.Error" />.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="spec" /> or <see cref="NativeCommandSpec.Arguments" /> is <see langword="null" />.</exception>
    /// <exception cref="ScriptConfigurationException">Thrown when <paramref name="spec" /> contains an empty executable or working directory, or when <see cref="NativeCommandSpec.ConfigureCommand" /> returns <see langword="null" />.</exception>
    /// <exception cref="NativeCommandException">
    /// Thrown when the native process cannot start, <c>CliWrap</c> validation fails, or the process exits with a non-zero code while <see cref="NativeCommandSpec.ThrowOnFailure" /> is enabled.
    /// </exception>
    public static async Task<NativeCommandResult> RunAsync(NativeCommandSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);
        ArgumentNullException.ThrowIfNull(spec.Arguments);

        if (string.IsNullOrWhiteSpace(spec.Executable))
        {
            throw new ScriptConfigurationException("Native command executable is empty.");
        }

        if (string.IsNullOrWhiteSpace(spec.WorkingDirectory))
        {
            throw new ScriptConfigurationException($"Native command '{spec.Executable}' has no working directory.");
        }

        var standardOutput = new StringBuilder();
        var standardError = new StringBuilder();

        try
        {
            var command = Cli.Wrap(spec.Executable)
                .WithArguments(spec.Arguments)
                .WithWorkingDirectory(spec.WorkingDirectory)
                .WithValidation(spec.Validation)
                .WithStandardOutputPipe(PipeTarget.Merge(
                    PipeTarget.ToStringBuilder(standardOutput),
                    PipeTarget.ToDelegate(Console.Out.WriteLine)))
                .WithStandardErrorPipe(PipeTarget.Merge(
                    PipeTarget.ToStringBuilder(standardError),
                    PipeTarget.ToDelegate(Console.Error.WriteLine)));

            if (spec.ConfigureCommand is not null)
            {
                command = spec.ConfigureCommand(command)
                    ?? throw new ScriptConfigurationException($"Native command '{spec.Executable}' returned a null CliWrap command configuration.");
            }

            var commandResult = await command.ExecuteAsync(cancellationToken);
            var nativeResult = new NativeCommandResult
            {
                ExitCode = commandResult.ExitCode,
                StandardOutput = standardOutput.ToString(),
                StandardError = standardError.ToString(),
            };

            if (spec.ThrowOnFailure && nativeResult.ExitCode != 0)
            {
                throw CreateFailureException(
                    spec,
                    nativeResult.ExitCode,
                    nativeResult.StandardOutput,
                    nativeResult.StandardError,
                    innerException: null);
            }

            return nativeResult;
        }
        catch (NativeCommandException)
        {
            throw;
        }
        catch (CommandExecutionException ex)
        {
            throw CreateFailureException(
                spec,
                ex.ExitCode,
                standardOutput.ToString(),
                standardError.ToString(),
                ex);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not ScriptException)
        {
            throw CreateFailureException(
                spec,
                exitCode: null,
                standardOutput.ToString(),
                standardError.ToString(),
                ex);
        }
    }

    private static NativeCommandException CreateFailureException(
        NativeCommandSpec spec,
        int? exitCode,
        string standardOutput,
        string standardError,
        Exception? innerException)
    {
        var message = new StringBuilder();
        _ = exitCode is int knownExitCode
            ? message.Append(CultureInfo.InvariantCulture, $"Native command '{spec.Executable}' failed with exit code {knownExitCode} in '{spec.WorkingDirectory}'.")
            : message.Append(CultureInfo.InvariantCulture, $"Native command '{spec.Executable}' failed before a normal exit code was available in '{spec.WorkingDirectory}'.");

        _ = message
            .AppendLine()
            .Append("Command: ")
            .Append(spec.Executable);

        var arguments = FormatArguments(spec.Arguments);
        if (!string.IsNullOrWhiteSpace(arguments))
        {
            _ = message
                .Append(' ')
                .Append(arguments);
        }

        AppendStreamSummary(message, "Standard output", standardOutput);
        AppendStreamSummary(message, "Standard error", standardError);

        return new NativeCommandException(
            message.ToString(),
            spec.Executable,
            CopyArguments(spec.Arguments),
            spec.WorkingDirectory,
            exitCode,
            standardOutput,
            standardError,
            innerException);
    }

    private static string FormatArguments(IReadOnlyList<string> arguments)
        => string.Join(
            " ",
            arguments.Select(FormatArgument));

    private static string FormatArgument(string argument)
    {
        if (argument.Length == 0)
        {
            return "\"\"";
        }

        return argument.Any(char.IsWhiteSpace) ? $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\"" : argument;
    }

    private static void AppendStreamSummary(StringBuilder message, string streamName, string streamContent)
    {
        if (string.IsNullOrEmpty(streamContent))
        {
            _ = message
                .AppendLine()
                .Append(streamName)
                .Append(": <empty>");
            return;
        }

        var normalized = streamContent.TrimEnd();
        const int MaxCharacters = 4_000;
        if (normalized.Length > MaxCharacters)
        {
            normalized = $"{normalized[^MaxCharacters..]}";
        }

        _ = message
            .AppendLine()
            .Append(streamName)
            .AppendLine(":")
            .Append(normalized);
    }

    private static string[] CopyArguments(IReadOnlyList<string> arguments)
        => arguments.ToArray();
}
