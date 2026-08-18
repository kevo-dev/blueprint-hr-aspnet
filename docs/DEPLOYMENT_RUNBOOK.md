# BluePrint HR deployment runbook

This runbook is for the container-based deployment path represented by `deploy/production.compose.yml`. It assumes that the ASP.NET Core API and React web application run as Linux containers, while SQL Server and SSRS are reachable as managed or separately hosted services. The same release tags can be consumed by a Windows or hybrid deployment process if your organization requires SSRS and SQL Server to remain on Windows.

## Required topology

The browser should reach the React web origin over HTTPS. The React bundle should be built with the public API origin in `VITE_API_URL`. The browser sends credentialed requests to that API origin, so the API must list the web origin in `Cors__AllowedOrigins__0` and must use `Auth__CookieSameSite=None` and `Auth__RequireHttps=true` for separate HTTPS origins.

The API container reaches SQL Server using `ConnectionStrings__DefaultConnection` and reaches SSRS using `Ssrs__ReportServerUrl`. SQL Server should not be exposed publicly. The reverse proxy should expose only the web and API HTTPS routes, and should forward the API `/health` endpoint without authentication so an external monitor can check it.

## Prepare the deployment host

Install Docker Engine and the Docker Compose plugin on a supported Linux host. Create a restricted deployment directory and transfer the Compose file and the protected environment file:

```bash
sudo install -d -m 750 -o blueprint-deploy -g blueprint-deploy /opt/blueprint-hr/deploy
scp deploy/production.compose.yml blueprint-deploy@deploy.example.com:/opt/blueprint-hr/deploy/
scp deploy/.env.production blueprint-deploy@deploy.example.com:/opt/blueprint-hr/deploy/
ssh blueprint-deploy@deploy.example.com \
  'chmod 600 /opt/blueprint-hr/deploy/.env.production'
```

The deployment host needs outbound access to `ghcr.io`, SQL Server, and SSRS. The SQL Server firewall should allow only the API host or private network. The SSRS service should accept requests only from the API service or a protected reporting network; the browser should never receive SSRS credentials.

## Configure the production environment

Start with `deploy/.env.production.example` and replace placeholders. A minimum configuration looks like this:

```dotenv
API_PORT=8080
WEB_PORT=8081
ConnectionStrings__DefaultConnection=Server=sql.example.com,1433;Database=BluePrintHr;User Id=blueprint_hr_app;Password=REPLACE_ME;TrustServerCertificate=True;MultipleActiveResultSets=True
Database__UseInMemory=false
Database__ApplyMigrations=false
Cors__AllowedOrigins__0=https://app.example.com
Auth__CookieSameSite=None
Auth__RequireHttps=true
Ssrs__ReportServerUrl=https://reports.example.com/ReportServer
```

For a first install, you may temporarily set `Database__ApplyMigrations=true` while running one API instance. For normal releases, set it to `false` and apply a reviewed SQL script before starting the new application image. This avoids multiple replicas competing to update the schema.

## Apply SQL Server migrations

Generate and inspect the idempotent script in CI or from a trusted release checkout:

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
dotnet tool restore
dotnet ef migrations script --idempotent \
  --project backend/BluePrintHr.Api/BluePrintHr.Api.csproj \
  --startup-project backend/BluePrintHr.Api/BluePrintHr.Api.csproj \
  --context BluePrintHrDbContext \
  --output database/Release.sql
```

Back up the database, review the script, and apply it with a controlled SQL Server identity. The exact command depends on whether `sqlcmd` is installed on the operator workstation or a database administration host:

```bash
sqlcmd -S sql.example.com,1433 -d BluePrintHr \
  -U blueprint_hr_migrator -P "$SQL_MIGRATOR_PASSWORD" \
  -b -i database/Release.sql
```

Do not run a destructive migration directly against production without a backup and a tested rollback or restore plan. A database restore is the normal rollback mechanism for data-loss migrations; application image rollback alone does not undo schema changes.

## Pull and start a release manually

The deployment workflow automates these commands after environment approval. They are also useful for a first install or troubleshooting:

```bash
cd /opt/blueprint-hr
echo "$GHCR_READ_TOKEN" | docker login ghcr.io --username kevo-dev --password-stdin
export API_IMAGE=ghcr.io/kevo-dev/blueprint-hr-api
export WEB_IMAGE=ghcr.io/kevo-dev/blueprint-hr-web
export IMAGE_TAG=v1.0.0
docker compose --env-file deploy/.env.production \
  -f deploy/production.compose.yml pull api web
docker compose --env-file deploy/.env.production \
  -f deploy/production.compose.yml up -d --remove-orphans api web
docker compose --env-file deploy/.env.production \
  -f deploy/production.compose.yml ps
```

Verify the API locally on the host before checking the public route:

```bash
curl --fail http://127.0.0.1:8080/health
curl --fail http://127.0.0.1:8081/health
```

Then verify the public HTTPS routes, browser login, dashboard data, and a report-catalog request. Confirm that the browser's cookie is marked secure and that no SQL Server or SSRS credential appears in browser storage or network responses.

## Release checklist

A release is ready when the pull request has a green CI run, the database script has been reviewed, the version tag points to the intended `main` commit, and the image workflow has published both images. Before approval, confirm that the target environment's `APP_URL`, host key, deployment path, and GHCR read token are current. After deployment, inspect API logs, confirm `/health`, verify login and one read-only dashboard request, and review the reverse-proxy access logs for unexpected errors.

## Rollback checklist

Stop promotion first if health or login checks fail. Dispatch `deploy-vm.yml` with the previous known-good image tag. If the failure is schema-related, do not keep restarting the older application image against an incompatible schema; follow the database restore or backward-compatible migration plan. Capture container logs and the failed workflow run before deleting old images.

```bash
docker compose --env-file deploy/.env.production \
  -f deploy/production.compose.yml logs --tail=200 api web
```

## Troubleshooting matrix

| Symptom | Likely cause | First check |
|---|---|---|
| React loads but API calls fail with CORS errors | API origin or allowed web origin is wrong | Compare `VITE_API_URL` with `Cors__AllowedOrigins__0`; both must be HTTPS in production |
| Login succeeds but later requests are anonymous | Cookie `SameSite`/`Secure` settings do not match the cross-origin topology | Confirm `Auth__CookieSameSite=None`, `Auth__RequireHttps=true`, and HTTPS on both origins |
| API container starts but reports database errors | SQL Server DNS, firewall, credentials, or certificate trust issue | Inspect `docker compose logs api` and test SQL connectivity from the host |
| Images cannot be pulled | GHCR package visibility or read token scope is wrong | Run `docker login ghcr.io` with the read-only token and check package access |
| Reports list but do not render | SSRS URL, report path, credentials, or report-server permissions are wrong | Test the report-server URL from the API host and inspect SSRS logs |
| Deployment job cannot read secrets | Environment approval or private-repository plan restrictions | Review the `production` environment protection rules and GitHub plan availability |
| Migration fails | Schema drift, blocked lock, or destructive SQL change | Restore a backup to a test database and run the same script there first |

## Additional operational tips

Use UTC for server logs and release timestamps. Keep the API and web images immutable by deploying version tags rather than floating `latest` in production. Retain at least one previous image tag and one recent database backup. Add uptime monitoring for `/health` and alert on repeated 5xx responses, SQL connection failures, and SSRS report failures. Schedule dependency updates for .NET, Node, nginx, the SQL Server client packages, and GitHub Actions. Keep production configuration outside Git and document every required variable in `deploy/.env.production.example`.
