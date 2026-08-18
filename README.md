# BluePrint HR — ASP.NET Core Port

BluePrint HR is a Kenya-focused, multi-tenant human-resources and payroll workspace ported from the original `blueprint-hr` repository. The implementation preserves the source product direction while replacing the original data and server layers with the requested strict stack.

> **Strict stack:** ASP.NET Core 8 Web API, SQL Server, React + Vite, and SSRS report definitions. Laravel is intentionally not included because it conflicts with the specified ASP.NET Core backend requirement.

## Implemented scope

The port includes cookie-based authentication, five source-aligned roles, tenant-scoped data access, employee master records, organization setup, Kenyan payroll calculations, leave balances and approval workflows, employee self-service, audit history, and an SSRS report catalog. The seeded tenant is `BluePrint Kenya Ltd` and includes an administrator, an employee profile, a payroll period, leave types, and two report definitions.

| Area | Implementation |
| --- | --- |
| Backend | ASP.NET Core 8 Web API with controllers, cookie sessions, role policies, and OpenAPI/Swagger in development |
| Database | EF Core 8 with SQL Server provider; development can use the in-memory provider for fast local runs |
| Frontend | React 19 + TypeScript + Vite, with a role-aware dashboard and responsive dark workspace UI |
| Reporting | SSRS-ready `.rdl` files, report catalog API, and server-side launch URLs |
| Security | Tenant claim on every session, tenant predicates in data queries, role policies, HttpOnly cookie, audit records |
| Payroll | Gross pay, PAYE, personal relief, NSSF, SHIF, Housing Levy, other deductions, net pay |

## Repository layout

```text
backend/
  BluePrintHr.Api/
    Controllers/       REST endpoints
    Contracts/         Request and response records
    Data/              EF Core context and seed initializer
    Models/            HR domain entities and enums
    Services/          Password, request context, payroll calculation
  BluePrintHr.Api.Tests/  xUnit payroll tests
frontend/
  src/                 React dashboard, API client, and styles
reports/                SSRS PayrollSummary and EmployeeRoster RDL files
database/               SQL Server bootstrap schema
docker-compose.yml      SQL Server Developer container for local development
```

## Local setup

### 1. Prerequisites

Install .NET 8 SDK, Node.js 20 or later, pnpm, and Docker Desktop or Docker Engine if SQL Server is required locally.

### 2. Start SQL Server

```bash
docker compose up -d sqlserver
```

The development compose file uses SQL Server Developer Edition on `localhost:1433` with the sample password shown in the compose file. Replace it before sharing or deploying the environment.

### 3. Configure the API

For a fast development run, the API automatically uses the in-memory provider when `ASPNETCORE_ENVIRONMENT=Development`. For SQL Server, set `Database:UseInMemory` to `false` and provide `ConnectionStrings:DefaultConnection` in `backend/BluePrintHr.Api/appsettings.json` or an environment-specific settings file.

To apply the explicit SQL Server schema:

```bash
sqlcmd -S localhost,1433 -U sa -P 'Your_strong_password123!' -C \
  -i database/001_schema.sql
```

Then set:

```json
{
  "Database": {
    "UseInMemory": false,
    "ApplyMigrations": false
  }
}
```

The API's initializer is idempotent and seeds the first tenant when the database is empty. Set `Database:ApplyMigrations` to `true` if a migration history is introduced later.

### 4. Run the API

```bash
export PATH="$HOME/.dotnet:$PATH"
cd backend/BluePrintHr.Api
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5000 dotnet run
```

Health check: `http://localhost:5000/health`  
Swagger UI: `http://localhost:5000/swagger`

### 5. Run the React frontend

```bash
cd frontend
cp .env.example .env.local
pnpm install
pnpm dev
```

The default frontend API URL is `http://localhost:5000`. Override it in `frontend/.env.local` with `VITE_API_URL` when the API is hosted elsewhere.

## Seeded credentials

| User | Email | Password | Role |
| --- | --- | --- | --- |
| Administrator | `admin@blueprinthr.co.ke` | `BluePrint!2026` | Company Admin |
| Employee | `amina.njoroge@blueprinthr.co.ke` | `Employee!2026` | Employee |

Change these seeded passwords before using a shared or production environment.

## API surface

| Route | Purpose | Access |
| --- | --- | --- |
| `POST /api/auth/login` | Start a secure cookie session | Anonymous |
| `GET /api/dashboard` | Tenant overview and metrics | Authenticated |
| `GET/POST /api/employees` | Employee master | Managers for writes; tenant-scoped reads |
| `GET/POST /api/organization/*` | Branches and departments | Managers for writes |
| `GET /api/payroll/periods` | Payroll period list | Authenticated |
| `GET /api/payroll/transactions` | Payroll transactions | Role-scoped |
| `POST /api/payroll/process` | Calculate and approve a period | Payroll managers and admins |
| `GET/POST /api/leave/*` | Types, balances, requests | Authenticated |
| `PATCH /api/leave/requests/{id}/status` | Approve or reject leave | Approvers |
| `GET /api/ess/*` | Employee profile and payslips | Authenticated |
| `GET /api/audit` | Recent audit events | Super Admin, Company Admin, HR Manager |
| `GET /api/reports` | SSRS report catalog and launch URLs | Authenticated |

## SSRS integration

The `reports/` directory contains:

- `PayrollSummary.rdl`, parameterized by `TenantId` and `PayrollPeriodId`.
- `EmployeeRoster.rdl`, parameterized by `TenantId` and `EmploymentStatus`.

Deploy both RDL files to an SSRS folder such as `/BluePrintHR`, create a shared data source named `BluePrintHRDataSource`, and set `Ssrs:ReportServerUrl` to the report server base URL. The frontend receives report launch metadata from `GET /api/reports`; report credentials remain on the SSRS server and are never placed in browser code.

## Verification

The current implementation has been verified with:

```bash
dotnet build backend/BluePrintHr.Api/BluePrintHr.Api.csproj --no-restore
dotnet test backend/BluePrintHr.Api.Tests/BluePrintHr.Api.Tests.csproj --no-restore
cd frontend && pnpm build
```

The API build, four xUnit tests, and React production build complete successfully.
