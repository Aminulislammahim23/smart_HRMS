import { useCallback, useState } from 'react'
import { useApi } from '../../hooks/useApi'
import { useToast } from '../../hooks/useToast'
import { assignManager, getEmployeeById, getEmployees } from '../../services/employeeService'
import { ApiErrorAlert } from '../common/ApiErrorAlert'
import { FormField } from '../common/FormField'
import { Loading } from '../common/Loading'
import { Modal } from '../common/Modal'

/**
 * Sets or clears an employee's line manager, who then reviews their leave. The server refuses the employee
 * themself, former employees, and reporting loops.
 */
export function ManagerModal({ employeeId, onClose, onSaved }: { employeeId: string; onClose: () => void; onSaved?: () => void }) {
  const { notify } = useToast()
  const load = useCallback((signal: AbortSignal) => Promise.all([getEmployeeById(employeeId, signal), getEmployees(signal)]), [employeeId])
  const { data, error: loadError } = useApi(load)
  const [selected, setSelected] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<unknown>(null)

  const [employee, employees] = data ?? [undefined, []]
  const value = selected ?? employee?.managerId ?? ''
  const candidates = employees.filter((e) => e.isActive && e.id !== employeeId)

  const save = async () => {
    setBusy(true)
    setError(null)
    try {
      const updated = await assignManager(employeeId, value || null)
      notify('success', updated.managerName ? `${updated.fullName} now reports to ${updated.managerName}.` : `${updated.fullName} has no manager.`)
      onSaved?.()
      onClose()
    } catch (caught) {
      setError(caught)
    } finally {
      setBusy(false)
    }
  }

  return (
    <Modal open title="Line manager" onClose={onClose} busy={busy}>
      {loadError ? (
        <ApiErrorAlert error={loadError} />
      ) : !employee ? (
        <Loading label="Loading employees…" />
      ) : (
        <div className="flex flex-col gap-3">
          <p className="text-sm text-base-content/70">
            The manager of <span className="font-medium">{employee.fullName}</span> reviews their leave requests. Managers do not see salaries.
          </p>
          {error !== null && <ApiErrorAlert error={error} />}
          <FormField label="Manager" htmlFor="manager-select" hint="The manager's account needs the Manager role to use the approvals page.">
            <select id="manager-select" className="select w-full" value={value} onChange={(e) => setSelected(e.target.value)}>
              <option value="">No manager</option>
              {candidates.map((e) => (
                <option key={e.id} value={e.id}>
                  {e.fullName} ({e.employeeCode})
                </option>
              ))}
            </select>
          </FormField>
          <div className="modal-action">
            <button type="button" className="btn btn-ghost" onClick={onClose} disabled={busy}>
              Cancel
            </button>
            <button type="button" className="btn btn-primary" onClick={save} disabled={busy}>
              {busy && <span className="loading loading-spinner loading-sm" />} Save
            </button>
          </div>
        </div>
      )}
    </Modal>
  )
}
