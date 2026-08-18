# Frontend connection verification

Verified on 2026-08-18 against the local development servers:

- React/Vite loaded at `http://127.0.0.1:5173/`.
- Vite proxied `/health` and `/api/*` requests to ASP.NET Core at `http://127.0.0.1:5000`.
- Seeded administrator login succeeded through the React form.
- The ASP.NET Core cookie session was preserved for authenticated requests.
- The React dashboard rendered tenant, employee, payroll, and organization data returned by the API.
- API status indicator rendered as `API connected` in the dashboard.
