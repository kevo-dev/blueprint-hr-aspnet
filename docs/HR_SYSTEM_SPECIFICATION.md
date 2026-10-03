# BluePrint HR — HR & Leave Management System Specification

## Architecture
- Frontend: React + TypeScript + Vite (existing production baseline)
- API: ASP.NET Core 8 Web API
- Database target: PostgreSQL/Supabase
- Deployment target: Vercel for frontend; API on a container-capable host
- Repository: kevo-dev/blueprint-hr-aspnet

## Roles
- Super Admin: platform-wide administration
- Company Admin: tenant administration
- HR Manager: employees, organization, leave approvals, audit
- Payroll Manager: payroll and leave approvals
- Employee: own profile, leave applications, balances, payslips

## Modules
1. Authentication and session management
2. Employee master
3. Organization: branches, departments, designations, employment types
4. Leave types, balances, applications and approvals
5. Payroll periods and statutory calculations
6. Employee self-service
7. Notifications
8. Audit trail
9. Reports

## Leave workflow
Employee -> submit request -> validation against balance/date rules -> Pending -> authorized approver -> Approved or Rejected -> balance updated only on approval -> audit event -> notification.

## Core rules
- A request must have a valid leave type, employee and date range.
- End date cannot precede start date.
- Requested days must be positive.
- Approved requests must not exceed available balance unless an explicit policy permits negative balances.
- Used balance changes only when a request transitions to Approved.
- Rejecting/cancelling a pending request does not consume balance.
- Tenant boundaries apply to every HR record.

## Core entities
Tenant, Branch, Department, Designation, EmploymentType, Employee, User, LeaveType, LeaveBalance, LeaveRequest, PayrollPeriod, PayrollTransaction, AuditLog, ReportDefinition.

## API surface
Authentication: /api/auth/login, /api/auth/me, /api/auth/logout
Dashboard: /api/dashboard
Employees: /api/employees
Organization: /api/organization, /api/organization/branches, /api/organization/departments
Leave: /api/leave/types, /api/leave/balances, /api/leave/requests, /api/leave/requests/{id}/status
Payroll: /api/payroll/periods, /api/payroll/transactions, /api/payroll/process
ESS: /api/ess/profile, /api/ess/payslips
Governance: /api/audit
Reports: /api/reports

## Security
- HttpOnly authentication cookie
- Role-based authorization
- Strict tenant scoping
- Never expose secrets or database credentials to the frontend
- Audit privileged mutations
- PostgreSQL tables in exposed schemas must use RLS with actual tenant/ownership predicates
- Service credentials remain server-side

## Delivery phases
Phase 1: schema and database connectivity
Phase 2: API persistence and tenant isolation
Phase 3: authentication/RBAC
Phase 4: employee and organization management
Phase 5: leave engine and approval workflow
Phase 6: payroll and statutory rules
Phase 7: ESS, notifications and reports
Phase 8: tests, security review and deployment
