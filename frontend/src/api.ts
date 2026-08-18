const API_BASE = (import.meta.env.VITE_API_URL || '').replace(/\/+$/, '')

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    ...options,
    credentials: 'include',
    headers: { 'Content-Type': 'application/json', ...(options.headers || {}) },
  })
  if (!response.ok) {
    let message = `Request failed with status ${response.status}`
    try {
      const payload = await response.json()
      message = payload.message || payload.title || message
    } catch {
      // Keep the HTTP status message when the response has no JSON body.
    }
    throw new Error(message)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export type User = { id: number; name: string; email: string; role: string; tenantId: number; employeeId?: number | null }
export type Tenant = { id: number; companyName: string; kraPin?: string; email?: string; phone?: string; address?: string; status: string }
export type Dashboard = { totalEmployees: number; monthlyGross: number; branches: number; departments: number; payrollStatus: string; tenant: Tenant; user: User }
export type Employee = { id: number; employeeNo: string; payrollNo?: string; fullName: string; email?: string; phone?: string; kraPin: string; nssfNo?: string; shifNo?: string; employmentStatus: string; basicSalary: number; bankName?: string; accountNumber?: string; departmentId?: number; branchId?: number }
export type Branch = { id: number; tenantId: number; name: string; code?: string; location?: string }
export type Department = { id: number; tenantId: number; name: string; code?: string; branchId?: number }
export type Organization = { branches: Branch[]; departments: Department[]; designations: { id: number; name: string }[]; grades: { id: number; name: string; level?: string; minSalary: number; maxSalary: number }[]; employmentTypes: { id: number; name: string; description?: string }[] }
export type PayrollPeriod = { id: number; name: string; month: number; year: number; status: string; processedAt?: string }
export type PayrollTransaction = { id: number; employeeId: number; employeeName: string; grossPay: number; paye: number; nssf: number; shif: number; housingLevy: number; totalDeductions: number; netPay: number; status: string }
export type LeaveType = { id: number; name: string; defaultDays: number; paid: boolean; description?: string }
export type LeaveBalance = { id: number; employeeId: number; leaveTypeId: number; leaveType: string; year: number; allocatedDays: number; usedDays: number; availableDays: number }
export type LeaveRequest = { id: number; employeeId: number; employeeName: string; leaveTypeId: number; leaveType: string; startDate: string; endDate: string; daysRequested: number; reason?: string; status: string; createdAt: string }
export type AuditLog = { id: number; action: string; entityType: string; entityId?: number; userName?: string; details?: string; createdAt: string }
export type Report = { id: number; name: string; description: string; reportPath: string; launchUrl?: string }

const json = (body: unknown): RequestInit => ({ method: 'POST', body: JSON.stringify(body) })

export const api = {
  me: () => request<User>('/api/auth/me'),
  login: (body: { email: string; password: string }) => request<User>('/api/auth/login', json(body)),
  logout: () => request<void>('/api/auth/logout', { method: 'POST' }),
  dashboard: () => request<Dashboard>('/api/dashboard'),
  employees: () => request<Employee[]>('/api/employees'),
  createEmployee: (body: Record<string, unknown>) => request<Employee>('/api/employees', json(body)),
  organization: () => request<Organization>('/api/organization'),
  createBranch: (body: Record<string, unknown>) => request<Branch>('/api/organization/branches', json(body)),
  createDepartment: (body: Record<string, unknown>) => request<Department>('/api/organization/departments', json(body)),
  periods: () => request<PayrollPeriod[]>('/api/payroll/periods'),
  transactions: (periodId: number) => request<PayrollTransaction[]>(`/api/payroll/transactions?payrollPeriodId=${periodId}`),
  processPayroll: (body: Record<string, unknown>) => request<PayrollTransaction[]>('/api/payroll/process', json(body)),
  leaveTypes: () => request<LeaveType[]>('/api/leave/types'),
  leaveBalances: () => request<LeaveBalance[]>('/api/leave/balances'),
  leaveRequests: () => request<LeaveRequest[]>('/api/leave/requests'),
  createLeaveRequest: (body: Record<string, unknown>) => request<LeaveRequest>('/api/leave/requests', json(body)),
  updateLeaveStatus: (id: number, status: string) => request<void>(`/api/leave/requests/${id}/status`, { method: 'PATCH', body: JSON.stringify({ status }) }),
  essProfile: () => request<Employee | null>('/api/ess/profile'),
  payslips: () => request<PayrollTransaction[]>('/api/ess/payslips'),
  audit: () => request<AuditLog[]>('/api/audit'),
  reports: () => request<Report[]>('/api/reports'),
}
