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
    var stagedShellScripts = scriptIndex.ListStagedShellScripts();

    if (stagedShellScripts.Count == 0)
    {
        return 0;
    }

    scriptIndex.SetExecutableBit(stagedShellScripts);

    var nonLfShellScripts = scriptIndex.ListStagedShellScriptsWithNonLfLineEndings();
    if (nonLfShellScripts.Count == 0)
    {
        return 0;
    }

    Console.Error.WriteLine("Shell scripts must use LF line endings before commit:");

    foreach (var path in nonLfShellScripts)
    {
        Console.Error.WriteLine($"  - {path}");
    }

    Console.Error.WriteLine("Convert the files to LF and stage them again.");
    return 1;
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
