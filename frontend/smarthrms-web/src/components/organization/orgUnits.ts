import {
  createDepartment,
  deactivateDepartment,
  getDepartmentById,
  getDepartments,
  updateDepartment,
} from '../../services/departmentService'
import {
  createDesignation,
  deactivateDesignation,
  getDesignationById,
  getDesignations,
  updateDesignation,
} from '../../services/designationService'
import type { CreateDepartmentRequest, Department, UpdateDepartmentRequest } from '../../types/department'

/**
 * Departments and designations have identical DTOs and endpoints in the backend, so their pages share one
 * implementation configured by an OrgUnitModule.
 */
export type OrgUnit = Department
export type CreateOrgUnitRequest = CreateDepartmentRequest
export type UpdateOrgUnitRequest = UpdateDepartmentRequest

export interface OrgUnitModule {
  singular: string
  plural: string
  basePath: string
  list: (signal?: AbortSignal) => Promise<OrgUnit[]>
  getById: (id: string, signal?: AbortSignal) => Promise<OrgUnit>
  create: (data: CreateOrgUnitRequest) => Promise<OrgUnit>
  update: (id: string, data: UpdateOrgUnitRequest) => Promise<OrgUnit>
  deactivate: (id: string) => Promise<string>
}

export const departmentModule: OrgUnitModule = {
  singular: 'Department',
  plural: 'Departments',
  basePath: '/departments',
  list: getDepartments,
  getById: getDepartmentById,
  create: createDepartment,
  update: updateDepartment,
  deactivate: deactivateDepartment,
}

export const designationModule: OrgUnitModule = {
  singular: 'Designation',
  plural: 'Designations',
  basePath: '/designations',
  list: getDesignations,
  getById: getDesignationById,
  create: createDesignation,
  update: updateDesignation,
  deactivate: deactivateDesignation,
}
