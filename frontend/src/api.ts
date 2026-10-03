const API_BASE = (import.meta.env.VITE_API_URL || '/api').replace(/\/+$/, '')

async function request<T>(path: string, options: RequestInit = {}): Promise<T> {
  const response = await fetch(API_BASE === '/api' ? `${API_BASE}${path.replace(/^\/api/, '')}` : `${API_BASE}${path}`, {
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
export type Employee = { id: number; employeeNo: string; payrollNo?: string; fullName: string; firstName?: string; middleName?: string; lastName?: string; email?: string; phone?: string; kraPin: string; nssfNo?: string; shifNo?: string; employmentStatus: string; basicSalary: number; bankName?: string; bankBranch?: string; accountNumber?: string; departmentId?: number; branchId?: number; designationId?: number; gradeId?: number; employmentTypeId?: number; employmentDate?: string; terminationDate?: string }
export type Branch = { id: number; tenantId: number; name: string; code?: string; location?: string }
export type Department = { id: number; tenantId: number; name: string; code?: string; branchId?: number }
export type Organization = { branches: Branch[]; departments: Department[]; designations: { id: number; tenantId: number; name: string }[]; grades: { id: number; tenantId: number; name: string; level?: string; minSalary: number; maxSalary: number }[]; employmentTypes: { id: number; tenantId: number; name: string; description?: string }[] }
export type PayrollPeriod = { id: number; name: string; month: number; year: number; status: string; processedAt?: string }
export type PayrollTransaction = { id: number; employeeId: number; employeeName: string; grossPay: number; paye: number; nssf: number; shif: number; housingLevy: number; totalDeductions: number; netPay: number; status: string }
export type PayrollComponent = { id: number; employeeId: number; name: string; componentType: string; amount: number; taxable: boolean; recurring: boolean; active: boolean }
export type Notification = { id: number; type: string; title: string; message: string; entityType?: string; entityId?: number; isRead: boolean; createdAt: string }
export type LeaveType = { id: number; name: string; defaultDays: number; paid: boolean; description?: string }
export type LeaveBalance = { id: number; employeeId: number; leaveTypeId: number; leaveType: string; year: number; allocatedDays: number; usedDays: number; availableDays: number }
export type LeaveRequest = { id: number; employeeId: number; employeeName: string; leaveTypeId: number; leaveType: string; startDate: string; endDate: string; daysRequested: number; reason?: string; status: string; createdAt: string }
export type AuditLog = { id: number; action: string; entityType: string; entityId?: number; userName?: string; details?: string; createdAt: string }
export type Report = { id: number; name: string; description: string; reportPath: string; launchUrl?: string }
export type ReportData = { name: string; description: string; columns: string[]; rows: string[][]; generatedAt: string }
export type Attendance = { id: number; employeeId: number; employeeName: string; employeeNo: string; attendanceDate: string; checkIn?: string; checkOut?: string; status: string; hoursWorked: number; notes?: string }
export type AttendanceSummary = { from: string; to: string; present: number; late: number; absent: number; leave: number; halfDay: number; hoursWorked: number }

const json = (body: unknown): RequestInit => ({ method: 'POST', body: JSON.stringify(body) })

export const api = {
  me: () => request<User>('/api/auth/me'),
  login: (body: { email: string; password: string }) => request<User>('/api/auth/login', json(body)),
  logout: () => request<void>('/api/auth/logout', { method: 'POST' }),
  changePassword: (body: { currentPassword: string; newPassword: string }) => request<void>('/api/accounts/change-password', json(body)),
  forgotPassword: (body: { email: string }) => request<{ message: string }>('/api/accounts/forgot-password', json(body)),
  resetPassword: (body: { token: string; newPassword: string }) => request<void>('/api/accounts/reset-password', json(body)),
  activateEmployeeAccount: (employeeId: number, body: { email?: string }) => request('/api/accounts/employees/' + employeeId + '/activate', json(body)),
  deactivateUserAccount: (userId: number) => request<void>('/api/accounts/users/' + userId + '/deactivate', { method: 'POST' }),
  activateUserAccount: (userId: number) => request<void>('/api/accounts/users/' + userId + '/activate', { method: 'POST' }),
  accountUsers: () => request<any[]>('/api/accounts/users'),
  platformTenants: () => request<any[]>('/api/platform/tenants'),
  createTenant: (body: Record<string, unknown>) => request('/api/platform/tenants', json(body)),
  updateTenant: (id: number, body: Record<string, unknown>) => request('/api/platform/tenants/' + id, { method: 'PUT', body: JSON.stringify(body) }),
  platformTenantUsers: (tenantId: number) => request<any[]>('/api/platform/tenants/' + tenantId + '/users'),
  createTenantAdmin: (tenantId: number, body: Record<string, unknown>) => request('/api/platform/tenants/' + tenantId + '/admins', json(body)),
  deactivatePlatformUser: (userId: number) => request<void>('/api/platform/users/' + userId + '/deactivate', { method: 'POST' }),
  statutoryReport: (periodId: number, type = 'all', csv = false) => request<any>('/api/payroll/statutory?payrollPeriodId=' + periodId + '&type=' + encodeURIComponent(type) + '&csv=' + csv),
  dashboard: () => request<Dashboard>('/api/dashboard'),
  employees: () => request<Employee[]>('/api/employees'),
  createEmployee: (body: Record<string, unknown>) => request<Employee>('/api/employees', json(body)),
  updateEmployee: (id: number, body: Record<string, unknown>) => request<Employee>(`/api/employees/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deactivateEmployee: (id: number) => request<void>(`/api/employees/${id}`, { method: 'DELETE' }),
  organization: () => request<Organization>('/api/organization'),
  createBranch: (body: Record<string, unknown>) => request<Branch>('/api/organization/branches', json(body)),
  createDepartment: (body: Record<string, unknown>) => request<Department>('/api/organization/departments', json(body)),
  updateBranch: (id: number, body: Record<string, unknown>) => request<Branch>(`/api/organization/branches/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteBranch: (id: number) => request<void>(`/api/organization/branches/${id}`, { method: 'DELETE' }),
  updateDepartment: (id: number, body: Record<string, unknown>) => request<Department>(`/api/organization/departments/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteDepartment: (id: number) => request<void>(`/api/organization/departments/${id}`, { method: 'DELETE' }),
  createDesignation: (body: Record<string, unknown>) => request('/api/organization/designations', json(body)),
  updateDesignation: (id: number, body: Record<string, unknown>) => request(`/api/organization/designations/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteDesignation: (id: number) => request<void>(`/api/organization/designations/${id}`, { method: 'DELETE' }),
  createGrade: (body: Record<string, unknown>) => request('/api/organization/grades', json(body)),
  updateGrade: (id: number, body: Record<string, unknown>) => request(`/api/organization/grades/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteGrade: (id: number) => request<void>(`/api/organization/grades/${id}`, { method: 'DELETE' }),
  createEmploymentType: (body: Record<string, unknown>) => request('/api/organization/employment-types', json(body)),
  updateEmploymentType: (id: number, body: Record<string, unknown>) => request(`/api/organization/employment-types/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteEmploymentType: (id: number) => request<void>(`/api/organization/employment-types/${id}`, { method: 'DELETE' }),
  periods: () => request<PayrollPeriod[]>('/api/payroll/periods'),
  transactions: (periodId: number) => request<PayrollTransaction[]>(`/api/payroll/transactions?payrollPeriodId=${periodId}`),
  processPayroll: (body: Record<string, unknown>) => request<PayrollTransaction[]>('/api/payroll/process', json(body)),
  createPayrollPeriod: (body: Record<string, unknown>) => request<PayrollPeriod>('/api/payroll/periods', json(body)),
  lockPayrollPeriod: (id: number) => request<void>(`/api/payroll/periods/${id}/lock`, { method: 'POST' }),
  payrollComponents: (employeeId?: number) => request<PayrollComponent[]>(`/api/payroll/components${employeeId ? `?employeeId=${employeeId}` : ''}`),
  createPayrollComponent: (body: Record<string, unknown>) => request<PayrollComponent>('/api/payroll/components', json(body)),
  updatePayrollComponent: (id: number, body: Record<string, unknown>) => request<PayrollComponent>(`/api/payroll/components/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deletePayrollComponent: (id: number) => request<void>(`/api/payroll/components/${id}`, { method: 'DELETE' }),
  leaveTypes: () => request<LeaveType[]>('/api/leave/types'),
  leaveBalances: () => request<LeaveBalance[]>('/api/leave/balances'),
  leaveRequests: () => request<LeaveRequest[]>('/api/leave/requests'),
  createLeaveRequest: (body: Record<string, unknown>) => request<LeaveRequest>('/api/leave/requests', json(body)),
  updateLeaveStatus: (id: number, status: string) => request<void>(`/api/leave/requests/${id}/status`, { method: 'PATCH', body: JSON.stringify({ status }) }),
  cancelLeaveRequest: (id: number) => request<void>(`/api/leave/requests/${id}/cancel`, { method: 'PATCH' }),
  createLeaveType: (body: Record<string, unknown>) => request<LeaveType>('/api/leave/types', json(body)),
  updateLeaveType: (id: number, body: Record<string, unknown>) => request<LeaveType>(`/api/leave/types/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
  deleteLeaveType: (id: number) => request<void>(`/api/leave/types/${id}`, { method: 'DELETE' }),
  essProfile: () => request<Employee | null>('/api/ess/profile'),
  payslips: () => request<PayrollTransaction[]>('/api/ess/payslips'),
  audit: () => request<AuditLog[]>('/api/audit'),
  reports: () => request<Report[]>('/api/reports'),
  reportData: (id: number) => request<ReportData>(`/api/reports/${id}/data`),
  notifications: () => request<Notification[]>('/api/notifications'),
  markNotificationRead: (id: number) => request<void>(`/api/notifications/${id}/read`, { method: 'PATCH' }),
  markAllNotificationsRead: () => request<void>('/api/notifications/read-all', { method: 'PATCH' }),
  downloadPayslip: async (id: number) => {
    const response = await fetch(`${API_BASE === '/api' ? '/api' : API_BASE}/payslips/${id}/pdf`, { credentials: 'include' })
    if (!response.ok) throw new Error('Could not download payslip.')
    return response.blob()
  },
  attendance: (from?: string, to?: string) => request<Attendance[]>(`/api/attendance?${new URLSearchParams({ ...(from ? { from } : {}), ...(to ? { to } : {}) })}`),
  attendanceSummary: () => request<AttendanceSummary>('/api/attendance/summary'),
  createAttendance: (body: Record<string, unknown>) => request<Attendance>('/api/attendance', json(body)),
  updateAttendance: (id: number, body: Record<string, unknown>) => request<Attendance>(`/api/attendance/${id}`, { method: 'PUT', body: JSON.stringify(body) }),
}
