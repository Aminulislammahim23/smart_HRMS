import { Pencil, Plus, Trash2, type LucideIcon } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { useToast } from '../../hooks/useToast'
import type { ProfileRecordService } from '../../services/profileService'
import type { EmployeeOwnedRecord } from '../../types/profile'
import { ConfirmDialog } from '../common/ConfirmDialog'
import { EmptyState } from '../common/EmptyState'
import { Modal } from '../common/Modal'

export interface RecordFormProps<TRecord, TSave> {
  /** Absent when adding a new record. */
  initial?: TRecord
  onSubmit: (data: TSave) => Promise<void>
  onCancel: () => void
}

interface ProfileRecordSectionProps<TRecord extends EmployeeOwnedRecord, TSave> {
  employeeId: string
  /** Singular, lower case: "address", "emergency contact", ... */
  noun: string
  title: string
  description: string
  icon: LucideIcon
  items: TRecord[]
  service: ProfileRecordService<TRecord, TSave>
  describe: (record: TRecord) => string
  renderItem: (record: TRecord) => ReactNode
  renderForm: (props: RecordFormProps<TRecord, TSave>) => ReactNode
  onChanged: () => void
  /** Explains why nothing more can be added (e.g. both address types exist). */
  addBlockedReason?: string
}

type Editing<TRecord> = { record?: TRecord } | null

/**
 * List + add/edit/delete for one of the multi-record profile sections. Records come from the profile response;
 * after a change the page reloads the profile.
 */
export function ProfileRecordSection<TRecord extends EmployeeOwnedRecord, TSave>({
  employeeId,
  noun,
  title,
  description,
  icon,
  items,
  service,
  describe,
  renderItem,
  renderForm,
  onChanged,
  addBlockedReason,
}: ProfileRecordSectionProps<TRecord, TSave>) {
  const { notify } = useToast()
  const [editing, setEditing] = useState<Editing<TRecord>>(null)
  const [deleting, setDeleting] = useState<TRecord | null>(null)
  const capitalized = noun.charAt(0).toUpperCase() + noun.slice(1)

  const save = async (data: TSave) => {
    const record = editing?.record
    if (record) {
      await service.update(employeeId, record.id, data)
      notify('success', `${capitalized} updated.`)
    } else {
      await service.create(employeeId, data)
      notify('success', `${capitalized} added.`)
    }
    setEditing(null)
    onChanged()
  }

  const addButton = (
    <button type="button" className="btn btn-primary btn-sm" onClick={() => setEditing({})} disabled={!!addBlockedReason} title={addBlockedReason}>
      <Plus className="size-4" /> Add {noun}
    </button>
  )

  return (
    <div className="card bg-base-100 shadow-sm">
      <div className="card-body gap-4">
        <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <h2 className="font-semibold">{title}</h2>
            <p className="text-sm text-base-content/60">{addBlockedReason ?? description}</p>
          </div>
          {items.length > 0 && addButton}
        </div>

        {items.length === 0 ? (
          <EmptyState compact icon={icon} title={`No ${noun} added yet`} action={addButton} />
        ) : (
          <ul className="grid gap-3 md:grid-cols-2">
            {items.map((record) => (
              <li key={record.id} className="rounded-box border border-base-300 p-4">
                <div className="flex items-start justify-between gap-3">
                  <div className="min-w-0 flex-1">{renderItem(record)}</div>
                  <div className="flex shrink-0 gap-1">
                    <button type="button" className="btn btn-ghost btn-xs btn-square" onClick={() => setEditing({ record })} aria-label={`Edit ${describe(record)}`}>
                      <Pencil className="size-3.5" />
                    </button>
                    <button type="button" className="btn btn-ghost btn-xs btn-square text-error" onClick={() => setDeleting(record)} aria-label={`Delete ${describe(record)}`}>
                      <Trash2 className="size-3.5" />
                    </button>
                  </div>
                </div>
              </li>
            ))}
          </ul>
        )}
      </div>

      <Modal open={editing !== null} title={editing?.record ? `Edit ${noun}` : `Add ${noun}`} onClose={() => setEditing(null)} size="lg">
        {editing !== null && renderForm({ initial: editing.record, onSubmit: save, onCancel: () => setEditing(null) })}
      </Modal>

      <ConfirmDialog
        open={deleting !== null}
        title={`Delete ${noun}?`}
        message={`"${deleting ? describe(deleting) : ''}" will be permanently deleted. This cannot be undone.`}
        confirmLabel="Delete"
        onCancel={() => setDeleting(null)}
        onConfirm={async () => {
          if (!deleting) return
          await service.remove(employeeId, deleting.id)
          notify('success', `${capitalized} deleted.`)
          setDeleting(null)
          onChanged()
        }}
      />
    </div>
  )
}
