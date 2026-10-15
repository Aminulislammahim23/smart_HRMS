import { Banknote, Eye, MinusCircle, Printer, ReceiptText, Wallet } from 'lucide-react'
import { useCallback, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { ExportButtons } from '../../components/common/ExportButtons'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { Pagination } from '../../components/common/Pagination'
import { Table, type Column } from '../../components/common/Table'
import { PaymentTransactionStatusBadge } from '../../components/payments/PaymentBadges'
import { PaymentDetailsModal } from '../../components/payments/PaymentDetailsModal'
import { PayrollRecordModal } from '../../components/payroll/PayrollRecordModal'
import { PaymentStatusBadge } from '../../components/payroll/PayrollStatusBadge'
import { PayslipPdfButton } from '../../components/payroll/PayslipPdfButton'
import { PayslipView } from '../../components/payroll/PayslipView'
import { useApi } from '../../hooks/useApi'
import { getMyPayments } from '../../services/paymentService'
import { exportMyPayslips, getMyCurrentPayslip, getMyPayrollSummary, getMyPayslips } from '../../services/payrollService'
import { ApiError } from '../../types/api'
import type { Payment } from '../../types/payment'
import type { PayrollRecord } from '../../types/payroll'
import { enumLabel, formatAmount, formatDate } from '../../utils/formatters'

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
        <EmptyState icon={ReceiptText} title="No payslips yet" description="Your payslip appears here once the payroll for the month has been finalized." />
      </div>
    )
  }
  if (error) return <div className="card bg-base-100 shadow-sm"><ErrorState error={error} onRetry={reload} /></div>
  if (loading && !payslip) return <Loading label="Loading your payslip…" />
  if (!payslip) return null

  return (
    <div className="flex flex-col gap-4">
      <div className="flex justify-end gap-2 print:hidden">
        {payslip.payslipId && <PayslipPdfButton payslipId={payslip.payslipId} payslipNumber={payslip.payslipNumber} label="Download current payslip as PDF" />}
        <Link to={`/payroll/payslips/${payslip.payslipId}?print=1`} className="btn btn-primary btn-sm">
          <Printer className="size-4" /> Print / save as PDF
        </Link>
      </div>
      <PayslipView payslip={payslip} />
    </div>
  )
}

const PAYMENT_NOTE: Record<Payment['status'], string> = {
  Pending: 'Your salary payment is scheduled.',
  Processing: 'Your salary payment is being processed.',
  Paid: 'Your salary has been paid.',
  Failed: 'The last payment attempt did not go through; it will be retried.',
  Cancelled: 'This payment was withdrawn; your salary will be paid in a later payment.',
}

/**
 * Day 21: the status of the employee's most recent salary payment (read-only). The server takes the employee from
 * the token; nothing here can change a payment. Renders nothing until a payment exists.
 */
function LatestPayment() {
  const [viewing, setViewing] = useState(false)
  const load = useCallback((signal: AbortSignal) => getMyPayments({ page: 1, pageSize: 1 }, signal), [])
  const { data } = useApi(load)
  const payment = data?.items[0]
  if (!payment) return null

  return (
    <div className="card bg-base-100 shadow-sm">
      <div className="card-body flex-col gap-3 p-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p className="text-sm text-base-content/60">Latest salary payment</p>
          <p className="flex flex-wrap items-center gap-2 font-semibold">
            {payment.periodName} · <span className="tabular-nums">{formatAmount(payment.amount)}</span>
            <PaymentTransactionStatusBadge status={payment.status} />
          </p>
          <p className="text-xs text-base-content/60">
            {PAYMENT_NOTE[payment.status]} {enumLabel(payment.paymentMethod)}
            {payment.status === 'Paid' ? ` · paid ${formatDate(payment.paymentDate)}` : payment.paymentDate ? ` · planned ${formatDate(payment.paymentDate)}` : ''}
            {payment.status === 'Paid' && payment.transactionReference ? ` · ref ${payment.transactionReference}` : ''}
          </p>
        </div>
        <button type="button" className="btn btn-sm self-start sm:self-center" onClick={() => setViewing(true)}>
          <Eye className="size-4" /> Details
        </button>
      </div>
      {viewing && <PaymentDetailsModal paymentId={payment.id} onClose={() => setViewing(false)} />}
    </div>
  )
}

/**
 * Payroll summary of the year (server totals of the employee's finalized payroll), the latest payslip and the latest
 * salary payment. The year is chosen here; the employee always comes from the token.
 */
function MySummary() {
  const currentYear = new Date().getFullYear()
  const [year, setYear] = useState(currentYear)
  const load = useCallback((signal: AbortSignal) => getMyPayrollSummary(year, signal), [year])
  const { data, error, loading, reload } = useApi(load)

  const cards = data
    ? [
        { label: 'Payslips', value: String(data.payslipCount), hint: `in ${data.year}`, icon: ReceiptText, tone: 'bg-primary/10 text-primary' },
        { label: 'Gross salary', value: formatAmount(data.grossSalary), hint: `${data.year} total`, icon: Banknote, tone: 'bg-info/10 text-info' },
        { label: 'Total deduction', value: formatAmount(data.totalDeduction), hint: `${data.year} total`, icon: MinusCircle, tone: 'bg-error/10 text-error' },
        { label: 'Net salary', value: formatAmount(data.netSalary), hint: `Paid ${formatAmount(data.paidNetSalary)}`, icon: Wallet, tone: 'bg-success/10 text-success' },
      ]
    : []

  return (
    <section className="mb-6 flex flex-col gap-4 print:hidden" aria-label="Payroll summary">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <h2 className="font-semibold">Payroll summary</h2>
        <select className="select select-sm w-32" value={year} onChange={(e) => setYear(Number(e.target.value))} aria-label="Summary year">
          {Array.from({ length: 5 }, (_, i) => currentYear - i).map((y) => (
            <option key={y} value={y}>
              {y}
            </option>
          ))}
        </select>
      </div>
      {error ? (
        <div className="card bg-base-100 shadow-sm"><ErrorState error={error} onRetry={reload} /></div>
      ) : loading && !data ? (
        <Loading label="Loading summary…" />
      ) : data ? (
        <>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            {cards.map(({ label, value, hint, icon: Icon, tone }) => (
              <div key={label} className="card bg-base-100 shadow-sm">
                <div className="card-body flex-row items-center gap-4 p-5">
                  <div className={`rounded-xl p-3 ${tone}`}>
                    <Icon className="size-6" />
                  </div>
                  <div className="min-w-0">
                    <p className="text-sm text-base-content/60">{label}</p>
                    <p className="truncate text-xl font-semibold tabular-nums">{value}</p>
                    <p className="truncate text-xs text-base-content/50">{hint}</p>
                  </div>
                </div>
              </div>
            ))}
          </div>
          {data.latest && (
            <div className="card bg-base-100 shadow-sm">
              <div className="card-body flex-col gap-3 p-4 sm:flex-row sm:items-center sm:justify-between">
                <div>
                  <p className="text-sm text-base-content/60">Latest payslip</p>
                  <p className="font-semibold">
                    {data.latest.periodName} · <span className="tabular-nums">{formatAmount(data.latest.netSalary)}</span> net
                  </p>
                  <p className="text-xs text-base-content/60">
                    {data.latest.payslipNumber} · <PaymentStatusBadge status={data.latest.paymentStatus} />
                    {data.latest.paymentDate && ` · paid ${formatDate(data.latest.paymentDate)}`}
                  </p>
                </div>
                <div className="flex flex-wrap gap-2">
                  <Link to={`/payroll/payslips/${data.latest.payslipId}`} className="btn btn-sm">
                    <Eye className="size-4" /> View
                  </Link>
                  <PayslipPdfButton payslipId={data.latest.payslipId} payslipNumber={data.latest.payslipNumber} label="Download latest payslip as PDF" />
                  <Link to={`/payroll/payslips/${data.latest.payslipId}?print=1`} className="btn btn-sm">
                    <Printer className="size-4" /> Print
                  </Link>
                </div>
              </div>
            </div>
          )}
          <LatestPayment />
        </>
      ) : null}
    </section>
  )
}

/** Own payslips, paged on the server. The employee always comes from the sign-in token. */
function MyPayslips() {
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [viewing, setViewing] = useState<PayrollRecord | null>(null)

  const load = useCallback((signal: AbortSignal) => getMyPayslips({ page, pageSize }, signal), [page, pageSize])
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
          {r.payslipId && <PayslipPdfButton compact payslipId={r.payslipId} payslipNumber={r.payslipNumber} label={`Download payslip of ${r.periodName} as PDF`} />}
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
          <EmptyState icon={ReceiptText} title="No payslips yet" description="Payslips appear here once payroll is approved." />
        ) : (
          <>
            <div className="flex justify-end">
              <ExportButtons label="Export my payslips" onExport={exportMyPayslips} />
            </div>
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

/**
 * Own salary payments (read-only), paged on the server. The server takes the employee from the token and never
 * offers payment actions here.
 */
function MyPayments() {
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [viewing, setViewing] = useState<string | null>(null)

  const load = useCallback((signal: AbortSignal) => getMyPayments({ page, pageSize }, signal), [page, pageSize])
  const { data, error, loading, reload } = useApi(load)

  const columns: Column<Payment>[] = [
    { key: 'period', header: 'Payroll month', render: (p) => <span className="font-medium">{p.periodName}</span> },
    { key: 'net', header: 'Net salary', render: (p) => <span className="font-semibold tabular-nums">{formatAmount(p.amount)}</span>, className: 'text-right' },
    { key: 'status', header: 'Payment', render: (p) => <PaymentTransactionStatusBadge status={p.status} /> },
    { key: 'date', header: 'Payment date', render: (p) => (p.status === 'Paid' ? formatDate(p.paymentDate) : '—'), className: 'whitespace-nowrap' },
    { key: 'method', header: 'Method', render: (p) => enumLabel(p.paymentMethod), className: 'hidden sm:table-cell whitespace-nowrap' },
    { key: 'reference', header: 'Transaction ref.', render: (p) => p.transactionReference ?? '—', className: 'hidden md:table-cell' },
    {
      key: 'actions',
      header: <span className="sr-only">Actions</span>,
      className: 'text-right',
      render: (p) => (
        <div className="flex justify-end gap-1">
          <button type="button" className="btn btn-ghost btn-xs btn-square" onClick={() => setViewing(p.id)} title="Details" aria-label={`Payment details of ${p.periodName}`}>
            <Eye className="size-4" />
          </button>
          {p.payslipId && (
            <Link to={`/payroll/payslips/${p.payslipId}`} className="btn btn-ghost btn-xs btn-square" title="Payslip" aria-label={`Payslip of ${p.periodName}`}>
              <ReceiptText className="size-4" />
            </Link>
          )}
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
          <EmptyState icon={Wallet} title="No payments yet" description="A payment appears here once your salary for the month is being paid." />
        ) : (
          <>
            <Table columns={columns} rows={data.items} rowKey={(p) => p.id} compact />
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
      {viewing && <PaymentDetailsModal paymentId={viewing} onClose={() => setViewing(null)} />}
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
      <PageHeader title="My payroll" description="Your payslips and salary payments. Only finalized payroll is shown." />
      <MySummary />
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
      {tab === 'history' && <MyPayslips />}
      {tab === 'payments' && <MyPayments />}
    </>
  )
}
