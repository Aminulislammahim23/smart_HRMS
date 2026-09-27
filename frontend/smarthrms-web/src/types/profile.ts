import type { EmployeeDocument } from './document'
import type { EmployeeStatus, EmploymentType } from './employee'

export const GENDERS = ['Male', 'Female', 'Other'] as const
export type Gender = (typeof GENDERS)[number]

export const MARITAL_STATUSES = ['Single', 'Married', 'Divorced', 'Widowed', 'Separated'] as const
export type MaritalStatus = (typeof MARITAL_STATUSES)[number]

export const BLOOD_GROUPS = [
  'APositive',
  'ANegative',
  'BPositive',
  'BNegative',
  'ABPositive',
  'ABNegative',
  'OPositive',
  'ONegative',
] as const
export type BloodGroup = (typeof BLOOD_GROUPS)[number]

export const ADDRESS_TYPES = ['Present', 'Permanent'] as const
export type AddressType = (typeof ADDRESS_TYPES)[number]

/** Every employee-owned profile record has these fields. */
export interface EmployeeOwnedRecord {
  id: string
  employeeId: string
  createdAt: string
  updatedAt: string | null
}

/** EmployeePersonalDetailsDto: the full, unmasked NID and passport number. */
export interface PersonalDetails extends EmployeeOwnedRecord {
  /** Read-only here; stored on the employee record. */
  dateOfBirth: string
  gender: Gender | null
  maritalStatus: MaritalStatus | null
  bloodGroup: BloodGroup | null
  nationality: string | null
  nationalId: string | null
  passportNo: string | null
}

/** Create/UpdateEmployeePersonalDetailsDto. An update replaces every field. */
export interface SavePersonalDetailsRequest {
  gender: Gender | null
  maritalStatus: MaritalStatus | null
  bloodGroup: BloodGroup | null
  nationality: string | null
  nationalId: string | null
  passportNo: string | null
}

/** EmployeeAddressDto */
export interface Address extends EmployeeOwnedRecord {
  addressType: AddressType
  address: string
  city: string
  district: string
  postalCode: string | null
}

export interface SaveAddressRequest {
  addressType: AddressType
  address: string
  city: string
  district: string
  postalCode: string | null
}

/** EmployeeEmergencyContactDto */
export interface EmergencyContact extends EmployeeOwnedRecord {
  name: string
  relationship: string
  phone: string
  email: string | null
  address: string | null
}

export interface SaveEmergencyContactRequest {
  name: string
  relationship: string
  phone: string
  email: string | null
  address: string | null
}

/** EmployeeEducationDto */
export interface Education extends EmployeeOwnedRecord {
  degree: string
  institution: string
  major: string | null
  result: string | null
  passingYear: number
}

export interface SaveEducationRequest {
  degree: string
  institution: string
  major: string | null
  result: string | null
  passingYear: number
}

/** EmployeeExperienceDto */
export interface Experience extends EmployeeOwnedRecord {
  companyName: string
  designation: string
  startDate: string
  endDate: string | null
  /** True when endDate is null (ongoing job). */
  isCurrent: boolean
  responsibilities: string | null
}

export interface SaveExperienceRequest {
  companyName: string
  designation: string
  startDate: string
  endDate: string | null
  responsibilities: string | null
}

/** EmployeePersonalInformationDto. The NID and passport number are masked here. */
export interface PersonalInformation {
  dateOfBirth: string
  phone: string | null
  email: string
  hasPersonalDetails: boolean
  gender: Gender | null
  maritalStatus: MaritalStatus | null
  bloodGroup: BloodGroup | null
  nationality: string | null
  nationalId: string | null
  passportNo: string | null
}

/** EmployeeJobInformationDto */
export interface JobInformation {
  departmentId: string
  departmentName: string | null
  designationId: string
  designationName: string | null
  joiningDate: string
  employmentType: EmploymentType
  employmentStatus: EmployeeStatus
  basicSalary: number | null
}

/** EmployeeProfileDto: GET /api/employees/{id}/profile */
export interface EmployeeProfile {
  employeeId: string
  employeeCode: string
  fullName: string
  firstName: string
  lastName: string
  photoUrl: string | null
  isActive: boolean
  personalInformation: PersonalInformation
  jobInformation: JobInformation
  /** Present first, then Permanent. */
  addresses: Address[]
  emergencyContacts: EmergencyContact[]
  /** Most recent first. */
  educations: Education[]
  /** Most recent first. */
  experiences: Experience[]
  /** Active documents only, newest first. */
  documents: EmployeeDocument[]
  createdAt: string
  updatedAt: string | null
}
