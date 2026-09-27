import { z } from 'zod'
import {
  EMPLOYEE_STATUSES,
  EMPLOYMENT_TYPES,
  type CreateEmployeeRequest,
  type Employee,
  type UpdateEmployeeRequest,
} from '../../types/employee'
import { blankToNull, toDateInput } from '../../utils/formatters'
import {
  optionalAmount,
  optionalPhone,
  pastDate,
  requiredDate,
  requiredEmail,
  requiredSelect,
  requiredText,
} from '../../utils/validators'

export type EmployeeFormMode = 'create' | 'edit'

/** Mirrors CreateEmployeeDto / UpdateEmployeeDto and the date rules in EmployeeService. */
export function employeeFormSchema(mode: EmployeeFormMode) {
  return z
    .object({
      // The code can't change after creation, so it is only validated when creating.
      employeeCode: mode === 'create' ? requiredText('Employee code', 50) : z.string(),
      firstName: requiredText('First name', 100),
      lastName: requiredText('Last name', 100),
      email: requiredEmail(200),
      phone: optionalPhone(30),
      dateOfBirth: pastDate('Date of birth'),
      joiningDate: requiredDate('Joining date'),
      departmentId: requiredSelect('Department'),
      designationId: requiredSelect('Designation'),
      employmentType: z.enum(EMPLOYMENT_TYPES),
      basicSalary: optionalAmount,
      status: z.enum(EMPLOYEE_STATUSES),
    })
    .refine((values) => !values.dateOfBirth || !values.joiningDate || values.joiningDate >= values.dateOfBirth, {
      path: ['joiningDate'],
      message: 'The joining date cannot be earlier than the date of birth.',
    })
}

export type EmployeeFormValues = z.infer<ReturnType<typeof employeeFormSchema>>

export function toFormValues(employee?: Employee): EmployeeFormValues {
  return {
    employeeCode: employee?.employeeCode ?? '',
    firstName: employee?.firstName ?? '',
    lastName: employee?.lastName ?? '',
    email: employee?.email ?? '',
    phone: employee?.phone ?? '',
    dateOfBirth: toDateInput(employee?.dateOfBirth),
    joiningDate: toDateInput(employee?.joiningDate),
    departmentId: employee?.departmentId ?? '',
    designationId: employee?.designationId ?? '',
    employmentType: employee?.employmentType ?? 'FullTime',
    basicSalary: employee?.basicSalary?.toString() ?? '',
    status: employee?.status ?? 'Active',
  }
}

function toRequestBase(values: EmployeeFormValues) {
  return {
    firstName: values.firstName,
    lastName: values.lastName,
    email: values.email,
    phone: blankToNull(values.phone),
    dateOfBirth: values.dateOfBirth,
    joiningDate: values.joiningDate,
    departmentId: values.departmentId,
    designationId: values.designationId,
    employmentType: values.employmentType,
    basicSalary: values.basicSalary ? Number(values.basicSalary) : null,
  }
}

export function toCreateRequest(values: EmployeeFormValues): CreateEmployeeRequest {
  return { ...toRequestBase(values), employeeCode: values.employeeCode.trim() }
}

export function toUpdateRequest(values: EmployeeFormValues): UpdateEmployeeRequest {
  return { ...toRequestBase(values), status: values.status }
}
