# GitHub Actions secrets and permissions reference

Keep configuration in the narrowest scope that can satisfy the workflow. A repository variable is appropriate for a non-sensitive public API origin. Deployment credentials belong to the `production` environment so they are released only after environment protection rules pass. SQL Server and SSRS runtime credentials belong on the deployment host or in an approved secret manager, not in a repository file.

## Public configuration

| Name | Scope | Sensitive? | Used by |
|---|---|---:|---|
| `FRONTEND_API_URL` | Repository variable | No | `publish-images.yml`; embedded into the React production bundle |
| `APP_URL` | `production` environment variable | No | `deploy-vm.yml`; deployment URL and health check |

## Deployment secrets

| Name | Scope | Purpose |
|---|---|---|
| `DEPLOY_HOST` | `production` environment | SSH host name or address |
| `DEPLOY_USER` | `production` environment | Restricted deployment user |
| `DEPLOY_PATH` | `production` environment | Remote Compose directory |
| `DEPLOY_KNOWN_HOSTS` | `production` environment | Pinned SSH host key line(s) |
| `DEPLOY_SSH_PRIVATE_KEY` | `production` environment | Dedicated GitHub Actions SSH key |
| `GHCR_READ_TOKEN` | `production` environment | Read-only package token for the deployment host |

The workflows intentionally do not ask GitHub Actions to hold the SQL Server password or SSRS password. Store those values in the protected `deploy/.env.production` file on the host, a cloud secret manager, or an organization-managed deployment system. If a future workflow must access them, create separate environment secrets with the minimum scope and use them only in the job that needs them.

## Recommended token settings

The `GITHUB_TOKEN` used by CI has `contents: read`. The image publisher additionally has `packages: write`, `attestations: write`, and `id-token: write` because it publishes GHCR images and creates provenance attestations. The deployment workflow has `contents: read` and uses a separate read-only GHCR token on the target host.

Create a fine-grained GHCR token with package read access only for the deployment account or package. Do not reuse a personal classic token unless the organization policy requires it. Rotate the token and SSH key on a schedule and immediately after a suspected exposure.

## Secret handling rules

Never print secrets, write them to artifacts, or place them in generated JSON/YAML blobs. Avoid using secrets in pull request workflows from forks. The deployment workflow is manual and environment-protected so a pull request cannot reach production credentials merely by modifying application code. Review the Actions logs after the first deployment and remove any debug command that could expose connection strings, access tokens, or private keys.

## Repository settings checklist

Enable secret scanning and push protection if available. Limit who can approve the `production` environment. Require the CI check on protected branches. Review the Actions permissions setting and keep the default `GITHUB_TOKEN` permission read-only. Review installed third-party actions and update them deliberately; for stricter supply-chain controls, pin action references to full commit SHAs after the organization has approved the exact commits.
