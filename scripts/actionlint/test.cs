#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false
#:property RestorePackagesWithLockFile=false
#:property ExperimentalFileBasedProgramEnableTransitiveDirectives=true
#:include ../shared/*.cs
#:include ActionlintTool.cs

using System.Runtime.InteropServices;
using System.Text;
using Template.Scripting;

AssertAsset("windows", Architecture.X64, "win-x64", "actionlint_1.7.12_windows_amd64.zip", "6e7241b51e6817ea6a047693d8e6fed13b31819c9a0dd6c5a726e1592d22f6e9", "actionlint.exe");
AssertAsset("windows", Architecture.Arm64, "win-arm64", "actionlint_1.7.12_windows_arm64.zip", "cadcf7ea4efe3a68728893813643cebe1185e5b1d4be5b96245f65c9a4d5ea41", "actionlint.exe");
AssertAsset("linux", Architecture.X64, "linux-x64", "actionlint_1.7.12_linux_amd64.tar.gz", "8aca8db96f1b94770f1b0d72b6dddcb1ebb8123cb3712530b08cc387b349a3d8", "actionlint");
AssertAsset("linux", Architecture.Arm64, "linux-arm64", "actionlint_1.7.12_linux_arm64.tar.gz", "325e971b6ba9bfa504672e29be93c24981eeb1c07576d730e9f7c8805afff0c6", "actionlint");
AssertAsset("macos", Architecture.X64, "osx-x64", "actionlint_1.7.12_darwin_amd64.tar.gz", "5b44c3bc2255115c9b69e30efc0fecdf498fdb63c5d58e17084fd5f16324c644", "actionlint");
AssertAsset("macos", Architecture.Arm64, "osx-arm64", "actionlint_1.7.12_darwin_arm64.tar.gz", "aba9ced2dee8d27fecca3dc7feb1a7f9a52caefa1eb46f3271ea66b6e0e6953f", "actionlint");

AssertThrows<ScriptConfigurationException>(() => ActionlintTool.ResolveAsset("freebsd", Architecture.X64));
AssertThrows<ScriptConfigurationException>(() => ActionlintTool.ResolveAsset("linux", Architecture.X86));

Assert(ActionlintTool.HasExpectedVersion("1.7.12\ninstalled from release\n"), "The pinned version output should be accepted.");
Assert(!ActionlintTool.HasExpectedVersion("1.7.11\n"), "A different Actionlint version should be rejected.");

using var checksumStream = new MemoryStream(Encoding.UTF8.GetBytes("abc"));
Assert(
    ActionlintTool.HasExpectedChecksum(checksumStream, "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"),
    "The expected SHA-256 should be accepted.");

checksumStream.Position = 0;
Assert(
    !ActionlintTool.HasExpectedChecksum(checksumStream, "0000000000000000000000000000000000000000000000000000000000000000"),
    "An unexpected SHA-256 should be rejected.");

var cachePath = ActionlintTool.GetExecutablePath("/repository", ActionlintTool.ResolveAsset("linux", Architecture.Arm64));
Assert(
    cachePath.EndsWith(Path.Combine(".tools", "actionlint", "1.7.12", "linux-arm64", "actionlint"), StringComparison.Ordinal),
    "The executable should use the pinned repository-local cache path.");

var promotionDirectory = Path.Combine(Path.GetTempPath(), $"template-actionlint-test-{Guid.NewGuid():N}");
try
{
    Directory.CreateDirectory(promotionDirectory);
    var temporaryArchive = Path.Combine(promotionDirectory, "archive.download");
    var verifiedArchive = Path.Combine(promotionDirectory, "archive.zip");
    File.WriteAllText(temporaryArchive, "abc");

    ActionlintTool.PromoteVerifiedArchive(
        temporaryArchive,
        verifiedArchive,
        "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");

    Assert(File.Exists(verifiedArchive), "The verified archive should be promoted after its checksum stream is closed.");
    Assert(!File.Exists(temporaryArchive), "The temporary archive should be moved rather than copied.");
}
finally
{
    if (Directory.Exists(promotionDirectory))
    {
        Directory.Delete(promotionDirectory, recursive: true);
    }
}

var lockDirectory = Path.Combine(Path.GetTempPath(), $"template-actionlint-lock-test-{Guid.NewGuid():N}");
try
{
    Directory.CreateDirectory(lockDirectory);
    var lockPath = Path.Combine(lockDirectory, "acquire.lock");
    await using var firstLock = await ActionlintTool.AcquireCacheLockAsync(lockPath, TimeSpan.FromSeconds(2));
    using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

    await AssertThrowsAsync<OperationCanceledException>(
        () => ActionlintTool.AcquireCacheLockAsync(lockPath, TimeSpan.FromSeconds(2), cancellationTokenSource.Token));

    await firstLock.DisposeAsync();
    await using var secondLock = await ActionlintTool.AcquireCacheLockAsync(lockPath, TimeSpan.FromSeconds(2));
    Assert(secondLock.CanWrite, "The cache lock should become available after the previous owner releases it.");
}
finally
{
    if (Directory.Exists(lockDirectory))
    {
        Directory.Delete(lockDirectory, recursive: true);
    }
}

Console.WriteLine("Actionlint tool tests passed.");
return 0;

static void AssertAsset(
    string operatingSystem,
    Architecture architecture,
    string expectedRuntime,
    string expectedArchive,
    string expectedChecksum,
    string expectedExecutable)
{
    var asset = ActionlintTool.ResolveAsset(operatingSystem, architecture);
    Assert(asset.Runtime == expectedRuntime, $"Expected runtime '{expectedRuntime}', got '{asset.Runtime}'.");
    Assert(asset.ArchiveName == expectedArchive, $"Expected archive '{expectedArchive}', got '{asset.ArchiveName}'.");
    Assert(asset.Sha256 == expectedChecksum, $"Expected checksum '{expectedChecksum}', got '{asset.Sha256}'.");
    Assert(asset.ExecutableName == expectedExecutable, $"Expected executable '{expectedExecutable}', got '{asset.ExecutableName}'.");
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertThrows<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static async Task AssertThrowsAsync<TException>(Func<Task> action)
    where TException : Exception
{
    try
    {
        await action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}
