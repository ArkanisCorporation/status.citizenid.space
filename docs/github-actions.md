# GitHub Actions

This repository validates the CitizenId Status deployment model without publishing a Helm release or changing infrastructure.
Reusable behavior is pinned to `ArkanisCorporation/ci@v1`.

## Workflow Graph

[`main.yaml`](../.github/workflows/main.yaml) runs for pull requests to `main`, pushes to `main`, `release/*`, and `ci`, manual dispatches, and repository dispatches.
It keeps top-level permissions empty.

| Job | Purpose | Permissions |
| --- | --- | --- |
| Workflow | Lints workflows through `wf-lint-github-actions.yml@v1`. | `contents: read` |
| Tests | Runs the local reusable test, format, and complexity workflow. | `contents: read` |
| Dry Run: Release | Predicts semantic-release output without publishing. | `contents: write` |

[`_test.yaml`](../.github/workflows/_test.yaml) passes `Arkanis.Infra.Deployment.CitizenId.Status.slnx` to the shared .NET test and format workflows.
The complexity job builds the same solution with CA1502 and CA1509 promoted to errors.

The repository does not contain a .NET service image or a packable library.
Container and NuGet verification workflows are intentionally absent.

## Runner Trust

Untrusted pull-request code always runs on `ubuntu-latest`.
Trusted pushes, manual dispatches, and repository dispatches select the manual `runner` input, `RUNNER_DEFAULT`, or `daedalus` in that order.

The supported manual choices are `daedalus`, `arkanis-runners`, and `ubuntu-latest`.
Every reusable workflow caller keeps `runs-on-self-hosted` consistent with the selected runner.

## Permissions

Keep top-level workflow permissions empty and grant scopes per job.
Reusable workflows cannot elevate beyond the permissions supplied by their caller.

The workflow and test lanes use `contents: read`.
The release verification lane receives `contents: write` only because its shared contract verifies tag-push access during semantic-release dry-run behavior.
No executable job declares `packages: write`, `id-token: write`, a registry credential, a NuGet credential, or a deployment credential.

## Local Validation

Run the repository-local Actionlint wrapper before committing workflow changes.

```powershell
dotnet run --file scripts/actionlint.cs
```

Local checks cannot prove remote reusable-workflow resolution, token permissions, artifact retention, or runner-group policy.
GitHub-hosted workflow runs remain the required verification environment for those concerns.
