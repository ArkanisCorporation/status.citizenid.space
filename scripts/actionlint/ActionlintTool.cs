using System.Formats.Tar;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Template.Scripting;

/// <summary>
/// Describes one checksum-pinned Actionlint release archive for a supported runtime.
/// </summary>
/// <param name="Runtime">Repository cache segment for the operating system and architecture.</param>
/// <param name="ArchiveName">Official Actionlint release archive name.</param>
/// <param name="Sha256">Expected lowercase SHA-256 digest of the archive.</param>
/// <param name="ExecutableName">Executable name contained in the archive.</param>
internal sealed record ActionlintAsset(string Runtime, string ArchiveName, string Sha256, string ExecutableName);

/// <summary>
/// Acquires and runs the checksum-pinned repository-local Actionlint binary.
/// </summary>
/// <remarks>
/// Downloads come only from the official <c>rhysd/actionlint</c> GitHub release.
/// The archive is verified before extraction and cached under <c>.tools/actionlint</c> inside the repository.
/// </remarks>
internal static class ActionlintTool
{
    /// <summary>
    /// Gets the exact Actionlint version used by this repository.
    /// </summary>
    public const string Version = "1.7.12";

    private const string DownloadBaseUrl = "https://github.com/rhysd/actionlint/releases/download/v1.7.12";

    /// <summary>
    /// Maps an operating system and process architecture to its pinned Actionlint release archive.
    /// </summary>
    /// <param name="operatingSystem">Normalized operating system name: <c>windows</c>, <c>linux</c>, or <c>macos</c>.</param>
    /// <param name="architecture">Current process architecture.</param>
    /// <returns>The supported release asset and its expected checksum.</returns>
    /// <exception cref="ScriptConfigurationException">Thrown when the operating system or architecture is unsupported.</exception>
    internal static ActionlintAsset ResolveAsset(string operatingSystem, Architecture architecture)
        => (ScriptEnvironment.LowerInvariant(operatingSystem), architecture) switch
        {
            ("windows", Architecture.X64) => new("win-x64", "actionlint_1.7.12_windows_amd64.zip", "6e7241b51e6817ea6a047693d8e6fed13b31819c9a0dd6c5a726e1592d22f6e9", "actionlint.exe"),
            ("windows", Architecture.Arm64) => new("win-arm64", "actionlint_1.7.12_windows_arm64.zip", "cadcf7ea4efe3a68728893813643cebe1185e5b1d4be5b96245f65c9a4d5ea41", "actionlint.exe"),
            ("linux", Architecture.X64) => new("linux-x64", "actionlint_1.7.12_linux_amd64.tar.gz", "8aca8db96f1b94770f1b0d72b6dddcb1ebb8123cb3712530b08cc387b349a3d8", "actionlint"),
            ("linux", Architecture.Arm64) => new("linux-arm64", "actionlint_1.7.12_linux_arm64.tar.gz", "325e971b6ba9bfa504672e29be93c24981eeb1c07576d730e9f7c8805afff0c6", "actionlint"),
            ("macos", Architecture.X64) => new("osx-x64", "actionlint_1.7.12_darwin_amd64.tar.gz", "5b44c3bc2255115c9b69e30efc0fecdf498fdb63c5d58e17084fd5f16324c644", "actionlint"),
            ("macos", Architecture.Arm64) => new("osx-arm64", "actionlint_1.7.12_darwin_arm64.tar.gz", "aba9ced2dee8d27fecca3dc7feb1a7f9a52caefa1eb46f3271ea66b6e0e6953f", "actionlint"),
            _ => throw new ScriptConfigurationException(
                $"Actionlint {Version} is not configured for '{operatingSystem}/{architecture}'. Supported runtimes: windows-x64, windows-arm64, linux-x64, linux-arm64, macos-x64, macos-arm64."),
        };

    /// <summary>
    /// Gets the repository-local executable path for a release asset.
    /// </summary>
    /// <param name="repositoryRoot">Repository root that owns the local tool cache.</param>
    /// <param name="asset">Runtime-specific release asset.</param>
    /// <returns>An absolute executable path beneath <c>.tools/actionlint</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="repositoryRoot"/> is empty.</exception>
    internal static string GetExecutablePath(string repositoryRoot, ActionlintAsset asset)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            throw new ArgumentException("Repository root cannot be empty.", nameof(repositoryRoot));
        }

        return Path.GetFullPath(Path.Combine(repositoryRoot, ".tools", "actionlint", Version, asset.Runtime, asset.ExecutableName));
    }

    /// <summary>
    /// Determines whether Actionlint version output starts with the pinned version.
    /// </summary>
    /// <param name="standardOutput">Output from <c>actionlint -version</c>.</param>
    /// <returns><see langword="true"/> when the first non-empty line exactly matches <see cref="Version"/>.</returns>
    internal static bool HasExpectedVersion(string standardOutput)
        => standardOutput
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() is { } reportedVersion
            && string.Equals(reportedVersion, Version, StringComparison.Ordinal);

    /// <summary>
    /// Verifies a stream against an expected SHA-256 digest.
    /// </summary>
    /// <param name="content">Archive content positioned at the beginning of the data to hash.</param>
    /// <param name="expectedSha256">Expected lowercase hexadecimal digest.</param>
    /// <returns><see langword="true"/> when the calculated digest matches the expected digest.</returns>
    internal static bool HasExpectedChecksum(Stream content, string expectedSha256)
    {
        var actual = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        return string.Equals(actual, expectedSha256, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies a completed temporary archive and atomically promotes it to its cache path.
    /// </summary>
    /// <param name="temporaryPath">Completed download that must match the expected checksum.</param>
    /// <param name="archivePath">Final repository-local archive cache path.</param>
    /// <param name="expectedSha256">Expected lowercase SHA-256 digest.</param>
    /// <exception cref="ScriptConfigurationException">Thrown when the downloaded archive checksum differs.</exception>
    /// <remarks>
    /// The checksum stream is disposed before the move so Windows does not retain a conflicting file handle.
    /// </remarks>
    internal static void PromoteVerifiedArchive(string temporaryPath, string archivePath, string expectedSha256)
    {
        using (var downloadedArchive = File.OpenRead(temporaryPath))
        {
            if (!HasExpectedChecksum(downloadedArchive, expectedSha256))
            {
                throw new ScriptConfigurationException($"Checksum verification failed for downloaded Actionlint archive '{Path.GetFileName(archivePath)}'.");
            }
        }

        File.Move(temporaryPath, archivePath, overwrite: true);
    }

    /// <summary>
    /// Acquires an exclusive cross-process lock for one repository-local Actionlint cache.
    /// </summary>
    /// <param name="lockPath">Lock-file path beneath the repository-local tool cache.</param>
    /// <param name="timeout">Maximum time to wait for another acquisition process.</param>
    /// <param name="cancellationToken">Token that cancels lock acquisition.</param>
    /// <returns>An open exclusive file stream whose disposal releases the lock.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="lockPath"/> is empty or <paramref name="timeout"/> is not positive.</exception>
    /// <exception cref="ScriptConfigurationException">Thrown when the lock remains unavailable for the complete timeout.</exception>
    internal static async Task<FileStream> AcquireCacheLockAsync(
        string lockPath,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lockPath))
        {
            throw new ArgumentException("Cache lock path cannot be empty.", nameof(lockPath));
        }

        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("Cache lock timeout must be positive.", nameof(timeout));
        }

        var resolvedPath = Path.GetFullPath(lockPath);
        Directory.CreateDirectory(Path.GetDirectoryName(resolvedPath)
            ?? throw new ScriptConfigurationException($"Could not resolve the Actionlint cache lock directory for '{resolvedPath}'."));

        var deadline = DateTimeOffset.UtcNow + timeout;
        IOException? lastFailure = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(
                    resolvedPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.Asynchronous);
            }
            catch (IOException exception)
            {
                lastFailure = exception;
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            }
        }

        throw new ScriptConfigurationException(
            $"Timed out after {timeout.TotalSeconds:F0} seconds waiting for the Actionlint cache lock '{resolvedPath}'.",
            lastFailure);
    }

    /// <summary>
    /// Downloads, verifies, extracts, and validates the pinned Actionlint executable when it is not already cached.
    /// </summary>
    /// <param name="repositoryRoot">Repository root that owns the local tool cache.</param>
    /// <param name="cancellationToken">Token that cancels download or native command execution.</param>
    /// <returns>The absolute path to the verified executable.</returns>
    /// <exception cref="ScriptConfigurationException">Thrown when the platform is unsupported, the download fails, the checksum differs, or the extracted executable reports another version.</exception>
    internal static async Task<string> AcquireAsync(string repositoryRoot, CancellationToken cancellationToken = default)
    {
        var resolvedRoot = Path.GetFullPath(repositoryRoot);
        var asset = ResolveAsset(GetOperatingSystem(), RuntimeInformation.ProcessArchitecture);
        var executablePath = GetExecutablePath(resolvedRoot, asset);

        if (File.Exists(executablePath) && await ReportsExpectedVersionAsync(executablePath, resolvedRoot, cancellationToken))
        {
            return executablePath;
        }

        var versionRoot = Path.Combine(resolvedRoot, ".tools", "actionlint", Version);
        await using var cacheLock = await AcquireCacheLockAsync(
            Path.Combine(versionRoot, $"{asset.Runtime}.lock"),
            TimeSpan.FromMinutes(3),
            cancellationToken);

        if (File.Exists(executablePath) && await ReportsExpectedVersionAsync(executablePath, resolvedRoot, cancellationToken))
        {
            return executablePath;
        }

        var archiveDirectory = Path.Combine(versionRoot, "downloads");
        var archivePath = Path.Combine(archiveDirectory, asset.ArchiveName);
        Directory.CreateDirectory(archiveDirectory);

        var archiveIsValid = false;
        if (File.Exists(archivePath))
        {
            await using var cachedArchive = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81_920, useAsync: true);
            archiveIsValid = HasExpectedChecksum(cachedArchive, asset.Sha256);
        }

        if (!archiveIsValid)
        {
            await DownloadArchiveAsync(asset, archivePath, cancellationToken);
        }

        var runtimeDirectory = Path.GetDirectoryName(executablePath)
            ?? throw new ScriptConfigurationException($"Could not resolve the Actionlint runtime directory for '{executablePath}'.");
        var extractionDirectory = $"{runtimeDirectory}.extract-{Guid.NewGuid():N}";

        try
        {
            Directory.CreateDirectory(extractionDirectory);
            ExtractArchive(archivePath, extractionDirectory, asset);

            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(
                    Path.Combine(extractionDirectory, asset.ExecutableName),
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                    UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                    UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
            }

            if (Directory.Exists(runtimeDirectory))
            {
                Directory.Delete(runtimeDirectory, recursive: true);
            }

            Directory.Move(extractionDirectory, runtimeDirectory);
        }
        finally
        {
            if (Directory.Exists(extractionDirectory))
            {
                Directory.Delete(extractionDirectory, recursive: true);
            }
        }

        if (!await ReportsExpectedVersionAsync(executablePath, resolvedRoot, cancellationToken))
        {
            Directory.Delete(runtimeDirectory, recursive: true);
            throw new ScriptConfigurationException($"The extracted Actionlint executable did not report pinned version {Version}.");
        }

        return executablePath;
    }

    /// <summary>
    /// Runs the pinned Actionlint executable with structured arguments.
    /// </summary>
    /// <param name="repositoryRoot">Repository working directory and local tool-cache owner.</param>
    /// <param name="arguments">Arguments forwarded directly to Actionlint.</param>
    /// <param name="cancellationToken">Token that cancels acquisition or linting.</param>
    /// <returns>The captured Actionlint result.</returns>
    /// <exception cref="ScriptException">Thrown when acquisition or Actionlint execution fails.</exception>
    internal static async Task<NativeCommandResult> RunAsync(
        string repositoryRoot,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var executablePath = await AcquireAsync(repositoryRoot, cancellationToken);
        return await NativeCommandRunner.RunAsync(
            new NativeCommandSpec
            {
                Executable = executablePath,
                Arguments = arguments,
                WorkingDirectory = Path.GetFullPath(repositoryRoot),
            },
            cancellationToken);
    }

    private static async Task DownloadArchiveAsync(ActionlintAsset asset, string archivePath, CancellationToken cancellationToken)
    {
        var temporaryPath = $"{archivePath}.{Guid.NewGuid():N}.download";
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            await using var source = await client.GetStreamAsync($"{DownloadBaseUrl}/{asset.ArchiveName}", cancellationToken);
            await using (var destination = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81_920, useAsync: true))
            {
                await source.CopyToAsync(destination, cancellationToken);
            }

            PromoteVerifiedArchive(temporaryPath, archivePath, asset.Sha256);
        }
        catch (ScriptException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new ScriptConfigurationException(
                $"Could not download Actionlint {Version} archive '{asset.ArchiveName}': {exception.Message}",
                exception);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static void ExtractArchive(string archivePath, string destinationDirectory, ActionlintAsset asset)
    {
        if (asset.ArchiveName.EndsWith(".zip", StringComparison.Ordinal))
        {
            ZipFile.ExtractToDirectory(archivePath, destinationDirectory, overwriteFiles: true);
            return;
        }

        using var archive = File.OpenRead(archivePath);
        using var gzip = new GZipStream(archive, CompressionMode.Decompress);
        TarFile.ExtractToDirectory(gzip, destinationDirectory, overwriteFiles: true);
    }

    private static async Task<bool> ReportsExpectedVersionAsync(string executablePath, string workingDirectory, CancellationToken cancellationToken)
    {
        var result = await NativeCommandRunner.RunAsync(
            new NativeCommandSpec
            {
                Executable = executablePath,
                Arguments = ["-version"],
                WorkingDirectory = workingDirectory,
                ThrowOnFailure = false,
            },
            cancellationToken);
        return result.ExitCode == 0 && HasExpectedVersion(result.StandardOutput);
    }

    private static string GetOperatingSystem()
        => OperatingSystem.IsWindows()
            ? "windows"
            : OperatingSystem.IsLinux()
                ? "linux"
                : OperatingSystem.IsMacOS()
                    ? "macos"
                    : throw new ScriptConfigurationException("Actionlint direct download supports only Windows, Linux, and macOS.");
}
