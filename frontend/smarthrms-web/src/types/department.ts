/** DepartmentDto */
export interface Department {
  id: string
  name: string
  description: string | null
  isActive: boolean
  /** All employees assigned to the department, whatever their status. */
  employeeCount: number
  createdAt: string
  updatedAt: string | null
}

/** CreateDepartmentDto */
export interface CreateDepartmentRequest {
  name: string
  description: string | null
}

/** UpdateDepartmentDto (isActive is required by the backend). */
export interface UpdateDepartmentRequest extends CreateDepartmentRequest {
  isActive: boolean
}
