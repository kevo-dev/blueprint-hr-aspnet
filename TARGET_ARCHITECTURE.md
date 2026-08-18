# BluePrint HR target architecture

## Decision

The target repository is a native port of the existing BluePrint HR workflows. It uses **ASP.NET Core 8 Web API** for the backend, **Entity Framework Core with SQL Server** for persistence, **React 19 with Vite and TypeScript** for the frontend, and **SSRS-compatible RDL reports** for reporting. Laravel is intentionally not used because it conflicts with the requested ASP.NET Core backend.

## Repository layout

| Path | Responsibility |
| --- | --- |
| `backend/BluePrintHr.Api` | ASP.NET Core API, EF Core models, services, auth, tenant scope, payroll calculations, and report endpoints |
| `backend/BluePrintHr.Api/Database` | SQL Server initialization and seed scripts |
| `frontend` | React/Vite application with login, dashboard, employees, organization, payroll, leave, ESS, audit, and reporting surfaces |
| `reports` | SSRS `.rdl` definitions and deployment notes |
| `docker-compose.yml` | Local SQL Server container and application wiring |
| `README.md` | Setup, migration, seed credentials, local run, and SSRS deployment instructions |

## Backend design

The API uses cookie authentication for the browser session and role policies for `Super Admin`, `Company Admin`, `HR Manager`, `Payroll Manager`, and `Employee`. Tenant-aware queries derive the tenant ID from the authenticated user and never accept it from the browser for normal operations. Administrative users can view tenant-wide data; employees are restricted to their own employee profile, leave balances, leave requests, payslips, and profile data.

Entity Framework Core models mirror the most important source entities: users, tenants, branches, departments, employees, payroll periods, payroll transactions, leave types, leave balances, leave requests, audit logs, and report catalog entries. The implementation retains extension points for the source enterprise modules without pretending that SSRS itself is an OLTP store.

## API surface

The first port exposes health, session, dashboard, organization, employees, payroll, leave, ESS, audit, and reporting endpoints. Mutating endpoints write audit records. Payroll processing is deterministic and calculates PAYE, NSSF, SHIF, Housing Levy, deductions, and net pay from configured rates. A report endpoint returns SSRS render metadata and the configured report server URL; it does not embed credentials in the React bundle.

## Database and reporting

SQL Server is the only supported relational database. The repository includes EF Core migrations and an idempotent SQL seed script. SSRS reports use `DataSourceReference` placeholders and parameterized datasets so they can be deployed to an existing SSRS instance. The API provides a safe redirect/metadata endpoint for report launch while leaving authentication to the report server's configured security model.

## Verification target

The solution must compile with the .NET 8 SDK, pass backend tests without a live SQL Server by using an in-memory EF provider for unit tests, build the React frontend, and provide a Docker Compose path for SQL Server-backed local development. Where the sandbox cannot run SQL Server or SSRS, the repository will document those environment-dependent checks explicitly.
