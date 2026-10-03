import { Eye, ListChecks } from 'lucide-react'
import { Link } from 'react-router-dom'
import type { PayrollPeriod } from '../../types/payroll'
import { formatAmount, formatDate } from '../../utils/formatters'
import { Table, type Column } from '../common/Table'
import { PayrollStatusBadge } from './PayrollStatusBadge'

/** Payroll periods with their totals. */
export function PayrollPeriodTable({ periods }: { periods: readonly PayrollPeriod[] }) {
  const columns: Column<PayrollPeriod>[] = [
    {
      key: 'name',
      header: 'Period',
      render: (p) => (
        <Link to={`/payroll/${p.id}`} className="font-medium hover:text-primary">
          {p.name}
        </Link>
      ),
    },
    { key: 'start', header: 'Start', render: (p) => formatDate(p.startDate), className: 'hidden md:table-cell' },
    { key: 'end', header: 'End', render: (p) => formatDate(p.endDate), className: 'hidden md:table-cell' },
    { key: 'status', header: 'Status', render: (p) => <PayrollStatusBadge status={p.status} /> },
    { key: 'employees', header: 'Employees', render: (p) => p.employeeCount, className: 'hidden sm:table-cell text-right' },
    { key: 'gross', header: 'Gross', render: (p) => <span className="tabular-nums">{formatAmount(p.grossTotal)}</span>, className: 'hidden lg:table-cell text-right' },
    { key: 'deduction', header: 'Deduction', render: (p) => <span className="tabular-nums">{formatAmount(p.deductionTotal)}</span>, className: 'hidden xl:table-cell text-right' },
    { key: 'net', header: 'Net', render: (p) => <span className="font-semibold tabular-nums">{formatAmount(p.netTotal)}</span>, className: 'text-right' },
    {
      key: 'actions',
      header: <span className="sr-only">Actions</span>,
      className: 'text-right',
      render: (p) => (
        <div className="flex justify-end gap-1">
          <Link to={`/payroll/${p.id}`} className="btn btn-ghost btn-xs btn-square" title="Open" aria-label={`Open ${p.name}`}>
            <Eye className="size-4" />
          </Link>
          <Link to={`/payroll/${p.id}/records`} className="btn btn-ghost btn-xs btn-square" title="Records" aria-label={`Records of ${p.name}`}>
            <ListChecks className="size-4" />
          </Link>
        </div>
      ),
    },
  ]

  return <Table columns={columns} rows={periods} rowKey={(p) => p.id} compact />
}
