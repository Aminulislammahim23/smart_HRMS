export const EMPLOYEE_STATUSES = ['Active', 'OnLeave', 'Inactive', 'Resigned', 'Terminated'] as const
export type EmployeeStatus = (typeof EMPLOYEE_STATUSES)[number]

export const EMPLOYMENT_TYPES = ['FullTime', 'PartTime', 'Contract', 'Intern'] as const
export type EmploymentType = (typeof EMPLOYMENT_TYPES)[number]

/** EmployeeDto */
export interface Employee {
  id: string
  employeeCode: string
  firstName: string
  lastName: string
  fullName: string
  email: string
  phone: string | null
  dateOfBirth: string
  joiningDate: string
  departmentId: string
  departmentName: string | null
  designationId: string
  designationName: string | null
  employmentType: EmploymentType
  basicSalary: number | null
  status: EmployeeStatus
  /** True while status is Active or OnLeave. */
  isActive: boolean
  /** Relative URL (e.g. /uploads/employees/x.jpg) or null. */
  photoUrl: string | null
  createdAt: string
  updatedAt: string | null
}

/** Fields shared by CreateEmployeeDto and UpdateEmployeeDto. Dates are sent as yyyy-MM-dd. */
interface EmployeeRequestBase {
  firstName: string
  lastName: string
  email: string
  phone: string | null
  dateOfBirth: string
  joiningDate: string
  departmentId: string
  designationId: string
  employmentType: EmploymentType | null
  basicSalary: number | null
}

/** CreateEmployeeDto */
export interface CreateEmployeeRequest extends EmployeeRequestBase {
  employeeCode: string
}

/** UpdateEmployeeDto. The employee code can't be changed; a null salary/type/status keeps the current value. */
export interface UpdateEmployeeRequest extends EmployeeRequestBase {
  status: EmployeeStatus | null
}
