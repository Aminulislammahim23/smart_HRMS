import type { ApiResponse } from '../types/api'
import type {
  Address,
  Education,
  EmergencyContact,
  Experience,
  PersonalDetails,
  SaveAddressRequest,
  SaveEducationRequest,
  SaveEmergencyContactRequest,
  SaveExperienceRequest,
  SavePersonalDetailsRequest,
} from '../types/profile'
import { api, unwrap, unwrapMessage } from './api'

// ---- Personal details: one record per employee at /api/employees/{id}/personal-details ----

/** Returns the full (unmasked) details. The backend answers 404 when none have been added yet. */
export function getPersonalDetails(employeeId: string, signal?: AbortSignal): Promise<PersonalDetails> {
  return unwrap(api.get<ApiResponse<PersonalDetails>>(`/employees/${employeeId}/personal-details`, { signal }))
}

export function createPersonalDetails(employeeId: string, data: SavePersonalDetailsRequest): Promise<PersonalDetails> {
  return unwrap(api.post<ApiResponse<PersonalDetails>>(`/employees/${employeeId}/personal-details`, data))
}

export function updatePersonalDetails(employeeId: string, data: SavePersonalDetailsRequest): Promise<PersonalDetails> {
  return unwrap(api.put<ApiResponse<PersonalDetails>>(`/employees/${employeeId}/personal-details`, data))
}

export function deletePersonalDetails(employeeId: string): Promise<string> {
  return unwrapMessage(api.delete<ApiResponse<unknown>>(`/employees/${employeeId}/personal-details`))
}

// ---- Multi-record sections: addresses, emergency contacts, educations, experiences ----

/** CRUD for one of the /api/employees/{id}/{resource} collections. Deletes are permanent. */
export interface ProfileRecordService<TRecord, TSave> {
  list(employeeId: string, signal?: AbortSignal): Promise<TRecord[]>
  create(employeeId: string, data: TSave): Promise<TRecord>
  update(employeeId: string, id: string, data: TSave): Promise<TRecord>
  remove(employeeId: string, id: string): Promise<string>
}

function recordService<TRecord, TSave>(resource: string): ProfileRecordService<TRecord, TSave> {
  const base = (employeeId: string) => `/employees/${employeeId}/${resource}`
  return {
    list: (employeeId, signal) => unwrap(api.get<ApiResponse<TRecord[]>>(base(employeeId), { signal })),
    create: (employeeId, data) => unwrap(api.post<ApiResponse<TRecord>>(base(employeeId), data)),
    update: (employeeId, id, data) => unwrap(api.put<ApiResponse<TRecord>>(`${base(employeeId)}/${id}`, data)),
    remove: (employeeId, id) => unwrapMessage(api.delete<ApiResponse<unknown>>(`${base(employeeId)}/${id}`)),
  }
}

export const addressService = recordService<Address, SaveAddressRequest>('addresses')
export const emergencyContactService = recordService<EmergencyContact, SaveEmergencyContactRequest>('emergency-contacts')
export const educationService = recordService<Education, SaveEducationRequest>('educations')
export const experienceService = recordService<Experience, SaveExperienceRequest>('experiences')
