# Recommended Railway architecture

Do not deploy the repository root as one Railway service. Create three services in one Railway project:

```text
Browser
   |
   v
Frontend service (frontend/; Vite + nginx)
   |
   | HTTPS requests to the public API URL
   v
Backend service (backend/; ASP.NET Core API)
   |
   | private Railway connection string
   v
PostgreSQL service (managed Railway database)
```

## Services

| Railway service | Source directory / configuration | Public access | Required configuration |
|---|---|---|---|
| `frontend` | Repository root `.` with Dockerfile path `deploy/frontend.Dockerfile`, or root directory `frontend/` with an equivalent frontend Dockerfile | Yes | Build arg `VITE_API_URL=https://<backend-domain>` |
| `backend` | Repository root `.` with Dockerfile path `backend/backend.Dockerfile` | Yes | `ConnectionStrings__DefaultConnection`, `Cors__AllowedOrigins__0`, `Auth__CookieSameSite=None`, `Auth__RequireHttps=true` |
| `postgres` | Railway PostgreSQL template/plugin | No | Use the connection URL/reference exposed by Railway in the backend service |

The backend is the only application service that talks to PostgreSQL. Never put the database URL in frontend variables: Vite variables are embedded in browser JavaScript.

## Railway setup

1. Create a Railway project and add a PostgreSQL service.
2. Add a backend service from this repository. Set its Dockerfile to `backend/backend.Dockerfile` and keep the build context at the repository root, because the Dockerfile copies `backend/BluePrintHr.Api`.
3. Add a public backend domain, such as `api.example.com`.
4. Add a frontend service from the same repository. Set its Dockerfile to `deploy/frontend.Dockerfile`, keep the build context at the repository root, and set `VITE_API_URL` to the backend's public HTTPS URL. This is a build-time variable, so redeploy the frontend after changing it.
5. Add the frontend public URL to the backend service as `Cors__AllowedOrigins__0`.
6. Set the backend's `ConnectionStrings__DefaultConnection` to Railway PostgreSQL's connection string/reference. Set `Database__UseInMemory=false`.
7. Set `Database__ApplyMigrations=true` only for the first controlled deployment, with one backend replica. After the schema is created, use a reviewed migration step and set it to `false` for normal releases.
8. Verify `https://<backend-domain>/health`, then load the frontend and test login, cookie authentication, and one database-backed request.

## PostgreSQL migration requirement

The committed EF Core migration under `backend/BluePrintHr.Api/Migrations/` was generated for SQL Server. It cannot be applied directly to Railway PostgreSQL. Before production cutover, add the Npgsql EF Core provider, select PostgreSQL from configuration, and generate/review a PostgreSQL migration set from the current model. Keep the existing SQL Server path if the portable VM deployment still uses SQL Server.

Do not use the frontend service to proxy database traffic, and do not expose PostgreSQL publicly. Railway private networking and the backend's public HTTPS domain are separate concerns: service-to-database traffic stays private, while browser-to-backend traffic uses the backend domain.

## Recommended domains

| Purpose | Example |
|---|---|
| Frontend | `https://app.example.com` |
| Backend API | `https://api.example.com` |
| PostgreSQL | Private Railway service only |

The frontend must be built with `VITE_API_URL=https://api.example.com`; the API must allow `https://app.example.com` and use `SameSite=None` plus secure cookies because the two application services have different origins.