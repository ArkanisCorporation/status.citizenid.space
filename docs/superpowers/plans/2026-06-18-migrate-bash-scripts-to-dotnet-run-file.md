# Bash Script Migration To .NET File-Based Apps Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.
> Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace repository Bash script entry points with C# file-based apps invoked through `dotnet run --file`, while preserving current Husky, act, and semantic-release behavior.

**Implementation Status:** Completed on 2026-06-18.
The migration now uses C# file-based apps for Husky, act, and semantic-release entry points.
Native commands flow through `CliWrap`, Git operations flow through `LibGit2Sharp`, script-scoped package references remain in `scripts/shared/Packages.cs`, and solution-wide central package management was not modified for script-only packages.

**Architecture:** Use C# entry files for every current script command and share implementation through `#:include`.
Use `CliWrap` for all native command execution and output handling.
Use `CliWrap` pipe targets for output routing instead of manual event-stream handling when commands only need stdout and stderr.
Use `LibGit2Sharp` for repository discovery, staged file inspection, staged blob inspection, and Git index updates.

**Tech Stack:** .NET SDK `10.0.300` from `global.json`, C# file-based apps, `dotnet run --file`, script-scoped `#:package CliWrap@3.10.2`, script-scoped `#:package LibGit2Sharp@0.31.0`, Husky.NET, semantic-release exec plugin, nektos/act.

---

## Source Facts

The .NET SDK file-based app documentation documents explicit execution with `dotnet run --file path.cs`.
The same documentation shows shebang support through `#!` and recommends `#!/usr/bin/env -S dotnet --`.
The `--` separator prevents `dotnet` from consuming script arguments that look like .NET CLI options.
If `env -S` is unavailable, `#!/usr/bin/env dotnet` is the documented fallback.
This plan uses `#!/usr/bin/env -S dotnet --`, not `/usr/bin/env/dotnet`, because `/usr/bin/env/dotnet` names a non-standard executable path.
It states that file-based apps include only the entry file by default.
It documents `#:include` for adding more source files to the virtual project.
It documents `#:package` for package references.
It documents explicit package versions through `#:package PackageName@Version`.
It says omitting package versions only works with central package management through `Directory.Packages.props`.
This plan avoids changing `Directory.Packages.props` because `Directory.Packages.props` affects the full solution scope, while the package references are only needed by script file-based apps.
It documents `#:ref` as feature-gated, so this plan uses `#:include` instead.
Reference: [dotnet-run-file.md](https://github.com/dotnet/sdk/blob/main/documentation/general/dotnet-run-file.md#multiple-c-files).

Context7 selected `/tyrrrz/cliwrap` for `CliWrap`.
The docs show `Cli.Wrap("path/to/exe").WithArguments([...]).ExecuteAsync()` and `WithValidation(CommandResultValidation.None)` for manual exit-code handling.
The docs show output piping with `PipeTarget.ToDelegate`, `PipeTarget.ToFile`, `PipeTarget.Merge`, and tuple syntax for piping stdout and stderr to separate streams.
The plan uses `PipeTarget.Merge` to capture output while also mirroring logs to the console.
The plan keeps `ListenAsync` available only for future cases that need process lifecycle events beyond normal output.

Context7 selected `/libgit2/libgit2sharp` for `LibGit2Sharp`.
The docs show `Repository.Discover`, `new Repository(path)`, `repo.RetrieveStatus(...)`, `repo.Index.Add(...)`, and `repo.Index.Write()`.
Local API probing against `LibGit2Sharp` `0.31.0` confirmed `Index.Add(Blob, string, Mode)` and writable `IndexEntry.Mode`.

NuGet search on 2026-06-18 returned `CliWrap` `3.10.2` and `LibGit2Sharp` `0.31.0` as latest package versions.

## Durable Design Rules

Every file-based app that executes native commands must use `CliWrap`.
No new code may use `Process`, `ProcessStartInfo`, shell command concatenation, Bash, PowerShell, `cmd`, or direct shell launch for native command execution.
Command specs must expose `CommandResultValidation` with a default of `CommandResultValidation.None`.
Command specs must expose an optional command configuration callback so a caller can fluently adjust pipes, environment variables, validation, or other `CliWrap` options on the built command.
The shared runner should prefer `PipeTarget` composition for stdout and stderr over event-stream handling.

Every file-based app that needs Git repository data must use `LibGit2Sharp`.
No new code may shell out to `git` for repository discovery, status, index inspection, staged content inspection, or index mode updates.

Every executable file-based script entry must start with `#!/usr/bin/env -S dotnet --`.
Keep the shebang on the first line, before all `#:` directives and C# code.
Do not use `/usr/bin/env/dotnet` because it is interpreted as a literal executable path instead of invoking `env` to resolve `dotnet`.
Every executable script entry that includes shared script files must include this directive block immediately after the shebang:

```csharp
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false
#:property RestorePackagesWithLockFile=false
#:property ExperimentalFileBasedProgramEnableTransitiveDirectives=true
```

`ManagePackageVersionsCentrally=false` keeps script-scoped `#:package Package@Version` directives compatible with this repository's solution-wide central package management.
`RestorePackagesWithLockFile=false` prevents `dotnet run --file` probes and script runs from generating `packages.lock.json` files under `scripts`.
`ExperimentalFileBasedProgramEnableTransitiveDirectives=true` allows shared included files such as `scripts/shared/Packages.cs` to carry package directives.

Every public or internal type, property, method, and exception introduced for scripts must have XML documentation.
Use `<summary>` for the quick overview of what the member does and how.
Use `<remarks>` for preconditions, side effects, and consumer-important implementation details.
Use `<exception>` for all possible exceptions thrown and why they are thrown.
Use `<inheritdoc />` for interface implementations or overrides when no additional side effects exist.
Use `<see>`, `<paramref>`, `<c>`, `<code>`, `<typeparam>`, `<returns>`, and `<value>` when they make the docs clearer.

Record classes used as option bags or result models must use `required` init-only properties when constructor argument order would be unclear.
Short positional records are allowed only for tiny, obvious tuples where argument order is not consumer-hostile.

Custom exceptions must be specific enough to explain the failing subsystem.
Exception messages must include the operation, relevant path or command, and remediation when available.

File names must describe the primary contents.
Use `Exceptions.cs` for the shared exception hierarchy because it contains multiple exception types.
Use type-aligned filenames such as `NativeCommandRunner.cs` and `GitRepositoryContext.cs` when a file contains one primary type.
Use purpose-aligned helper filenames such as `VerifyTargets.cs`, `PrepareTargets.cs`, and `PublishTargets.cs` when a file contains multiple related helper methods.
Do not use vague filenames such as `common.cs` unless the file only contains unavoidable cross-cutting glue.

## Current Inventory

The repository currently has Bash script entry points under `scripts/prepare-shell-scripts.sh`, `scripts/update-shell-script-permissions.sh`, `scripts/act/*.sh`, and `scripts/semantic-release/**/*.sh`.
The repository also has PowerShell twins for Husky and act under `scripts/*.ps1` and `scripts/act/*.ps1`.
Husky.NET calls Bash by default and PowerShell on Windows in `.husky/task-runner.json`.
semantic-release calls Bash scripts from `release.config.mjs`.
README and `scripts/semantic-release/README.md` document Bash and PowerShell script commands.
AGENTS.md documents shell script permission tasks.
The solution file lists every script file under the `/scripts/` solution folder.

## File Structure

Create `scripts/shared/Packages.cs`.
This file contains script-scoped `#:package` directives for `CliWrap` and `LibGit2Sharp`.
It intentionally avoids changing `Directory.Packages.props`, because these package references are not solution-wide dependencies.

Create `scripts/shared/Exceptions.cs`.
This file defines the base exception type and specific exceptions for configuration, native commands, Git operations, and release targets.

Create `scripts/shared/NativeCommandSpec.cs`.
This file defines the `required` property model for a native command invocation.

Create `scripts/shared/NativeCommandResult.cs`.
This file defines the `required` property model for command result data.

Create `scripts/shared/NativeCommandRunner.cs`.
This file runs native commands through `CliWrap`, captures output, streams logs, and returns `NativeCommandResult`.

Create `scripts/shared/ScriptEnvironment.cs`.
This file provides documented environment access helpers.

Create `scripts/shared/GitRepositoryContext.cs`.
This file owns `LibGit2Sharp` repository discovery and repository lifetime.

Create `scripts/shared/GitScriptIndex.cs`.
This file uses `LibGit2Sharp` to list staged shell scripts, read staged blob bytes for line-ending checks, and set staged executable modes.

Create `scripts/prepare-shell-scripts.cs`.
This file replaces `scripts/prepare-shell-scripts.sh` and `scripts/prepare-shell-scripts.ps1`.

Create `scripts/update-shell-script-permissions.cs`.
This file replaces `scripts/update-shell-script-permissions.sh` and `scripts/update-shell-script-permissions.ps1`.

Create `scripts/act/ActRunner.cs`.
This file locates `act` through `CliWrap`, creates `.act/artifacts`, and runs one act event.

Create `scripts/act/test-pr.cs`.
This file replaces `scripts/act/test-pr.sh` and `scripts/act/test-pr.ps1`.

Create `scripts/act/test-ci.cs`.
This file replaces `scripts/act/test-ci.sh` and `scripts/act/test-ci.ps1`.

Create `scripts/semantic-release/shared/ReleaseDefaults.cs`.
This file models release defaults with required properties.

Create `scripts/semantic-release/shared/ReleaseSubScripts.cs`.
This file finds phase scripts by numeric prefix and runs them through `dotnet run --file` using `CliWrap`.

Create `scripts/semantic-release/shared/ReleaseValidation.cs`.
This file contains validation helpers for commands, files, directories, and optional skips.

Create `.cs` replacements for every `scripts/semantic-release/**/*.sh` file.
Use phase-local helper files named for their contents, such as `VerifyTargets.cs`, `PrepareTargets.cs`, and `PublishTargets.cs`, plus shared code through `#:include`.

Modify `.husky/task-runner.json`.
Use `dotnet run --file` directly and remove platform-specific PowerShell branches.

Modify `release.config.mjs`.
Point semantic-release commands to `dotnet run --file ./scripts/semantic-release/<phase>/<entry>.cs`.

Modify `README.md`, `scripts/semantic-release/README.md`, `Template.slnx`, and `AGENTS.md`.
Remove stale `.sh` and `.ps1` references and document the new file-based app convention.

Delete every migrated `.sh` and duplicated `.ps1` script after its `.cs` replacement passes verification.

## Task 1: Script Package Directives And Shared Exception Model

**Files:**
- Create: `scripts/shared/Packages.cs`
- Create: `scripts/shared/Exceptions.cs`
- Create: `scripts/shared/ScriptEnvironment.cs`

- [ ] **Step 1: Create script-scoped package directives**

Create `scripts/shared/Packages.cs`.
This file is included by script entry files through `#:include shared/*.cs`, `#:include ../shared/*.cs`, or `#:include ../../shared/*.cs`.
It keeps `CliWrap` and `LibGit2Sharp` scoped to file-based scripts instead of making them solution-wide dependencies.

```csharp
#:package CliWrap@3.10.2
#:package LibGit2Sharp@0.31.0
```

- [ ] **Step 2: Create documented custom exceptions**

Create `scripts/shared/Exceptions.cs`.

```csharp
/// <summary>
/// Represents a failure that a file-based repository automation script can report to its caller.
/// </summary>
/// <remarks>
/// Script entry points catch this exception type and return <see cref="ExitCode"/> as the process exit code.
/// The message must already contain enough context for humans and CI logs because the entry point writes it directly to standard error.
/// </remarks>
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
    public string Executable { get; }

    /// <summary>
    /// Gets the arguments passed to the executable.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; }

    /// <summary>
    /// Gets the working directory used for command execution.
    /// </summary>
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
    public string StandardOutput { get; }

    /// <summary>
    /// Gets captured standard error.
    /// </summary>
    public string StandardError { get; }
}

/// <summary>
/// Represents a Git repository or Git index operation failure.
/// </summary>
/// <remarks>
/// Throw this exception when `LibGit2Sharp` cannot discover a repository, read staged content, or write the index.
/// </remarks>
public sealed class GitScriptException(string message, Exception? innerException = null) : ScriptException(message, innerException: innerException);

/// <summary>
/// Represents a release target validation, prepare, or publish failure.
/// </summary>
/// <remarks>
/// Throw this exception when release-specific state is invalid, such as a missing Dockerfile, missing package artifact, or missing Helm chart.
/// </remarks>
public sealed class ReleaseTargetException(string message, Exception? innerException = null) : ScriptException(message, innerException: innerException);
```

- [ ] **Step 3: Verify exception docs are present**

Run: `rg -n "public (abstract |sealed )?class .*Exception|/// <summary>|/// <remarks>|/// <exception" scripts/shared/Exceptions.cs`
Expected: every exception type is preceded by XML documentation.

- [ ] **Step 4: Create documented environment helper**

Create `scripts/shared/ScriptEnvironment.cs`.

```csharp
using System.Globalization;

/// <summary>
/// Reads and normalizes environment values used by file-based scripts.
/// </summary>
/// <remarks>
/// This helper intentionally keeps environment access centralized so missing values and defaults produce consistent diagnostic messages.
/// Values are read from the current process environment at call time.
/// </remarks>
public static class ScriptEnvironment
{
    /// <summary>
    /// Reads a required environment variable.
    /// </summary>
    /// <param name="name">Environment variable name.</param>
    /// <returns>The non-empty environment variable value.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty.</exception>
    /// <exception cref="ScriptConfigurationException">Thrown when the environment variable is missing or empty.</exception>
    public static string Require(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Environment variable name cannot be empty.", nameof(name));
        }

        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? throw new ScriptConfigurationException($"{name} is not set.") : value;
    }

    /// <summary>
    /// Reads an optional environment variable and returns a default when it is missing or empty.
    /// </summary>
    /// <param name="name">Environment variable name.</param>
    /// <param name="defaultValue">Value returned when the environment variable is missing or empty.</param>
    /// <returns>The configured environment value or <paramref name="defaultValue"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty.</exception>
    public static string GetOrDefault(string name, string defaultValue)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Environment variable name cannot be empty.", nameof(name));
        }

        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(value) ? defaultValue : value;
    }

    /// <summary>
    /// Interprets a common truthy environment value.
    /// </summary>
    /// <param name="value">Value to inspect.</param>
    /// <returns><see langword="true"/> for <c>true</c>, <c>1</c>, or <c>yes</c>, otherwise <see langword="false"/>.</returns>
    public static bool IsEnabled(string? value)
        => string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Converts text to lowercase with invariant-culture rules.
    /// </summary>
    /// <param name="value">Value to convert.</param>
    /// <returns>Lowercase text using <see cref="CultureInfo.InvariantCulture"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is <see langword="null"/>.</exception>
    public static string LowerInvariant(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return value.ToLower(CultureInfo.InvariantCulture);
    }
}
```

## Task 2: Native Command Runtime With CliWrap

**Files:**
- Create: `scripts/shared/NativeCommandSpec.cs`
- Create: `scripts/shared/NativeCommandResult.cs`
- Create: `scripts/shared/NativeCommandRunner.cs`

- [ ] **Step 1: Create command spec record**

Create `scripts/shared/NativeCommandSpec.cs`.

```csharp
using CliWrap;

/// <summary>
/// Describes a native command invocation executed through <see cref="NativeCommandRunner"/>.
/// </summary>
/// <remarks>
/// This record uses required properties because executable, arguments, and working directory are easy to swap when passed positionally.
/// The runner never invokes a shell, so <see cref="Arguments"/> are passed to <c>CliWrap</c> as structured arguments.
/// <see cref="ConfigureCommand"/> is applied after the runner sets executable, arguments, working directory, validation, and default output pipes.
/// </remarks>
public sealed record NativeCommandSpec
{
    /// <summary>
    /// Gets the executable name or path.
    /// </summary>
    /// <value>
    /// A command available on <c>PATH</c>, such as <c>dotnet</c>, or an absolute executable path.
    /// </value>
    public required string Executable { get; init; }

    /// <summary>
    /// Gets the structured argument list passed to <c>CliWrap</c>.
    /// </summary>
    public required IReadOnlyList<string> Arguments { get; init; }

    /// <summary>
    /// Gets the working directory used for the process.
    /// </summary>
    public required string WorkingDirectory { get; init; }

    /// <summary>
    /// Gets the `CliWrap` result validation mode.
    /// </summary>
    /// <value>
    /// <see cref="CommandResultValidation.None"/> by default so the runner can add script-specific exception context.
    /// </value>
    public CommandResultValidation Validation { get; init; } = CommandResultValidation.None;

    /// <summary>
    /// Gets a value indicating whether non-zero native exit codes should throw <see cref="NativeCommandException"/>.
    /// </summary>
    /// <value>
    /// <see langword="true"/> by default.
    /// </value>
    public bool ThrowOnFailure { get; init; } = true;

    /// <summary>
    /// Gets an optional callback that can fluently configure the built `CliWrap` command.
    /// </summary>
    /// <value>
    /// A callback that receives the command after the runner has applied default options and returns the command to execute.
    /// </value>
    /// <remarks>
    /// Use this to configure additional `CliWrap` features, such as environment variables, credentials, input pipes, output pipes, or non-default validation.
    /// If the callback replaces stdout or stderr pipes, <see cref="NativeCommandResult.StandardOutput"/> or <see cref="NativeCommandResult.StandardError"/> may no longer contain mirrored output.
    /// </remarks>
    public Func<Command, Command>? ConfigureCommand { get; init; }
}
```

- [ ] **Step 2: Create command result record**

Create `scripts/shared/NativeCommandResult.cs`.

```csharp
/// <summary>
/// Captures the result of a native command invocation.
/// </summary>
/// <remarks>
/// Standard output and standard error are captured even when the runner also mirrors them to the console.
/// Consumers should inspect <see cref="ExitCode"/> instead of parsing output when possible.
/// </remarks>
public sealed record NativeCommandResult
{
    /// <summary>
    /// Gets the native process exit code.
    /// </summary>
    public required int ExitCode { get; init; }

    /// <summary>
    /// Gets the captured standard output text.
    /// </summary>
    public required string StandardOutput { get; init; }

    /// <summary>
    /// Gets the captured standard error text.
    /// </summary>
    public required string StandardError { get; init; }
}
```

- [ ] **Step 3: Create `CliWrap` runner**

Create `scripts/shared/NativeCommandRunner.cs`.

```csharp
using System.Text;
using CliWrap;

/// <summary>
/// Runs native commands using <c>CliWrap</c>.
/// </summary>
/// <remarks>
/// Commands are executed without a shell.
/// Default output pipes capture stdout and stderr while mirroring both streams to the current console streams.
/// A command spec can replace or extend the built command through <see cref="NativeCommandSpec.ConfigureCommand"/>.
/// </remarks>
public static class NativeCommandRunner
{
    /// <summary>
    /// Executes a native command asynchronously.
    /// </summary>
    /// <param name="spec">Command specification that provides executable, arguments, working directory, and failure behavior.</param>
    /// <param name="cancellationToken">Token that cancels command execution.</param>
    /// <returns>Captured command result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="spec"/> is <see langword="null"/>.</exception>
    /// <exception cref="ScriptConfigurationException">Thrown when <paramref name="spec"/> contains an empty executable or working directory.</exception>
    /// <exception cref="NativeCommandException">Thrown when the native process cannot start, `CliWrap` validation fails, or the process exits with a non-zero code while <see cref="NativeCommandSpec.ThrowOnFailure"/> is enabled.</exception>
    public static async Task<NativeCommandResult> RunAsync(NativeCommandSpec spec, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        if (string.IsNullOrWhiteSpace(spec.Executable))
        {
            throw new ScriptConfigurationException("Native command executable is empty.");
        }

        if (string.IsNullOrWhiteSpace(spec.WorkingDirectory))
        {
            throw new ScriptConfigurationException($"Native command '{spec.Executable}' has no working directory.");
        }

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var result = default(CommandResult);

        try
        {
            var command = Cli.Wrap(spec.Executable)
                .WithArguments(spec.Arguments)
                .WithWorkingDirectory(spec.WorkingDirectory)
                .WithValidation(spec.Validation)
                .WithStandardOutputPipe(PipeTarget.Merge(
                    PipeTarget.ToStringBuilder(stdout),
                    PipeTarget.ToDelegate(Console.Out.WriteLine)))
                .WithStandardErrorPipe(PipeTarget.Merge(
                    PipeTarget.ToStringBuilder(stderr),
                    PipeTarget.ToDelegate(Console.Error.WriteLine)));

            command = spec.ConfigureCommand?.Invoke(command) ?? command;
            result = await command.ExecuteAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not ScriptException)
        {
            throw new NativeCommandException(
                $"Native command '{spec.Executable}' failed before normal result handling in '{spec.WorkingDirectory}'. Verify that the executable exists, command validation is intentional, and custom pipes are valid.",
                spec.Executable,
                spec.Arguments,
                spec.WorkingDirectory,
                null,
                stdout.ToString(),
                stderr.ToString(),
                ex);
        }

        var nativeResult = new NativeCommandResult
        {
            ExitCode = result.ExitCode,
            StandardOutput = stdout.ToString(),
            StandardError = stderr.ToString(),
        };

        if (spec.ThrowOnFailure && result.ExitCode != 0)
        {
            throw new NativeCommandException(
                $"Native command '{spec.Executable}' failed with exit code {result.ExitCode} in '{spec.WorkingDirectory}'. Arguments: {string.Join(' ', spec.Arguments)}",
                spec.Executable,
                spec.Arguments,
                spec.WorkingDirectory,
                result.ExitCode,
                nativeResult.StandardOutput,
                nativeResult.StandardError);
        }

        return nativeResult;
    }
}
```

- [ ] **Step 4: Verify file-based package restore and compile**

Create `scripts/native-command-probe.cs`.

```csharp
#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false
#:property RestorePackagesWithLockFile=false
#:property ExperimentalFileBasedProgramEnableTransitiveDirectives=true
#:include shared/*.cs

var result = await NativeCommandRunner.RunAsync(new NativeCommandSpec
{
    Executable = "dotnet",
    Arguments = ["--version"],
    WorkingDirectory = Directory.GetCurrentDirectory(),
});

Console.WriteLine(result.ExitCode);
```

Run: `dotnet run --file scripts/native-command-probe.cs`
Expected: stdout includes the .NET SDK version and `0`.
Delete `scripts/native-command-probe.cs` after verification passes.

- [ ] **Step 5: Document direct pipe usage for command-specific scripts**

Use direct `CliWrap` pipe composition when a script needs command-specific output routing that does not fit `NativeCommandRunner`.
Keep this pattern in `scripts/semantic-release/README.md`.

```csharp
var command = Cli.Wrap("mysqldump")
    .WithArguments(["-u", "root", "mydb"])
    .WithValidation(CommandResultValidation.None)
    | PipeTarget.Merge(
        PipeTarget.ToDelegate(Console.WriteLine),
        PipeTarget.ToFile("backup.sql"));

await command.ExecuteAsync();
```

Use tuple piping when stdout and stderr should go to separate streams without extra capture.

```csharp
await using var stdOut = Console.OpenStandardOutput();
await using var stdErr = Console.OpenStandardError();

var command = Cli.Wrap("docker")
    .WithArguments(["build", "."])
    .WithValidation(CommandResultValidation.None)
    | (stdOut, stdErr);

await command.ExecuteAsync();
```

Prefer `NativeCommandRunner` for normal repository automation.
Use direct pipe composition only when a command needs file output, binary streams, merged targets, or output routing that the shared runner should not own.

## Task 3: Git Runtime With LibGit2Sharp

**Files:**
- Create: `scripts/shared/GitRepositoryContext.cs`
- Create: `scripts/shared/GitScriptIndex.cs`

- [ ] **Step 1: Create repository context**

Create `scripts/shared/GitRepositoryContext.cs`.

```csharp
using LibGit2Sharp;

/// <summary>
/// Opens the Git repository that contains a script invocation path.
/// </summary>
/// <remarks>
/// The repository is discovered with <see cref="Repository.Discover(string)"/>.
/// Dispose this context to release native libgit2 resources.
/// </remarks>
public sealed class GitRepositoryContext : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GitRepositoryContext"/> class.
    /// </summary>
    /// <param name="startPath">Path used as the repository discovery starting point.</param>
    /// <exception cref="GitScriptException">Thrown when no Git repository can be discovered from <paramref name="startPath"/>.</exception>
    public GitRepositoryContext(string startPath)
    {
        var repositoryPath = Repository.Discover(startPath);

        if (repositoryPath is null)
        {
            throw new GitScriptException($"Could not discover a Git repository from '{startPath}'. Run the script from inside the repository working tree.");
        }

        Repository = new Repository(repositoryPath);
        WorkingDirectory = Repository.Info.WorkingDirectory;
    }

    /// <summary>
    /// Gets the opened repository.
    /// </summary>
    public Repository Repository { get; }

    /// <summary>
    /// Gets the repository working directory.
    /// </summary>
    public string WorkingDirectory { get; }

    /// <inheritdoc />
    public void Dispose()
        => Repository.Dispose();
}
```

- [ ] **Step 2: Create Git script index helper**

Create `scripts/shared/GitScriptIndex.cs`.

```csharp
using LibGit2Sharp;

/// <summary>
/// Provides Git index operations used by repository automation scripts.
/// </summary>
/// <remarks>
/// All operations use `LibGit2Sharp`.
/// This type does not call the `git` executable.
/// </remarks>
public sealed class GitScriptIndex
{
    private readonly Repository repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GitScriptIndex"/> class.
    /// </summary>
    /// <param name="repository">Repository whose index will be inspected or updated.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="repository"/> is <see langword="null"/>.</exception>
    public GitScriptIndex(Repository repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    /// <summary>
    /// Gets staged shell script paths from the Git index.
    /// </summary>
    /// <returns>Repository-relative paths for added, copied, modified, renamed, or type-changed staged shell scripts.</returns>
    /// <remarks>
    /// Deleted files are excluded because no executable bit or staged content can be repaired for them.
    /// </remarks>
    /// <exception cref="GitScriptException">Thrown when `LibGit2Sharp` cannot read repository status.</exception>
    public IReadOnlyList<string> GetStagedShellScripts()
    {
        try
        {
            return repository.RetrieveStatus(new StatusOptions())
                .Where(item => item.FilePath.EndsWith(".sh", StringComparison.OrdinalIgnoreCase))
                .Where(item => (item.State & (FileStatus.NewInIndex | FileStatus.ModifiedInIndex | FileStatus.RenamedInIndex | FileStatus.TypeChangeInIndex)) != 0)
                .Select(item => item.FilePath)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception ex)
        {
            throw new GitScriptException($"Failed to read staged shell scripts from repository '{repository.Info.WorkingDirectory}'.", ex);
        }
    }

    /// <summary>
    /// Gets tracked shell script paths from the Git index.
    /// </summary>
    /// <returns>Repository-relative shell script paths currently tracked by Git.</returns>
    /// <exception cref="GitScriptException">Thrown when the index cannot be enumerated.</exception>
    public IReadOnlyList<string> GetTrackedShellScripts()
    {
        try
        {
            return repository.Index
                .Where(entry => entry.Path.EndsWith(".sh", StringComparison.OrdinalIgnoreCase))
                .Select(entry => entry.Path)
                .Order(StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception ex)
        {
            throw new GitScriptException($"Failed to enumerate tracked shell scripts from repository '{repository.Info.WorkingDirectory}'.", ex);
        }
    }

    /// <summary>
    /// Determines whether the staged blob for a path contains CRLF line endings.
    /// </summary>
    /// <param name="path">Repository-relative path to inspect in the index.</param>
    /// <returns><see langword="true"/> when the staged blob contains a CRLF byte sequence, otherwise <see langword="false"/>.</returns>
    /// <remarks>
    /// This inspects staged blob bytes, not the working tree file, so it catches exactly what would be committed.
    /// </remarks>
    /// <exception cref="GitScriptException">Thrown when the path is not present in the index or its blob cannot be read.</exception>
    public bool HasCrLfInStagedBlob(string path)
    {
        try
        {
            var entry = repository.Index[path] ?? throw new GitScriptException($"Path '{path}' is not present in the Git index.");
            var blob = repository.Lookup<Blob>(entry.Id) ?? throw new GitScriptException($"Path '{path}' points to missing blob '{entry.Id}'.");

            using var stream = blob.GetContentStream();
            var previous = -1;
            var current = stream.ReadByte();

            while (current >= 0)
            {
                if (previous == '\r' && current == '\n')
                {
                    return true;
                }

                previous = current;
                current = stream.ReadByte();
            }

            return false;
        }
        catch (ScriptException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GitScriptException($"Failed to inspect staged line endings for '{path}'.", ex);
        }
    }

    /// <summary>
    /// Marks an indexed file as executable in the Git index.
    /// </summary>
    /// <param name="path">Repository-relative path to update.</param>
    /// <remarks>
    /// The method rewrites the index entry with <see cref="Mode.ExecutableFile"/> while preserving the staged blob object.
    /// It calls <see cref="Index.Write"/> immediately so later Git commands and hooks observe the mode change.
    /// </remarks>
    /// <exception cref="GitScriptException">Thrown when the path is not present in the index, the staged blob is missing, or the index write fails.</exception>
    public void MarkExecutableInIndex(string path)
    {
        try
        {
            var entry = repository.Index[path] ?? throw new GitScriptException($"Path '{path}' is not present in the Git index.");
            var blob = repository.Lookup<Blob>(entry.Id) ?? throw new GitScriptException($"Path '{path}' points to missing blob '{entry.Id}'.");
            repository.Index.Add(blob, path, Mode.ExecutableFile);
            repository.Index.Write();
        }
        catch (ScriptException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new GitScriptException($"Failed to mark '{path}' executable in the Git index.", ex);
        }
    }
}
```

- [ ] **Step 3: Verify Git runtime compiles**

Create `scripts/git-probe.cs`.

```csharp
#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false
#:property RestorePackagesWithLockFile=false
#:property ExperimentalFileBasedProgramEnableTransitiveDirectives=true
#:include shared/*.cs

using var context = new GitRepositoryContext(Directory.GetCurrentDirectory());
var index = new GitScriptIndex(context.Repository);
Console.WriteLine(context.WorkingDirectory);
Console.WriteLine(index.GetTrackedShellScripts().Count);
```

Run: `dotnet run --file scripts/git-probe.cs`
Expected: stdout includes the repository working directory and a shell script count.
Delete `scripts/git-probe.cs` after verification passes.

## Task 4: Husky File-Based Script Entries

**Files:**
- Create: `scripts/prepare-shell-scripts.cs`
- Create: `scripts/update-shell-script-permissions.cs`
- Modify: `.husky/task-runner.json`
- Delete: `scripts/prepare-shell-scripts.sh`
- Delete: `scripts/prepare-shell-scripts.ps1`
- Delete: `scripts/update-shell-script-permissions.sh`
- Delete: `scripts/update-shell-script-permissions.ps1`

- [ ] **Step 1: Create staged shell preparation entry**

Create `scripts/prepare-shell-scripts.cs`.

```csharp
#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false
#:property RestorePackagesWithLockFile=false
#:property ExperimentalFileBasedProgramEnableTransitiveDirectives=true
#:include shared/*.cs

try
{
    using var context = new GitRepositoryContext(Directory.GetCurrentDirectory());
    var index = new GitScriptIndex(context.Repository);
    var shellScripts = index.GetStagedShellScripts();

    foreach (var file in shellScripts)
    {
        index.MarkExecutableInIndex(file);
    }

    var failedFiles = shellScripts.Where(index.HasCrLfInStagedBlob).ToArray();

    if (failedFiles.Length > 0)
    {
        Console.Error.WriteLine("Shell scripts must use LF line endings before commit:");

        foreach (var file in failedFiles)
        {
            Console.Error.WriteLine($"  - {file}");
        }

        Console.Error.WriteLine("Convert the files to LF and stage them again.");
        return 1;
    }

    return 0;
}
catch (ScriptException ex)
{
    Console.Error.WriteLine(ex.Message);
    return ex.ExitCode;
}
```

- [ ] **Step 2: Create repository-wide shell permission entry**

Create `scripts/update-shell-script-permissions.cs`.

```csharp
#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false
#:property RestorePackagesWithLockFile=false
#:property ExperimentalFileBasedProgramEnableTransitiveDirectives=true
#:include shared/*.cs

try
{
    using var context = new GitRepositoryContext(Directory.GetCurrentDirectory());
    Directory.SetCurrentDirectory(context.WorkingDirectory);
    var index = new GitScriptIndex(context.Repository);
    var shellScripts = index.GetTrackedShellScripts();

    foreach (var file in shellScripts)
    {
        index.MarkExecutableInIndex(file);
    }

    if (shellScripts.Count > 0)
    {
        Console.WriteLine("Marked tracked shell scripts executable in the Git index:");

        foreach (var file in shellScripts)
        {
            Console.WriteLine($"  - {file}");
        }
    }

    return 0;
}
catch (ScriptException ex)
{
    Console.Error.WriteLine(ex.Message);
    return ex.ExitCode;
}
```

- [ ] **Step 3: Update Husky task runner**

Modify `.husky/task-runner.json`.

```json
{
   "$schema": "https://alirezanet.github.io/Husky.Net/schema.json",
   "tasks": [
       {
           "name": "prepare-shell-scripts",
           "group": "pre-commit",
           "command": "dotnet",
           "args": [ "run", "--file", "scripts/prepare-shell-scripts.cs" ]
       },
       {
           "name": "update-shell-script-permissions",
           "command": "dotnet",
           "args": [ "run", "--file", "scripts/update-shell-script-permissions.cs" ]
       },
       {
           "name": "dotnet-format-check",
           "group": "pre-commit",
           "command": "dotnet",
           "args": [ "format", "Template.slnx", "--verify-no-changes", "--verbosity", "diagnostic", "--no-restore" ]
       },
       {
           "name": "dotnet-format",
           "command": "dotnet",
           "args": [ "format", "Template.slnx", "--verbosity", "diagnostic", "--no-restore" ]
       }
   ]
}
```

- [ ] **Step 4: Verify Husky scripts**

Run: `dotnet run --file scripts/prepare-shell-scripts.cs`
Expected: exit code `0`.
Run: `dotnet run --file scripts/update-shell-script-permissions.cs`
Expected: exit code `0`.
Run: `dotnet husky run --name prepare-shell-scripts`
Expected: exit code `0`.
Run: `dotnet husky run --name update-shell-script-permissions`
Expected: exit code `0`.

## Task 5: act File-Based Script Entries

**Files:**
- Create: `scripts/act/ActRunner.cs`
- Create: `scripts/act/test-pr.cs`
- Create: `scripts/act/test-ci.cs`
- Delete: `scripts/act/test-pr.sh`
- Delete: `scripts/act/test-pr.ps1`
- Delete: `scripts/act/test-ci.sh`
- Delete: `scripts/act/test-ci.ps1`

- [ ] **Step 1: Create documented act runner**

Create `scripts/act/ActRunner.cs`.

```csharp
/// <summary>
/// Runs local GitHub Actions jobs through `act`.
/// </summary>
/// <remarks>
/// The runner honors <c>ACT_BIN</c> first, then probes <c>act</c> and <c>act.exe</c> through <see cref="NativeCommandRunner"/>.
/// It creates `.act/artifacts` before starting `act` because the workflow wrappers expect artifacts to land there.
/// </remarks>
public static class ActRunner
{
    /// <summary>
    /// Runs a single `act` event against `.github/workflows/main.yaml`.
    /// </summary>
    /// <param name="eventName">The act event name, such as <c>pull_request</c> or <c>push</c>.</param>
    /// <param name="eventFile">Path to the JSON event payload.</param>
    /// <returns>The act process exit code.</returns>
    /// <exception cref="ScriptConfigurationException">Thrown when <paramref name="eventName"/> or <paramref name="eventFile"/> is empty.</exception>
    /// <exception cref="NativeCommandException">Thrown when `act` cannot be found or exits with a non-zero code.</exception>
    public static async Task<int> RunAsync(string eventName, string eventFile)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            throw new ScriptConfigurationException("act event name is empty.");
        }

        if (string.IsNullOrWhiteSpace(eventFile))
        {
            throw new ScriptConfigurationException("act event file is empty.");
        }

        var actBinary = await ResolveActBinaryAsync();
        Directory.CreateDirectory(".act/artifacts");
        var result = await NativeCommandRunner.RunAsync(new NativeCommandSpec
        {
            Executable = actBinary,
            Arguments =
            [
                eventName,
                "-W",
                ".github/workflows/main.yaml",
                "-j",
                "test",
                "--artifact-server-path",
                ".act/artifacts",
                "-e",
                eventFile,
            ],
            WorkingDirectory = Directory.GetCurrentDirectory(),
        });

        return result.ExitCode;
    }

    /// <summary>
    /// Resolves the `act` executable.
    /// </summary>
    /// <returns>The executable name or path that should be passed to <see cref="NativeCommandRunner"/>.</returns>
    /// <remarks>
    /// The method uses `ACT_BIN` when present and probes candidates with `--version` without treating a non-zero exit as fatal.
    /// </remarks>
    /// <exception cref="NativeCommandException">Thrown with exit code `127` when no act executable can be resolved.</exception>
    private static async Task<string> ResolveActBinaryAsync()
    {
        var configured = Environment.GetEnvironmentVariable("ACT_BIN");

        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        foreach (var candidate in OperatingSystem.IsWindows() ? ["act", "act.exe"] : new[] { "act" })
        {
            var result = await NativeCommandRunner.RunAsync(new NativeCommandSpec
            {
                Executable = candidate,
                Arguments = ["--version"],
                WorkingDirectory = Directory.GetCurrentDirectory(),
                ThrowOnFailure = false,
            });

            if (result.ExitCode == 0)
            {
                return candidate;
            }
        }

        throw new NativeCommandException(
            "act executable not found. Install act or set ACT_BIN to its executable path.",
            "act",
            ["--version"],
            Directory.GetCurrentDirectory(),
            127,
            string.Empty,
            string.Empty);
    }
}
```

- [ ] **Step 2: Create act entry files**

Create `scripts/act/test-pr.cs`.

```csharp
#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false
#:property RestorePackagesWithLockFile=false
#:property ExperimentalFileBasedProgramEnableTransitiveDirectives=true
#:include ../shared/*.cs
#:include ActRunner.cs

try
{
    return await ActRunner.RunAsync("pull_request", ".act/events/pull_request.json");
}
catch (ScriptException ex)
{
    Console.Error.WriteLine(ex.Message);
    return ex.ExitCode;
}
```

Create `scripts/act/test-ci.cs`.

```csharp
#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false
#:property RestorePackagesWithLockFile=false
#:property ExperimentalFileBasedProgramEnableTransitiveDirectives=true
#:include ../shared/*.cs
#:include ActRunner.cs

try
{
    return await ActRunner.RunAsync("push", ".act/events/push-ci.json");
}
catch (ScriptException ex)
{
    Console.Error.WriteLine(ex.Message);
    return ex.ExitCode;
}
```

- [ ] **Step 3: Verify act wrappers**

Run: `dotnet run --file scripts/act/test-pr.cs`
Expected when `act` is absent: exit code `127` and stderr contains `act executable not found`.
Expected when `act` is installed: the `pull_request` test job starts and `.act/artifacts` is created.

## Task 6: semantic-release Shared Runtime

**Files:**
- Create: `scripts/semantic-release/shared/ReleaseDefaults.cs`
- Create: `scripts/semantic-release/shared/ReleaseSubScripts.cs`
- Create: `scripts/semantic-release/shared/ReleaseValidation.cs`

- [ ] **Step 1: Create release defaults record with required properties**

Create `scripts/semantic-release/shared/ReleaseDefaults.cs`.

```csharp
/// <summary>
/// Stores semantic-release defaults and derived release paths.
/// </summary>
/// <remarks>
/// Values are loaded from environment variables on each entry-point invocation.
/// Defaults match the previous Bash scripts.
/// </remarks>
public sealed record ReleaseDefaults
{
    /// <summary>
    /// Gets the .NET build configuration used by package and container build commands.
    /// </summary>
    public required string Configuration { get; init; }

    /// <summary>
    /// Gets the container registry host used by Docker image helpers.
    /// </summary>
    public required string Registry { get; init; }

    /// <summary>
    /// Gets the root release artifact directory.
    /// </summary>
    public required string ArtifactsDirectory { get; init; }

    /// <summary>
    /// Gets the root directory for prepared NuGet packages.
    /// </summary>
    public required string NuGetArtifactsDirectory { get; init; }

    /// <summary>
    /// Gets the directory where Aspire emits Helm manifest files.
    /// </summary>
    public required string HelmManifestDirectory { get; init; }

    /// <summary>
    /// Gets the directory where packaged Helm chart archives are stored.
    /// </summary>
    public required string HelmChartDirectory { get; init; }

    /// <summary>
    /// Gets the GitHub owner derived from <c>GITHUB_OWNER</c> or <c>GITHUB_REPOSITORY</c>.
    /// </summary>
    /// <value>
    /// <see langword="null"/> when neither environment variable is available.
    /// </value>
    public string? GitHubOwner { get; init; }

    /// <summary>
    /// Loads release defaults from the current process environment.
    /// </summary>
    /// <returns>Release defaults with all required path properties populated.</returns>
    public static ReleaseDefaults Load()
    {
        var artifacts = ScriptEnvironment.GetOrDefault("RELEASE_ARTIFACTS_DIR", "artifacts");
        var githubOwner = Environment.GetEnvironmentVariable("GITHUB_OWNER");
        var githubRepository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY");

        if (string.IsNullOrWhiteSpace(githubOwner) && !string.IsNullOrWhiteSpace(githubRepository))
        {
            githubOwner = githubRepository.Split('/')[0];
        }

        return new ReleaseDefaults
        {
            Configuration = ScriptEnvironment.GetOrDefault("CONFIGURATION", "Release"),
            Registry = ScriptEnvironment.GetOrDefault("REGISTRY", "ghcr.io"),
            ArtifactsDirectory = artifacts,
            NuGetArtifactsDirectory = ScriptEnvironment.GetOrDefault("RELEASE_NUGET_ARTIFACTS_DIR", $"{artifacts}/nuget"),
            HelmManifestDirectory = ScriptEnvironment.GetOrDefault("RELEASE_HELM_MANIFEST_DIR", $"{artifacts}/helm/manifest"),
            HelmChartDirectory = ScriptEnvironment.GetOrDefault("RELEASE_HELM_CHART_DIR", $"{artifacts}/helm/chart"),
            GitHubOwner = githubOwner,
        };
    }
}
```

- [ ] **Step 2: Create release sub-script runner**

Create `scripts/semantic-release/shared/ReleaseSubScripts.cs`.

```csharp
/// <summary>
/// Finds and runs semantic-release phase sub-scripts.
/// </summary>
/// <remarks>
/// Sub-scripts are matched by filename prefix in the phase directory and run in ordinal filename order.
/// Each child script is executed through <see cref="NativeCommandRunner"/> as <c>dotnet run --file</c>.
/// </remarks>
public static class ReleaseSubScripts
{
    /// <summary>
    /// Runs all C# file-based sub-scripts in a directory whose filenames start with a prefix.
    /// </summary>
    /// <param name="directory">Phase directory that contains child `.cs` files.</param>
    /// <param name="prefix">Filename prefix used to select child scripts.</param>
    /// <returns>A task that completes after all matching sub-scripts complete.</returns>
    /// <exception cref="ScriptConfigurationException">Thrown when <paramref name="directory"/> or <paramref name="prefix"/> is empty.</exception>
    /// <exception cref="ReleaseTargetException">Thrown when the directory does not exist or a child script exits with a non-zero code.</exception>
    /// <exception cref="NativeCommandException">Thrown when `dotnet` cannot be started.</exception>
    public static async Task RunWithPrefixAsync(string directory, string prefix)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ScriptConfigurationException("Release sub-script directory is empty.");
        }

        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ScriptConfigurationException("Release sub-script prefix is empty.");
        }

        if (!Directory.Exists(directory))
        {
            throw new ReleaseTargetException($"Release sub-script directory '{directory}' does not exist.");
        }

        var scripts = Directory
            .EnumerateFiles(directory, $"{prefix}*.cs", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal)
            .ToArray();

        if (scripts.Length == 0)
        {
            Console.Error.WriteLine($"no sub-scripts found in {directory} with prefix {prefix}");
            return;
        }

        foreach (var script in scripts)
        {
            Console.Error.WriteLine($"running {script}");
            var result = await NativeCommandRunner.RunAsync(new NativeCommandSpec
            {
                Executable = "dotnet",
                Arguments = ["run", "--file", script],
                WorkingDirectory = Directory.GetCurrentDirectory(),
                ThrowOnFailure = false,
            });

            if (result.ExitCode != 0)
            {
                throw new ReleaseTargetException($"Release sub-script '{script}' failed with exit code {result.ExitCode}.");
            }
        }
    }
}
```

- [ ] **Step 3: Create release validation helpers**

Create `scripts/semantic-release/shared/ReleaseValidation.cs`.

```csharp
/// <summary>
/// Provides validation helpers for semantic-release scripts.
/// </summary>
/// <remarks>
/// Methods throw <see cref="ReleaseTargetException"/> with target-specific context so semantic-release logs explain which release target failed and why.
/// </remarks>
public static class ReleaseValidation
{
    /// <summary>
    /// Verifies that a native command is available.
    /// </summary>
    /// <param name="name">Executable name to probe with <c>--version</c>.</param>
    /// <returns>A task that completes after the probe command exits successfully.</returns>
    /// <exception cref="ScriptConfigurationException">Thrown when <paramref name="name"/> is empty.</exception>
    /// <exception cref="ReleaseTargetException">Thrown when the command exits with a non-zero code.</exception>
    /// <exception cref="NativeCommandException">Thrown when the executable cannot be started.</exception>
    public static async Task RequireCommandAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ScriptConfigurationException("Required command name is empty.");
        }

        var result = await NativeCommandRunner.RunAsync(new NativeCommandSpec
        {
            Executable = name,
            Arguments = ["--version"],
            WorkingDirectory = Directory.GetCurrentDirectory(),
            ThrowOnFailure = false,
        });

        if (result.ExitCode != 0)
        {
            throw new ReleaseTargetException($"Required command '{name}' is not available. Install it or make it available on PATH before running release automation.");
        }
    }

    /// <summary>
    /// Verifies that a required file exists.
    /// </summary>
    /// <param name="path">File path to verify.</param>
    /// <param name="targetKind">Human-readable target kind used in the failure message.</param>
    /// <exception cref="ScriptConfigurationException">Thrown when <paramref name="path"/> or <paramref name="targetKind"/> is empty.</exception>
    /// <exception cref="ReleaseTargetException">Thrown when the file does not exist.</exception>
    public static void RequireFile(string path, string targetKind)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ScriptConfigurationException("Required file path is empty.");
        }

        if (string.IsNullOrWhiteSpace(targetKind))
        {
            throw new ScriptConfigurationException("Required file target kind is empty.");
        }

        if (!File.Exists(path))
        {
            throw new ReleaseTargetException($"{targetKind} file '{path}' does not exist.");
        }
    }

    /// <summary>
    /// Verifies that a required directory exists.
    /// </summary>
    /// <param name="path">Directory path to verify.</param>
    /// <param name="targetKind">Human-readable target kind used in the failure message.</param>
    /// <exception cref="ScriptConfigurationException">Thrown when <paramref name="path"/> or <paramref name="targetKind"/> is empty.</exception>
    /// <exception cref="ReleaseTargetException">Thrown when the directory does not exist.</exception>
    public static void RequireDirectory(string path, string targetKind)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ScriptConfigurationException("Required directory path is empty.");
        }

        if (string.IsNullOrWhiteSpace(targetKind))
        {
            throw new ScriptConfigurationException("Required directory target kind is empty.");
        }

        if (!Directory.Exists(path))
        {
            throw new ReleaseTargetException($"{targetKind} directory '{path}' does not exist.");
        }
    }

    /// <summary>
    /// Writes a release target skip message to standard error.
    /// </summary>
    /// <param name="message">Skip reason written after the word `Skipping`.</param>
    /// <remarks>
    /// Semantic-release can treat stdout as parseable output in some phases, so skip logs go to stderr.
    /// </remarks>
    /// <exception cref="ScriptConfigurationException">Thrown when <paramref name="message"/> is empty.</exception>
    public static void SkipReleaseTarget(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ScriptConfigurationException("Release target skip message is empty.");
        }

        Console.Error.WriteLine($"Skipping {message}");
    }
}
```

## Task 7: semantic-release Phase Entries

**Files:**
- Create: `scripts/semantic-release/100-verify/verify.cs`
- Create: `scripts/semantic-release/100-verify/VerifyTargets.cs`
- Create: `scripts/semantic-release/100-verify/101-verify_server.cs`
- Create: `scripts/semantic-release/100-verify/102-verify_nuget.cs`
- Create: `scripts/semantic-release/100-verify/103-verify_helm.cs`
- Create: `scripts/semantic-release/200-prepare/prepare.cs`
- Create: `scripts/semantic-release/200-prepare/PrepareTargets.cs`
- Create: `scripts/semantic-release/200-prepare/201-prepare_server.cs`
- Create: `scripts/semantic-release/200-prepare/202-prepare_nuget.cs`
- Create: `scripts/semantic-release/200-prepare/203-prepare_helm.cs`
- Create: `scripts/semantic-release/300-publish/publish.cs`
- Create: `scripts/semantic-release/300-publish/PublishTargets.cs`
- Create: `scripts/semantic-release/300-publish/301-publish_server.cs`
- Create: `scripts/semantic-release/300-publish/302-publish_nuget.cs`
- Create: `scripts/semantic-release/300-publish/303-publish_helm.cs`
- Delete: matching `.sh` files after verification

- [ ] **Step 1: Create verify phase**

Replace `verify.sh`, the helper logic from `verify-common.sh`, `101-verify_server.sh`, `102-verify_nuget.sh`, and `103-verify_helm.sh`.
Name the helper file `VerifyTargets.cs` because it contains verify target helper methods, not a generic common module.
Start every executable verify entry and leaf file with `#!/usr/bin/env -S dotnet --`.
Use `#:include ../../shared/*.cs`, `#:include ../shared/*.cs`, and `#:include VerifyTargets.cs` after the shebang.
Require `VERSION`, `VERSION_TAG`, and `VERSION_CHANNEL`.
Run `dotnet tool restore` through `NativeCommandRunner`.
Validate Docker, NuGet, and Helm targets with documented helpers.
Keep server and NuGet default target calls commented as C# comments.
Keep Helm disabled by default through `RELEASE_HELM_ENABLED`.

- [ ] **Step 2: Create prepare phase**

Replace `prepare.sh`, the helper logic from `prepare-common.sh`, `201-prepare_server.sh`, `202-prepare_nuget.sh`, and `203-prepare_helm.sh`.
Name the helper file `PrepareTargets.cs` because it contains artifact preparation helper methods, not a generic common module.
Start every executable prepare entry and leaf file with `#!/usr/bin/env -S dotnet --`.
Use `#:` directives after the shebang.
Use `NativeCommandRunner` for `dotnet setversion`, `dotnet pack`, `docker buildx build`, `dotnet aspire publish`, `helm lint`, and `helm package`.
Keep all native command arguments equivalent to the Bash scripts.
Keep logs on stderr where semantic-release expects parseable stdout.

- [ ] **Step 3: Create publish phase**

Replace `publish.sh`, the helper logic from `publish-common.sh`, `301-publish_server.sh`, `302-publish_nuget.sh`, and `303-publish_helm.sh`.
Name the helper file `PublishTargets.cs` because it contains artifact publishing helper methods, not a generic common module.
Start every executable publish entry and leaf file with `#!/usr/bin/env -S dotnet --`.
Use `#:` directives after the shebang.
Use `NativeCommandRunner` for `dotnet nuget push`, `docker buildx build --push`, and `helm push`.
Preserve stable-channel Docker `latest` tag behavior.
Preserve NuGet `--skip-duplicate`.
Preserve Helm owner lowercase behavior.

- [ ] **Step 4: Verify safe default phases**

Run with PowerShell:

```powershell
$env:VERSION='1.2.3'
$env:VERSION_TAG='v1.2.3'
$env:VERSION_CHANNEL='ci'
dotnet run --file scripts/semantic-release/100-verify/verify.cs
dotnet run --file scripts/semantic-release/300-publish/publish.cs
```

Expected: both commands exit with code `0` when optional release targets remain disabled.
Expected stderr includes Helm skip messages.

## Task 8: Config, Docs, And Solution Ties

**Files:**
- Modify: `.husky/task-runner.json`
- Modify: `release.config.mjs`
- Modify: `README.md`
- Modify: `scripts/semantic-release/README.md`
- Modify: `Template.slnx`
- Modify: `AGENTS.md`

- [ ] **Step 1: Update semantic-release exec commands**

Use this pattern in `release.config.mjs`.

```javascript
verifyReleaseCmd:
    "VERSION=${nextRelease.version} " +
    "VERSION_TAG=${nextRelease.gitTag} " +
    "VERSION_CHANNEL=${nextRelease.channel} " +
    "dotnet run --file ./scripts/semantic-release/100-verify/verify.cs",
prepareCmd:
    "VERSION=${nextRelease.version} " +
    "VERSION_TAG=${nextRelease.gitTag} " +
    "VERSION_CHANNEL=${nextRelease.channel} " +
    "dotnet run --file ./scripts/semantic-release/200-prepare/prepare.cs",
publishCmd:
    "VERSION=${nextRelease.version} " +
    "VERSION_TAG=${nextRelease.gitTag} " +
    "VERSION_CHANNEL=${nextRelease.channel} " +
    "dotnet run --file ./scripts/semantic-release/300-publish/publish.cs",
```

- [ ] **Step 2: Update README commands**

Use `dotnet run --file scripts/act/test-pr.cs`.
Use `dotnet run --file scripts/act/test-ci.cs`.
Use `dotnet run --file scripts/semantic-release/100-verify/verify.cs`.
Use `dotnet run --file scripts/semantic-release/200-prepare/prepare.cs`.
Use `dotnet run --file scripts/semantic-release/300-publish/publish.cs`.

- [ ] **Step 3: Update semantic-release README**

Rename documented entry points from `.sh` to `.cs`.
Rename sub-script matching from `*.sh` to `*.cs`.
Document that entry files use `#:include` for shared logic and that script package references live in `scripts/shared/Packages.cs`.
Document that executable entry files start with `#!/usr/bin/env -S dotnet --`.
Document that logs which semantic-release must not parse stay on stderr.

- [ ] **Step 4: Update AGENTS.md durable convention**

Document that repository automation scripts live as C# file-based apps run with `dotnet run --file`.
Document that executable file-based script entries start with `#!/usr/bin/env -S dotnet --`.
Document that shared script logic uses `#:include`.
Document that native command execution uses `CliWrap`.
Document that Git automation uses `LibGit2Sharp`.
Document that script public APIs and exceptions require XML docs.

- [ ] **Step 5: Update solution file**

Remove every deleted `.sh` and `.ps1` script entry.
Add every created `.cs` script entry under the existing `/scripts/` solution folder.

## Task 9: Remove Bash And PowerShell Script Surface

**Files:**
- Delete: all migrated `scripts/**/*.sh`
- Delete: duplicated `scripts/**/*.ps1` wrappers replaced by `.cs`
- Modify: docs and config files with stale references

- [ ] **Step 1: Verify no migrated files remain**

Run: `rg --files scripts | rg "\\.(sh|ps1)$"`
Expected: no output.

- [ ] **Step 2: Verify no stale script references remain**

Run: `rg -n "scripts/.*\\.(sh|ps1)|\\.sh|\\.ps1|bash|powershell" README.md scripts release.config.mjs .husky Template.slnx AGENTS.md`
Expected: no references to deleted script paths.
Expected remaining matches are accepted only when they describe external shell examples or GitHub hook internals outside the migrated script surface.

- [ ] **Step 3: Verify no forbidden process or Git CLI usage exists**

Run: `rg -n "ProcessStartInfo|System\\.Diagnostics\\.Process|\\bgit\\b|bash|powershell|cmd\\.exe" scripts --glob "*.cs"`
Expected: no matches for process APIs, shell launch, or `git` native command execution.
Expected matches for documentation comments are accepted only if they explicitly say the code does not use them.

## Task 10: Full Verification

**Files:**
- Verify: whole repository

- [ ] **Step 1: Restore tools**

Run: `dotnet tool restore`
Expected: exit code `0`.

- [ ] **Step 2: Restore solution**

Run: `dotnet restore Template.slnx --locked-mode`
Expected: exit code `0`.

- [ ] **Step 3: Build solution**

Run: `dotnet build Template.slnx --no-restore`
Expected: exit code `0`.

- [ ] **Step 4: Test solution**

Run: `dotnet test Template.slnx --no-build`
Expected: exit code `0`.

- [ ] **Step 5: Format solution**

Run: `dotnet format Template.slnx --verbosity diagnostic --no-restore`
Expected: exit code `0`.

- [ ] **Step 6: Run Husky tasks**

Run: `dotnet husky run --name prepare-shell-scripts`
Expected: exit code `0`.
Run: `dotnet husky run --name update-shell-script-permissions`
Expected: exit code `0`.

- [ ] **Step 7: Run semantic-release safe phases**

Run with PowerShell:

```powershell
$env:VERSION='1.2.3'
$env:VERSION_TAG='v1.2.3'
$env:VERSION_CHANNEL='ci'
dotnet run --file scripts/semantic-release/100-verify/verify.cs
dotnet run --file scripts/semantic-release/300-publish/publish.cs
```

Expected: exit code `0` for both commands.

- [ ] **Step 8: Verify XML docs exist for script APIs**

Run: `rg -n "^(public|internal) (sealed |abstract |static |record|class|interface|enum|Task|void|string|bool|int)" scripts --glob "*.cs"`
Expected: each public or internal API introduced by the migration has XML documentation immediately above it.

- [ ] **Step 9: Verify git status**

Run: `git status --short`
Expected: only planned files are modified, added, or deleted.
Expected: `Directory.Packages.props` is not modified by this migration.

- [ ] **Step 10: Verify package directives stay script-scoped**

Run: `rg -n "PackageVersion Include=\"(CliWrap|LibGit2Sharp)\"|#:package (CliWrap|LibGit2Sharp)" Directory.Packages.props scripts`
Expected: no `PackageVersion` entries for `CliWrap` or `LibGit2Sharp` in `Directory.Packages.props`.
Expected: `#:package CliWrap@3.10.2` and `#:package LibGit2Sharp@0.31.0` appear in `scripts/shared/Packages.cs`.

- [ ] **Step 11: Verify helper filenames match contents**

Run: `rg -n "ScriptException.cs|verify-common.cs|prepare-common.cs|publish-common.cs|common.cs" scripts`
Expected: no proposed `.cs` helper filenames use `ScriptException.cs`, `verify-common.cs`, `prepare-common.cs`, `publish-common.cs`, or vague `common.cs`.

## Self-Review

Spec coverage is complete for Bash-to-`.cs` migration, `dotnet run --file`, script-scoped package directives, shared directives, Husky ties, semantic-release ties, act ties, README ties, solution ties, AGENTS.md durable context, `CliWrap`, `LibGit2Sharp`, custom exceptions, XML docs, required-property records, and content-matching filenames.
The plan uses `#:include` because it is documented for multiple C# files and avoids the feature-gated `#:ref` path.
Native command execution is centralized through `NativeCommandRunner` and `CliWrap`.
Git automation is centralized through `GitRepositoryContext` and `GitScriptIndex` using `LibGit2Sharp`.
No implementation step depends on Bash, PowerShell, `ProcessStartInfo`, shell-specific script launching, the `git` CLI, or solution-wide package additions for script-only dependencies.
