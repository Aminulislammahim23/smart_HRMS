import { Eye, Printer, ReceiptText, Wallet } from 'lucide-react'
import { useCallback, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { Pagination } from '../../components/common/Pagination'
import { Table, type Column } from '../../components/common/Table'
import { PayrollRecordModal } from '../../components/payroll/PayrollRecordModal'
import { PaymentStatusBadge } from '../../components/payroll/PayrollStatusBadge'
import { PayslipView } from '../../components/payroll/PayslipView'
import { useApi } from '../../hooks/useApi'
import { getMyCurrentPayslip, getMyPayments, getMyPayslips } from '../../services/payrollService'
import { ApiError } from '../../types/api'
import type { PayrollRecord } from '../../types/payroll'
import { formatAmount, formatDate } from '../../utils/formatters'

type Tab = 'current' | 'history' | 'payments'
const TABS: { id: Tab; label: string }[] = [
  { id: 'current', label: 'Current payslip' },
  { id: 'history', label: 'Payslip history' },
  { id: 'payments', label: 'Payment history' },
]

/** The latest issued payslip, in full. */
function CurrentPayslip() {
  const load = useCallback((signal: AbortSignal) => getMyCurrentPayslip(signal), [])
  const { data: payslip, error, loading, reload } = useApi(load)

  if (error instanceof ApiError && error.status === 404) {
    return (
      <div className="card bg-base-100 shadow-sm">
        <EmptyState icon={ReceiptText} title="No payslips yet" description="Your payslip is issued when HR's payroll for the month has been approved." />
      </div>
    )
  }
  if (error) return <div className="card bg-base-100 shadow-sm"><ErrorState error={error} onRetry={reload} /></div>
  if (loading && !payslip) return <Loading label="Loading your payslip…" />
  if (!payslip) return null

  return (
    <div className="flex flex-col gap-4">
      <div className="flex justify-end print:hidden">
        <Link to={`/payroll/payslips/${payslip.payslipId}?print=1`} className="btn btn-primary btn-sm">
          <Printer className="size-4" /> Print / save as PDF
        </Link>
      </div>
      <PayslipView payslip={payslip} />
    </div>
  )
}

/** Own payslips or own payments, paged on the server. The employee always comes from the sign-in token. */
function MyList({ kind }: { kind: 'history' | 'payments' }) {
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [viewing, setViewing] = useState<PayrollRecord | null>(null)

  const load = useCallback(
    (signal: AbortSignal) => (kind === 'history' ? getMyPayslips({ page, pageSize }, signal) : getMyPayments({ page, pageSize }, signal)),
    [kind, page, pageSize],
  )
  const { data, error, loading, reload } = useApi(load)

  const columns: Column<PayrollRecord>[] = [
    {
      key: 'period',
      header: 'Pay period',
      render: (r) => (
        <div>
          <p className="font-medium">{r.periodName}</p>
          <p className="text-xs text-base-content/60">{r.payslipNumber}</p>
        </div>
      ),
    },
    { key: 'gross', header: 'Gross', render: (r) => <span className="tabular-nums">{formatAmount(r.grossSalary)}</span>, className: 'hidden sm:table-cell text-right' },
    { key: 'deduction', header: 'Deduction', render: (r) => <span className="tabular-nums">{formatAmount(r.totalDeduction)}</span>, className: 'hidden md:table-cell text-right' },
    { key: 'net', header: 'Net salary', render: (r) => <span className="font-semibold tabular-nums">{formatAmount(r.netSalary)}</span>, className: 'text-right' },
    { key: 'payment', header: 'Payment', render: (r) => <PaymentStatusBadge status={r.paymentStatus} /> },
    { key: 'paidOn', header: 'Paid on', render: (r) => (r.paymentDate ? formatDate(r.paymentDate) : '—'), className: 'whitespace-nowrap' },
    {
      key: 'actions',
      header: <span className="sr-only">Actions</span>,
      className: 'text-right',
      render: (r) => (
        <div className="flex justify-end gap-1">
          <button type="button" className="btn btn-ghost btn-xs btn-square" onClick={() => setViewing(r)} title="Breakdown" aria-label={`Breakdown of ${r.periodName}`}>
            <Eye className="size-4" />
          </button>
          <Link to={`/payroll/payslips/${r.payslipId}`} className="btn btn-ghost btn-xs btn-square" title="Payslip" aria-label={`Payslip of ${r.periodName}`}>
            <ReceiptText className="size-4" />
          </Link>
          <Link to={`/payroll/payslips/${r.payslipId}?print=1`} className="btn btn-ghost btn-xs btn-square" title="Print" aria-label={`Print payslip of ${r.periodName}`}>
            <Printer className="size-4" />
          </Link>
        </div>
      ),
    },
  ]

  return (
    <div className="card bg-base-100 shadow-sm">
      <div className="card-body gap-3 p-4 sm:p-6">
        {error ? (
          <ErrorState error={error} onRetry={reload} />
        ) : loading && !data ? (
          <Loading label="Loading…" />
        ) : !data || data.items.length === 0 ? (
          <EmptyState
            icon={kind === 'history' ? ReceiptText : Wallet}
            title={kind === 'history' ? 'No payslips yet' : 'No payments recorded yet'}
            description={kind === 'history' ? 'Payslips appear here once payroll is approved.' : 'A payment appears once your salary for the month has been paid.'}
          />
        ) : (
          <>
            <Table columns={columns} rows={data.items} rowKey={(r) => r.id} compact />
            <Pagination
              page={data.page}
              pageCount={Math.max(1, data.totalPages)}
              pageSize={data.pageSize}
              total={data.totalCount}
              onPageChange={setPage}
              onPageSizeChange={(size) => {
                setPageSize(size)
                setPage(1)
              }}
            />
          </>
        )}
      </div>
      {viewing && <PayrollRecordModal record={viewing} onClose={() => setViewing(null)} />}
    </div>
  )
}

/** The signed-in employee's payslips and payments. Only their own records exist here: the server takes the employee from the token. */
export default function MyPayrollPage() {
  const [params, setParams] = useSearchParams()
  const requested = params.get('tab')
  const tab: Tab = TABS.some((t) => t.id === requested) ? (requested as Tab) : 'current'

  return (
    <>
      <PageHeader title="My payroll" description="Your payslips and salary payments." />
      <div role="tablist" className="tabs tabs-box mb-6 w-max max-w-full overflow-x-auto bg-base-100 shadow-sm print:hidden">
        {TABS.map(({ id, label }) => (
          <button
            key={id}
            type="button"
            role="tab"
            aria-selected={tab === id}
            className={`tab whitespace-nowrap ${tab === id ? 'tab-active' : ''}`}
            onClick={() => setParams(id === 'current' ? {} : { tab: id }, { replace: true })}
          >
            {label}
          </button>
        ))}
      </div>
      {tab === 'current' && <CurrentPayslip />}
      {tab === 'history' && <MyList key="history" kind="history" />}
      {tab === 'payments' && <MyList key="payments" kind="payments" />}
    </>
  )
}
