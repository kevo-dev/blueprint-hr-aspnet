# BluePrint HR release checklist

Use this checklist for every versioned release. The checklist is intentionally short enough to be copied into a pull request or release description.

## Before tagging

| Check | Evidence |
|---|---|
| Backend builds in Release mode | Green `CI / Build and test ASP.NET Core` job |
| Backend tests pass | Green test step and no unexplained warnings |
| React lint and production build pass | Green `CI / Build and lint React` job |
| EF migration reviewed | SQL script uploaded by CI and reviewed by an approved database owner |
| SSRS RDL files validate | XML validation passes; query and parameter changes reviewed |
| Configuration changes documented | README and deployment templates updated; no credentials committed |
| Rollback target selected | Previous application image tag and database backup identified |

## Publish and deploy

Create an annotated version tag only after the checks above are complete. Confirm that `publish-images.yml` pushed both images to GHCR and that the React image was built with the correct `FRONTEND_API_URL`. Apply the database script during the approved release window. Dispatch `deploy-vm.yml` with the exact image tag and obtain the `production` environment approval.

## After deployment

Check the API and web container status, the public `/health` endpoint, login, dashboard data, one employee or ESS read-only request, and one report catalog or report launch path. Review API, reverse-proxy, SQL Server, and SSRS logs. Record the deployed tag, migration version, report version, and operator approval in the release notes.

## Abort criteria

Stop the release if the API cannot connect to SQL Server, if secure cookies are not present on the HTTPS origin, if the public API returns CORS failures, if the SSRS data source is unavailable, or if the migration script fails in the backup test environment. Roll back the application image only when the current database schema remains compatible with the previous image.
