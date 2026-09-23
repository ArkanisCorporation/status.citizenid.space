#!/usr/bin/env -S dotnet --
#:property TargetFramework=net10.0
#:property Nullable=enable
#:property ManagePackageVersionsCentrally=false
#:property RestorePackagesWithLockFile=false
#:property ExperimentalFileBasedProgramEnableTransitiveDirectives=true
#:include shared/*.cs

using System.IO;
using Template.Scripting;

try
{
    using var repositoryContext = new GitRepositoryContext(Directory.GetCurrentDirectory());
    var scriptIndex = new GitScriptIndex(repositoryContext);
    var trackedShellScripts = scriptIndex.ListTrackedShellScripts();

    if (trackedShellScripts.Count == 0)
    {
        return 0;
    }

    scriptIndex.SetExecutableBit(trackedShellScripts);

    Console.WriteLine("Marked tracked shell scripts executable in the Git index:");

    foreach (var path in trackedShellScripts)
    {
        Console.WriteLine($"  - {path}");
    }

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
