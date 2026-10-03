import { IdCard, Pencil, Plus, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { useToast } from '../../hooks/useToast'
import {
  createPersonalDetails,
  deletePersonalDetails,
  getPersonalDetails,
  updatePersonalDetails,
} from '../../services/profileService'
import type { PersonalDetails, PersonalInformation, SavePersonalDetailsRequest } from '../../types/profile'
import { errorMessage } from '../../utils/errors'
import { enumLabel, formatDate } from '../../utils/formatters'
import { ConfirmDialog } from '../common/ConfirmDialog'
import { DetailList } from '../common/DetailList'
import { EmptyState } from '../common/EmptyState'
import { Modal } from '../common/Modal'
import { PersonalDetailsForm } from './PersonalDetailsForm'
import { useCanEditProfile } from './profileEditing'

interface PersonalDetailsSectionProps {
  employeeId: string
  info: PersonalInformation
  onChanged: () => void
}

type Editing = { details?: PersonalDetails } | null

/**
 * One personal-details record per employee. The profile shows the NID and passport masked; the full values are
 * fetched from /personal-details only when the user opens the edit form.
 */
export function PersonalDetailsSection({ employeeId, info, onChanged }: PersonalDetailsSectionProps) {
  const canEdit = useCanEditProfile()
  const { notify } = useToast()
  const [editing, setEditing] = useState<Editing>(null)
  const [loadingDetails, setLoadingDetails] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)

  const openEdit = async () => {
    setLoadingDetails(true)
    try {
      setEditing({ details: await getPersonalDetails(employeeId) })
    } catch (error) {
      notify('error', errorMessage(error))
    } finally {
      setLoadingDetails(false)
    }
  }

  const save = async (data: SavePersonalDetailsRequest) => {
    if (editing?.details) {
      await updatePersonalDetails(employeeId, data)
      notify('success', 'Personal details updated.')
    } else {
      await createPersonalDetails(employeeId, data)
      notify('success', 'Personal details added.')
    }
    setEditing(null)
    onChanged()
  }

  return (
    <div className="card bg-base-100 shadow-sm">
      <div className="card-body gap-4">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <h2 className="font-semibold">Personal details</h2>
            <p className="text-sm text-base-content/60">Gender, marital status, blood group, nationality and identity documents.</p>
          </div>
          {canEdit && info.hasPersonalDetails && (
            <div className="flex gap-2">
              <button type="button" className="btn btn-sm" onClick={openEdit} disabled={loadingDetails}>
                {loadingDetails ? <span className="loading loading-spinner loading-xs" /> : <Pencil className="size-4" />} Edit
              </button>
              <button type="button" className="btn btn-sm btn-ghost text-error" onClick={() => setConfirmDelete(true)}>
                <Trash2 className="size-4" /> Delete
              </button>
            </div>
          )}
        </div>

        {info.hasPersonalDetails ? (
          <DetailList
            columns={3}
            items={[
              { label: 'Date of birth', value: formatDate(info.dateOfBirth) },
              { label: 'Gender', value: info.gender },
              { label: 'Marital status', value: info.maritalStatus },
              { label: 'Blood group', value: info.bloodGroup && enumLabel(info.bloodGroup) },
              { label: 'Nationality', value: info.nationality },
              { label: 'National ID', value: info.nationalId && <span className="font-mono">{info.nationalId}</span> },
              { label: 'Passport number', value: info.passportNo && <span className="font-mono">{info.passportNo}</span> },
            ]}
          />
        ) : (
          <EmptyState
            compact
            icon={IdCard}
            title="No personal details yet"
            action={
              canEdit ? (
                <button type="button" className="btn btn-primary btn-sm" onClick={() => setEditing({})}>
                  <Plus className="size-4" /> Add personal details
                </button>
              ) : undefined
            }
          />
        )}
        {info.hasPersonalDetails && (
          <p className="text-xs text-base-content/50">National ID and passport number are masked. Open Edit to see the full values.</p>
        )}
      </div>

      <Modal open={editing !== null} title={editing?.details ? 'Edit personal details' : 'Add personal details'} onClose={() => setEditing(null)} size="lg">
        {editing !== null && <PersonalDetailsForm initial={editing.details} onSubmit={save} onCancel={() => setEditing(null)} />}
      </Modal>

      <ConfirmDialog
        open={confirmDelete}
        title="Delete personal details?"
        message="Gender, marital status, blood group, nationality, National ID and passport number will be permanently deleted."
        confirmLabel="Delete"
        onCancel={() => setConfirmDelete(false)}
        onConfirm={async () => {
          await deletePersonalDetails(employeeId)
          notify('success', 'Personal details deleted.')
          setConfirmDelete(false)
          onChanged()
        }}
      />
    </div>
  )
}
