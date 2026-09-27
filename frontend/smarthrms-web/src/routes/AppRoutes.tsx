import { lazy } from 'react'
import { Navigate, Route, Routes } from 'react-router-dom'
import AuthLayout from '../layouts/AuthLayout'
import DashboardLayout from '../layouts/DashboardLayout'
import { ProtectedRoute } from './ProtectedRoute'

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
const NotFound = lazy(() => import('../pages/NotFound'))

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

          <Route path="/profile" element={<MyProfile />} />
          <Route path="*" element={<NotFound />} />
        </Route>
      </Route>
    </Routes>
  )
}
