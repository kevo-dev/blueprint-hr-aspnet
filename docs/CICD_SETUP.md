# BluePrint HR CI/CD setup guide

This repository now contains a complete build-and-release path for the requested stack: ASP.NET Core 8, SQL Server, React + Vite, and SSRS. The workflows are deliberately split into validation, image publishing, and deployment. A pull request can build and test the application without production credentials; publishing happens only for a version tag or an explicit manual run; production deployment is a separate manually dispatched job protected by a GitHub environment.

## Deployment choices

The repository does not assume a cloud vendor, SQL Server host, or SSRS host. Choose the operating model that matches the infrastructure you already control.

| Approach | Tradeoffs | Cost | Setup complexity |
|---|---|---:|---:|
| Publish GHCR images and deploy to a Linux VM with Docker Compose | Portable, auditable, and implemented by `publish-images.yml` plus `deploy-vm.yml`; requires SSH access and an external SQL Server/SSRS service | VM, SQL Server, and SSRS hosting costs | Medium |
| Build artifacts only and deploy through an existing platform | Lowest repository coupling; your platform handles hosting, TLS, logs, and rollback, but its deployment adapter must be added | Depends on platform | Low to medium |
| Host API and web containers on a Windows or hybrid host, with SQL Server and SSRS in the Windows environment | Best fit when SSRS is already Windows-based; requires a host-specific deployment script or runner and more platform administration | Windows and SQL Server/SSRS licensing or hosting costs | High |

The included default is the first option because it produces immutable container images and keeps SQL Server and SSRS endpoints configurable. It does not attempt to run SSRS inside the Linux Compose file; SSRS should remain on a supported Windows/SQL Server deployment or an existing reporting service.

## Workflow map

| Workflow | Trigger | Purpose | Production credentials |
|---|---|---|---|
| `.github/workflows/ci.yml` | Pull requests, pushes to `main`, manual | Restore, build, test, lint, generate an idempotent EF SQL script, and upload build artifacts | None |
| `.github/workflows/publish-images.yml` | `v*.*.*` tags or manual | Build the API and React images, push them to GHCR, and generate image provenance attestations | Built-in `GITHUB_TOKEN` plus `FRONTEND_API_URL` repository variable |
| `.github/workflows/deploy-vm.yml` | Manual dispatch only | Pull a selected GHCR tag on a Docker host, restart the API/web services, and check `/health` | `production` environment secrets |

Every workflow sets minimum repository permissions. The CI workflow has read-only contents access. The image workflow grants package write and attestation permissions only because it publishes images. The deployment workflow has read-only repository access and obtains deployment secrets only through the configured environment.

For a local pre-push check, install the optional YAML parser and run the repository validator:

```bash
python3 -m pip install --user pyyaml
python3 scripts/validate_cicd.py
```

The validator parses workflow YAML, checks the required deployment files, parses RDL XML, and scans the checked-in templates for common accidental credential patterns. It is a convenience check; GitHub Actions remains the authoritative CI environment.

## One-time GitHub configuration

### 1. Enable Actions and protect `main`

Open **Settings → Actions → General** and allow the repository workflows to run. Protect the `main` branch under **Settings → Branches**. Require the `CI` workflow to pass before merging, require pull requests, and prevent force pushes. If the repository is private on GitHub Free, environment protection rules and environment secrets may be unavailable; in that case use a protected deployment branch or upgrade the repository plan before relying on environment approvals. See the GitHub environment documentation in the references below.

### 2. Create the production environment

Create an environment named `production` under **Settings → Environments**. Add required reviewers, prevent self-review where available, restrict deployment branches/tags to `main` or version tags, and set the environment URL variable `APP_URL` to the public web URL.

The deployment job references this environment:

```yaml
environment:
  name: production
  url: ${{ vars.APP_URL }}
```

A reviewer must approve the job before the environment secrets become available when the environment is configured with required reviewers.

### 3. Add the image workflow variable

Create a repository variable named `FRONTEND_API_URL` with the public API origin that must be compiled into the React bundle. It should contain only the origin, for example:

```text
https://api.example.com
```

Do not include `/api` because the React client appends its route paths.

### 4. Add deployment environment secrets

Add these secrets to the `production` environment. They are consumed only by `deploy-vm.yml`.

| Secret | Example value | Purpose |
|---|---|---|
| `DEPLOY_HOST` | `deploy.example.com` | DNS name or IP of the Docker host |
| `DEPLOY_USER` | `blueprint-deploy` | Restricted SSH user |
| `DEPLOY_PATH` | `/opt/blueprint-hr` | Directory containing the deployment files |
| `DEPLOY_KNOWN_HOSTS` | Output of `ssh-keyscan -H deploy.example.com` | SSH host-key pinning |
| `DEPLOY_SSH_PRIVATE_KEY` | Ed25519 private key | GitHub runner authentication to the host |
| `GHCR_READ_TOKEN` | Fine-grained token with package read access | Host authentication to pull private GHCR images |

Generate a dedicated deploy key rather than using a personal key:

```bash
ssh-keygen -t ed25519 -C "github-actions-blueprint-hr" -f ./blueprint-hr-deploy-key
ssh-keyscan -H deploy.example.com
```

Install only the public key on the deployment host. Do not paste the private key into the repository, workflow YAML, an issue, or a log. The host user should have access to the deployment directory and Docker, but should not be a general-purpose administrator if that can be avoided.

## First-time deployment host setup

The host must have Docker Engine and the Compose plugin installed. Copy the following files to `DEPLOY_PATH`:

```text
deploy/production.compose.yml
deploy/.env.production
```

Create the environment file from the committed template, then replace every placeholder:

```bash
mkdir -p /opt/blueprint-hr/deploy
cp deploy/.env.production.example /opt/blueprint-hr/deploy/.env.production
chmod 600 /opt/blueprint-hr/deploy/.env.production
```

The deployment host environment file contains the SQL Server connection string, CORS origin, secure cookie settings, and SSRS URL. It is intentionally not stored in GitHub. The host must be able to reach SQL Server and SSRS over their configured network routes.

Log in to GHCR once on the host using a read-only package token, or allow the deployment workflow to perform the login on every release:

```bash
echo "$GHCR_READ_TOKEN" | docker login ghcr.io --username kevo-dev --password-stdin
```

The Compose template binds the API and web containers to localhost. Put a reverse proxy such as nginx, Caddy, or an existing load balancer in front of them to terminate HTTPS. Route the public API origin to the API port and the public web origin to the web port.

## Release procedure

Use a version tag for a release candidate or production release:

```bash
git checkout main
git pull --ff-only origin main
git tag -a v1.0.0 -m "BluePrint HR 1.0.0"
git push origin v1.0.0
```

The tag starts `publish-images.yml`. Confirm that the CI workflow is green, confirm the API and web images exist in GHCR, and then open **Actions → Deploy to Docker host → Run workflow**. Enter the exact image tag, such as `v1.0.0`, and submit it. The `production` environment approval gate should be reviewed before the SSH deployment runs.

The deploy workflow pulls both immutable images, restarts only the API and web services, prints the Compose status, prunes images older than seven days, and checks the public `/health` endpoint when `APP_URL` is configured.

## Database migration policy

The API can apply pending EF Core migrations at startup when `Database__ApplyMigrations=true`. This is convenient for a first deployment or a single-instance maintenance window. It should not be treated as a substitute for a controlled migration process when several API replicas start simultaneously or when a migration is destructive.

For a schema change, generate and review the SQL script in CI, take a SQL Server backup, apply the approved SQL script during the release window, then deploy the application image. Keep migrations backward compatible where possible: add nullable columns or new tables first, deploy code that can read both shapes, backfill data, and remove old columns only in a later release.

Never place a production SQL password in a workflow file. Prefer a secret manager or the deployment host's protected environment file. If the API is configured to migrate on startup, keep the deployment at one API replica during the migration and monitor application logs.

## Rollback

The release is rollback-friendly because images are tagged. To roll back, dispatch `deploy-vm.yml` with the previous known-good tag. Do not automatically roll back a database schema unless the migration was explicitly designed to be reversible and the rollback script has been tested against a backup copy.

```bash
# Example manual rollback input
image_tag: v0.9.4
```

## Security hardening checklist

Use least-privilege `GITHUB_TOKEN` permissions and environment-scoped secrets. Avoid `pull_request_target` for workflows that check out untrusted pull request code. Keep deployment jobs manual and environment-protected. Pin third-party actions to full commit SHAs when the repository's security policy requires immutable action references, and review action source changes during upgrades. Do not interpolate untrusted pull request text directly into shell scripts. Use environment variables for dynamic values and quote shell variables.

Enable Dependabot updates for actions and container base images, enable secret scanning and push protection if available, and periodically rotate the SSH key, GHCR token, SQL credentials, and SSRS credentials. Review workflow logs after the first successful and failed deployment to ensure no secrets are printed.

## Official references

[1]: https://docs.github.com/actions/using-workflows/workflow-syntax-for-github-actions "GitHub Actions workflow syntax"
[2]: https://docs.github.com/actions/deployment/targeting-different-environments/using-environments-for-deployment "Managing environments for deployment"
[3]: https://docs.github.com/actions/guides/publishing-docker-images "Publishing Docker images with GitHub Actions"
[4]: https://docs.github.com/en/actions/reference/security/secure-use "GitHub Actions secure use reference"
[5]: https://docs.github.com/packages/working-with-a-github-packages-registry/working-with-the-container-registry "Working with the GitHub Container registry"
