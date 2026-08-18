# BluePrint HR — Architecture, CI/CD, and Deployment Strategy

## Presentation design

Create a 16:9 executive-technical deck with a dark navy workspace palette, white text, teal primary accents, warm amber for release gates, and restrained red only for risk callouts. Use clean system diagrams, short statements, and one strong visual per slide. Avoid dense code; show only the commands and configuration concepts needed to explain how the system is operated.

## Slide 1 — Title

**Title:** BluePrint HR

**Subtitle:** Architecture, CI/CD pipelines, and deployment strategy

**Supporting line:** ASP.NET Core 8 · SQL Server · React/Vite · SSRS

**Visual direction:** Abstract layered HR platform graphic: browser dashboard on the left, API/data/reporting services in the center, and protected release pipeline on the right. Include a small status label: “Ported implementation · GitHub-ready delivery”.

**Speaker emphasis:** This is a native port of the BluePrint HR workflows using the required stack. Laravel is intentionally excluded because the required backend is ASP.NET Core.

## Slide 2 — Executive architecture at a glance

**Title:** One product, four cooperating layers

**Core message:** The platform separates user experience, business APIs, relational persistence, and reporting so each concern can evolve without exposing operational credentials to the browser.

**Visual:** Four horizontal layers with arrows:

```text
React + Vite browser
        ↓ credentialed REST calls
ASP.NET Core 8 API
        ↓ EF Core / server-side report metadata
SQL Server                         SSRS
```

**Callouts:**

| Layer | Responsibility |
|---|---|
| React | Login, dashboard, employees, organization, payroll, leave, ESS, audit, report launch |
| ASP.NET Core | Cookie sessions, RBAC, tenant scope, domain workflows, audit records, report metadata |
| SQL Server | Users, tenants, employees, payroll, leave, audit, report catalog |
| SSRS | Parameterized operational reports rendered by the report server |

## Slide 3 — Runtime architecture and trust boundaries

**Title:** The browser never owns the sensitive boundary

**Core message:** The API is the policy enforcement point. Tenant scope comes from the authenticated session, and SQL Server/SSRS credentials remain server-side.

**Visual:** Trust-boundary diagram with three zones:

```text
[Public browser]
 React bundle ── HTTPS + HttpOnly cookie ──▶ [API trust boundary]
                                             ├─ role policies
                                             ├─ tenant predicates
                                             ├─ audit writes
                                             ├─ EF Core ──▶ [SQL Server]
                                             └─ report metadata ──▶ [SSRS]
```

**Key controls:**

- Single login form with role-aware navigation.
- Super Admin, Company Admin, HR Manager, Payroll Manager, and Employee policies.
- Employees are restricted to their own profile, leave, payslips, and ESS records.
- Administrative roles can access tenant-wide employee and operational data.
- Reports receive parameters and metadata through the API; report credentials are not embedded in React.

## Slide 4 — Domain and data design

**Title:** A tenant-aware HR domain on SQL Server

**Core message:** EF Core models preserve the source HR workflows while giving SQL Server a durable relational model and a controlled migration path.

**Visual:** Entity group diagram with four clusters:

```text
Tenant / Organization
  Tenant → Branch → Department → Designation → Employee

Identity / Governance
  User → Role claims → AuditLog

Payroll
  Employee → PayrollPeriod → PayrollTransaction

Leave / ESS
  Employee → LeaveBalance → LeaveRequest
```

**Data strategy:**

| Concern | Approach |
|---|---|
| Schema evolution | EF Core migrations plus idempotent SQL release scripts |
| Local development | Optional SQL Server container; in-memory provider for fast development/tests |
| Tenant safety | Tenant ID derived from authenticated claims and applied in data queries |
| Payroll | Deterministic calculation of PAYE, NSSF, SHIF, Housing Levy, deductions, and net pay |
| Auditability | Mutating endpoints create audit records |

## Slide 5 — Frontend-to-API flow

**Title:** Same-origin locally, explicit origins in production

**Core message:** The React client has one API contract that works through a Vite proxy in development and an absolute API origin in production.

**Visual:** Two-lane flow:

```text
Local:    Browser :5173 ── Vite proxy /api ──▶ ASP.NET Core :5000

Prod:     Browser HTTPS ── CORS + secure cookie ──▶ API HTTPS
                         └────────────────────────▶ SQL Server / SSRS
```

**Implementation details:**

- `VITE_API_URL` is empty locally so Vite proxies `/api/*` and `/health`.
- `VITE_API_URL=https://api.example.com` is embedded into production React builds.
- API requests use `credentials: include` for cookie sessions.
- Separate production origins require explicit CORS origins, `SameSite=None`, `Secure`, and HTTPS.
- The browser receives no SQL Server or SSRS passwords.

## Slide 6 — Pull-request CI pipeline

**Title:** Every change earns the right to ship

**Core message:** Pull requests and pushes to `main` validate the full application without production credentials.

**Visual:** Pipeline with parallel lanes:

```text
Pull request / main push
            │
      ┌─────┴─────┐
      ▼           ▼
 .NET lane     React lane
 restore       pnpm install
 build         lint
 tests         production build
 EF SQL        bundle artifact
 script
      └─────┬─────┘
            ▼
      Artifacts + green check
```

**Checks:**

- .NET 8 restore, Release build, and xUnit tests.
- EF Core idempotent SQL migration script generation and artifact upload.
- React dependency install, lint, production build, and bundle artifact upload.
- Read-only repository permissions; no production secrets in CI.
- Dedicated report-validation workflow parses SSRS RDL XML on report changes.

## Slide 7 — Release and image publication

**Title:** Version tags create immutable release candidates

**Core message:** Publishing is separated from validation and happens only on `v*.*.*` tags or explicit manual dispatch.

**Visual:** Release conveyor:

```text
Green main
   ↓ annotated v1.0.0 tag
Resolve FRONTEND_API_URL
   ↓
Build API image ──▶ GHCR
Build React image ─▶ GHCR
   ↓
Image metadata + cache + provenance attestations
```

**Release properties:**

| Property | Design choice |
|---|---|
| Image registry | GitHub Container Registry |
| Image names | `blueprint-hr-api` and `blueprint-hr-web` |
| React configuration | Public API origin supplied through repository variable or manual input |
| Tags | Version tag, commit SHA, and `latest` for version releases |
| Supply chain | Build metadata, cached layers, and provenance attestations |
| Credentials | `GITHUB_TOKEN` package write permission; no database passwords |

## Slide 8 — Production deployment strategy

**Title:** Approved deployment to a controlled Docker host

**Core message:** The default path uses a Linux Docker host for API/web containers while SQL Server and SSRS remain external, protected services.

**Visual:** Deployment topology:

```text
GitHub Actions
  ├─ protected production environment
  ├─ pinned SSH host key + dedicated deploy key
  └─ GHCR read token
             │ SSH
             ▼
Reverse proxy / HTTPS
  ├─ React web container :8080 (localhost-bound)
  └─ API container :8080 (localhost-bound)
             ├─ private route → SQL Server
             └─ private route → SSRS
```

**Deployment sequence:**

1. Reviewer approves the `production` environment.
2. Workflow logs into GHCR on the host and pulls the selected immutable tag.
3. Docker Compose restarts only the API and web services.
4. Compose status and public `/health` are checked with retries.
5. Old images older than seven days are pruned after the release succeeds.

**Boundary note:** SQL Server and SSRS are intentionally not run inside the Linux production Compose template.

## Slide 9 — Database, reporting, and rollback gates

**Title:** Release safety is more than a green build

**Core message:** Schema and reporting changes have explicit review points because application rollback alone cannot undo a data migration.

**Visual:** Three gated tracks:

```text
Application image ───────────────┐
EF SQL script + backup ──────────┼─▶ Approved release
SSRS RDL + query/parameter review ┘
```

**Operational policy:**

- CI generates an idempotent EF SQL script for review.
- Production migrations normally run as a controlled database step, not concurrently from multiple API replicas.
- Take a SQL Server backup before applying a schema change.
- Prefer backward-compatible expand/migrate/contract changes.
- Keep the previous image tag and report definitions for rollback.
- Use database restore for data-loss rollback; do not assume an older image reverses the schema.
- SSRS publishing should use a controlled Windows runner, approved management API, or manual report-owner process.

## Slide 10 — Security, operations, and next steps

**Title:** The operating model is ready for infrastructure-specific hardening

**Security and operations:**

| Area | Current control | Next hardening step |
|---|---|---|
| GitHub Actions | Least-privilege permissions and protected deployment environment | Pin third-party actions to full SHAs if required by policy |
| Secrets | Environment-scoped SSH/GHCR secrets; SQL/SSRS credentials stay on host/secret manager | Rotate keys/tokens and enable secret scanning/push protection |
| Runtime | Non-root API image, localhost-bound containers, HTTPS reverse proxy | Add centralized logs, metrics, and uptime alerts |
| Database | Reviewed EF script, backup-before-migration policy | Add staging restore rehearsal and migration approval evidence |
| Reporting | RDLs in source control and XML validation | Add Windows runner or approved SSRS publish adapter |

**Closing statement:** The repository now has a repeatable path from pull request to validated artifacts, versioned images, approved deployment, health verification, and rollback-ready operations. The remaining decisions are infrastructure-specific: production host, public URLs, SQL Server/SSRS endpoints, and the organization's secret-management policy.

## Slide 11 — Appendix: operator quick start

**Title:** First release in eight commands

**Commands:**

```bash
git checkout main
git pull --ff-only origin main
git tag -a v1.0.0 -m "BluePrint HR 1.0.0"
git push origin v1.0.0
# Review Publish container images in GitHub Actions
# Apply the approved database/Release.sql to SQL Server
# Dispatch Deploy to Docker VM with image_tag=v1.0.0
# Verify https://app.example.com/health
```

**Presenter note:** The exact secrets, environment setup, host preparation, migration procedure, SSRS deployment choices, and rollback steps are documented in `docs/CICD_SETUP.md`, `docs/DEPLOYMENT_RUNBOOK.md`, `docs/SSRS-CICD.md`, and `docs/REPOSITORY_SECRETS.md`.
