import { CalendarDays, Pencil, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useToast } from '../../hooks/useToast'
import { deleteAttendance, updateAttendance } from '../../services/attendanceService'
import type { Attendance } from '../../types/attendance'
import { formatDate, formatDateTime, formatDuration, formatTime } from '../../utils/formatters'
import { ConfirmDialog } from '../common/ConfirmDialog'
import { DetailList } from '../common/DetailList'
import { Modal } from '../common/Modal'
import { AttendanceForm } from './AttendanceForm'
import { AttendanceStatusBadge } from './AttendanceStatusBadge'

interface AttendanceDetailsModalProps {
  record: Attendance
  onClose: () => void
  /** Called after a successful edit or delete so the page can reload its data. */
  onChanged: () => void
}

/** Details of one attendance day, with HR correction (edit) and delete. */
export function AttendanceDetailsModal({ record, onClose, onChanged }: AttendanceDetailsModalProps) {
  const { notify } = useToast()
  const [editing, setEditing] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)

  if (confirmDelete) {
    return (
      <ConfirmDialog
        open
        title="Delete attendance record?"
        message={`The ${formatDate(record.attendanceDate)} record of ${record.employeeName} will be permanently deleted.`}
        confirmLabel="Delete"
        onCancel={() => setConfirmDelete(false)}
        onConfirm={async () => {
          await deleteAttendance(record.id)
          notify('success', 'Attendance record deleted.')
          onChanged()
          onClose()
        }}
      />
    )
  }

  return (
    <Modal open title={editing ? 'Correct attendance' : 'Attendance details'} onClose={onClose} size="lg">
      <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
        <div>
          <p className="font-semibold">{record.employeeName}</p>
          <p className="text-sm text-base-content/60">
            {record.employeeCode} · {formatDate(record.attendanceDate)}
          </p>
        </div>
        <AttendanceStatusBadge status={record.status} />
      </div>

      {editing ? (
        <AttendanceForm
          initial={record}
          onCancel={() => setEditing(false)}
          onSubmit={async ({ status, checkInTime, checkOutTime, remarks }) => {
            await updateAttendance(record.id, { status, checkInTime, checkOutTime, remarks })
            notify('success', 'Attendance updated.')
            onChanged()
            onClose()
          }}
        />
      ) : (
        <>
          <DetailList
            columns={3}
            items={[
              { label: 'Check in', value: formatTime(record.checkInTime) },
              { label: 'Check out', value: formatTime(record.checkOutTime) },
              { label: 'Working hours', value: formatDuration(record.workingMinutes) },
              { label: 'Remarks', value: record.remarks },
              { label: 'Recorded', value: formatDateTime(record.createdAt) },
              { label: 'Last updated', value: formatDateTime(record.updatedAt) },
            ]}
          />
          <p className="mt-3 text-xs text-base-content/50">Times are office time as recorded by the server.</p>
          <div className="modal-action flex-wrap">
            <Link to={`/attendance/employee/${record.employeeId}?month=${record.attendanceDate.slice(0, 7)}`} className="btn btn-ghost btn-sm" onClick={onClose}>
              <CalendarDays className="size-4" /> Month view
            </Link>
            <button type="button" className="btn btn-ghost btn-sm text-error" onClick={() => setConfirmDelete(true)}>
              <Trash2 className="size-4" /> Delete
            </button>
            <button type="button" className="btn btn-primary btn-sm" onClick={() => setEditing(true)}>
              <Pencil className="size-4" /> Edit
            </button>
          </div>
        </>
      )}
    </Modal>
  )
}
