import type { ReactNode } from 'react'
import type { EmployeeProfile } from '../../types/profile'
import { enumLabel, formatAmount, formatDate, formatDateTime } from '../../utils/formatters'
import { DetailList } from '../common/DetailList'
import { EmployeeStatusBadge } from '../common/StatusBadge'

function Card({ title, children }: { title: string; children: ReactNode }) {
  return (
    <div className="card bg-base-100 shadow-sm">
      <div className="card-body gap-4">
        <h2 className="font-semibold">{title}</h2>
        {children}
      </div>
    </div>
  )
}

/** Overview of the employee record: contact, personal summary, employment, and record history. */
export function EmployeeDetailsCard({ profile }: { profile: EmployeeProfile }) {
  const { personalInformation: personal, jobInformation: job } = profile
  const presentAddress = profile.addresses.find((address) => address.addressType === 'Present')

  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <Card title="Contact information">
        <DetailList
          items={[
            { label: 'Email', value: <a className="link link-hover" href={`mailto:${personal.email}`}>{personal.email}</a> },
            { label: 'Phone', value: personal.phone && <a className="link link-hover" href={`tel:${personal.phone}`}>{personal.phone}</a> },
            {
              label: 'Present address',
              value: presentAddress && `${presentAddress.address}, ${presentAddress.city}, ${presentAddress.district}`,
            },
            {
              label: 'Emergency contact',
              value: profile.emergencyContacts[0] && `${profile.emergencyContacts[0].name} (${profile.emergencyContacts[0].relationship}) · ${profile.emergencyContacts[0].phone}`,
            },
          ]}
        />
      </Card>

      <Card title="Personal information">
        <DetailList
          items={[
            { label: 'Date of birth', value: formatDate(personal.dateOfBirth) },
            { label: 'Gender', value: personal.gender },
            { label: 'Marital status', value: personal.maritalStatus },
            { label: 'Blood group', value: personal.bloodGroup && enumLabel(personal.bloodGroup) },
            { label: 'Nationality', value: personal.nationality },
            { label: 'National ID', value: personal.nationalId && <span className="font-mono">{personal.nationalId}</span> },
          ]}
        />
      </Card>

      <Card title="Employment information">
        <DetailList
          items={[
            { label: 'Employee code', value: profile.employeeCode },
            { label: 'Status', value: <EmployeeStatusBadge status={job.employmentStatus} /> },
            { label: 'Department', value: job.departmentName },
            { label: 'Designation', value: job.designationName },
            { label: 'Joining date', value: formatDate(job.joiningDate) },
            { label: 'Employment type', value: enumLabel(job.employmentType) },
            { label: 'Basic salary (monthly)', value: formatAmount(job.basicSalary) },
          ]}
        />
      </Card>

      <Card title="Record">
        <DetailList
          items={[
            { label: 'Created', value: formatDateTime(profile.createdAt) },
            { label: 'Last updated', value: formatDateTime(profile.updatedAt) },
            { label: 'Education records', value: profile.educations.length },
            { label: 'Work experience records', value: profile.experiences.length },
            { label: 'Active documents', value: profile.documents.length },
          ]}
        />
      </Card>
    </div>
  )
}
