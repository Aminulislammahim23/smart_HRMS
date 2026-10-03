import { Eye, Pencil, ReceiptText } from 'lucide-react'
import { Link } from 'react-router-dom'
import type { PayrollRecord } from '../../types/payroll'
import { formatAmount, formatDate } from '../../utils/formatters'
import { Table, type Column } from '../common/Table'
import { PaymentStatusBadge, PayrollRecordStatusBadge } from './PayrollStatusBadge'

interface PayrollRecordTableProps {
  records: readonly PayrollRecord[]
  onView: (record: PayrollRecord) => void
  onEdit?: (record: PayrollRecord) => void
  /** Employee self-service lists show the period instead of the employee. */
  showPeriod?: boolean
  /** Adds the payslip payment status and date. */
  showPayment?: boolean
}

/** Payroll records with view, edit (when the server allows it) and payslip actions. */
export function PayrollRecordTable({ records, onView, onEdit, showPeriod = false, showPayment = false }: PayrollRecordTableProps) {
  const columns: Column<PayrollRecord>[] = [
    showPeriod
      ? { key: 'period', header: 'Period', render: (r) => <span className="font-medium">{r.periodName}</span> }
      : {
          key: 'employee',
          header: 'Employee',
          render: (r) => (
            <div className="min-w-0">
              <p className="truncate font-medium">{r.employeeName}</p>
              <p className="text-xs text-base-content/60">{r.employeeCode}</p>
            </div>
          ),
        },
    { key: 'department', header: 'Department', render: (r) => r.departmentName ?? '—', className: showPeriod ? 'hidden' : 'hidden xl:table-cell' },
    { key: 'basic', header: 'Basic', render: (r) => <span className="tabular-nums">{formatAmount(r.basicSalary)}</span>, className: 'hidden md:table-cell text-right' },
    { key: 'gross', header: 'Gross', render: (r) => <span className="tabular-nums">{formatAmount(r.grossSalary)}</span>, className: 'text-right' },
    { key: 'deduction', header: 'Deduction', render: (r) => <span className="tabular-nums">{formatAmount(r.totalDeduction)}</span>, className: 'hidden sm:table-cell text-right' },
    {
      key: 'net',
      header: 'Net salary',
      render: (r) => <span className={`font-semibold tabular-nums ${r.netSalary < 0 ? 'text-error' : ''}`}>{formatAmount(r.netSalary)}</span>,
      className: 'text-right',
    },
    { key: 'status', header: 'Status', render: (r) => <PayrollRecordStatusBadge status={r.status} /> },
    ...(showPayment
      ? [
          {
            key: 'payment',
            header: 'Payment',
            render: (r: PayrollRecord) => (
              <div className="flex flex-col items-start gap-0.5">
                <PaymentStatusBadge status={r.paymentStatus} />
                {r.paymentDate && <span className="text-xs text-base-content/60">{formatDate(r.paymentDate)}</span>}
              </div>
            ),
          },
        ]
      : []),
    {
      key: 'actions',
      header: <span className="sr-only">Actions</span>,
      className: 'text-right',
      render: (r) => (
        <div className="flex justify-end gap-1">
          <button type="button" className="btn btn-ghost btn-xs btn-square" onClick={() => onView(r)} title="View" aria-label={`View payroll of ${r.employeeName}`}>
            <Eye className="size-4" />
          </button>
          {onEdit && r.canEdit && (
            <button type="button" className="btn btn-ghost btn-xs btn-square" onClick={() => onEdit(r)} title="Edit" aria-label={`Edit payroll of ${r.employeeName}`}>
              <Pencil className="size-4" />
            </button>
          )}
          <Link to={r.payslipId ? `/payroll/payslips/${r.payslipId}` : `/payroll/payslip/${r.id}`} className="btn btn-ghost btn-xs btn-square" title={r.payslipId ? 'Payslip' : 'Payslip preview'} aria-label={`Payslip of ${r.employeeName}`}>
            <ReceiptText className="size-4" />
          </Link>
        </div>
      ),
    },
  ]

  return <Table columns={columns} rows={records} rowKey={(r) => r.id} compact />
}
