import { useEffect, useMemo, useState } from 'react'
import type { FormEvent, ReactNode } from 'react'
import { api } from './api'
import type { Attendance, AttendanceSummary, AuditLog, Dashboard, Employee, LeaveBalance, LeaveRequest, LeaveType, Organization, PayrollPeriod, PayrollTransaction, Report, ReportData, User, Notification } from './api'
import './App.css'

type Tab = 'dashboard' | 'employees' | 'organization' | 'payroll' | 'leave' | 'ess' | 'attendance' | 'audit' | 'reports'

const formatMoney = (value: number) => `KES ${Number(value || 0).toLocaleString('en-KE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`
const formatDate = (value?: string) => value ? new Date(value).toLocaleDateString('en-KE', { year: 'numeric', month: 'short', day: 'numeric' }) : '—'
const roleKey = (role: string) => role.replace(/\s+/g, '').toLowerCase()
const canManagePeople = (role: string) => ['superadmin', 'companyadmin', 'hrmanager'].includes(roleKey(role))
const canManagePayroll = (role: string) => ['superadmin', 'companyadmin', 'payrollmanager'].includes(roleKey(role))
const canApprove = (role: string) => ['superadmin', 'companyadmin', 'hrmanager', 'payrollmanager'].includes(roleKey(role))
const canViewAudit = (role: string) => ['superadmin', 'companyadmin', 'hrmanager'].includes(roleKey(role))
const canViewReports = (role: string) => ['superadmin', 'companyadmin', 'hrmanager', 'payrollmanager'].includes(roleKey(role))

function App() {
  const [user, setUser] = useState<User | null>(null)
  const [booting, setBooting] = useState(true)
  const [activeTab, setActiveTab] = useState<Tab>('dashboard')
  const [notice, setNotice] = useState<{ type: 'success' | 'error'; text: string } | null>(null)
  const [loginEmail, setLoginEmail] = useState('')
  const [loginPassword, setLoginPassword] = useState('')
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
  const [attendance, setAttendance] = useState<Attendance[]>([])
  const [attendanceSummary, setAttendanceSummary] = useState<AttendanceSummary | null>(null)
  const [notifications, setNotifications] = useState<Notification[]>([])
  const [showNotifications, setShowNotifications] = useState(false)
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
    const failures: string[] = []
    const load = async <T,>(label: string, task: () => Promise<T>, apply: (value: T) => void) => {
      try {
        apply(await task())
      } catch (error) {
        failures.push(label + ': ' + (error instanceof Error ? error.message : 'request failed'))
      }
    }

    await Promise.all([
      load('Dashboard', api.dashboard, setDashboard),
      load('Employees', api.employees, setEmployees),
      load('Organization', api.organization, setOrganization),
      load('Payroll periods', api.periods, rows => {
        setPeriods(rows)
        setSelectedPeriodId(previous => previous || rows[0]?.id || 0)
      }),
      load('Leave types', api.leaveTypes, setLeaveTypes),
      load('Leave balances', api.leaveBalances, setLeaveBalances),
      load('Leave requests', api.leaveRequests, setLeaveRequests),
      load('ESS profile', api.essProfile, setEssProfile),
      load('Payslips', api.payslips, setPayslips),
      load('Reports', api.reports, setReports),
      load('Attendance', api.attendance, setAttendance),
      load('Attendance summary', api.attendanceSummary, setAttendanceSummary),
      load('Notifications', api.notifications, setNotifications),
      ...(canViewAudit(activeUser.role) ? [load('Audit', api.audit, setAudit)] : []),
    ])

    try {
      const currentPeriods = await api.periods()
      const periodId = selectedPeriodId || currentPeriods[0]?.id || 0
      if (periodId) setTransactions(await api.transactions(periodId))
      else setTransactions([])
    } catch (error) {
      failures.push('Payroll transactions: ' + (error instanceof Error ? error.message : 'request failed'))
    }

    if (failures.length) {
      showNotice('error', 'Some workspace data could not be loaded. ' + failures.slice(0, 3).join(' · '))
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
    const base: { id: Tab; label: string; hint: string }[] = [{ id: 'dashboard', label: 'Dashboard', hint: 'Overview' }, { id: 'leave', label: 'Leave', hint: 'Requests and balances' }, { id: 'ess', label: 'ESS portal', hint: 'Your employee records' }]
    if (!user || !canManagePeople(user.role)) return base
    return [{ id: 'dashboard', label: 'Dashboard', hint: 'Overview' }, { id: 'employees', label: 'Employee master', hint: 'People records' }, { id: 'organization', label: 'Organization', hint: 'Structure and units' }, { id: 'payroll', label: 'Kenyan payroll', hint: 'Statutory processing' }, { id: 'leave', label: 'Leave', hint: 'Requests and balances' }, { id: 'ess', label: 'ESS portal', hint: 'Self-service' }, { id: 'attendance', label: 'Attendance', hint: 'Time and attendance' }, ...(canViewAudit(user.role) ? [{ id: 'audit' as Tab, label: 'Audit trail', hint: 'Change history' }] : []), ...(canViewReports(user.role) ? [{ id: 'reports' as Tab, label: 'Reports', hint: 'Live HR reporting' }] : [])]
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
    try { await api.processPayroll({ payrollPeriodId: selectedPeriodId, allowances: Number(payrollForm.allowances), otherDeductions: Number(payrollForm.otherDeductions) }); await refresh(); showNotice('success', 'Payroll processed with employee components and Kenyan statutory calculations.') } catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not process payroll.') }
  }
  const lockPayroll = async () => {
    try { await api.lockPayrollPeriod(selectedPeriodId); await refresh(); showNotice('success', 'Payroll period locked. Further processing is blocked.') } catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not lock payroll period.') }
  }
  const downloadPayslip = async (id: number) => {
    try {
      const blob = await api.downloadPayslip(id)
      const url = URL.createObjectURL(blob); const link = document.createElement('a'); link.href = url; link.download = `payslip-${id}.pdf`; link.click(); URL.revokeObjectURL(url)
    } catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not download payslip.') }
  }
  const markNotificationRead = async (id: number) => { try { await api.markNotificationRead(id); setNotifications(rows => rows.map(x => x.id === id ? { ...x, isRead: true } : x)) } catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not update notification.') } }
  const markAllNotificationsRead = async () => { try { await api.markAllNotificationsRead(); setNotifications(rows => rows.map(x => ({ ...x, isRead: true }))) } catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not update notifications.') } }

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
        <header className="topbar"><div><span className="eyebrow">{activeTab === 'dashboard' ? 'OPERATIONS CONTROL CENTER' : navItems.find(item => item.id === activeTab)?.label.toUpperCase()}</span><h1>{activeTab === 'dashboard' ? 'People operations, brought into focus.' : navItems.find(item => item.id === activeTab)?.label}</h1></div><div className="user-menu"><button className="ghost-button" onClick={() => setShowNotifications(value => !value)}>Notifications {notifications.filter(x => !x.isRead).length ? `(${notifications.filter(x => !x.isRead).length})` : ''}</button>{showNotifications && <div className="notification-popover"><div className="panel-heading"><strong>Notifications</strong><button className="text-button" onClick={markAllNotificationsRead}>Mark all read</button></div>{notifications.length === 0 ? <EmptyState text="No notifications." /> : notifications.slice(0,8).map(item => <button className={item.isRead ? 'notification-item read' : 'notification-item'} key={item.id} onClick={() => markNotificationRead(item.id)}><strong>{item.title}</strong><span>{item.message}</span><small>{formatDate(item.createdAt)}</small></button>)}</div>}<div className="avatar">{user.name.split(' ').map(part => part[0]).slice(0, 2).join('')}</div><div><strong>{user.name}</strong><span>{user.role}</span></div><button className="ghost-button" onClick={onLogout}>Sign out</button></div></header>
        {notice && <div className={`notice ${notice.type}`}>{notice.text}</div>}
        <div className="content">
          {activeTab === 'dashboard' && <DashboardView dashboard={dashboard} employees={employees} transactions={transactions} openRequests={openRequests} onNavigate={setActiveTab} />}
          {activeTab === 'employees' && <EmployeesView employees={employees} organization={organization} form={employeeForm} setForm={setEmployeeForm} onSubmit={createEmployee} canManage={canManagePeople(user.role)} onRefresh={refresh} showNotice={showNotice} />}
          {activeTab === 'organization' && <OrganizationView organization={organization} branchForm={branchForm} setBranchForm={setBranchForm} departmentForm={departmentForm} setDepartmentForm={setDepartmentForm} createBranch={createBranch} createDepartment={createDepartment} canManage={canManagePeople(user.role)} onRefresh={refresh} showNotice={showNotice} />}
          {activeTab === 'payroll' && <PayrollView periods={periods} selectedPeriodId={selectedPeriodId} setSelectedPeriodId={setSelectedPeriodId} transactions={transactions} employees={employees} form={payrollForm} setForm={setPayrollForm} onProcess={processPayroll} onLock={lockPayroll} canManage={canManagePayroll(user.role)} showNotice={showNotice} onDownloadPayslip={downloadPayslip} />}
          {activeTab === 'leave' && <LeaveView leaveTypes={leaveTypes} balances={leaveBalances} requests={leaveRequests} form={leaveForm} setForm={setLeaveForm} onCreate={createLeave} onUpdate={updateLeaveStatus} canApprove={canApprove(user.role)} canManage={canManagePeople(user.role)} onRefresh={refresh} showNotice={showNotice} />}
          {activeTab === 'ess' && <EssView profile={essProfile} payslips={payslips} onDownloadPayslip={downloadPayslip} />}
          {activeTab === 'attendance' && <AttendanceView employees={employees} rows={attendance} summary={attendanceSummary} canManage={canManagePeople(user.role)} onRefresh={refresh} showNotice={showNotice} />}
          {activeTab === 'audit' && <AuditView rows={audit} />}
          {activeTab === 'reports' && <ReportsView reports={reports} />}
        </div>
      </main>
    </div>
  )
}

function LoginScreen({ email, password, busy, setEmail, setPassword, onSubmit, notice }: { email: string; password: string; busy: boolean; setEmail: (value: string) => void; setPassword: (value: string) => void; onSubmit: (event: FormEvent) => void; notice: { type: 'success' | 'error'; text: string } | null }) {
  return <div className="login-page"><div className="login-visual"><div className="brand-mark large">BP</div><span className="eyebrow">BLUEPRINT HR</span><h1>People operations, brought into focus.</h1><p>A Kenya-focused HR and payroll foundation for modern teams — structured, compliant, and ready to scale.</p><div className="feature-list"><span>Multi-tenant architecture with strict isolation</span><span>Employee master with Kenya statutory identifiers</span><span>Role-aware workflows and audit visibility</span></div></div><div className="login-card"><span className="eyebrow">SECURE WORKSPACE ACCESS</span><h2>Sign in to your workspace</h2><p className="muted">Use your BluePrint HR account credentials to continue.</p>{notice && <div className={`notice ${notice.type}`}>{notice.text}</div>}<form onSubmit={onSubmit} className="stack-form"><label>Work email<input value={email} onChange={event => setEmail(event.target.value)} type="email" required /></label><label>Password<input value={password} onChange={event => setPassword(event.target.value)} type="password" required minLength={8} /></label><button className="primary-button" disabled={busy}>{busy ? 'Signing in…' : 'Sign in securely'}</button></form><p className="fine-print">Signed server-side sessions · SQL Server-ready HR foundation</p></div></div>
}

function DashboardView({ dashboard, employees, transactions, openRequests, onNavigate }: { dashboard: Dashboard | null; employees: Employee[]; transactions: PayrollTransaction[]; openRequests: number; onNavigate: (tab: Tab) => void }) {
  const gross = dashboard?.monthlyGross || employees.reduce((sum, employee) => sum + employee.basicSalary, 0)
  return <div className="stack-layout"><section className="hero-card"><div><span className="eyebrow">WORKSPACE OVERVIEW</span><h2>Welcome, {dashboard?.user.name || 'team'}</h2><p>Your current role is <strong>{dashboard?.user.role}</strong>. You have access to tenant-scoped HR operations.</p></div><div className="hero-stat"><span>PAYROLL STATUS</span><strong>{dashboard?.payrollStatus || 'Open'}</strong></div></section><section className="metric-grid"><Metric label="Active headcount" value={dashboard?.totalEmployees ?? employees.length} detail="Across this tenant" accent="blue" /><Metric label="Monthly gross" value={formatMoney(gross)} detail="Pre-statutory salary sum" accent="green" /><Metric label="Branches and units" value={dashboard?.branches ?? 0} detail={`${dashboard?.departments ?? 0} departments active`} accent="purple" /><Metric label="Open approvals" value={openRequests} detail="Leave requests awaiting action" accent="amber" /></section><div className="two-column"><Panel title="Recent employee records" eyebrow="EMPLOYEE MASTER" action={<button className="text-button" onClick={() => onNavigate('employees')}>View employee master</button>}><DataTable headers={['Employee number', 'Full name', 'KRA PIN', 'Basic salary', 'Status']} rows={employees.slice(0, 6).map(employee => [employee.employeeNo, employee.fullName, employee.kraPin, formatMoney(employee.basicSalary), <StatusBadge key={employee.id} value={employee.employmentStatus} />])} empty="No employees registered yet." /></Panel><Panel title="Tenant overview" eyebrow="COMPANY CONFIGURATION"><div className="detail-list"><Detail label="Company name" value={dashboard?.tenant.companyName || 'BluePrint Kenya Ltd'} /><Detail label="KRA PIN" value={dashboard?.tenant.kraPin || '—'} mono /><Detail label="Official email" value={dashboard?.tenant.email || '—'} /><Detail label="Phone number" value={dashboard?.tenant.phone || '—'} /><Detail label="Address" value={dashboard?.tenant.address || '—'} /></div></Panel></div><Panel title="Latest payroll snapshot" eyebrow="PAYROLL CONTROL"><DataTable headers={['Employee', 'Gross pay', 'Deductions', 'Net pay', 'Status']} rows={transactions.slice(0, 5).map(row => [row.employeeName, formatMoney(row.grossPay), formatMoney(row.totalDeductions), formatMoney(row.netPay), <StatusBadge key={row.id} value={row.status} />])} empty="Process a payroll period to see transactions." /></Panel></div>
}

function EmployeesView({ employees, organization, form, setForm, onSubmit, canManage, onRefresh, showNotice }: { employees: Employee[]; organization: Organization | null; form: Record<string, string>; setForm: (value: Record<string, string>) => void; onSubmit: (event: FormEvent) => void; canManage: boolean; onRefresh: () => Promise<void>; showNotice: (type: 'success' | 'error', text: string) => void }) {
  const [editing, setEditing] = useState<Employee | null>(null)
  const [edit, setEdit] = useState<Record<string, string>>({})

  const beginEdit = (employee: Employee) => {
    setEditing(employee)
    setEdit({
      employeeNo: employee.employeeNo, firstName: employee.firstName || employee.fullName.split(' ')[0] || '', middleName: employee.middleName || '',
      lastName: employee.lastName || '', payrollNo: employee.payrollNo || '', kraPin: employee.kraPin || '', basicSalary: String(employee.basicSalary || 0),
      email: employee.email || '', phone: employee.phone || '', nssfNo: employee.nssfNo || '', shifNo: employee.shifNo || '',
      branchId: employee.branchId ? String(employee.branchId) : '', departmentId: employee.departmentId ? String(employee.departmentId) : '',
      designationId: employee.designationId ? String(employee.designationId) : '', gradeId: employee.gradeId ? String(employee.gradeId) : '',
      employmentTypeId: employee.employmentTypeId ? String(employee.employmentTypeId) : '', employmentDate: employee.employmentDate?.slice(0, 10) || '',
      terminationDate: employee.terminationDate?.slice(0, 10) || '', employmentStatus: employee.employmentStatus || 'Active',
      bankName: employee.bankName || '', bankBranch: employee.bankBranch || '', accountNumber: employee.accountNumber || ''
    })
  }

  const saveEdit = async (event: FormEvent) => {
    event.preventDefault()
    if (!editing) return
    try {
      await api.updateEmployee(editing.id, {
        ...edit, basicSalary: Number(edit.basicSalary), branchId: edit.branchId ? Number(edit.branchId) : null,
        departmentId: edit.departmentId ? Number(edit.departmentId) : null, designationId: edit.designationId ? Number(edit.designationId) : null,
        gradeId: edit.gradeId ? Number(edit.gradeId) : null, employmentTypeId: edit.employmentTypeId ? Number(edit.employmentTypeId) : null,
        employmentDate: edit.employmentDate || null, terminationDate: edit.terminationDate || null
      })
      setEditing(null); await onRefresh(); showNotice('success', 'Employee record updated.')
    } catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not update employee.') }
  }

  const deactivate = async (employee: Employee) => {
    if (!window.confirm(`Deactivate ${employee.fullName}?`)) return
    try { await api.deactivateEmployee(employee.id); await onRefresh(); showNotice('success', 'Employee deactivated.') }
    catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not deactivate employee.') }
  }

  const setEditField = (key: string, value: string) => setEdit(previous => ({ ...previous, [key]: value }))

  return <div className="stack-layout">
    <Panel title="Employee master" eyebrow="PEOPLE RECORDS" action={<span className="count-pill">{employees.length} records</span>}>
      <p className="muted">Create, edit, and deactivate employee profiles. All mutations are tenant-scoped and audited.</p>
      {canManage && <form className="form-grid compact-form" onSubmit={onSubmit}>
        <Field label="Employee number" value={form.employeeNo} onChange={value => setForm({ ...form, employeeNo: value })} required />
        <Field label="First name" value={form.firstName} onChange={value => setForm({ ...form, firstName: value })} required />
        <Field label="Last name" value={form.lastName} onChange={value => setForm({ ...form, lastName: value })} required />
        <Field label="KRA PIN" value={form.kraPin} onChange={value => setForm({ ...form, kraPin: value })} required />
        <Field label="Basic salary (KES)" value={form.basicSalary} onChange={value => setForm({ ...form, basicSalary: value })} type="number" required />
        <Field label="Email" value={form.email} onChange={value => setForm({ ...form, email: value })} type="email" />
        <Field label="Phone" value={form.phone} onChange={value => setForm({ ...form, phone: value })} />
        <Field label="NSSF number" value={form.nssfNo} onChange={value => setForm({ ...form, nssfNo: value })} />
        <Field label="SHIF number" value={form.shifNo} onChange={value => setForm({ ...form, shifNo: value })} />
        <Field label="Bank name" value={form.bankName} onChange={value => setForm({ ...form, bankName: value })} />
        <Field label="Account number" value={form.accountNumber} onChange={value => setForm({ ...form, accountNumber: value })} />
        <div className="field-action"><button className="primary-button">Add employee</button></div>
      </form>}
    </Panel>
    {editing && <Panel title={`Edit employee · ${editing.employeeNo}`} eyebrow="EMPLOYEE PROFILE">
      <form className="form-grid" onSubmit={saveEdit}>
        <Field label="Employee number" value={edit.employeeNo || ''} onChange={v => setEditField('employeeNo', v)} required />
        <Field label="Payroll number" value={edit.payrollNo || ''} onChange={v => setEditField('payrollNo', v)} />
        <Field label="First name" value={edit.firstName || ''} onChange={v => setEditField('firstName', v)} required />
        <Field label="Middle name" value={edit.middleName || ''} onChange={v => setEditField('middleName', v)} />
        <Field label="Last name" value={edit.lastName || ''} onChange={v => setEditField('lastName', v)} required />
        <Field label="KRA PIN" value={edit.kraPin || ''} onChange={v => setEditField('kraPin', v)} required />
        <Field label="Basic salary" value={edit.basicSalary || ''} onChange={v => setEditField('basicSalary', v)} type="number" required />
        <Field label="Email" value={edit.email || ''} onChange={v => setEditField('email', v)} type="email" />
        <Field label="Phone" value={edit.phone || ''} onChange={v => setEditField('phone', v)} />
        <Field label="NSSF" value={edit.nssfNo || ''} onChange={v => setEditField('nssfNo', v)} />
        <Field label="SHIF" value={edit.shifNo || ''} onChange={v => setEditField('shifNo', v)} />
        <label>Branch<select value={edit.branchId || ''} onChange={e => setEditField('branchId', e.target.value)}><option value="">Unassigned</option>{organization?.branches.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
        <label>Department<select value={edit.departmentId || ''} onChange={e => setEditField('departmentId', e.target.value)}><option value="">Unassigned</option>{organization?.departments.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
        <label>Designation<select value={edit.designationId || ''} onChange={e => setEditField('designationId', e.target.value)}><option value="">Unassigned</option>{organization?.designations.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
        <label>Grade<select value={edit.gradeId || ''} onChange={e => setEditField('gradeId', e.target.value)}><option value="">Unassigned</option>{organization?.grades.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
        <label>Employment type<select value={edit.employmentTypeId || ''} onChange={e => setEditField('employmentTypeId', e.target.value)}><option value="">Unassigned</option>{organization?.employmentTypes.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
        <Field label="Employment date" value={edit.employmentDate || ''} onChange={v => setEditField('employmentDate', v)} type="date" />
        <Field label="Termination date" value={edit.terminationDate || ''} onChange={v => setEditField('terminationDate', v)} type="date" />
        <label>Employment status<select value={edit.employmentStatus || 'Active'} onChange={e => setEditField('employmentStatus', e.target.value)}><option>Active</option><option>Inactive</option><option>Terminated</option><option>On Leave</option></select></label>
        <Field label="Bank name" value={edit.bankName || ''} onChange={v => setEditField('bankName', v)} />
        <Field label="Bank branch" value={edit.bankBranch || ''} onChange={v => setEditField('bankBranch', v)} />
        <Field label="Account number" value={edit.accountNumber || ''} onChange={v => setEditField('accountNumber', v)} />
        <div className="action-row"><button className="primary-button">Save changes</button><button type="button" className="ghost-button" onClick={() => setEditing(null)}>Cancel</button></div>
      </form>
    </Panel>}
    <Panel title="Employee directory" eyebrow="TENANT-SCOPED DATA"><DataTable headers={['Employee number', 'Full name', 'KRA PIN', 'Department', 'Basic salary', 'Status', 'Actions']} rows={employees.map(employee => [employee.employeeNo, employee.fullName, employee.kraPin, organization?.departments.find(x => x.id === employee.departmentId)?.name || '—', formatMoney(employee.basicSalary), <StatusBadge key={employee.id} value={employee.employmentStatus} />, canManage ? <span className="action-row" key={`actions-${employee.id}`}><button className="small-button" onClick={() => beginEdit(employee)}>Edit</button>{employee.employmentStatus === 'Active' && <button className="small-button reject" onClick={() => deactivate(employee)}>Deactivate</button>}</span> : '—'])} empty="No employees found." /></Panel>
  </div>
}

function OrganizationView({ organization, branchForm, setBranchForm, departmentForm, setDepartmentForm, createBranch, createDepartment, canManage, onRefresh, showNotice }: { organization: Organization | null; branchForm: Record<string, string>; setBranchForm: (value: Record<string, string>) => void; departmentForm: Record<string, string>; setDepartmentForm: (value: Record<string, string>) => void; createBranch: (event: FormEvent) => void; createDepartment: (event: FormEvent) => void; canManage: boolean; onRefresh: () => Promise<void>; showNotice: (type: 'success' | 'error', text: string) => void }) {
  const [editingBranch, setEditingBranch] = useState<number | null>(null)
  const [editingDepartment, setEditingDepartment] = useState<number | null>(null)
  const [designationForm, setDesignationForm] = useState({ id: 0, name: '' })
  const [gradeForm, setGradeForm] = useState({ id: 0, name: '', level: '', minSalary: '0', maxSalary: '0' })
  const [employmentForm, setEmploymentForm] = useState({ id: 0, name: '', description: '' })

  const editBranch = organization?.branches.find(x => x.id === editingBranch)
  const editDepartment = organization?.departments.find(x => x.id === editingDepartment)

  const saveDesignation = async (e: FormEvent) => { e.preventDefault(); try { const b={name:designationForm.name}; if(designationForm.id) await api.updateDesignation(designationForm.id,b); else await api.createDesignation(b); setDesignationForm({id:0,name:''}); await onRefresh(); showNotice('success','Designation saved.') } catch(error){showNotice('error',error instanceof Error?error.message:'Could not save designation.')} }
  const saveGrade = async (e: FormEvent) => { e.preventDefault(); try { const b={name:gradeForm.name,level:gradeForm.level||null,minSalary:Number(gradeForm.minSalary),maxSalary:Number(gradeForm.maxSalary)}; if(gradeForm.id) await api.updateGrade(gradeForm.id,b); else await api.createGrade(b); setGradeForm({id:0,name:'',level:'',minSalary:'0',maxSalary:'0'}); await onRefresh(); showNotice('success','Grade saved.') } catch(error){showNotice('error',error instanceof Error?error.message:'Could not save grade.')} }
  const saveEmployment = async (e: FormEvent) => { e.preventDefault(); try { const b={name:employmentForm.name,description:employmentForm.description||null}; if(employmentForm.id) await api.updateEmploymentType(employmentForm.id,b); else await api.createEmploymentType(b); setEmploymentForm({id:0,name:'',description:''}); await onRefresh(); showNotice('success','Employment type saved.') } catch(error){showNotice('error',error instanceof Error?error.message:'Could not save employment type.')} }
  const deleteItem = async (kind: string, id: number) => { if(!window.confirm('Delete this item?')) return; try { if(kind==='branch') await api.deleteBranch(id); if(kind==='department') await api.deleteDepartment(id); if(kind==='designation') await api.deleteDesignation(id); if(kind==='grade') await api.deleteGrade(id); if(kind==='employment') await api.deleteEmploymentType(id); await onRefresh(); showNotice('success','Item deleted.') } catch(error){showNotice('error',error instanceof Error?error.message:'Could not delete item.')} }

  return <div className="stack-layout">
    <div className="metric-grid"><Metric label="Branches" value={organization?.branches.length || 0} detail="Operating locations" accent="purple" /><Metric label="Departments" value={organization?.departments.length || 0} detail="Reporting units" accent="blue" /><Metric label="Designations" value={organization?.designations.length || 0} detail="Job titles" accent="green" /><Metric label="Employment types" value={organization?.employmentTypes.length || 0} detail="Contract categories" accent="amber" /></div>
    <div className="two-column">
      <Panel title="Branches" eyebrow="ORGANIZATION SETUP">
        {canManage && <form className="stack-form inline-form" onSubmit={createBranch}><Field label="Branch name" value={branchForm.name} onChange={value => setBranchForm({ ...branchForm, name: value })} required /><Field label="Code" value={branchForm.code} onChange={value => setBranchForm({ ...branchForm, code: value })} /><Field label="Location" value={branchForm.location} onChange={value => setBranchForm({ ...branchForm, location: value })} /><button className="secondary-button">Add branch</button></form>}
        {editBranch && canManage && <form className="stack-form" onSubmit={async e=>{e.preventDefault();try{await api.updateBranch(editBranch.id,branchForm);setEditingBranch(null);setBranchForm({name:'',code:'',location:''});await onRefresh();showNotice('success','Branch updated.')}catch(error){showNotice('error',error instanceof Error?error.message:'Could not update branch.')}}}><Field label="Edit branch" value={branchForm.name || editBranch.name} onChange={v=>setBranchForm({...branchForm,name:v})} required /><Field label="Code" value={branchForm.code || editBranch.code || ''} onChange={v=>setBranchForm({...branchForm,code:v})}/><Field label="Location" value={branchForm.location || editBranch.location || ''} onChange={v=>setBranchForm({...branchForm,location:v})}/><div className="action-row"><button className="primary-button">Save</button><button type="button" className="ghost-button" onClick={()=>setEditingBranch(null)}>Cancel</button></div></form>}
        <div className="list-stack">{organization?.branches.map(branch=><div className="list-row" key={branch.id}><span><strong>{branch.name}</strong><small>{branch.location||'Location not set'}</small></span><span className="action-row"><code>{branch.code||'—'}</code>{canManage&&<><button className="small-button" onClick={()=>{setEditingBranch(branch.id);setBranchForm({name:branch.name,code:branch.code||'',location:branch.location||''})}}>Edit</button><button className="small-button reject" onClick={()=>deleteItem('branch',branch.id)}>Delete</button></>}</span></div>)}</div>
      </Panel>
      <Panel title="Departments" eyebrow="REPORTING UNITS">
        {canManage && <form className="stack-form inline-form" onSubmit={createDepartment}><Field label="Department name" value={departmentForm.name} onChange={value => setDepartmentForm({ ...departmentForm, name: value })} required /><Field label="Code" value={departmentForm.code} onChange={value => setDepartmentForm({ ...departmentForm, code: value })} /><label>Branch<select value={departmentForm.branchId} onChange={event => setDepartmentForm({ ...departmentForm, branchId: event.target.value })}><option value="">No branch</option>{organization?.branches.map(branch => <option key={branch.id} value={branch.id}>{branch.name}</option>)}</select></label><button className="secondary-button">Add department</button></form>}
        {editDepartment && canManage && <form className="stack-form" onSubmit={async e=>{e.preventDefault();try{await api.updateDepartment(editDepartment.id,{name:departmentForm.name,code:departmentForm.code,branchId:departmentForm.branchId?Number(departmentForm.branchId):null});setEditingDepartment(null);await onRefresh();showNotice('success','Department updated.')}catch(error){showNotice('error',error instanceof Error?error.message:'Could not update department.')}}}><Field label="Edit department" value={departmentForm.name} onChange={v=>setDepartmentForm({...departmentForm,name:v})} required /><Field label="Code" value={departmentForm.code} onChange={v=>setDepartmentForm({...departmentForm,code:v})}/><label>Branch<select value={departmentForm.branchId} onChange={e=>setDepartmentForm({...departmentForm,branchId:e.target.value})}><option value="">No branch</option>{organization?.branches.map(x=><option key={x.id} value={x.id}>{x.name}</option>)}</select></label><div className="action-row"><button className="primary-button">Save</button><button type="button" className="ghost-button" onClick={()=>setEditingDepartment(null)}>Cancel</button></div></form>}
        <div className="list-stack">{organization?.departments.map(department=><div className="list-row" key={department.id}><span><strong>{department.name}</strong><small>{organization.branches.find(branch=>branch.id===department.branchId)?.name||'Unassigned branch'}</small></span><span className="action-row"><code>{department.code||'—'}</code>{canManage&&<><button className="small-button" onClick={()=>{setEditingDepartment(department.id);setDepartmentForm({name:department.name,code:department.code||'',branchId:department.branchId?String(department.branchId):''})}}>Edit</button><button className="small-button reject" onClick={()=>deleteItem('department',department.id)}>Delete</button></>}</span></div>)}</div>
      </Panel>
    </div>
    <div className="three-column">
      <Panel title="Designations" eyebrow="JOB TITLES">{canManage&&<form className="stack-form" onSubmit={saveDesignation}><Field label="Name" value={designationForm.name} onChange={v=>setDesignationForm({...designationForm,name:v})} required/><button className="secondary-button">{designationForm.id?'Save':'Add'}</button>{designationForm.id&&<button type="button" className="ghost-button" onClick={()=>setDesignationForm({id:0,name:''})}>Cancel</button>}</form>}<div className="list-stack">{organization?.designations.map(x=><div className="list-row" key={x.id}><strong>{x.name}</strong>{canManage&&<span className="action-row"><button className="small-button" onClick={()=>setDesignationForm({id:x.id,name:x.name})}>Edit</button><button className="small-button reject" onClick={()=>deleteItem('designation',x.id)}>Delete</button></span>}</div>)}</div></Panel>
      <Panel title="Grades" eyebrow="SALARY BANDS">{canManage&&<form className="stack-form" onSubmit={saveGrade}><Field label="Name" value={gradeForm.name} onChange={v=>setGradeForm({...gradeForm,name:v})} required/><Field label="Level" value={gradeForm.level} onChange={v=>setGradeForm({...gradeForm,level:v})}/><div className="form-grid two"><Field label="Min salary" value={gradeForm.minSalary} onChange={v=>setGradeForm({...gradeForm,minSalary:v})} type="number"/><Field label="Max salary" value={gradeForm.maxSalary} onChange={v=>setGradeForm({...gradeForm,maxSalary:v})} type="number"/></div><button className="secondary-button">{gradeForm.id?'Save':'Add'}</button>{gradeForm.id&&<button type="button" className="ghost-button" onClick={()=>setGradeForm({id:0,name:'',level:'',minSalary:'0',maxSalary:'0'})}>Cancel</button>}</form>}<div className="list-stack">{organization?.grades.map(x=><div className="list-row" key={x.id}><span><strong>{x.name}</strong><small>{x.level||'—'} · {formatMoney(x.minSalary)}–{formatMoney(x.maxSalary)}</small></span>{canManage&&<span className="action-row"><button className="small-button" onClick={()=>setGradeForm({id:x.id,name:x.name,level:x.level||'',minSalary:String(x.minSalary),maxSalary:String(x.maxSalary)})}>Edit</button><button className="small-button reject" onClick={()=>deleteItem('grade',x.id)}>Delete</button></span>}</div>)}</div></Panel>
      <Panel title="Employment types" eyebrow="CONTRACT CATEGORIES">{canManage&&<form className="stack-form" onSubmit={saveEmployment}><Field label="Name" value={employmentForm.name} onChange={v=>setEmploymentForm({...employmentForm,name:v})} required/><Field label="Description" value={employmentForm.description} onChange={v=>setEmploymentForm({...employmentForm,description:v})}/><button className="secondary-button">{employmentForm.id?'Save':'Add'}</button>{employmentForm.id&&<button type="button" className="ghost-button" onClick={()=>setEmploymentForm({id:0,name:'',description:''})}>Cancel</button>}</form>}<div className="list-stack">{organization?.employmentTypes.map(x=><div className="list-row" key={x.id}><span><strong>{x.name}</strong><small>{x.description||'—'}</small></span>{canManage&&<span className="action-row"><button className="small-button" onClick={()=>setEmploymentForm({id:x.id,name:x.name,description:x.description||''})}>Edit</button><button className="small-button reject" onClick={()=>deleteItem('employment',x.id)}>Delete</button></span>}</div>)}</div></Panel>
    </div>
  </div>
}

function PayrollView({ periods, selectedPeriodId, setSelectedPeriodId, transactions, employees, form, setForm, onProcess, onLock, canManage, showNotice, onDownloadPayslip }: { periods: PayrollPeriod[]; selectedPeriodId: number; setSelectedPeriodId: (value: number) => void; transactions: PayrollTransaction[]; employees: Employee[]; form: Record<string, string>; setForm: (value: Record<string, string>) => void; onProcess: () => void; onLock: () => void; canManage: boolean; showNotice: (type: 'success' | 'error', text: string) => void; onDownloadPayslip: (id: number) => Promise<void> }) {
  const [employeeId, setEmployeeId] = useState('')
  const [components, setComponents] = useState<import('./api').PayrollComponent[]>([])
  const [component, setComponent] = useState({ id: 0, name: '', componentType: 'Allowance', amount: '0', taxable: true, recurring: true })
  const [periodForm, setPeriodForm] = useState({ month: String(new Date().getMonth() + 1), year: String(new Date().getFullYear()) })
  const totals = transactions.reduce((acc, row) => ({ gross: acc.gross + row.grossPay, paye: acc.paye + row.paye, nssf: acc.nssf + row.nssf, shif: acc.shif + row.shif, levy: acc.levy + row.housingLevy, net: acc.net + row.netPay }), { gross: 0, paye: 0, nssf: 0, shif: 0, levy: 0, net: 0 })

  const createPeriod = async (e: FormEvent) => {
    e.preventDefault()
    try { await api.createPayrollPeriod({ month: Number(periodForm.month), year: Number(periodForm.year) }); setPeriodForm({ month: String(new Date().getMonth() + 1), year: String(new Date().getFullYear()) }); window.location.reload() }
    catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not create payroll period.') }
  }
  const loadComponents = async (id: number) => {
    try { setComponents(await api.payrollComponents(id)) } catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not load payroll components.') }
  }
  const saveComponent = async (e: FormEvent) => {
    e.preventDefault()
    if (!employeeId) return
    try {
      const body = { name: component.name, componentType: component.componentType, amount: Number(component.amount), taxable: component.taxable, recurring: component.recurring }
      if (component.id) await api.updatePayrollComponent(component.id, { ...body, active: true }); else await api.createPayrollComponent({ ...body, employeeId: Number(employeeId) })
      setComponent({ id: 0, name: '', componentType: 'Allowance', amount: '0', taxable: true, recurring: true }); await loadComponents(Number(employeeId)); showNotice('success', 'Payroll component saved.')
    } catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not save payroll component.') }
  }
  const deleteComponent = async (id: number) => { try { await api.deletePayrollComponent(id); await loadComponents(Number(employeeId)); showNotice('success', 'Payroll component deactivated.') } catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not deactivate payroll component.') } }

  return <div className="stack-layout">
    <Panel title="Kenyan payroll engine" eyebrow="STATUTORY PROCESSING" action={canManage && <div className="action-row"><button className="primary-button" onClick={onProcess} disabled={!selectedPeriodId}>Process period</button><button className="secondary-button" onClick={onLock} disabled={periods.find(x=>x.id===selectedPeriodId)?.status !== 'Approved'}>Lock period</button></div>}>
      <div className="toolbar"><label>Payroll period<select value={selectedPeriodId} onChange={event => setSelectedPeriodId(Number(event.target.value))}>{periods.map(period => <option key={period.id} value={period.id}>{period.name} · {period.status}</option>)}</select></label><Field label="Global allowances (KES)" value={form.allowances} onChange={value => setForm({ ...form, allowances: value })} type="number" /><Field label="Global deductions (KES)" value={form.otherDeductions} onChange={value => setForm({ ...form, otherDeductions: value })} type="number" /></div>
      <div className="compliance-strip"><strong>Calculation coverage</strong><span>PAYE</span><span>NSSF</span><span>SHIF</span><span>Housing Levy</span><span>Employee components</span><span>Lockable periods</span></div>
      {canManage && <form className="toolbar" onSubmit={createPeriod}><Field label="New period month" value={periodForm.month} onChange={v=>setPeriodForm({...periodForm,month:v})} type="number"/><Field label="Year" value={periodForm.year} onChange={v=>setPeriodForm({...periodForm,year:v})} type="number"/><button className="secondary-button">Create payroll period</button></form>}
    </Panel>
    {canManage && <Panel title="Employee payroll components" eyebrow="ALLOWANCES & DEDUCTIONS">
      <div className="form-grid two"><label>Employee<select value={employeeId} onChange={e=>{setEmployeeId(e.target.value); if(e.target.value) loadComponents(Number(e.target.value))}}><option value="">Select employee</option>{employees.map(x=><option key={x.id} value={x.id}>{x.employeeNo} — {x.fullName}</option>)}</select></label><div className="muted">Recurring components are included automatically when payroll is processed.</div></div>
      {employeeId && <><form className="form-grid" onSubmit={saveComponent}><Field label="Component name" value={component.name} onChange={v=>setComponent({...component,name:v})} required/><label>Type<select value={component.componentType} onChange={e=>setComponent({...component,componentType:e.target.value})}><option>Allowance</option><option>Deduction</option></select></label><Field label="Amount" value={component.amount} onChange={v=>setComponent({...component,amount:v})} type="number" required/><label>Taxable<select value={component.taxable?'true':'false'} onChange={e=>setComponent({...component,taxable:e.target.value==='true'})}><option value="true">Taxable</option><option value="false">Non-taxable</option></select></label><label>Recurring<select value={component.recurring?'true':'false'} onChange={e=>setComponent({...component,recurring:e.target.value==='true'})}><option value="true">Recurring</option><option value="false">One-off</option></select></label><div className="action-row"><button className="primary-button">{component.id?'Save component':'Add component'}</button>{component.id&&<button type="button" className="ghost-button" onClick={()=>setComponent({id:0,name:'',componentType:'Allowance',amount:'0',taxable:true,recurring:true})}>Cancel</button>}</div></form><div className="list-stack">{components.filter(x=>x.active).map(x=><div className="list-row" key={x.id}><span><strong>{x.name}</strong><small>{x.componentType} · {formatMoney(x.amount)} · {x.taxable?'Taxable':'Non-taxable'} · {x.recurring?'Recurring':'One-off'}</small></span><span className="action-row"><button className="small-button" onClick={()=>setComponent({id:x.id,name:x.name,componentType:x.componentType,amount:String(x.amount),taxable:x.taxable,recurring:x.recurring})}>Edit</button><button className="small-button reject" onClick={()=>deleteComponent(x.id)}>Deactivate</button></span></div>)}</div></>}
    </Panel>}
    <div className="metric-grid"><Metric label="Gross pay" value={formatMoney(totals.gross)} detail={`${transactions.length} transactions`} accent="blue" /><Metric label="PAYE" value={formatMoney(totals.paye)} detail="After personal relief" accent="amber" /><Metric label="Statutory deductions" value={formatMoney(totals.nssf + totals.shif + totals.levy)} detail="NSSF · SHIF · Housing" accent="purple" /><Metric label="Net pay" value={formatMoney(totals.net)} detail="Employee take-home" accent="green" /></div>
    <Panel title="Payroll transactions" eyebrow="PERIOD DETAIL"><DataTable headers={['Employee', 'Gross', 'PAYE', 'NSSF', 'SHIF', 'Housing levy', 'Net pay', 'Status', 'Payslip']} rows={transactions.map(row => [row.employeeName, formatMoney(row.grossPay), formatMoney(row.paye), formatMoney(row.nssf), formatMoney(row.shif), formatMoney(row.housingLevy), formatMoney(row.netPay), <StatusBadge key={row.id} value={row.status} />, <button className="small-button" key={`pdf-${row.id}`} onClick={()=>onDownloadPayslip(row.id)}>PDF</button>])} empty="No transactions for this period. Process payroll to generate statutory calculations." /></Panel>
  </div>
}

function LeaveView({ leaveTypes, balances, requests, form, setForm, onCreate, onUpdate, canApprove, canManage, onRefresh, showNotice }: { leaveTypes: LeaveType[]; balances: LeaveBalance[]; requests: LeaveRequest[]; form: Record<string, string>; setForm: (value: Record<string, string>) => void; onCreate: (event: FormEvent) => void; onUpdate: (id: number, status: string) => void; canApprove: boolean; canManage: boolean; onRefresh: () => Promise<void>; showNotice: (type: 'success' | 'error', text: string) => void }) {
  const [typeForm, setTypeForm] = useState({ id: 0, name: '', defaultDays: '21', paid: true, description: '' })
  const saveType = async (e: FormEvent) => {
    e.preventDefault()
    try {
      const body = { name: typeForm.name, defaultDays: Number(typeForm.defaultDays), paid: typeForm.paid, description: typeForm.description || null }
      if (typeForm.id) await api.updateLeaveType(typeForm.id, body); else await api.createLeaveType(body)
      setTypeForm({ id: 0, name: '', defaultDays: '21', paid: true, description: '' }); await onRefresh(); showNotice('success', 'Leave type saved.')
    } catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not save leave type.') }
  }
  const deleteType = async (id: number) => {
    if (!window.confirm('Delete this leave type?')) return
    try { await api.deleteLeaveType(id); await onRefresh(); showNotice('success', 'Leave type deleted.') }
    catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not delete leave type.') }
  }
  const cancelRequest = async (id: number) => {
    try { await api.cancelLeaveRequest(id); await onRefresh(); showNotice('success', 'Leave request cancelled and balance restored where applicable.') }
    catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not cancel leave request.') }
  }

  return <div className="stack-layout">
    <div className="two-column">
      <Panel title="Apply for leave" eyebrow="EMPLOYEE SELF-SERVICE"><form className="stack-form" onSubmit={onCreate}><label>Leave type<select required value={form.leaveTypeId} onChange={event => setForm({ ...form, leaveTypeId: event.target.value })}><option value="">Choose a leave type</option>{leaveTypes.map(type => <option key={type.id} value={type.id}>{type.name} · {type.defaultDays} days</option>)}</select></label><div className="form-grid two"><Field label="Start date" value={form.startDate} onChange={value => setForm({ ...form, startDate: value })} type="date" required /><Field label="End date" value={form.endDate} onChange={value => setForm({ ...form, endDate: value })} type="date" required /></div><Field label="Days requested" value={form.daysRequested} onChange={value => setForm({ ...form, daysRequested: value })} type="number" required /><label>Reason<textarea value={form.reason} onChange={event => setForm({ ...form, reason: event.target.value })} placeholder="Family vacation, medical rest…" /></label><button className="primary-button">Submit request</button></form></Panel>
      <Panel title="My leave balances" eyebrow="CURRENT YEAR"><div className="balance-grid">{balances.map(balance => <div className="balance-card" key={balance.id}><span>{balance.leaveType}</span><strong>{balance.availableDays}</strong><small>of {balance.allocatedDays} days available</small><div className="progress"><span style={{ width: `${Math.min(100, balance.usedDays / Math.max(balance.allocatedDays, 1) * 100)}%` }} /></div></div>)}{balances.length === 0 && <EmptyState text="No leave balances have been configured." />}</div></Panel>
    </div>
    {canManage && <Panel title="Leave types" eyebrow="POLICY CONFIGURATION"><form className="form-grid" onSubmit={saveType}><Field label="Name" value={typeForm.name} onChange={v=>setTypeForm({...typeForm,name:v})} required/><Field label="Default days" value={typeForm.defaultDays} onChange={v=>setTypeForm({...typeForm,defaultDays:v})} type="number" required/><label>Paid<select value={typeForm.paid?'true':'false'} onChange={e=>setTypeForm({...typeForm,paid:e.target.value==='true'})}><option value="true">Paid</option><option value="false">Unpaid</option></select></label><Field label="Description" value={typeForm.description} onChange={v=>setTypeForm({...typeForm,description:v})}/><div className="action-row"><button className="primary-button">{typeForm.id?'Save changes':'Add leave type'}</button>{typeForm.id&&<button type="button" className="ghost-button" onClick={()=>setTypeForm({id:0,name:'',defaultDays:'21',paid:true,description:''})}>Cancel</button>}</div></form><div className="list-stack">{leaveTypes.map(type=><div className="list-row" key={type.id}><span><strong>{type.name}</strong><small>{type.defaultDays} days · {type.paid?'Paid':'Unpaid'} · {type.description||'No description'}</small></span><span className="action-row"><button className="small-button" onClick={()=>setTypeForm({id:type.id,name:type.name,defaultDays:String(type.defaultDays),paid:type.paid,description:type.description||''})}>Edit</button><button className="small-button reject" onClick={()=>deleteType(type.id)}>Delete</button></span></div>)}</div></Panel>}
    <Panel title="Leave requests and approval queue" eyebrow="WORKFLOW"><DataTable headers={['Employee', 'Leave type', 'Dates', 'Days', 'Reason', 'Status', 'Action']} rows={requests.map(request => [request.employeeName || 'Employee', request.leaveType, `${formatDate(request.startDate)} – ${formatDate(request.endDate)}`, request.daysRequested, request.reason || '—', <StatusBadge key={request.id} value={request.status} />, <span className="action-row" key={`action-${request.id}`}>{canApprove && request.status === 'Pending' && <><button className="small-button approve" onClick={() => onUpdate(request.id, 'Approved')}>Approve</button><button className="small-button reject" onClick={() => onUpdate(request.id, 'Rejected')}>Reject</button></>}{(request.status === 'Pending' || request.status === 'Approved') && <button className="small-button" onClick={() => cancelRequest(request.id)}>Cancel</button>}</span>])} empty="No leave requests yet." /></Panel>
  </div>
}

function EssView({ profile, payslips, onDownloadPayslip }: { profile: Employee | null; payslips: PayrollTransaction[]; onDownloadPayslip: (id: number) => Promise<void> }) {
  return <div className="stack-layout"><Panel title="Employee self-service" eyebrow="MY PROFILE"><div className="profile-grid">{profile ? <><Detail label="Full name" value={profile.fullName} /><Detail label="Employee number" value={profile.employeeNo} mono /><Detail label="Email" value={profile.email || '—'} /><Detail label="KRA PIN" value={profile.kraPin} mono /><Detail label="NSSF / SHIF" value={`${profile.nssfNo || '—'} / ${profile.shifNo || '—'}`} mono /><Detail label="Basic salary" value={formatMoney(profile.basicSalary)} /></> : <EmptyState text="This user has no linked employee self-service profile." />}</div></Panel><Panel title="Payslips and payroll history" eyebrow="PAYROLL HISTORY"><DataTable headers={['Payroll employee', 'Gross pay', 'PAYE', 'NSSF', 'SHIF', 'Net pay', 'Status', 'Payslip']} rows={payslips.map(row => [row.employeeName, formatMoney(row.grossPay), formatMoney(row.paye), formatMoney(row.nssf), formatMoney(row.shif), formatMoney(row.netPay), <StatusBadge key={row.id} value={row.status} />, <button className="small-button" key={`pdf-${row.id}`} onClick={()=>onDownloadPayslip(row.id)}>Download PDF</button>])} empty="No payslip transactions available." /></Panel></div>
}

function AuditView({ rows }: { rows: AuditLog[] }) {
  return <div className="stack-layout"><Panel title="Audit trail" eyebrow="CONTROL AND GOVERNANCE"><p className="muted">Recent tenant-scoped create, update, and payroll actions captured by the ASP.NET Core API.</p><DataTable headers={['Time', 'Action', 'Entity', 'User', 'Details']} rows={rows.map(row => [formatDate(row.createdAt), <StatusBadge key={row.id} value={row.action} />, `${row.entityType}${row.entityId ? ` #${row.entityId}` : ''}`, row.userName || 'System', row.details || '—'])} empty="No audit events recorded yet." /></Panel></div>
}

function AttendanceView({ employees, rows, summary, canManage, onRefresh, showNotice }: { employees: Employee[]; rows: Attendance[]; summary: AttendanceSummary | null; canManage: boolean; onRefresh: () => Promise<void>; showNotice: (type: 'success' | 'error', text: string) => void }) {
  const [form, setForm] = useState({ employeeId: '', attendanceDate: new Date().toISOString().slice(0,10), checkIn: '08:30', checkOut: '17:30', status: 'Present', notes: '' })
  const submit = async (e: FormEvent) => {
    e.preventDefault()
    try {
      const dateTime = (time: string) => time ? form.attendanceDate + 'T' + time + ':00Z' : undefined
      await api.createAttendance({ employeeId: Number(form.employeeId), attendanceDate: form.attendanceDate, checkIn: dateTime(form.checkIn), checkOut: dateTime(form.checkOut), status: form.status, notes: form.notes || null })
      await onRefresh(); showNotice('success', 'Attendance recorded.')
    } catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not record attendance.') }
  }
  return <div className="stack-layout">
    <section className="metric-grid"><Metric label="Present" value={summary?.present ?? 0} detail="Recent period" accent="blue" /><Metric label="Late" value={summary?.late ?? 0} detail="Late arrivals" accent="green" /><Metric label="Absent" value={summary?.absent ?? 0} detail="Absent records" accent="purple" /><Metric label="Hours worked" value={(summary?.hoursWorked ?? 0).toFixed(1)} detail="Recorded hours" accent="orange" /></section>
    {canManage && <section className="panel"><div className="panel-heading"><div><span className="eyebrow">TIME & ATTENDANCE</span><h2>Record attendance</h2></div></div><form className="form-grid" onSubmit={submit}>
      <label>Employee<select required value={form.employeeId} onChange={e => setForm({...form, employeeId:e.target.value})}><option value="">Select employee</option>{employees.map(e=><option key={e.id} value={e.id}>{e.employeeNo} — {e.fullName}</option>)}</select></label>
      <label>Date<input type="date" required value={form.attendanceDate} onChange={e=>setForm({...form,attendanceDate:e.target.value})}/></label>
      <label>Check in<input type="time" value={form.checkIn} onChange={e=>setForm({...form,checkIn:e.target.value})}/></label>
      <label>Check out<input type="time" value={form.checkOut} onChange={e=>setForm({...form,checkOut:e.target.value})}/></label>
      <label>Status<select value={form.status} onChange={e=>setForm({...form,status:e.target.value})}>{['Present','Late','Absent','Leave','HalfDay','Holiday','Weekend'].map(x=><option key={x}>{x}</option>)}</select></label>
      <label>Notes<input value={form.notes} onChange={e=>setForm({...form,notes:e.target.value})}/></label>
      <button className="primary-button" type="submit">Record attendance</button>
    </form></section>}
    <section className="panel"><div className="panel-heading"><div><span className="eyebrow">ATTENDANCE LOG</span><h2>Recent records</h2></div></div><DataTable headers={['Date','Employee','Status','Check in','Check out','Hours','Notes']} rows={rows.map(r=>[formatDate(r.attendanceDate),r.employeeNo+' — '+r.employeeName,r.status,r.checkIn?new Date(r.checkIn).toLocaleTimeString('en-KE',{hour:'2-digit',minute:'2-digit'}):'—',r.checkOut?new Date(r.checkOut).toLocaleTimeString('en-KE',{hour:'2-digit',minute:'2-digit'}):'—',r.hoursWorked.toFixed(2),r.notes||'—'])} empty="No attendance records found." /></section>
  </div>
}

function ReportsView({ reports }: { reports: Report[] }) {
  const [selected, setSelected] = useState<ReportData | null>(null)
  const [busy, setBusy] = useState(false)

  const openReport = async (id: number) => {
    setBusy(true)
    try { setSelected(await api.reportData(id)) } catch (error) { window.alert(error instanceof Error ? error.message : 'Could not generate report.') } finally { setBusy(false) }
  }

  const downloadCsv = () => {
    if (!selected) return
    const escape = (value: string) => '"' + value.replaceAll('"', '""') + '"'
    const csv = [selected.columns, ...selected.rows].map(row => row.map(escape).join(',')).join('\
')
    const blob = new Blob([csv], { type: 'text/csv;charset=utf-8' })
    const url = URL.createObjectURL(blob)
    const link = document.createElement('a')
    link.href = url
    link.download = selected.name.toLowerCase().replaceAll(/[^a-z0-9]+/g, '-') + '.csv'
    link.click()
    URL.revokeObjectURL(url)
  }

  const printReport = () => {
    if (!selected) return
    const popup = window.open('', '_blank', 'width=1200,height=800')
    if (!popup) return
    const head = selected.columns.map(column => '<th>' + column.replaceAll('<', '&lt;') + '</th>').join('')
    const body = selected.rows.map(row => '<tr>' + row.map(cell => '<td>' + cell.replaceAll('&', '&amp;').replaceAll('<', '&lt;') + '</td>').join('') + '</tr>').join('')
    popup.document.write('<html><head><title>' + selected.name + '</title><style>body{font-family:Arial;padding:32px}table{width:100%;border-collapse:collapse}th,td{border:1px solid #ddd;padding:8px;text-align:left}th{background:#f3f4f6}h1{margin-bottom:4px}</style></head><body><h1>' + selected.name + '</h1><p>' + selected.description + '</p><table><thead><tr>' + head + '</tr></thead><tbody>' + body + '</tbody></table><script>window.onload=function(){window.print()}</script></body></html>')
    popup.document.close()
  }

  return <div className="stack-layout"><section className="hero-card report-hero"><div><span className="eyebrow">BUILT-IN REPORTING</span><h2>Operational reports generated from live HR data.</h2><p>Reports are tenant-scoped and generated by the ASP.NET API. You can inspect the data, export CSV, or print a clean report.</p></div><div className="hero-stat"><span>REPORTS AVAILABLE</span><strong>{reports.length}</strong></div></section><div className="report-grid">{reports.map(report => <article className="report-card" key={report.id}><div className="report-icon">RPT</div><span className="eyebrow">LIVE REPORT</span><h3>{report.name}</h3><p>{report.description}</p><code>{report.reportPath}</code><div className="action-row"><button className="secondary-button" onClick={() => openReport(report.id)} disabled={busy}>{busy ? 'Generating…' : 'View report'}</button>{report.launchUrl && <a className="secondary-button" href={report.launchUrl} target="_blank" rel="noreferrer">Open in SSRS</a>}</div></article>)}{reports.length === 0 && <EmptyState text="No reports have been configured." />}</div>{selected && <section className="panel report-viewer"><div className="panel-heading"><div><span className="eyebrow">REPORT PREVIEW</span><h2>{selected.name}</h2><p className="muted">{selected.description}</p></div><div className="action-row"><button className="secondary-button" onClick={downloadCsv}>Download CSV</button><button className="secondary-button" onClick={printReport}>Print / PDF</button><button className="ghost-button" onClick={() => setSelected(null)}>Close</button></div></div><div className="report-meta">Generated {formatDate(selected.generatedAt)} · {selected.rows.length} rows</div><DataTable headers={selected.columns} rows={selected.rows} empty="No records match this report." /></section>}</div>
}

function Metric({ label, value, detail, accent }: { label: string; value: string | number; detail: string; accent: string }) { return <div className={`metric-card ${accent}`}><span>{label}</span><strong>{value}</strong><small>{detail}</small></div> }
function Panel({ title, eyebrow, action, children }: { title: string; eyebrow: string; action?: ReactNode; children: ReactNode }) { return <section className="panel"><div className="panel-heading"><div><span className="eyebrow">{eyebrow}</span><h2>{title}</h2></div>{action}</div>{children}</section> }
function Field({ label, value, onChange, type = 'text', required = false }: { label: string; value: string; onChange: (value: string) => void; type?: string; required?: boolean }) { return <label>{label}<input value={value} type={type} required={required} onChange={event => onChange(event.target.value)} /></label> }
function Detail({ label, value, mono = false }: { label: string; value: string; mono?: boolean }) { return <div className="detail"><span>{label}</span><strong className={mono ? 'mono' : ''}>{value}</strong></div> }
function StatusBadge({ value }: { value: string }) { return <span className={`status ${value.toLowerCase().replaceAll(' ', '-')}`}>{value}</span> }
function EmptyState({ text }: { text: string }) { return <div className="empty-state">{text}</div> }
function DataTable({ headers, rows, empty }: { headers: string[]; rows: (string | number | ReactNode)[][]; empty: string }) { return <div className="table-wrap"><table><thead><tr>{headers.map(header => <th key={header}>{header}</th>)}</tr></thead><tbody>{rows.length ? rows.map((row, index) => <tr key={index}>{row.map((cell, cellIndex) => <td key={cellIndex}>{cell}</td>)}</tr>) : <tr><td className="empty-cell" colSpan={headers.length}>{empty}</td></tr>}</tbody></table></div> }

export default App
