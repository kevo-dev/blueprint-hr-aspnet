import { useEffect, useMemo, useState } from 'react'
import type { FormEvent, ReactNode } from 'react'
import { api } from './api'
import type { AuditLog, Dashboard, Employee, LeaveBalance, LeaveRequest, LeaveType, Organization, PayrollPeriod, PayrollTransaction, Report, User } from './api'
import './App.css'

type Tab = 'dashboard' | 'employees' | 'organization' | 'payroll' | 'leave' | 'ess' | 'audit' | 'reports'

const formatMoney = (value: number) => `KES ${Number(value || 0).toLocaleString('en-KE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString('en-KE', { year: 'numeric', month: 'short', day: 'numeric' }) : '—'
const canManagePeople = (role: string) => ['Super Admin', 'Company Admin', 'HR Manager'].includes(role)
const canManagePayroll = (role: string) => ['Super Admin', 'Company Admin', 'Payroll Manager'].includes(role)
const canApprove = (role: string) => ['Super Admin', 'Company Admin', 'HR Manager', 'Payroll Manager'].includes(role)
const canViewAudit = (role: string) => ['Super Admin', 'Company Admin', 'HR Manager'].includes(role)

function App() {
  const [user, setUser] = useState<User | null>(null)
  const [booting, setBooting] = useState(true)
  const [activeTab, setActiveTab] = useState<Tab>('dashboard')
  const [notice, setNotice] = useState<{ type: 'success' | 'error'; text: string } | null>(null)
  const [loginEmail, setLoginEmail] = useState('admin@blueprinthr.co.ke')
  const [loginPassword, setLoginPassword] = useState('BluePrint!2026')
  const [loginBusy, setLoginBusy] = useState(false)
  const [dashboard, setDashboard] = useState<Dashboard | null>(null)
  const [employees, setEmployees] = useState<Employee[]>([])
  const [organization, setOrganization] = useState<Organization | null>(null)
  const [periods, setPeriods] = useState<PayrollPeriod[]>([])
  const [selectedPeriodId, setSelectedPeriodId] = useState(0)
  const [transactions, setTransactions] = useState<PayrollTransaction[]>([])
  const [leaveTypes, setLeaveTypes] = useState<LeaveType[]>([])
  const [leaveBalances, setLeaveBalances] = useState<LeaveBalance[]>([])
  const [leaveRequests, setLeaveRequests] = useState<LeaveRequest[]>([])
  const [essProfile, setEssProfile] = useState<Employee | null>(null)
  const [payslips, setPayslips] = useState<PayrollTransaction[]>([])
  const [audit, setAudit] = useState<AuditLog[]>([])
  const [reports, setReports] = useState<Report[]>([])
  const [employeeForm, setEmployeeForm] = useState<Record<string, string>>({ employeeNo: '', firstName: '', lastName: '', kraPin: '', basicSalary: '85000', email: '', phone: '', nssfNo: '', shifNo: '', bankName: '', accountNumber: '' })
  const [branchForm, setBranchForm] = useState<Record<string, string>>({ name: '', code: '', location: '' })
  const [departmentForm, setDepartmentForm] = useState<Record<string, string>>({ name: '', code: '', branchId: '' })
  const [payrollForm, setPayrollForm] = useState<Record<string, string>>({ allowances: '0', otherDeductions: '0' })
  const [leaveForm, setLeaveForm] = useState<Record<string, string>>({ leaveTypeId: '', startDate: '', endDate: '', daysRequested: '1', reason: '' })

  const showNotice = (type: 'success' | 'error', text: string) => {
    setNotice({ type, text })
    window.setTimeout(() => setNotice(null), 4500)
  }

  const loadData = async (activeUser: User) => {
    try {
      const [dash, employeeRows, org, periodRows, types, balances, requests, profile, payslipRows, reportRows] = await Promise.all([
        api.dashboard(), api.employees(), api.organization(), api.periods(), api.leaveTypes(), api.leaveBalances(), api.leaveRequests(), api.essProfile(), api.payslips(), api.reports(),
      ])
      setDashboard(dash)
      setEmployees(employeeRows)
      setOrganization(org)
      setPeriods(periodRows)
      setSelectedPeriodId(previous => previous || periodRows[0]?.id || 0)
      setLeaveTypes(types)
      setLeaveBalances(balances)
      setLeaveRequests(requests)
      setEssProfile(profile)
      setPayslips(payslipRows)
      setReports(reportRows)
      if (periodRows[0]) setTransactions(await api.transactions(periodRows[0].id))
      if (canViewAudit(activeUser.role)) setAudit(await api.audit())
    } catch (error) {
      showNotice('error', error instanceof Error ? error.message : 'Could not load workspace data.')
    }
  }

  useEffect(() => {
    api.me().then(async activeUser => {
      setUser(activeUser)
      await loadData(activeUser)
    }).catch(() => undefined).finally(() => setBooting(false))
  }, [])

  useEffect(() => {
    if (!selectedPeriodId || !user) return
    api.transactions(selectedPeriodId).then(setTransactions).catch(error => showNotice('error', error.message))
  }, [selectedPeriodId, user])

  const navItems = useMemo(() => {
    const base: { id: Tab; label: string; hint: string }[] = [{ id: 'dashboard', label: 'Dashboard', hint: 'Overview' }, { id: 'leave', label: 'Leave', hint: 'Requests and balances' }, { id: 'ess', label: 'ESS portal', hint: 'Your employee records' }, { id: 'reports', label: 'Reports', hint: 'SSRS catalog' }]
    if (!user || !canManagePeople(user.role)) return base
    return [{ id: 'dashboard', label: 'Dashboard', hint: 'Overview' }, { id: 'employees', label: 'Employee master', hint: 'People records' }, { id: 'organization', label: 'Organization', hint: 'Structure and units' }, { id: 'payroll', label: 'Kenyan payroll', hint: 'Statutory processing' }, { id: 'leave', label: 'Leave', hint: 'Requests and balances' }, { id: 'ess', label: 'ESS portal', hint: 'Self-service' }, ...(canViewAudit(user.role) ? [{ id: 'audit' as Tab, label: 'Audit trail', hint: 'Change history' }] : []), { id: 'reports', label: 'Reports', hint: 'SSRS catalog' }]
  }, [user])

  const onLogin = async (event: FormEvent) => {
    event.preventDefault()
    setLoginBusy(true)
    try {
      const activeUser = await api.login({ email: loginEmail, password: loginPassword })
      setUser(activeUser)
      await loadData(activeUser)
      showNotice('success', 'Welcome back to BluePrint HR.')
    } catch (error) {
      showNotice('error', error instanceof Error ? error.message : 'Sign-in failed.')
    } finally {
      setLoginBusy(false)
    }
  }

  const onLogout = async () => {
    await api.logout().catch(() => undefined)
    setUser(null)
    setDashboard(null)
    setActiveTab('dashboard')
  }

  const refresh = async () => { if (user) await loadData(user) }

  const createEmployee = async (event: FormEvent) => {
    event.preventDefault()
    try {
      await api.createEmployee({ ...employeeForm, basicSalary: Number(employeeForm.basicSalary) })
      setEmployeeForm({ employeeNo: '', firstName: '', lastName: '', kraPin: '', basicSalary: '85000', email: '', phone: '', nssfNo: '', shifNo: '', bankName: '', accountNumber: '' })
      await refresh()
      showNotice('success', 'Employee master record created.')
    } catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not create employee.') }
  }

  const createBranch = async (event: FormEvent) => {
    event.preventDefault()
    try { await api.createBranch(branchForm); setBranchForm({ name: '', code: '', location: '' }); await refresh(); showNotice('success', 'Branch added to the organization.') } catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not create branch.') }
  }

  const createDepartment = async (event: FormEvent) => {
    event.preventDefault()
    try { await api.createDepartment({ ...departmentForm, branchId: departmentForm.branchId ? Number(departmentForm.branchId) : null }); setDepartmentForm({ name: '', code: '', branchId: '' }); await refresh(); showNotice('success', 'Department added to the organization.') } catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not create department.') }
  }

  const processPayroll = async () => {
    try { await api.processPayroll({ payrollPeriodId: selectedPeriodId, allowances: Number(payrollForm.allowances), otherDeductions: Number(payrollForm.otherDeductions) }); await refresh(); showNotice('success', 'Payroll processed with PAYE, NSSF, SHIF, and Housing Levy calculations.') } catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not process payroll.') }
  }

  const createLeave = async (event: FormEvent) => {
    event.preventDefault()
    if (!user) return
    try {
      await api.createLeaveRequest({ employeeId: user.employeeId || employees[0]?.id, leaveTypeId: Number(leaveForm.leaveTypeId), startDate: leaveForm.startDate, endDate: leaveForm.endDate, daysRequested: Number(leaveForm.daysRequested), reason: leaveForm.reason })
      setLeaveForm({ leaveTypeId: '', startDate: '', endDate: '', daysRequested: '1', reason: '' })
      await refresh()
      showNotice('success', 'Leave request submitted for approval.')
    } catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not submit leave request.') }
  }

  const updateLeaveStatus = async (id: number, status: string) => {
    try { await api.updateLeaveStatus(id, status); await refresh(); showNotice('success', `Leave request ${status.toLowerCase()}.`) } catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not update leave request.') }
  }

  if (booting) return <div className="splash"><div className="brand-mark">BP</div><p>Loading secure workspace…</p></div>
  if (!user) return <LoginScreen email={loginEmail} password={loginPassword} busy={loginBusy} setEmail={setLoginEmail} setPassword={setLoginPassword} onSubmit={onLogin} notice={notice} />

  const openRequests = leaveRequests.filter(row => row.status === 'Pending').length

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand-lockup"><div className="brand-mark">BP</div><div><strong>BluePrint HR</strong><span>People operations</span></div></div>
        <div className="tenant-chip"><span className="eyebrow">TENANT WORKSPACE</span><strong>{dashboard?.tenant.companyName || 'BluePrint Kenya Ltd'}</strong><span>{dashboard?.tenant.kraPin || 'Kenya compliance ready'}</span></div>
        <nav className="side-nav">{navItems.map(item => <button key={item.id} className={activeTab === item.id ? 'nav-item active' : 'nav-item'} onClick={() => setActiveTab(item.id as Tab)}><span>{item.label}</span><small>{item.hint}</small></button>)}</nav>
        <div className="sidebar-footer"><div className="online-dot" /> <span>API connected</span><span className="version">v1.0</span></div>
      </aside>
      <main className="main-area">
        <header className="topbar"><div><span className="eyebrow">{activeTab === 'dashboard' ? 'OPERATIONS CONTROL CENTER' : navItems.find(item => item.id === activeTab)?.label.toUpperCase()}</span><h1>{activeTab === 'dashboard' ? 'People operations, brought into focus.' : navItems.find(item => item.id === activeTab)?.label}</h1></div><div className="user-menu"><div className="avatar">{user.name.split(' ').map(part => part[0]).slice(0, 2).join('')}</div><div><strong>{user.name}</strong><span>{user.role}</span></div><button className="ghost-button" onClick={onLogout}>Sign out</button></div></header>
        {notice && <div className={`notice ${notice.type}`}>{notice.text}</div>}
        <div className="content">
          {activeTab === 'dashboard' && <DashboardView dashboard={dashboard} employees={employees} transactions={transactions} openRequests={openRequests} onNavigate={setActiveTab} />}
          {activeTab === 'employees' && <EmployeesView employees={employees} form={employeeForm} setForm={setEmployeeForm} onSubmit={createEmployee} canManage={canManagePeople(user.role)} />}
          {activeTab === 'organization' && <OrganizationView organization={organization} branchForm={branchForm} setBranchForm={setBranchForm} departmentForm={departmentForm} setDepartmentForm={setDepartmentForm} createBranch={createBranch} createDepartment={createDepartment} canManage={canManagePeople(user.role)} />}
          {activeTab === 'payroll' && <PayrollView periods={periods} selectedPeriodId={selectedPeriodId} setSelectedPeriodId={setSelectedPeriodId} transactions={transactions} form={payrollForm} setForm={setPayrollForm} onProcess={processPayroll} canManage={canManagePayroll(user.role)} />}
          {activeTab === 'leave' && <LeaveView leaveTypes={leaveTypes} balances={leaveBalances} requests={leaveRequests} form={leaveForm} setForm={setLeaveForm} onCreate={createLeave} onUpdate={updateLeaveStatus} canApprove={canApprove(user.role)} />}
          {activeTab === 'ess' && <EssView profile={essProfile} payslips={payslips} />}
          {activeTab === 'audit' && <AuditView rows={audit} />}
          {activeTab === 'reports' && <ReportsView reports={reports} />}
        </div>
      </main>
    </div>
  )
}

function LoginScreen({ email, password, busy, setEmail, setPassword, onSubmit, notice }: { email: string; password: string; busy: boolean; setEmail: (value: string) => void; setPassword: (value: string) => void; onSubmit: (event: FormEvent) => void; notice: { type: 'success' | 'error'; text: string } | null }) {
  return <div className="login-page"><div className="login-visual"><div className="brand-mark large">BP</div><span className="eyebrow">BLUEPRINT HR</span><h1>People operations, brought into focus.</h1><p>A Kenya-focused HR and payroll foundation for modern teams — structured, compliant, and ready to scale.</p><div className="feature-list"><span>Multi-tenant architecture with strict isolation</span><span>Employee master with Kenya statutory identifiers</span><span>Role-aware workflows and audit visibility</span></div></div><div className="login-card"><span className="eyebrow">SECURE WORKSPACE ACCESS</span><h2>Sign in to your workspace</h2><p className="muted">Use your BluePrint HR account credentials to continue.</p>{notice && <div className={`notice ${notice.type}`}>{notice.text}</div>}<form onSubmit={onSubmit} className="stack-form"><label>Work email<input value={email} onChange={event => setEmail(event.target.value)} type="email" required /></label><label>Password<input value={password} onChange={event => setPassword(event.target.value)} type="password" required minLength={8} /></label><button className="primary-button" disabled={busy}>{busy ? 'Signing in…' : 'Sign in securely'}</button></form><div className="credential-note"><strong>Seeded administrator</strong><span>admin@blueprinthr.co.ke</span><span>BluePrint!2026</span></div><p className="fine-print">Signed server-side sessions · SQL Server-ready HR foundation</p></div></div>
}

function DashboardView({ dashboard, employees, transactions, openRequests, onNavigate }: { dashboard: Dashboard | null; employees: Employee[]; transactions: PayrollTransaction[]; openRequests: number; onNavigate: (tab: Tab) => void }) {
  const gross = dashboard?.monthlyGross || employees.reduce((sum, employee) => sum + employee.basicSalary, 0)
  return <div className="stack-layout"><section className="hero-card"><div><span className="eyebrow">WORKSPACE OVERVIEW</span><h2>Welcome, {dashboard?.user.name || 'team'}</h2><p>Your current role is <strong>{dashboard?.user.role}</strong>. You have access to tenant-scoped HR operations.</p></div><div className="hero-stat"><span>PAYROLL STATUS</span><strong>{dashboard?.payrollStatus || 'Open'}</strong></div></section><section className="metric-grid"><Metric label="Active headcount" value={dashboard?.totalEmployees ?? employees.length} detail="Across this tenant" accent="blue" /><Metric label="Monthly gross" value={formatMoney(gross)} detail="Pre-statutory salary sum" accent="green" /><Metric label="Branches and units" value={dashboard?.branches ?? 0} detail={`${dashboard?.departments ?? 0} departments active`} accent="purple" /><Metric label="Open approvals" value={openRequests} detail="Leave requests awaiting action" accent="amber" /></section><div className="two-column"><Panel title="Recent employee records" eyebrow="EMPLOYEE MASTER" action={<button className="text-button" onClick={() => onNavigate('employees')}>View employee master</button>}><DataTable headers={['Employee number', 'Full name', 'KRA PIN', 'Basic salary', 'Status']} rows={employees.slice(0, 6).map(employee => [employee.employeeNo, employee.fullName, employee.kraPin, formatMoney(employee.basicSalary), <StatusBadge key={employee.id} value={employee.employmentStatus} />])} empty="No employees registered yet." /></Panel><Panel title="Tenant overview" eyebrow="COMPANY CONFIGURATION"><div className="detail-list"><Detail label="Company name" value={dashboard?.tenant.companyName || 'BluePrint Kenya Ltd'} /><Detail label="KRA PIN" value={dashboard?.tenant.kraPin || '—'} mono /><Detail label="Official email" value={dashboard?.tenant.email || '—'} /><Detail label="Phone number" value={dashboard?.tenant.phone || '—'} /><Detail label="Address" value={dashboard?.tenant.address || '—'} /></div></Panel></div><Panel title="Latest payroll snapshot" eyebrow="PAYROLL CONTROL"><DataTable headers={['Employee', 'Gross pay', 'Deductions', 'Net pay', 'Status']} rows={transactions.slice(0, 5).map(row => [row.employeeName, formatMoney(row.grossPay), formatMoney(row.totalDeductions), formatMoney(row.netPay), <StatusBadge key={row.id} value={row.status} />])} empty="Process a payroll period to see transactions." /></Panel></div>
}

function EmployeesView({ employees, form, setForm, onSubmit, canManage }: { employees: Employee[]; form: Record<string, string>; setForm: (value: Record<string, string>) => void; onSubmit: (event: FormEvent) => void; canManage: boolean }) {
  return <div className="stack-layout"><Panel title="Employee master" eyebrow="PEOPLE RECORDS" action={<span className="count-pill">{employees.length} records</span>}><p className="muted">Manage employee profiles, statutory numbers, salary, and bank details within the active tenant.</p>{canManage && <form className="form-grid compact-form" onSubmit={onSubmit}><Field label="Employee number" value={form.employeeNo} onChange={value => setForm({ ...form, employeeNo: value })} required /><Field label="First name" value={form.firstName} onChange={value => setForm({ ...form, firstName: value })} required /><Field label="Last name" value={form.lastName} onChange={value => setForm({ ...form, lastName: value })} required /><Field label="KRA PIN" value={form.kraPin} onChange={value => setForm({ ...form, kraPin: value })} required /><Field label="Basic salary (KES)" value={form.basicSalary} onChange={value => setForm({ ...form, basicSalary: value })} type="number" required /><Field label="Email" value={form.email} onChange={value => setForm({ ...form, email: value })} type="email" /><Field label="Phone" value={form.phone} onChange={value => setForm({ ...form, phone: value })} /><Field label="NSSF number" value={form.nssfNo} onChange={value => setForm({ ...form, nssfNo: value })} /><Field label="SHIF number" value={form.shifNo} onChange={value => setForm({ ...form, shifNo: value })} /><Field label="Bank name" value={form.bankName} onChange={value => setForm({ ...form, bankName: value })} /><Field label="Account number" value={form.accountNumber} onChange={value => setForm({ ...form, accountNumber: value })} /><div className="field-action"><button className="primary-button">Add employee</button></div></form>}</Panel><Panel title="Employee directory" eyebrow="TENANT-SCOPED DATA"><DataTable headers={['Employee number', 'Full name', 'KRA PIN', 'NSSF / SHIF', 'Basic salary', 'Bank', 'Status']} rows={employees.map(employee => [employee.employeeNo, employee.fullName, employee.kraPin, `${employee.nssfNo || '—'} / ${employee.shifNo || '—'}`, formatMoney(employee.basicSalary), employee.bankName || '—', <StatusBadge key={employee.id} value={employee.employmentStatus} />])} empty="No employees found." /></Panel></div>
}

function OrganizationView({ organization, branchForm, setBranchForm, departmentForm, setDepartmentForm, createBranch, createDepartment, canManage }: { organization: Organization | null; branchForm: Record<string, string>; setBranchForm: (value: Record<string, string>) => void; departmentForm: Record<string, string>; setDepartmentForm: (value: Record<string, string>) => void; createBranch: (event: FormEvent) => void; createDepartment: (event: FormEvent) => void; canManage: boolean }) {
  return <div className="stack-layout"><div className="metric-grid"><Metric label="Branches" value={organization?.branches.length || 0} detail="Operating locations" accent="purple" /><Metric label="Departments" value={organization?.departments.length || 0} detail="Reporting units" accent="blue" /><Metric label="Designations" value={organization?.designations.length || 0} detail="Job titles" accent="green" /><Metric label="Employment types" value={organization?.employmentTypes.length || 0} detail="Contract categories" accent="amber" /></div><div className="two-column"><Panel title="Branches" eyebrow="ORGANIZATION SETUP">{canManage && <form className="stack-form inline-form" onSubmit={createBranch}><Field label="Branch name" value={branchForm.name} onChange={value => setBranchForm({ ...branchForm, name: value })} required /><Field label="Code" value={branchForm.code} onChange={value => setBranchForm({ ...branchForm, code: value })} /><Field label="Location" value={branchForm.location} onChange={value => setBranchForm({ ...branchForm, location: value })} /><button className="secondary-button">Add branch</button></form>}<div className="list-stack">{organization?.branches.map(branch => <div className="list-row" key={branch.id}><span><strong>{branch.name}</strong><small>{branch.location || 'Location not set'}</small></span><code>{branch.code || '—'}</code></div>)}</div></Panel><Panel title="Departments" eyebrow="REPORTING UNITS">{canManage && <form className="stack-form inline-form" onSubmit={createDepartment}><Field label="Department name" value={departmentForm.name} onChange={value => setDepartmentForm({ ...departmentForm, name: value })} required /><Field label="Code" value={departmentForm.code} onChange={value => setDepartmentForm({ ...departmentForm, code: value })} /><label>Branch<select value={departmentForm.branchId} onChange={event => setDepartmentForm({ ...departmentForm, branchId: event.target.value })}><option value="">No branch</option>{organization?.branches.map(branch => <option key={branch.id} value={branch.id}>{branch.name}</option>)}</select></label><button className="secondary-button">Add department</button></form>}<div className="list-stack">{organization?.departments.map(department => <div className="list-row" key={department.id}><span><strong>{department.name}</strong><small>{organization.branches.find(branch => branch.id === department.branchId)?.name || 'Unassigned branch'}</small></span><code>{department.code || '—'}</code></div>)}</div></Panel></div></div>
}

function PayrollView({ periods, selectedPeriodId, setSelectedPeriodId, transactions, form, setForm, onProcess, canManage }: { periods: PayrollPeriod[]; selectedPeriodId: number; setSelectedPeriodId: (value: number) => void; transactions: PayrollTransaction[]; form: Record<string, string>; setForm: (value: Record<string, string>) => void; onProcess: () => void; canManage: boolean }) {
  const totals = transactions.reduce((acc, row) => ({ gross: acc.gross + row.grossPay, paye: acc.paye + row.paye, nssf: acc.nssf + row.nssf, shif: acc.shif + row.shif, levy: acc.levy + row.housingLevy, net: acc.net + row.netPay }), { gross: 0, paye: 0, nssf: 0, shif: 0, levy: 0, net: 0 })
  return <div className="stack-layout"><Panel title="Kenyan payroll engine" eyebrow="STATUTORY PROCESSING" action={canManage && <button className="primary-button" onClick={onProcess} disabled={!selectedPeriodId}>Process selected period</button>}><div className="toolbar"><label>Payroll period<select value={selectedPeriodId} onChange={event => setSelectedPeriodId(Number(event.target.value))}>{periods.map(period => <option key={period.id} value={period.id}>{period.name} · {period.status}</option>)}</select></label><Field label="Allowances (KES)" value={form.allowances} onChange={value => setForm({ ...form, allowances: value })} type="number" /><Field label="Other deductions (KES)" value={form.otherDeductions} onChange={value => setForm({ ...form, otherDeductions: value })} type="number" /></div><div className="compliance-strip"><strong>Calculation coverage</strong><span>PAYE</span><span>NSSF</span><span>SHIF</span><span>Housing Levy</span></div></Panel><div className="metric-grid"><Metric label="Gross pay" value={formatMoney(totals.gross)} detail={`${transactions.length} transactions`} accent="blue" /><Metric label="PAYE" value={formatMoney(totals.paye)} detail="After personal relief" accent="amber" /><Metric label="Statutory deductions" value={formatMoney(totals.nssf + totals.shif + totals.levy)} detail="NSSF · SHIF · Housing" accent="purple" /><Metric label="Net pay" value={formatMoney(totals.net)} detail="Employee take-home" accent="green" /></div><Panel title="Payroll transactions" eyebrow="PERIOD DETAIL"><DataTable headers={['Employee', 'Gross', 'PAYE', 'NSSF', 'SHIF', 'Housing levy', 'Net pay', 'Status']} rows={transactions.map(row => [row.employeeName, formatMoney(row.grossPay), formatMoney(row.paye), formatMoney(row.nssf), formatMoney(row.shif), formatMoney(row.housingLevy), formatMoney(row.netPay), <StatusBadge key={row.id} value={row.status} />])} empty="No transactions for this period. Process payroll to generate statutory calculations." /></Panel></div>
}

function LeaveView({ leaveTypes, balances, requests, form, setForm, onCreate, onUpdate, canApprove }: { leaveTypes: LeaveType[]; balances: LeaveBalance[]; requests: LeaveRequest[]; form: Record<string, string>; setForm: (value: Record<string, string>) => void; onCreate: (event: FormEvent) => void; onUpdate: (id: number, status: string) => void; canApprove: boolean }) {
  return <div className="stack-layout"><div className="two-column"><Panel title="Apply for leave" eyebrow="EMPLOYEE SELF-SERVICE"><form className="stack-form" onSubmit={onCreate}><label>Leave type<select required value={form.leaveTypeId} onChange={event => setForm({ ...form, leaveTypeId: event.target.value })}><option value="">Choose a leave type</option>{leaveTypes.map(type => <option key={type.id} value={type.id}>{type.name} · {type.defaultDays} days</option>)}</select></label><div className="form-grid two"><Field label="Start date" value={form.startDate} onChange={value => setForm({ ...form, startDate: value })} type="date" required /><Field label="End date" value={form.endDate} onChange={value => setForm({ ...form, endDate: value })} type="date" required /></div><Field label="Days requested" value={form.daysRequested} onChange={value => setForm({ ...form, daysRequested: value })} type="number" required /><label>Reason<textarea value={form.reason} onChange={event => setForm({ ...form, reason: event.target.value })} placeholder="Family vacation, medical rest…" /></label><button className="primary-button">Submit request</button></form></Panel><Panel title="My leave balances" eyebrow="CURRENT YEAR"><div className="balance-grid">{balances.map(balance => <div className="balance-card" key={balance.id}><span>{balance.leaveType}</span><strong>{balance.availableDays}</strong><small>of {balance.allocatedDays} days available</small><div className="progress"><span style={{ width: `${Math.min(100, balance.usedDays / Math.max(balance.allocatedDays, 1) * 100)}%` }} /></div></div>)}{balances.length === 0 && <EmptyState text="No leave balances have been configured." />}</div></Panel></div><Panel title="Leave requests and approval queue" eyebrow="WORKFLOW"><DataTable headers={['Employee', 'Leave type', 'Dates', 'Days', 'Reason', 'Status', 'Action']} rows={requests.map(request => [request.employeeName || 'Employee', request.leaveType, `${formatDate(request.startDate)} – ${formatDate(request.endDate)}`, request.daysRequested, request.reason || '—', <StatusBadge key={request.id} value={request.status} />, canApprove && request.status === 'Pending' ? <span className="action-row" key={`action-${request.id}`}><button className="small-button approve" onClick={() => onUpdate(request.id, 'Approved')}>Approve</button><button className="small-button reject" onClick={() => onUpdate(request.id, 'Rejected')}>Reject</button></span> : '—'])} empty="No leave requests yet." /></Panel></div>
}

function EssView({ profile, payslips }: { profile: Employee | null; payslips: PayrollTransaction[] }) {
  return <div className="stack-layout"><Panel title="Employee self-service" eyebrow="MY PROFILE"><div className="profile-grid">{profile ? <><Detail label="Full name" value={profile.fullName} /><Detail label="Employee number" value={profile.employeeNo} mono /><Detail label="Email" value={profile.email || '—'} /><Detail label="KRA PIN" value={profile.kraPin} mono /><Detail label="NSSF / SHIF" value={`${profile.nssfNo || '—'} / ${profile.shifNo || '—'}`} mono /><Detail label="Basic salary" value={formatMoney(profile.basicSalary)} /></> : <EmptyState text="This user has no linked employee self-service profile." />}</div></Panel><Panel title="Payslips and payroll history" eyebrow="PAYROLL HISTORY"><DataTable headers={['Payroll employee', 'Gross pay', 'PAYE', 'NSSF', 'SHIF', 'Net pay', 'Status']} rows={payslips.map(row => [row.employeeName, formatMoney(row.grossPay), formatMoney(row.paye), formatMoney(row.nssf), formatMoney(row.shif), formatMoney(row.netPay), <StatusBadge key={row.id} value={row.status} />])} empty="No payslip transactions available." /></Panel></div>
}

function AuditView({ rows }: { rows: AuditLog[] }) {
  return <div className="stack-layout"><Panel title="Audit trail" eyebrow="CONTROL AND GOVERNANCE"><p className="muted">Recent tenant-scoped create, update, and payroll actions captured by the ASP.NET Core API.</p><DataTable headers={['Time', 'Action', 'Entity', 'User', 'Details']} rows={rows.map(row => [formatDate(row.createdAt), <StatusBadge key={row.id} value={row.action} />, `${row.entityType}${row.entityId ? ` #${row.entityId}` : ''}`, row.userName || 'System', row.details || '—'])} empty="No audit events recorded yet." /></Panel></div>
}

function ReportsView({ reports }: { reports: Report[] }) {
  return <div className="stack-layout"><section className="hero-card report-hero"><div><span className="eyebrow">SSRS REPORTING</span><h2>Operational reporting with a governed catalog.</h2><p>Reports are rendered by your SSRS server. The API publishes tenant-safe metadata without exposing report credentials in the browser.</p></div><div className="hero-stat"><span>REPORTS AVAILABLE</span><strong>{reports.length}</strong></div></section><div className="report-grid">{reports.map(report => <article className="report-card" key={report.id}><div className="report-icon">RDL</div><span className="eyebrow">SSRS DEFINITION</span><h3>{report.name}</h3><p>{report.description}</p><code>{report.reportPath}</code>{report.launchUrl ? <a className="secondary-button" href={report.launchUrl} target="_blank" rel="noreferrer">Open in SSRS</a> : <span className="muted">Configure Ssrs:ReportServerUrl to enable launch.</span>}</article>)}{reports.length === 0 && <EmptyState text="No reports have been configured." />}</div></div>
}

function Metric({ label, value, detail, accent }: { label: string; value: string | number; detail: string; accent: string }) { return <div className={`metric-card ${accent}`}><span>{label}</span><strong>{value}</strong><small>{detail}</small></div> }
function Panel({ title, eyebrow, action, children }: { title: string; eyebrow: string; action?: ReactNode; children: ReactNode }) { return <section className="panel"><div className="panel-heading"><div><span className="eyebrow">{eyebrow}</span><h2>{title}</h2></div>{action}</div>{children}</section> }
function Field({ label, value, onChange, type = 'text', required = false }: { label: string; value: string; onChange: (value: string) => void; type?: string; required?: boolean }) { return <label>{label}<input value={value} type={type} required={required} onChange={event => onChange(event.target.value)} /></label> }
function Detail({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) { return <div className="detail"><span>{label}</span><strong className={mono ? 'mono' : ''}>{value}</strong></div> }
function StatusBadge({ value }: { value: string }) { return <span className={`status ${value.toLowerCase().replaceAll(' ', '-')}`}>{value}</span> }
function EmptyState({ text }: { text: string }) { return <div className="empty-state">{text}</div> }
function DataTable({ headers, rows, empty }: { headers: string[]; rows: (string | number | ReactNode)[][]; empty: string }) { return <div className="table-wrap"><table><thead><tr>{headers.map(header => <th key={header}>{header}</th>)}</tr></thead><tbody>{rows.length ? rows.map((row, index) => <tr key={index}>{row.map((cell, cellIndex) => <td key={cellIndex}>{cell}</td>)}</tr>) : <tr><td className="empty-cell" colSpan={headers.length}>{empty}</td></tr>}</tbody></table></div> }

export default App
