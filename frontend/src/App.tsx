import { useEffect, useMemo, useState } from 'react'
import type { FormEvent, ReactNode } from 'react'
import { api } from './api'
import type { Attendance, AttendanceSummary, AuditLog, Dashboard, Employee, LeaveBalance, LeaveRequest, LeaveType, Organization, PayrollPeriod, PayrollTransaction, Report, ReportData, User, Notification } from './api'
import './App.css'

type Tab = 'dashboard' | 'employees' | 'organization' | 'payroll' | 'leave' | 'ess' | 'attendance' | 'audit' | 'reports' | 'account' | 'platform'

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
    const base: { id: Tab; label: string; hint: string }[] = [{ id: 'dashboard', label: 'Dashboard', hint: 'Overview' }, { id: 'leave', label: 'Leave', hint: 'Requests and balances' }, { id: 'ess', label: 'ESS portal', hint: 'Your employee records' }, { id: 'account', label: 'My account', hint: 'Password and security' }]
    if (!user || !canManagePeople(user.role)) return base
    return [{ id: 'dashboard', label: 'Dashboard', hint: 'Overview' }, { id: 'employees', label: 'Employee master', hint: 'People records' }, { id: 'organization', label: 'Organization', hint: 'Structure and units' }, { id: 'payroll', label: 'Kenyan payroll', hint: 'Statutory processing' }, { id: 'leave', label: 'Leave', hint: 'Requests and balances' }, { id: 'ess', label: 'ESS portal', hint: 'Self-service' }, { id: 'attendance', label: 'Attendance', hint: 'Time and attendance' }, ...(canViewAudit(user.role) ? [{ id: 'audit' as Tab, label: 'Audit trail', hint: 'Change history' }] : []), ...(canViewReports(user.role) ? [{ id: 'reports' as Tab, label: 'Reports', hint: 'Live HR reporting' }] : []), ...(roleKey(user.role) === 'superadmin' ? [{ id: 'platform' as Tab, label: 'Platform', hint: 'Tenants and administrators' }] : []), { id: 'account' as Tab, label: 'My account', hint: 'Password and security' }]
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
          {activeTab === 'account' && <AccountView user={user} onLogout={onLogout} showNotice={showNotice} />}
          {activeTab === 'platform' && <PlatformView showNotice={showNotice} />}
        </div>
      </main>
    </div>
  )
}

function LoginScreen({ email, password, busy, setEmail, setPassword, onSubmit, notice }: { email: string; password: string; busy: boolean; setEmail: (value: string) => void; setPassword: (value: string) => void; onSubmit: (event: FormEvent) => void; notice: { type: 'success' | 'error'; text: string } | null }) {
  const [mode, setMode] = useState<'login' | 'forgot' | 'reset'>(new URLSearchParams(window.location.search).has('token') ? 'reset' : 'login')
  const [resetToken, setResetToken] = useState(new URLSearchParams(window.location.search).get('token') || '')
  const [newPassword, setNewPassword] = useState('')
  const [message, setMessage] = useState('')
  const [busyLocal, setBusyLocal] = useState(false)

  const submitForgot = async (event: FormEvent) => {
    event.preventDefault(); setBusyLocal(true)
    try { const result = await api.forgotPassword({ email }); setMessage(result.message) } catch (error) { setMessage(error instanceof Error ? error.message : 'Could not start password reset.') } finally { setBusyLocal(false) }
  }
  const submitReset = async (event: FormEvent) => {
    event.preventDefault(); setBusyLocal(true)
    try { await api.resetPassword({ token: resetToken, newPassword }); setMessage('Password reset complete. You can now sign in.'); setMode('login'); window.history.replaceState({}, '', window.location.pathname) } catch (error) { setMessage(error instanceof Error ? error.message : 'Could not reset password.') } finally { setBusyLocal(false) }
  }

  return <div className="login-page"><div className="login-visual"><div className="brand-mark large">BP</div><span className="eyebrow">BLUEPRINT HR</span><h1>People operations, brought into focus.</h1><p>A Kenya-focused HR and payroll foundation for modern teams — structured, compliant, and ready to scale.</p><div className="feature-list"><span>Multi-tenant architecture with strict isolation</span><span>Employee master with Kenya statutory identifiers</span><span>Role-aware workflows and audit visibility</span></div></div><div className="login-card"><span className="eyebrow">SECURE WORKSPACE ACCESS</span>{mode === 'login' && <><h2>Sign in to your workspace</h2><p className="muted">Use your BluePrint HR account credentials to continue.</p>{notice && <div className={`notice ${notice.type}`}>{notice.text}</div>}<form onSubmit={onSubmit} className="stack-form"><label>Work email<input value={email} onChange={event => setEmail(event.target.value)} type="email" required /></label><label>Password<input value={password} onChange={event => setPassword(event.target.value)} type="password" required minLength={12} /></label><button className="primary-button" disabled={busy}>{busy ? 'Signing in…' : 'Sign in securely'}</button></form><button className="text-button" onClick={() => { setMessage(''); setMode('forgot') }}>Forgot password?</button></>}{mode === 'forgot' && <><h2>Reset your password</h2><p className="muted">Enter your work email. If the account exists, reset instructions will be sent.</p><form onSubmit={submitForgot} className="stack-form"><label>Work email<input value={email} onChange={event => setEmail(event.target.value)} type="email" required /></label><button className="primary-button" disabled={busyLocal}>{busyLocal ? 'Sending…' : 'Send reset link'}</button></form><button className="text-button" onClick={() => setMode('login')}>Back to sign in</button></>}{mode === 'reset' && <><h2>Set a new password</h2><form onSubmit={submitReset} className="stack-form"><label>New password<input value={newPassword} onChange={event => setNewPassword(event.target.value)} type="password" minLength={12} required /></label><label>Reset token<input value={resetToken} onChange={event => setResetToken(event.target.value)} required /></label><button className="primary-button" disabled={busyLocal}>{busyLocal ? 'Saving…' : 'Set password'}</button></form></>}{message && <div className="notice success">{message}</div>}<p className="fine-print">Server-side sessions · password reset links expire automatically</p></div></div>
}

function AccountView({ user, onLogout, showNotice }: { user: User; onLogout: () => Promise<void>; showNotice: (type: 'success' | 'error', text: string) => void }) {
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const change = async (event: FormEvent) => {
    event.preventDefault()
    if (newPassword !== confirmPassword) { showNotice('error', 'New passwords do not match.'); return }
    try { await api.changePassword({ currentPassword, newPassword }); showNotice('success', 'Password changed. Sign in again with the new password.'); await onLogout() } catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not change password.') }
  }
  return <div className="stack-layout"><section className="hero-card"><div><span className="eyebrow">ACCOUNT SECURITY</span><h2>{user.name}</h2><p>{user.email} · {user.role}</p></div></section><Panel title="Change password" eyebrow="PASSWORD"><form className="form-grid compact-form" onSubmit={change}><Field label="Current password" value={currentPassword} onChange={setCurrentPassword} type="password" required /><Field label="New password" value={newPassword} onChange={setNewPassword} type="password" required /><Field label="Confirm new password" value={confirmPassword} onChange={setConfirmPassword} type="password" required /><div className="field-action"><button className="primary-button">Change password</button></div></form><p className="muted">Use at least 12 characters with upper/lower-case letters, a number and a special character.</p></Panel></div>
}

function PlatformView({ showNotice }: { showNotice: (type: 'success' | 'error', text: string) => void }) {
  const [tenants, setTenants] = useState<any[]>([])
  const [form, setForm] = useState({ companyName: '', kraPin: '', email: '', phone: '', address: '', subdomain: '' })
  const load = async () => { try { setTenants(await api.platformTenants()) } catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not load tenants.') } }
  useEffect(() => { load() }, [])
  const create = async (event: FormEvent) => { event.preventDefault(); try { await api.createTenant(form); setForm({ companyName:'',kraPin:'',email:'',phone:'',address:'',subdomain:'' }); await load(); showNotice('success','Tenant created.') } catch (error) { showNotice('error', error instanceof Error ? error.message : 'Could not create tenant.') } }
  return <div className="stack-layout"><Panel title="Platform tenants" eyebrow="SUPER ADMIN"><form className="form-grid compact-form" onSubmit={create}><Field label="Company name" value={form.companyName} onChange={v=>setForm({...form,companyName:v})} required /><Field label="KRA PIN" value={form.kraPin} onChange={v=>setForm({...form,kraPin:v})} /><Field label="Email" value={form.email} onChange={v=>setForm({...form,email:v})} type="email" /><Field label="Phone" value={form.phone} onChange={v=>setForm({...form,phone:v})} /><Field label="Subdomain" value={form.subdomain} onChange={v=>setForm({...form,subdomain:v})} /><div className="field-action"><button className="primary-button">Create tenant</button></div></form><DataTable headers={['ID','Company','Status','Users','Employees']} rows={tenants.map(t=>[t.id,t.companyName,t.status,t.userCount,t.employeeCount])} empty="No tenants found." /></Panel></div>
}

