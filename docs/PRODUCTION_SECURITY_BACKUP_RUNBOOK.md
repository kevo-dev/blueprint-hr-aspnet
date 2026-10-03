# BluePrint HR production security, backup and monitoring runbook

## Required production environment

- ASPNETCORE_ENVIRONMENT=Production
- Database__UseInMemory=false
- Database__ApplyMigrations=false
- Auth__CookieSameSite=None
- Auth__RequireHttps=true
- Cors__AllowedOrigins__0 must be the exact Vercel origin, without a trailing slash.
- App__FrontendUrl must be the public frontend origin.
- Configure SMTP using Email__SmtpHost, Email__SmtpPort, Email__SmtpUsername, Email__SmtpPassword, Email__From and Email__EnableSsl=true.
- Configure DataProtection__KeyDirectory to a persistent mounted directory. Do not use an ephemeral container filesystem for production cookie keys.

## Backup and restore

The application connects directly to Supabase PostgreSQL. Database backup/restore must be performed using the Supabase project's managed backup/PITR facilities or an approved PostgreSQL backup process; application containers must not hold database credentials in source control.

At minimum:
1. Enable the appropriate Supabase backup/PITR plan for the production project.
2. Define a documented RPO and RTO with the business owner.
3. Test a restore into a non-production project at least quarterly.
4. Record the restore timestamp, schema migration version, row-count checks and application smoke-test result.
5. Never restore directly over production without an approved change window and a verified backup.

## Monitoring

- Render should monitor /health for liveness and /health/ready for database readiness.
- Alert on repeated 5xx responses, failed deployments, database connectivity failures and authentication spikes.
- Supabase logs should be reviewed for database/API errors and unusual traffic.
- Keep application logs free of passwords, reset tokens, connection strings and other secrets.

## Security audit checklist

- [x] HttpOnly/Secure production session cookie.
- [x] SameSite configured for cross-site Vercel -> Render deployment.
- [x] Exact-origin CSRF protection for mutating API requests.
- [x] Session invalidation through per-user security stamps.
- [x] Disabled users and suspended tenants rejected at login and during session validation.
- [x] Password reset tokens are random, hashed at rest, single-use and time-limited.
- [x] Password policy enforced server-side.
- [x] Tenant-scoped authorization on HR data.
- [x] Super Admin separated from tenant administration.
- [x] Security response headers.
- [x] Database readiness endpoint.
- [x] Automated password and API security tests in CI.
- [ ] Production SMTP credentials configured and tested.
- [ ] Persistent data-protection key storage configured and tested across a restart.
- [ ] Supabase backup/PITR configured and restore drill completed.
- [ ] Render/Supabase alerts configured.
- [ ] Independent penetration test completed before handling production employee data.

## Kenya payroll compliance checkpoint

The payroll engine must be reviewed against the statutory rules effective for the payroll period being processed. KRA's current PAYE guidance documents the 10%, 25%, 30%, 32.5% and 35% bands and KES 2,400 monthly personal relief; KRA also identifies SHIF and Affordable Housing Levy as allowable PAYE deductions. NSSF publishes year-specific contribution rates, so the NSSF parameters must be versioned rather than hard-coded forever.

Do not treat the statutory export as an automatic filing to KRA/NSSF/SHA. The export is a controlled data file for reconciliation and filing through the relevant government systems.

## Operational smoke test

1. Login as Super Admin.
2. Login as Company Admin.
3. Activate an employee account and verify invitation/reset email.
4. Change password and verify the old session is invalidated.
5. Request forgot-password and complete a reset.
6. Suspend a tenant and verify active sessions are rejected.
7. Process a payroll period and export PAYE/NSSF/SHIF/AHL reports.
8. Verify /health/ready.
9. Restart the API and confirm existing sessions remain valid when persistent data-protection keys are configured.
10. Perform a restore drill and verify application connectivity.
