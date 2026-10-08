import { lazy } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import AuthLayout from '../layouts/AuthLayout'
import DashboardLayout from '../layouts/DashboardLayout'
import type { UserRole } from '../types/auth'
import { ProtectedRoute, RoleRoute } from './ProtectedRoute'

// Pages load on demand so the first screen doesn't download the whole app.
const Login = lazy(() => import('../pages/auth/Login'))
const Dashboard = lazy(() => import('../pages/dashboard/Dashboard'))
const EmployeeList = lazy(() => import('../pages/employees/EmployeeList'))
const EmployeeCreate = lazy(() => import('../pages/employees/EmployeeCreate'))
const EmployeeEdit = lazy(() => import('../pages/employees/EmployeeEdit'))
const EmployeeDetails = lazy(() => import('../pages/employees/EmployeeDetails'))
const DepartmentList = lazy(() => import('../pages/departments/DepartmentList'))
const DepartmentCreate = lazy(() => import('../pages/departments/DepartmentCreate'))
const DepartmentEdit = lazy(() => import('../pages/departments/DepartmentEdit'))
const DesignationList = lazy(() => import('../pages/designations/DesignationList'))
const DesignationCreate = lazy(() => import('../pages/designations/DesignationCreate'))
const DesignationEdit = lazy(() => import('../pages/designations/DesignationEdit'))
const MyProfile = lazy(() => import('../pages/profile/MyProfile'))
const DocumentListPage = lazy(() => import('../pages/documents/DocumentListPage'))
const EmployeeDocumentsPage = lazy(() => import('../pages/documents/EmployeeDocumentsPage'))
const DocumentDetailsPage = lazy(() => import('../pages/documents/DocumentDetailsPage'))
const AttendanceDashboardPage = lazy(() => import('../pages/attendance/AttendanceDashboardPage'))
const AttendanceListPage = lazy(() => import('../pages/attendance/AttendanceListPage'))
const EmployeeAttendancePage = lazy(() => import('../pages/attendance/EmployeeAttendancePage'))
const MyLeavePage = lazy(() => import('../pages/leave/MyLeavePage'))
const LeaveApprovalsPage = lazy(() => import('../pages/leave/LeaveApprovalsPage'))
const PayrollDashboardPage = lazy(() => import('../pages/payroll/PayrollDashboardPage'))
const PayrollPeriodsPage = lazy(() => import('../pages/payroll/PayrollPeriodsPage'))
const PayrollCreatePage = lazy(() => import('../pages/payroll/PayrollCreatePage'))
const PayrollPeriodDetailsPage = lazy(() => import('../pages/payroll/PayrollPeriodDetailsPage'))
const PayrollRecordsPage = lazy(() => import('../pages/payroll/PayrollRecordsPage'))
const PayrollApprovalPage = lazy(() => import('../pages/payroll/PayrollApprovalPage'))
const PayslipPage = lazy(() => import('../pages/payroll/PayslipPage'))
const MyPayrollPage = lazy(() => import('../pages/payroll/MyPayrollPage'))
const SalaryStructuresPage = lazy(() => import('../pages/payroll/SalaryStructuresPage'))
const PayrollHistoryPage = lazy(() => import('../pages/payroll/PayrollHistoryPage'))
const PaymentBatchesPage = lazy(() => import('../pages/payments/PaymentBatchesPage'))
const PaymentBatchDetailsPage = lazy(() => import('../pages/payments/PaymentBatchDetailsPage'))
const PaymentHistoryPage = lazy(() => import('../pages/payments/PaymentHistoryPage'))
const PayrollReportsPage = lazy(() => import('../pages/payroll/PayrollReportsPage'))
const PayrollAuditPage = lazy(() => import('../pages/payroll/PayrollAuditPage'))
const UsersPage = lazy(() => import('../pages/users/UsersPage'))
const NotFound = lazy(() => import('../pages/NotFound'))

const HR: UserRole[] = ['HR', 'Admin']

/** /attendance/me → the signed-in employee's own month view. */
function MyAttendanceRedirect() {
  const { user } = useAuth()
  return <Navigate to={`/attendance/employee/${user?.employeeId}`} replace />
}

/**
 * Route guards decide only what the browser shows. Every API behind these pages checks the same role (and
 * ownership) on the server, so typing a URL can't bypass them.
 */
export function AppRoutes() {
  return (
    <Routes>
      <Route element={<AuthLayout />}>
        <Route path="/login" element={<Login />} />
      </Route>

      <Route element={<ProtectedRoute />}>
        <Route element={<DashboardLayout />}>
          <Route index element={<Navigate to="/dashboard" replace />} />
          <Route path="/dashboard" element={<Dashboard />} />

          {/* Self-service: any signed-in user linked to an employee. */}
          <Route element={<RoleRoute requireEmployee />}>
            <Route path="/profile" element={<MyProfile />} />
            <Route path="/attendance/me" element={<MyAttendanceRedirect />} />
            <Route path="/leave" element={<MyLeavePage />} />
            <Route path="/payroll/my" element={<MyPayrollPage />} />
          </Route>

          {/* The server allows the employee, their manager, and HR/Admin. */}
          <Route path="/attendance/employee/:employeeId" element={<EmployeeAttendancePage />} />
          {/* The server allows the employee (approved/paid only) and HR/Admin. */}
          <Route path="/payroll/payslip/:id" element={<PayslipPage by="record" />} />
          <Route path="/payroll/payslips/:id" element={<PayslipPage by="payslip" />} />

          <Route element={<RoleRoute roles={['Manager', 'HR', 'Admin']} />}>
            <Route path="/leave/approvals" element={<LeaveApprovalsPage />} />
          </Route>

          <Route element={<RoleRoute roles={HR} />}>
            <Route path="/employees" element={<EmployeeList />} />
            <Route path="/employees/create" element={<EmployeeCreate />} />
            <Route path="/employees/:id" element={<EmployeeDetails />} />
            <Route path="/employees/:id/edit" element={<EmployeeEdit />} />

            <Route path="/departments" element={<DepartmentList />} />
            <Route path="/departments/create" element={<DepartmentCreate />} />
            <Route path="/departments/:id/edit" element={<DepartmentEdit />} />

            <Route path="/designations" element={<DesignationList />} />
            <Route path="/designations/create" element={<DesignationCreate />} />
            <Route path="/designations/:id/edit" element={<DesignationEdit />} />

            <Route path="/documents" element={<DocumentListPage />} />
            <Route path="/documents/:employeeId" element={<EmployeeDocumentsPage />} />
            <Route path="/documents/:employeeId/:documentId" element={<DocumentDetailsPage />} />

            <Route path="/attendance" element={<AttendanceDashboardPage />} />
            <Route path="/attendance/records" element={<AttendanceListPage />} />

            <Route path="/payroll" element={<PayrollDashboardPage />} />
            <Route path="/payroll/periods" element={<PayrollPeriodsPage />} />
            <Route path="/payroll/create" element={<PayrollCreatePage />} />
            <Route path="/payroll/salaries" element={<SalaryStructuresPage />} />
            <Route path="/payroll/history" element={<PayrollHistoryPage mode="history" />} />
            <Route path="/payroll/payslips" element={<PayrollHistoryPage key="payslips" mode="payslips" />} />
            <Route path="/payroll/:id" element={<PayrollPeriodDetailsPage />} />
            <Route path="/payroll/:id/records" element={<PayrollRecordsPage />} />
            <Route path="/payroll/:id/approval" element={<PayrollApprovalPage />} />

            <Route path="/payments/batches" element={<PaymentBatchesPage />} />
            <Route path="/payments/batches/:id" element={<PaymentBatchDetailsPage />} />
            <Route path="/payments/history" element={<PaymentHistoryPage />} />
            <Route path="/payroll/reports" element={<PayrollReportsPage />} />
          </Route>

          <Route element={<RoleRoute roles={['Admin']} />}>
            <Route path="/users" element={<UsersPage />} />
            <Route path="/payroll/audit" element={<PayrollAuditPage />} />
          </Route>

          <Route path="*" element={<NotFound />} />
        </Route>
      </Route>
    </Routes>
  )
}
