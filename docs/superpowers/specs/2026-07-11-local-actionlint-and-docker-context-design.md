# Local Actionlint And Docker Context Design

## Objective

Add the two operator-approved template improvements without changing release behavior.

- Reduce Docker build context size and accidental file exposure with a root `.dockerignore`.
- Provide a pinned, cross-platform, repository-local Actionlint runner.
- Keep pull-request coverage comments excluded.
- Keep every executable release lane dry-run-only.

## Actionlint Acquisition

The repository will use direct downloads only.
The runner will not discover, install, or invoke package-manager-managed Actionlint binaries.
This avoids global machine changes and guarantees the same tool version on every supported platform.

The runner will pin Actionlint `1.7.12` and support these runtime combinations.

- Windows x64 and ARM64.
- Linux x64 and ARM64.
- macOS x64 and ARM64.

Each runtime maps to one official archive published by `rhysd/actionlint`.
The repository will commit the expected SHA-256 digest for every supported archive.
The runner will reject an archive before extraction when its digest does not match the pinned value.

Downloaded archives and extracted binaries will live beneath ignored `.tools/actionlint/1.7.12/<runtime>/` paths.
A valid cache entry will be reused after the runner confirms the executable reports the pinned version.
Cold-cache acquisition will use a version-and-runtime-scoped cross-process lock with a bounded cancellable wait and a cache recheck after lock acquisition.
An unsupported operating system or architecture will fail with a concise diagnostic listing the supported combinations.

## Repository Script

The entry point will be a .NET 10 file-based app run from the repository root.
It will follow the existing script conventions and reuse shared native-command helpers for executing Actionlint.
Download, hashing, and archive extraction will use .NET standard-library APIs.
No new package or global tool dependency will be introduced.

The default invocation will be:

```powershell
dotnet run --file scripts/actionlint.cs
```

With no arguments, the script will run the downloaded executable with `-config-file .github/actionlint.yaml`.
Explicit arguments after `--` will be forwarded to Actionlint for advanced local use.
Cancellation and non-zero Actionlint exits will propagate through the repository's existing script error conventions.

## Docker Context

The root `.dockerignore` will exclude content that cannot affect the service image build.
That includes Git metadata, editor state, agent metadata, documentation, automation scripts, local Actionlint downloads, Act state, build outputs, test results, and other local caches.

The ignore rules will retain the solution, central build files, NuGet configuration, project files, lock files, service source, contracts source, AppHost project metadata, and test project metadata required by the Dockerfile's solution-wide locked restore.
The existing Dockerfile and its dry-run-only behavior will remain unchanged.

## Documentation

`README.md` will replace the global `actionlint` command with the repository-local runner and explain its pinned local cache.
`AGENTS.md` will record the durable rule that workflow linting uses the pinned repository-local runner.
The existing CI and publication documentation will remain unchanged unless a command reference requires correction.

## Verification

Implementation will follow a red-green sequence.
A runnable file-based test will first fail because the acquisition helper is absent.
The test will then cover runtime-to-asset mapping, unsupported runtimes, checksum acceptance and rejection, exact-version parsing, and cache-path construction.

Completion requires fresh evidence from:

- The Actionlint helper test.
- A cold direct download followed by a successful repository lint.
- A second run proving cache reuse.
- Actionlint version output matching `1.7.12`.
- A local Docker build using the new context exclusions.
- Locked restore, Release build, integration tests, and `git diff --check`.
- Searches confirming that no coverage commenter, publisher, registry login, package push, image push, or deployment surface was introduced.

No command will publish, push an artifact, or deploy a resource.

## Delivery

The implementation will use semantic commits on `codex/integrate-common-shared-ci`.
After local verification, the GitHub yeet workflow will confirm scope, push the branch, and open a draft pull request against the repository's default branch.
