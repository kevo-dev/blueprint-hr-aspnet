# Deployment templates

This directory contains deployment templates for the portable Docker-host option.

| File | Commit? | Purpose |
|---|---:|---|
| `backend.Dockerfile` | Yes | Builds the ASP.NET Core API image |
| `frontend.Dockerfile` | Yes | Builds the React/Vite bundle and nginx image |
| `nginx.conf` | Yes | Serves the React single-page application |
| `production.compose.yml` | Yes | Runs the API and web images on a deployment host |
| `.env.production.example` | Yes | Documents required production variables without credentials |
| `.env.production` | No | Real host-only SQL Server, CORS, cookie, and SSRS configuration |

The GitHub Actions deploy workflow expects `production.compose.yml` and a real `.env.production` file to exist under the remote `DEPLOY_PATH/deploy` directory. Copy the template, replace placeholders, restrict permissions to the deployment user, and never commit the copied file.

The Compose template assumes that a reverse proxy terminates HTTPS and forwards the public web and API origins to the localhost-bound container ports. It also assumes SQL Server and SSRS are external services. Do not expose port 1433 or an SSRS management endpoint to the public internet.
