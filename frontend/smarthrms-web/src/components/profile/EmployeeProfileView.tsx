import {
  Briefcase,
  CalendarDays,
  FileText,
  GraduationCap,
  IdCard,
  LayoutGrid,
  Mail,
  MapPin,
  Phone,
  PhoneCall,
  type LucideIcon,
} from 'lucide-react'
import type { ReactNode } from 'react'
import { useSearchParams } from 'react-router-dom'
import { addressService, educationService, emergencyContactService, experienceService } from '../../services/profileService'
import type { EmployeeProfile } from '../../types/profile'
import { enumLabel, formatDate, toDateInput } from '../../utils/formatters'
import { EmployeeStatusBadge } from '../common/StatusBadge'
import { EmployeeDetailsCard } from '../employee/EmployeeDetailsCard'
import { EmployeePhoto } from '../employee/EmployeePhoto'
import { AddressForm } from './AddressForm'
import { DocumentsSection } from './DocumentsSection'
import { EducationForm } from './EducationForm'
import { EmergencyContactForm } from './EmergencyContactForm'
import { ExperienceForm } from './ExperienceForm'
import { PersonalDetailsSection } from './PersonalDetailsSection'
import { ProfileRecordSection } from './ProfileRecordSection'
import { ProfileEditingContext } from './profileEditing'

type TabId = 'overview' | 'personal' | 'addresses' | 'contacts' | 'education' | 'experience' | 'documents'

interface Tab {
  id: TabId
  label: string
  icon: LucideIcon
  count?: (profile: EmployeeProfile) => number
}

const TABS: Tab[] = [
  { id: 'overview', label: 'Overview', icon: LayoutGrid },
  { id: 'personal', label: 'Personal details', icon: IdCard },
  { id: 'addresses', label: 'Addresses', icon: MapPin, count: (p) => p.addresses.length },
  { id: 'contacts', label: 'Emergency contacts', icon: PhoneCall, count: (p) => p.emergencyContacts.length },
  { id: 'education', label: 'Education', icon: GraduationCap, count: (p) => p.educations.length },
  { id: 'experience', label: 'Experience', icon: Briefcase, count: (p) => p.experiences.length },
  { id: 'documents', label: 'Documents', icon: FileText, count: (p) => p.documents.length },
]

interface EmployeeProfileViewProps {
  profile: EmployeeProfile
  /** Reloads the profile after any section changes. */
  onChanged: () => void
  actions?: ReactNode
  /** Hides every add/edit/delete control (the API refuses those changes for non-HR users anyway). */
  readOnly?: boolean
}

function Meta({ icon: Icon, children }: { icon: LucideIcon; children: ReactNode }) {
  return (
    <span className="inline-flex items-center gap-1.5">
      <Icon className="size-4 opacity-60" />
      {children}
    </span>
  )
}

export function EmployeeProfileView({ profile, onChanged, actions, readOnly = false }: EmployeeProfileViewProps) {
  const [params, setParams] = useSearchParams()
  const requestedTab = params.get('tab')
  const tab: TabId = TABS.some((t) => t.id === requestedTab) ? (requestedTab as TabId) : 'overview'
  const employeeId = profile.employeeId
  const { personalInformation: personal, jobInformation: job } = profile

  const selectTab = (id: TabId) => {
    setParams(
      (current) => {
        const next = new URLSearchParams(current)
        if (id === 'overview') next.delete('tab')
        else next.set('tab', id)
        return next
      },
      { replace: true },
    )
  }

  const content = (
    <div className="flex flex-col gap-6">
      <div className="card bg-base-100 shadow-sm">
        <div className="card-body flex-col gap-5 sm:flex-row sm:items-center">
          <EmployeePhoto photoUrl={profile.photoUrl} firstName={profile.firstName} lastName={profile.lastName} size="xl" />
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <h1 className="text-2xl font-semibold tracking-tight">{profile.fullName}</h1>
              <EmployeeStatusBadge status={job.employmentStatus} />
            </div>
            <p className="text-base-content/70">
              {job.designationName ?? 'No designation'} · {job.departmentName ?? 'No department'}
            </p>
            <div className="mt-3 flex flex-wrap gap-x-5 gap-y-1 text-sm text-base-content/70">
              <Meta icon={IdCard}>{profile.employeeCode}</Meta>
              <Meta icon={Mail}>{personal.email}</Meta>
              {personal.phone && <Meta icon={Phone}>{personal.phone}</Meta>}
              <Meta icon={CalendarDays}>
                Joined {formatDate(job.joiningDate)} · {enumLabel(job.employmentType)}
              </Meta>
            </div>
          </div>
          {actions && <div className="flex flex-wrap gap-2 sm:self-start">{actions}</div>}
        </div>
      </div>

      <div className="overflow-x-auto">
        <div role="tablist" className="tabs tabs-box w-max bg-base-100 shadow-sm">
          {TABS.map(({ id, label, icon: Icon, count }) => (
            <button
              key={id}
              type="button"
              role="tab"
              aria-selected={tab === id}
              className={`tab gap-2 whitespace-nowrap ${tab === id ? 'tab-active' : ''}`}
              onClick={() => selectTab(id)}
            >
              <Icon className="size-4" />
              {label}
              {count && <span className="badge badge-sm">{count(profile)}</span>}
            </button>
          ))}
        </div>
      </div>

      <div role="tabpanel">
        {tab === 'overview' && <EmployeeDetailsCard profile={profile} />}

        {tab === 'personal' && <PersonalDetailsSection employeeId={employeeId} info={personal} onChanged={onChanged} />}

        {tab === 'addresses' && (
          <ProfileRecordSection
            employeeId={employeeId}
            noun="address"
            title="Addresses"
            description="Present and permanent address."
            icon={MapPin}
            items={profile.addresses}
            service={addressService}
            onChanged={onChanged}
            addBlockedReason={profile.addresses.length >= 2 ? 'Both present and permanent addresses are recorded.' : undefined}
            describe={(address) => `${address.addressType} address`}
            renderItem={(address) => (
              <>
                <span className="badge badge-primary badge-soft badge-sm">{address.addressType}</span>
                <p className="mt-2 text-sm">{address.address}</p>
                <p className="text-sm text-base-content/70">
                  {address.city}, {address.district}
                  {address.postalCode && ` ${address.postalCode}`}
                </p>
              </>
            )}
            renderForm={(props) => {
              const usedTypes = profile.addresses.map((a) => a.addressType)
              // Remount when the reloaded profile changes the used types, so the default type is never a taken one.
              return <AddressForm key={usedTypes.join()} {...props} usedTypes={usedTypes} />
            }}
          />
        )}

        {tab === 'contacts' && (
          <ProfileRecordSection
            employeeId={employeeId}
            noun="emergency contact"
            title="Emergency contacts"
            description="People to contact in an emergency."
            icon={PhoneCall}
            items={profile.emergencyContacts}
            service={emergencyContactService}
            onChanged={onChanged}
            describe={(contact) => contact.name}
            renderItem={(contact) => (
              <>
                <p className="font-medium">{contact.name}</p>
                <p className="text-sm text-base-content/70">{contact.relationship}</p>
                <p className="mt-2 text-sm">{contact.phone}</p>
                {contact.email && <p className="text-sm">{contact.email}</p>}
                {contact.address && <p className="text-sm text-base-content/70">{contact.address}</p>}
              </>
            )}
            renderForm={(props) => <EmergencyContactForm {...props} />}
          />
        )}

        {tab === 'education' && (
          <ProfileRecordSection
            employeeId={employeeId}
            noun="education record"
            title="Education"
            description="Academic qualifications, most recent first."
            icon={GraduationCap}
            items={profile.educations}
            service={educationService}
            onChanged={onChanged}
            describe={(education) => `${education.degree}, ${education.institution}`}
            renderItem={(education) => (
              <>
                <p className="font-medium">
                  {education.degree}
                  {education.major && <span className="font-normal text-base-content/70"> · {education.major}</span>}
                </p>
                <p className="text-sm text-base-content/70">{education.institution}</p>
                <p className="mt-2 text-sm">
                  {education.passingYear}
                  {education.result && ` · ${education.result}`}
                </p>
              </>
            )}
            renderForm={(props) => <EducationForm {...props} birthYear={Number(personal.dateOfBirth.slice(0, 4))} />}
          />
        )}

        {tab === 'experience' && (
          <ProfileRecordSection
            employeeId={employeeId}
            noun="work experience"
            title="Work experience"
            description="Previous and current jobs, most recent first."
            icon={Briefcase}
            items={profile.experiences}
            service={experienceService}
            onChanged={onChanged}
            describe={(experience) => `${experience.designation} at ${experience.companyName}`}
            renderItem={(experience) => (
              <>
                <p className="font-medium">{experience.designation}</p>
                <p className="text-sm text-base-content/70">{experience.companyName}</p>
                <p className="mt-2 text-sm">
                  {formatDate(experience.startDate)} – {experience.isCurrent ? <span className="badge badge-success badge-soft badge-sm">Present</span> : formatDate(experience.endDate)}
                </p>
                {experience.responsibilities && <p className="mt-2 line-clamp-3 text-sm text-base-content/70">{experience.responsibilities}</p>}
              </>
            )}
            renderForm={(props) => <ExperienceForm {...props} dateOfBirth={toDateInput(personal.dateOfBirth)} />}
          />
        )}

        {tab === 'documents' && <DocumentsSection employeeId={employeeId} onChanged={onChanged} />}
      </div>
    </div>
  )

  return <ProfileEditingContext.Provider value={!readOnly}>{content}</ProfileEditingContext.Provider>
}
