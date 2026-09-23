# GitHub Actions

This repository owns event triggers, branch filters, runner trust, job ordering, and least-privilege permissions.
Reusable validation and artifact-building behavior is pinned to `ArkanisCorporation/ci@v1`.

> [!IMPORTANT]
> Every executable workflow in this repository is verification-only.
> No executable workflow publishes a GitHub release, container image, NuGet package, or deployment.

## Executable Workflow Map

[`main.yaml`](../.github/workflows/main.yaml) is the entry workflow.
It runs for pull requests targeting `main`, pushes to `main`, `release/*`, and `ci`, manual dispatches, and repository dispatches.
Its top-level permissions are empty.

The entry workflow runs these lanes.

- `Workflow` calls `ArkanisCorporation/ci/.github/workflows/wf-lint-github-actions.yml@v1` to lint workflow and action YAML.
- `Tests` calls the local reusable [`_test.yaml`](../.github/workflows/_test.yaml) wrapper after selecting the trusted runner boundary.
- `Dry Run: Release` calls the local reusable [`_verify-release.yaml`](../.github/workflows/_verify-release.yaml) wrapper after tests and predicts semantic-release output without publishing.
- `Dry Run: Container` calls the local reusable [`_verify-container-image.yaml`](../.github/workflows/_verify-container-image.yaml) wrapper after tests and builds an image without registry authentication or push.
- `Dry Run: NuGet` calls the local reusable [`_verify-nuget-package.yaml`](../.github/workflows/_verify-nuget-package.yaml) wrapper after tests and packs package artifacts without authentication or push.

The semantic-release dry run is skipped for fork pull requests because its shared verification contract requires `contents: write` to test tag push access.
Container and NuGet dry runs use `0.0.0-ci.<run-number>.<run-attempt>` so every run has a valid synthetic version independent of release prediction.

## Shared `ci@v1` Contracts

The repository uses these shared workflows and actions.

### `wf-lint-github-actions.yml@v1`

The entry workflow supplies the effective runner and whether it is self-hosted.
The caller grants only `contents: read`.

### `wf-dotnet-test.yml@v1`

The `Coverage` job in `_test.yaml` supplies `global.json`, `Template.slnx`, the optional test filter, optional ReportGenerator settings, `coverage-pr-comment: false`, `upload-coverage: true`, and a 20-minute timeout.
The shared workflow restores, builds, tests, generates coverage reports, uploads the coverage output with its diagnostics artifact, and appends the Markdown coverage summary to the job summary.
The caller grants only `contents: read`.
CI deliberately does not comment on pull requests because repository administrators can enable write tokens for fork pull-request workflows.
A separately reviewed `pull_request_target` or artifact-reporting lane could be a future opt-in, but no such executable lane exists in this repository.

### `wf-dotnet-format.yml@v1`

The `Format` job in `_test.yaml` supplies `global.json`, `Template.slnx`, a 20-minute timeout, and `install-tool: true`.
The shared workflow runs both .NET formatting and the Common JetBrains CleanupCode verification contract.
The caller grants only `contents: read`.

### `setup-dotnet@v1`

The repository-owned `Complexity` job uses `ArkanisCorporation/ci/.github/actions/setup-dotnet@v1` for checkout-independent SDK and locked dependency setup.
It then builds `Template.slnx` with CA1502 and CA1509 promoted to errors.
The job grants only `contents: read`.

### `wf-verify-release-semantic.yml@v1`

The release wrapper forwards the predicted version, tag, channel, previous release metadata, and publication boolean.
The shared workflow always runs semantic-release in dry-run mode.
The caller grants the required `contents: write` permission but defines no publication environment or downstream publisher.

### `wf-verify-publish-container-dotnet.yml@v1`

The container wrapper builds `src/Template.Service/Dockerfile` from the repository root for `ghcr.io/arkaniscorporation/template` with synthetic version and OCI metadata inputs.
The shared workflow verifies the image build but receives no registry token and performs no registry login or push.
The caller grants only `contents: read`.

### `wf-verify-publish-nuget.yml@v1`

The NuGet wrapper packs `src/Template.Contracts/Template.Contracts.csproj`, includes source and symbol packages, disables project version stamping, and retains artifacts for seven days.
The shared workflow receives no NuGet credential and performs no package push.
The caller grants only `contents: read`.

## Runner Trust

Untrusted pull-request code always runs on `ubuntu-latest`.
This rule prevents fork or contributor code from executing on persistent organization runners.

Trusted pushes, manual dispatches, and repository dispatches select the manual `runner` input, `RUNNER_DEFAULT`, or `daedalus` in that order.
The supported manual choices are `daedalus`, `arkanis-runners`, and `ubuntu-latest`.
Every shared caller keeps `runs-on-self-hosted` consistent with the selected runner.

Do not weaken the pull-request runner expression when customizing this template.
Public downstream repositories must keep untrusted code away from self-hosted runners and their network boundary.

## Permissions And Fork Behavior

Keep top-level workflow permissions empty and grant scopes per job.
Reusable workflows cannot elevate beyond the permissions supplied by their caller.

The workflow and format lanes use `contents: read`.
The complexity, container verification, and NuGet verification lanes also use only `contents: read`.
The coverage lane also uses only `contents: read`, sets `coverage-pr-comment: false`, and uploads coverage artifacts instead of writing to pull requests.
The semantic-release verification lane receives `contents: write` only for same-repository pull requests and trusted events, and it cannot publish because it calls the verification workflow.

No executable job declares a publication environment, `id-token: write`, `packages: write`, registry credential, NuGet credential, or deployment credential.

## Expected Required Checks

Repository rules should require all validation and artifact-verification lanes before merge.
The expected check families are `Workflow`, `Tests / Coverage`, `Tests / Format`, `Tests / Complexity`, `Dry Run: Release`, `Dry Run: Container`, and `Dry Run: NuGet`.

GitHub expands reusable job names with called-workflow job names and may include a ref or synthetic version in the displayed check name.
Run the workflow on GitHub first, then select the exact checks observed in the repository rules UI rather than guessing a generated full name.
Do not make the fork-skipped release dry run a universal fork-required check unless the repository's merge policy accounts for its conditional absence.

## Local And GitHub-Hosted Verification

Run local workflow linting with the checksum-pinned repository wrapper.

```powershell
dotnet run --file scripts/actionlint.cs
```

The wrapper downloads the official Actionlint `1.7.12` archive for Windows, Linux, or macOS on x64 or ARM64.
It verifies the committed SHA-256 value before extraction and reuses the ignored `.tools/actionlint` cache only when the executable reports the pinned version.

List the pull-request graph locally when `act` and Docker are available.

```powershell
act -l pull_request -W .github/workflows/main.yaml
```

Local checks can validate YAML, expressions known to Actionlint, job discovery, .NET commands, package creation, and Docker image creation.
They cannot prove remote `ArkanisCorporation/ci@v1` resolution, GitHub token permission reduction, environment protection, fork-token behavior, pull-request comments, step summaries, artifact retention, attestations, Trusted Publishing, registry push, or package visibility.

GitHub-hosted runs are therefore required before changing repository rules or enabling any publication lane.
Use a same-repository pull request and a fork pull request to verify the two comment and release-dry-run paths.

## Publication Opt-In Policy

The following examples are intentionally incomplete Markdown fragments.
They omit the entire `on:` trigger and are not present under `.github/workflows`, so they cannot execute.
Do not copy one into an executable workflow until an operator explicitly approves the publication boundary and every checklist item is complete.

### Semantic-Release Publication

Security checklist before creating an executable workflow:

- Create a protected `release` environment with required reviewers and branch or tag restrictions.
- Grant only `contents: write`, `issues: write`, and `pull-requests: write` to the publication job unless a reviewed plugin proves another scope necessary.
- Replace template branch, tag, repository, release-channel, and versioning policy with repository-specific decisions.
- Verify the predicted release first, then remotely verify the created tag, GitHub release metadata, notes, and workflow outputs.
- Keep image and NuGet publication in separately approved jobs because they have different trust boundaries.

```yaml
jobs:
  release:
    permissions:
      contents: write
      issues: write
      pull-requests: write
    uses: ArkanisCorporation/ci/.github/workflows/wf-release-semantic.yml@v1
    with:
      runs-on: ubuntu-latest
      runs-on-self-hosted: false
      environment-name: release
```

This fragment has no trigger and is not executable by itself.

### GHCR Image Publication

Security checklist before creating an executable workflow:

- Create a protected `container` environment with required reviewers and branch or tag restrictions.
- Grant only `contents: read`, `packages: write`, `id-token: write`, and `attestations: write`, and remove any scope the approved shared contract no longer requires.
- Replace `ghcr.io/arkaniscorporation/template`, tag policy, channel policy, and version source with repository-specific values.
- Verify the image locally first, then remotely verify OCI metadata, provenance, SBOM, immutable digest, package visibility, and a pull by digest.
- Review whether GitHub's token is sufficient or an explicitly approved registry token is required.

```yaml
jobs:
  publish-container:
    permissions:
      contents: read
      packages: write
      id-token: write
      attestations: write
    uses: ArkanisCorporation/ci/.github/workflows/wf-publish-container-dotnet.yml@v1
    with:
      runs-on: ubuntu-latest
      runs-on-self-hosted: false
      environment-name: container
      image: ghcr.io/arkaniscorporation/template
      context: .
      dockerfile: src/Template.Service/Dockerfile
      version: 1.2.3
      version-tag: v1.2.3
      global-json-file: global.json
```

This fragment has no trigger and is not executable by itself.

### NuGet.org Trusted Publishing

Security checklist before creating an executable workflow:

- Select the package, license, ownership, and semantic-version policy before publication.
- Create a protected `nuget` environment with required reviewers and branch or tag restrictions.
- Configure a NuGet.org Trusted Publishing policy that exactly matches the repository owner, repository, executable workflow filename, environment, and package owner.
- Grant the pack job only `contents: read` and the environment-gated publishing job only `contents: read` plus `id-token: write`.
- Replace the template project path, package ID, NuGet user, and version source with repository-specific values.
- Verify the local `.nupkg` and `.snupkg` first, then remotely verify metadata, symbols, provenance, ownership, version visibility, and restore from NuGet.org.

```yaml
jobs:
  publish-nuget:
    permissions:
      contents: read
      id-token: write
    uses: ArkanisCorporation/ci/.github/workflows/wf-publish-nuget.yml@v1
    with:
      runs-on: ubuntu-latest
      runs-on-self-hosted: false
      environment-name: nuget
      project: src/Template.Contracts/Template.Contracts.csproj
      version: 1.2.3
      global-json-file: global.json
      trusted-publishing: true
      nuget-user: ${{ vars.NUGET_USER }}
      dotnet-setversion: false
      include-symbols: true
      include-source: true
```

This fragment has no trigger and is not executable by itself.

## Repository Settings Checklist

- Require the observed validation and dry-run checks before merging to the default branch.
- Keep `release/*` protected if a downstream project later assigns publication semantics to those branches.
- Restrict organization runner groups to approved repositories and trusted workflows.
- Keep protected publication environments absent until an operator approves real publication.
- Keep Renovate enabled for GitHub Actions, .NET packages, local tools, and Docker base images.
