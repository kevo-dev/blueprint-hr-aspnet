# SSRS CI/CD guidance

SSRS is part of the required reporting stack, but it is not included in the Linux API/web Compose deployment. Keep SSRS on a supported Windows/report-server environment or use an organization-managed SSRS service. The ASP.NET Core API exposes tenant-scoped report catalog metadata and the React application launches reports through the API contract; SSRS credentials and server-side data access remain outside the browser.

## Report assets in this repository

The `reports/` directory contains the checked-in RDL definitions:

| Report | Purpose | Parameters |
|---|---|---|
| `reports/PayrollSummary.rdl` | Payroll totals and statutory deductions for a tenant and period | `TenantId`, `PayrollPeriodId` |
| `reports/EmployeeRoster.rdl` | Employee roster with organization data and statutory identifiers | `TenantId` |

Keep RDL files in source control. Review query text, data-source references, parameters, and visibility rules in pull requests. Do not embed passwords, connection strings, or production server URLs in the RDL files.

## Validate RDL files in CI

The repository's CI workflow should treat RDL XML as a build input. A lightweight XML validation step can be added to a future Windows or Linux job:

```yaml
- name: Validate SSRS report XML
  shell: pwsh
  run: |
    $files = Get-ChildItem reports -Filter *.rdl
    if ($files.Count -eq 0) { throw 'No RDL files found.' }
    foreach ($file in $files) {
      [xml](Get-Content -Raw $file.FullName) | Out-Null
      Write-Host "Validated $($file.Name)"
    }
```

If the SSRS project uses a Visual Studio `.rptproj`, add a Windows runner job that builds the report project with the exact SSDT/Visual Studio version used by the report server. Do not mix a successful XML parse with a claim that the report has been rendered; query execution and rendering still require a real report server and database.

## Publishing options

| Option | Use when | Tradeoff |
|---|---|---|
| Publish from a controlled Windows self-hosted runner | The report server is reachable only from a private network and your organization already operates a runner | Strong network locality, but the runner must be patched and tightly restricted |
| Publish manually after an approved release | Report changes are infrequent and require a human report-owner review | Lowest automation risk, but a slower release path |
| Publish through the SSRS REST/SOAP management API | The report server exposes a controlled management endpoint and credentials can be stored in an environment | Repeatable, but requires a purpose-built script and careful permissions |

The repository does not automatically publish RDL files because an SSRS deployment endpoint, report-folder mapping, data-source policy, and authentication mechanism were not provided. Add them only after the report server owner confirms the target URL and security model.

## Controlled SSRS publishing pattern

A safe deployment job should run only after CI passes and a `production` environment approval succeeds. It should validate the RDL files, authenticate to the report server using environment secrets, publish to a staging folder, run a smoke-render or metadata check, and then promote or move the reports to the production folder. The job should never echo the report-server credential or serialize a structured credential object into logs.

A Windows runner can use a PowerShell script such as the following as a starting point. The exact command must be adapted to your SSRS version and organization policy:

```powershell
param(
    [Parameter(Mandatory = $true)][string]$ReportServerUrl,
    [Parameter(Mandatory = $true)][string]$ReportFolder,
    [Parameter(Mandatory = $true)][string]$RdlDirectory
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $RdlDirectory)) {
    throw "RDL directory not found: $RdlDirectory"
}

$rdls = Get-ChildItem -Path $RdlDirectory -Filter '*.rdl'
if ($rdls.Count -eq 0) {
    throw 'No RDL files found.'
}

foreach ($rdl in $rdls) {
    [xml](Get-Content -Raw $rdl.FullName) | Out-Null
    Write-Host "Validated $($rdl.Name)"
}

Write-Warning 'RDL files are validated locally. Configure the organization-approved SSRS publishing command here.'
Write-Host "Target server: $ReportServerUrl"
Write-Host "Target folder: $ReportFolder"
```

Replace the warning with the approved `rs.exe`, SSRS REST API, or vendor deployment command only after testing against a non-production report folder. Keep the SSRS publish identity limited to the target folder and data-source operations required by the release.

## SSRS security checklist

Use Windows authentication, service principals, or a managed identity where supported instead of embedding report passwords. Restrict the report-server management endpoint to the self-hosted runner network. Keep report execution credentials server-side. Map report parameters to tenant and employee scope in the API and verify that report users cannot change a tenant identifier to access another tenant. Test both an authorized manager report and a denied employee/admin boundary before a production release.

## Report rollback

Keep the previous RDL set as a release artifact. If a report fails after deployment, restore the previous report definition and leave the application image unchanged unless the API contract also changed. If a report query depends on a database migration, deploy the database change before publishing the RDL and keep the query backward compatible during the rollout.
