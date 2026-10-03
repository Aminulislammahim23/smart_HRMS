import { AlertTriangle, ReceiptText } from 'lucide-react'
import { Link } from 'react-router-dom'
import type { PayrollRecord } from '../../types/payroll'
import { formatAmount, formatDate } from '../../utils/formatters'
import { DetailList } from '../common/DetailList'
import { Modal } from '../common/Modal'
import { DeductionBreakdown, PayrollDays, SalaryBreakdown } from './AmountBreakdown'
import { PayrollRecordStatusBadge } from './PayrollStatusBadge'

/** Full breakdown of one payroll record: employee, earnings, deductions, net and the days used. */
export function PayrollRecordModal({ record, onClose }: { record: PayrollRecord; onClose: () => void }) {
  return (
    <Modal open title={`${record.employeeName} — ${record.periodName}`} onClose={onClose} size="lg">
      <div className="flex flex-col gap-5">
        <DetailList
          columns={3}
          items={[
            { label: 'Employee ID', value: record.employeeCode },
            { label: 'Department', value: record.departmentName },
            { label: 'Designation', value: record.designationName },
            { label: 'Period', value: `${formatDate(record.periodStartDate)} – ${formatDate(record.periodEndDate)}` },
            { label: 'Status', value: <PayrollRecordStatusBadge status={record.status} /> },
            { label: 'Remarks', value: record.remarks },
          ]}
        />

        {record.status === 'NeedsReview' && (
          <div role="alert" className="alert alert-warning alert-soft">
            <AlertTriangle className="size-5 shrink-0" />
            <span className="text-sm">The net salary is negative. Reduce the deductions before the payroll can be submitted.</span>
          </div>
        )}

        <div className="grid gap-4 md:grid-cols-2">
          <SalaryBreakdown record={record} />
          <DeductionBreakdown record={record} />
        </div>

        <div className="flex items-center justify-between rounded-box bg-primary/10 px-4 py-3">
          <span className="font-semibold">Net salary</span>
          <span className={`text-xl font-bold tabular-nums ${record.netSalary < 0 ? 'text-error' : 'text-primary'}`}>{formatAmount(record.netSalary)}</span>
        </div>

        <div>
          <h3 className="mb-2 text-sm font-semibold">Attendance used for this payroll</h3>
          <PayrollDays days={record} />
        </div>

        <div className="modal-action mt-0">
          <Link to={`/payroll/payslip/${record.id}`} className="btn btn-sm">
            <ReceiptText className="size-4" /> Payslip
          </Link>
          <button type="button" className="btn btn-sm btn-primary" onClick={onClose}>
            Close
          </button>
        </div>
      </div>
    </Modal>
  )
}
