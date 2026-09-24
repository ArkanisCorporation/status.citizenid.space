# GitHub Actions

This repository validates, releases, and deploys the CitizenId Status deployment model.
Reusable behavior is pinned to `ArkanisCorporation/ci@v1`.

## Workflow Graph

[`main.yaml`](../.github/workflows/main.yaml) runs for pull requests to `main`, pushes to `main`, `release/*`, and `ci`, manual dispatches, and repository dispatches.
It keeps top-level permissions empty.

| Job | Purpose | Permissions |
| --- | --- | --- |
| Workflow | Lints workflows through `wf-lint-github-actions.yml@v1`. | `contents: read` |
| Tests | Runs the local reusable test, format, and complexity workflow. | `contents: read` |
| Dry Run: Release | Predicts semantic-release output without publishing. | `contents: write` |
| Dry Run: Kubernetes Deployment | Validates the candidate release's selected Aspire/Kubernetes target without mutating a cluster. | `contents: read` |
| Release | Publishes semantic-release tags and GitHub release metadata for a validated candidate. | `contents: write`, `issues: write`, `pull-requests: write` |
| Deploy Staging | Deploys an actual staging release from `main`. | `contents: read`, `packages: write` |

[`_test.yaml`](../.github/workflows/_test.yaml) passes `Arkanis.Infra.Deployment.CitizenId.Status.slnx` to the shared .NET test and format workflows.
The complexity job builds the same solution with CA1502 and CA1509 promoted to errors.

The repository does not contain a .NET service image or a packable library.
Container and NuGet verification workflows are intentionally absent.
The Kener and Redis image tags remain pinned by the AppHost, so release metadata is never supplied as an `image-tag` override.

[`deploy-production.yaml`](../.github/workflows/deploy-production.yaml) is manual-only.
Select the immutable `vMAJOR.MINOR.PATCH` tag as its workflow ref and provide the exact same tag as the required input.
The workflow rejects branch, prerelease, bare-version, and mismatched-tag requests before calling the shared Aspire deployment workflow.

## Runner Trust

Untrusted pull-request code always runs on `ubuntu-latest`.
Trusted pushes, manual dispatches, and repository dispatches select the manual `runner` input, `RUNNER_DEFAULT`, or `daedalus` in that order.

The supported manual choices are `daedalus`, `arkanis-runners`, and `ubuntu-latest`.
Every reusable workflow caller keeps `runs-on-self-hosted` consistent with the selected runner.

## Permissions

Keep top-level workflow permissions empty and grant scopes per job.
Reusable workflows cannot elevate beyond the permissions supplied by their caller.

The workflow and test lanes use `contents: read`.
The shared `wf-dotnet-test.yml@v1` contract currently declares `pull-requests: write` even when this caller disables PR coverage comments.
The top-level test caller and its local adapter grant that scope solely to satisfy GitHub's nested-workflow validation; no local workflow step writes to a pull request.
The release verification lane receives `contents: write` only because its shared contract verifies tag-push access during semantic-release dry-run behavior.
The release lane receives only the semantic-release scopes.
Deployment jobs receive `packages: write` because the shared deployment contract logs in to GHCR, and receive cluster access only from the selected GitHub Environment's `KUBE_CONFIG` secret or runner context.
No job declares `id-token: write`, a registry credential, or a NuGet credential.

## Delivery Environments

Create the following GitHub Environments before enabling delivery:

| Environment | Purpose | Required configuration |
| --- | --- | --- |
| `release` | Guards semantic-release publication. | Release policy appropriate to the repository. |
| `Kubernetes-Staging` | Receives new `main` staging releases. | `KUBE_CONFIG`, or a runner with the staging cluster context. |
| `Kubernetes-Production` | Receives manual stable-tag promotion. | Required reviewers and `KUBE_CONFIG`, or a runner with the production cluster context. |

The repository has no local container build, so staging and production use the external Kener image fixed in the AppHost.

## Local Validation

Run the repository-local Actionlint wrapper before committing workflow changes.

```powershell
dotnet run --file scripts/actionlint.cs
```

Local checks cannot prove remote reusable-workflow resolution, token permissions, artifact retention, or runner-group policy.
GitHub-hosted workflow runs remain the required verification environment for those concerns.
