#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false
#:property RestorePackagesWithLockFile=false
#:property ExperimentalFileBasedProgramEnableTransitiveDirectives=true
#:include ../shared/*.cs
#:include ActRunner.cs

using System.IO;
using System.Threading;
using Template.Scripting;

using var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += OnCancelKeyPress;

try
{
    await ActRunner.RunPullRequestTestAsync(Directory.GetCurrentDirectory(), cancellationTokenSource.Token);
    return 0;
}
catch (ScriptException exception)
{
    Console.Error.WriteLine(exception.Message);
    return exception.ExitCode;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 2;
}
finally
{
    Console.CancelKeyPress -= OnCancelKeyPress;
}

void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs eventArgs)
{
    eventArgs.Cancel = true;
    cancellationTokenSource.Cancel();
}
