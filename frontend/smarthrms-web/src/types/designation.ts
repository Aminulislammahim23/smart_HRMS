/**
 * DesignationDto. Designations are independent of departments in the backend (there is no department link),
 * so the two are only combined on an employee.
 */
export interface Designation {
  id: string
  name: string
  description: string | null
  isActive: boolean
  /** All employees assigned to the designation, whatever their status. */
  employeeCount: number
  createdAt: string
  updatedAt: string | null
}

/** CreateDesignationDto */
export interface CreateDesignationRequest {
  name: string
  description: string | null
}

/** UpdateDesignationDto (isActive is required by the backend). */
export interface UpdateDesignationRequest extends CreateDesignationRequest {
  isActive: boolean
}
