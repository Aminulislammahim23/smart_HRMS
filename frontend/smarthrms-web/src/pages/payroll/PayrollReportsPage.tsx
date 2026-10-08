import { BarChart3, Banknote, CircleDollarSign, MinusCircle, RotateCcw, Search, Users, Wallet, type LucideIcon } from 'lucide-react'
import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { ExportButtons } from '../../components/common/ExportButtons'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { Pagination } from '../../components/common/Pagination'
import { Table, type Column } from '../../components/common/Table'
import { PaymentStatusBadge, PayrollStatusBadge } from '../../components/payroll/PayrollStatusBadge'
import { useApi } from '../../hooks/useApi'
import { getDepartments } from '../../services/departmentService'
import { getDesignations } from '../../services/designationService'
import { getEmployees } from '../../services/employeeService'
import { getPayrollPeriods } from '../../services/payrollService'
import { exportPayrollReport, getEmployeePayrollReport, getPayrollReport } from '../../services/payrollReportService'
import { PAYROLL_STATUSES, isPayrollStatus, type PayrollRecord } from '../../types/payroll'
import type { PayrollAmounts, PayrollReport, PayrollReportQuery, PayrollReportRow, ReportGroupBy } from '../../types/payrollReport'
import { PAGE_SIZE_OPTIONS } from '../../utils/constants'
import { enumLabel, formatAmount } from '../../utils/formatters'

type Tab = 'summary' | 'departments' | 'employees' | 'deductions' | 'allowances'
const TABS: { id: Tab; label: string }[] = [
  { id: 'summary', label: 'Monthly summary' },
  { id: 'departments', label: 'Departments' },
  { id: 'employees', label: 'Employees' },
  { id: 'deductions', label: 'Deductions' },
  { id: 'allowances', label: 'Allowances & bonus' },
]
const CURRENT_YEAR = new Date().getFullYear()
const MONTHS = Array.from({ length: 12 }, (_, i) => ({ value: i + 1, label: new Date(2000, i, 1).toLocaleDateString(undefined, { month: 'long' }) }))
const PAGE_SIZES: readonly number[] = PAGE_SIZE_OPTIONS
const DATE = /^\d{4}-\d{2}-\d{2}$/
/** Parameters that belong to the page, not to the report filters. */
const PAGE_KEYS = ['tab', 'groupBy', 'page', 'pageSize']

function readInt(params: URLSearchParams, key: string, min: number, max: number): number | undefined {
  const value = Number(params.get(key))
  return Number.isInteger(value) && value >= min && value <= max ? value : undefined
}

/** Reads the report filters from the URL; anything invalid is ignored (the server validates again). */
function filtersFromParams(params: URLSearchParams): PayrollReportQuery {
  const status = params.get('status') ?? ''
  const from = params.get('from') ?? ''
  const to = params.get('to') ?? ''
  return {
    month: readInt(params, 'month', 1, 12),
    year: readInt(params, 'year', 2000, 2100),
    payrollPeriodId: params.get('payrollPeriodId') || undefined,
    employeeId: params.get('employeeId') || undefined,
    departmentId: params.get('departmentId') || undefined,
    designationId: params.get('designationId') || undefined,
    status: isPayrollStatus(status) ? status : undefined,
    from: DATE.test(from) ? from : undefined,
    to: DATE.test(to) ? to : undefined,
    search: params.get('search') || undefined,
  }
}

const money = (value: number, strong = false) => <span className={`tabular-nums ${strong ? 'font-semibold' : ''}`}>{formatAmount(value)}</span>

type ReportLine = PayrollReportRow & { isTotal?: boolean }

function withTotal(report: PayrollReport): ReportLine[] {
  const total: ReportLine = { ...report.totals, label: 'Total', payrollPeriodId: null, periodStartDate: null, periodEndDate: null, periodStatus: null, isTotal: true }
  return [...report.rows, total]
}

function labelColumn(header: string): Column<ReportLine> {
  return {
    key: 'label',
    header,
    render: (r) =>
      r.isTotal ? (
        <span className="font-semibold">Total</span>
      ) : r.payrollPeriodId ? (
        <Link to={`/payroll/${r.payrollPeriodId}`} className="font-medium whitespace-nowrap hover:text-primary">
          {r.label}
        </Link>
      ) : (
        <span className="font-medium">{r.label}</span>
      ),
  }
}

function amountColumn(key: keyof PayrollAmounts, header: string, className = 'text-right'): Column<ReportLine> {
  return { key, header, render: (r) => money(r[key], r.isTotal || key === 'netSalary'), className }
}

/** A thin bar showing a row's share of a total (no chart library is installed). */
function ShareBar({ value, total, tone = 'bg-primary' }: { value: number; total: number; tone?: string }) {
  const percent = total > 0 ? Math.max(0, Math.min(100, (value / total) * 100)) : 0
  return (
    <div className="flex items-center gap-2" title={`${percent.toFixed(1)}%`}>
      <div className="h-2 w-24 overflow-hidden rounded-full bg-base-200">
        <div className={`h-full rounded-full ${tone}`} style={{ width: `${percent}%` }} />
      </div>
      <span className="w-12 text-right text-xs tabular-nums text-base-content/60">{percent.toFixed(1)}%</span>
    </div>
  )
}

function StatCard({ label, value, hint, icon: Icon, tone }: { label: string; value: string; hint?: string; icon: LucideIcon; tone: string }) {
  return (
    <div className="card bg-base-100 shadow-sm">
      <div className="card-body flex-row items-center gap-4 p-5">
        <div className={`rounded-xl p-3 ${tone}`}>
          <Icon className="size-6" />
        </div>
        <div className="min-w-0">
          <p className="text-sm text-base-content/60">{label}</p>
          <p className="truncate text-xl font-semibold tabular-nums">{value}</p>
          {hint && <p className="truncate text-xs text-base-content/50">{hint}</p>}
        </div>
      </div>
    </div>
  )
}

function ReportCard({ title, description, actions, children }: { title: string; description: string; actions?: ReactNode; children: ReactNode }) {
  return (
    <div className="card bg-base-100 shadow-sm">
      <div className="card-body gap-4 p-4 sm:p-6">
        <div className="flex flex-col gap-2 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <h2 className="font-semibold">{title}</h2>
            <p className="text-sm text-base-content/60">{description}</p>
          </div>
          {actions}
        </div>
        {children}
      </div>
    </div>
  )
}

/** A grouped report (summary, departments, deductions, allowances) loaded from the server with the page filters. */
function GroupedReport({ kind, filters, groupBy, render }: { kind: 'departments' | 'deductions' | 'allowances'; filters: PayrollReportQuery; groupBy?: ReportGroupBy; render: (report: PayrollReport) => ReactNode }) {
  const load = useCallback((signal: AbortSignal) => getPayrollReport(kind, { ...filters, groupBy }, signal), [kind, filters, groupBy])
  const { data, error, loading, reload } = useApi(load)
  if (error) return <ErrorState error={error} onRetry={reload} />
  if (loading && !data) return <Loading label="Loading report…" />
  if (!data || data.rows.length === 0) {
    return <EmptyState icon={BarChart3} title="No payroll matches these filters" description="Reports include issued payroll (approved, finalized or paid) unless you pick a status." />
  }
  return <>{render(data)}</>
}

/** The monthly summary reuses the response already loaded for the headline cards. */
function SummaryReport({ summary, render }: { summary: PayrollReport | undefined; render: (report: PayrollReport) => ReactNode }) {
  if (!summary) return <Loading label="Loading report…" />
  if (summary.rows.length === 0) {
    return <EmptyState icon={BarChart3} title="No payroll matches these filters" description="Reports include issued payroll (approved, finalized or paid) unless you pick a status." />
  }
  return <>{render(summary)}</>
}

function GroupToggle({ value, onChange }: { value: ReportGroupBy; onChange: (value: ReportGroupBy) => void }) {
  return (
    <div className="join" role="group" aria-label="Group by">
      {(['period', 'department'] as const).map((option) => (
        <button key={option} type="button" className={`btn btn-sm join-item ${value === option ? 'btn-active' : ''}`} onClick={() => onChange(option)} aria-pressed={value === option}>
          By {option}
        </button>
      ))}
    </div>
  )
}

/**
 * Payroll reporting (HR/Admin): headline totals and five reports. Every number is aggregated by the server from the
 * stored payroll records (nothing is recalculated here), and every filter is applied on the server.
 */
export default function PayrollReportsPage() {
  const [params, setParams] = useSearchParams()
  const filters = useMemo(() => filtersFromParams(params), [params])
  const requestedTab = params.get('tab')
  const tab: Tab = TABS.some((t) => t.id === requestedTab) ? (requestedTab as Tab) : 'summary'
  const groupBy: ReportGroupBy = params.get('groupBy') === 'department' ? 'department' : 'period'
  const page = readInt(params, 'page', 1, 100_000) ?? 1
  const pageSize = PAGE_SIZES.includes(Number(params.get('pageSize'))) ? Number(params.get('pageSize')) : 25
  const [searchInput, setSearchInput] = useState(filters.search ?? '')

  const update = useCallback(
    (patch: Record<string, string | number | undefined>) =>
      setParams(
        (current) => {
          const next = new URLSearchParams(current)
          for (const [key, value] of Object.entries(patch)) {
            if (value === undefined || value === '') next.delete(key)
            else next.set(key, String(value))
          }
          if (!('page' in patch)) next.delete('page')
          return next
        },
        { replace: true },
      ),
    [setParams],
  )

  useEffect(() => {
    if (searchInput.trim() === (filters.search ?? '')) return
    const timer = window.setTimeout(() => update({ search: searchInput.trim() }), 350)
    return () => window.clearTimeout(timer)
  }, [searchInput, filters.search, update])

  const lookups = useCallback(
    (signal: AbortSignal) => Promise.all([getEmployees(signal), getDepartments(signal), getDesignations(signal), getPayrollPeriods({}, signal)]),
    [],
  )
  const { data: lookupData } = useApi(lookups)
  const [employees, departments, designations, periods] = lookupData ?? [[], [], [], []]

  const loadTotals = useCallback((signal: AbortSignal) => getPayrollReport('summary', filters, signal), [filters])
  const { data: summary, error: summaryError, reload: reloadSummary } = useApi(loadTotals)
  const filtered = Object.values(filters).some((value) => value !== undefined)

  const reset = () => {
    setSearchInput('')
    setParams((current) => {
      const next = new URLSearchParams()
      for (const key of PAGE_KEYS) if (key !== 'page' && current.get(key)) next.set(key, current.get(key) ?? '')
      return next
    }, { replace: true })
  }

  const totals = summary?.totals
  const scope = filters.status ? `${enumLabel(filters.status)} payroll` : 'Issued payroll (approved, finalized, paid)'

  const summaryColumns: Column<ReportLine>[] = [
    labelColumn('Payroll period'),
    { key: 'status', header: 'Status', render: (r) => (r.periodStatus ? <PayrollStatusBadge status={r.periodStatus} /> : null), className: 'hidden md:table-cell' },
    { key: 'employees', header: 'Employees', render: (r) => r.employeeCount, className: 'text-right' },
    amountColumn('basicSalary', 'Basic', 'hidden 2xl:table-cell text-right'),
    amountColumn('totalAllowances', 'Allowances', 'hidden 2xl:table-cell text-right'),
    amountColumn('overtime', 'Overtime', 'hidden 2xl:table-cell text-right'),
    amountColumn('bonus', 'Bonus', 'hidden 2xl:table-cell text-right'),
    amountColumn('grossSalary', 'Gross', 'hidden sm:table-cell text-right'),
    amountColumn('tax', 'Tax', 'hidden lg:table-cell text-right'),
    amountColumn('otherDeductions', 'Other deductions', 'hidden 2xl:table-cell text-right'),
    amountColumn('totalDeduction', 'Total deduction', 'hidden lg:table-cell text-right'),
    amountColumn('netSalary', 'Net salary'),
    amountColumn('paidNetSalary', 'Paid', 'hidden 2xl:table-cell text-right'),
    amountColumn('unpaidNetSalary', 'Outstanding', 'hidden 2xl:table-cell text-right'),
  ]

  const employeeColumns: Column<PayrollRecord>[] = [
    {
      key: 'employee',
      header: 'Employee',
      render: (r) => (
        <div className="min-w-0">
          <p className="truncate font-medium">{r.employeeName}</p>
          <p className="text-xs text-base-content/60">{r.employeeCode}</p>
        </div>
      ),
    },
    { key: 'department', header: 'Department', render: (r) => r.departmentName ?? '—', className: 'hidden md:table-cell' },
    { key: 'designation', header: 'Designation', render: (r) => r.designationName ?? '—', className: 'hidden xl:table-cell' },
    { key: 'period', header: 'Payroll period', render: (r) => <span className="whitespace-nowrap">{r.periodName}</span> },
    { key: 'gross', header: 'Gross', render: (r) => money(r.grossSalary), className: 'hidden sm:table-cell text-right' },
    { key: 'deduction', header: 'Deduction', render: (r) => money(r.totalDeduction), className: 'hidden lg:table-cell text-right' },
    { key: 'net', header: 'Net salary', render: (r) => money(r.netSalary, true), className: 'text-right' },
    { key: 'payment', header: 'Payment', render: (r) => <PaymentStatusBadge status={r.paymentStatus} />, className: 'hidden md:table-cell' },
  ]

  const loadEmployees = useCallback(
    (signal: AbortSignal) => (tab === 'employees' ? getEmployeePayrollReport({ ...filters, page, pageSize }, signal) : Promise.resolve(null)),
    [tab, filters, page, pageSize],
  )
  const { data: employeePage, error: employeeError, loading: employeeLoading, reload: reloadEmployees } = useApi(loadEmployees)

  const exportCurrent = (format: 'csv' | 'xlsx') =>
    exportPayrollReport(tab === 'summary' ? 'summary' : tab, { ...filters, groupBy: tab === 'deductions' || tab === 'allowances' ? groupBy : undefined }, format)

  return (
    <>
      <PageHeader title="Payroll reports" description="Payroll totals, salary expense, departments, employees, deductions, allowances and bonus." />

      <div className="flex flex-col gap-6">
        <div className="card bg-base-100 shadow-sm">
          <div className="card-body gap-3 p-4 sm:p-6">
            <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
              <div className="grid grid-cols-2 gap-2">
                <select className="select select-sm w-full" value={filters.month ?? ''} onChange={(e) => update({ month: e.target.value })} aria-label="Month">
                  <option value="">Any month</option>
                  {MONTHS.map((m) => (
                    <option key={m.value} value={m.value}>
                      {m.label}
                    </option>
                  ))}
                </select>
                <select className="select select-sm w-full" value={filters.year ?? ''} onChange={(e) => update({ year: e.target.value })} aria-label="Year">
                  <option value="">Any year</option>
                  {Array.from({ length: 6 }, (_, i) => CURRENT_YEAR + 1 - i).map((y) => (
                    <option key={y} value={y}>
                      {y}
                    </option>
                  ))}
                </select>
              </div>
              <select className="select select-sm w-full" value={filters.payrollPeriodId ?? ''} onChange={(e) => update({ payrollPeriodId: e.target.value })} aria-label="Payroll period">
                <option value="">All payroll periods</option>
                {periods.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.name}
                  </option>
                ))}
              </select>
              <select className="select select-sm w-full" value={filters.status ?? ''} onChange={(e) => update({ status: e.target.value })} aria-label="Payroll status">
                <option value="">Issued payroll</option>
                {PAYROLL_STATUSES.map((s) => (
                  <option key={s} value={s}>
                    {enumLabel(s)} only
                  </option>
                ))}
              </select>
              <div className="grid grid-cols-2 gap-2">
                <input type="date" className="input input-sm w-full" value={filters.from ?? ''} max={filters.to} onChange={(e) => update({ from: e.target.value })} aria-label="From date" title="Payroll periods overlapping from" />
                <input type="date" className="input input-sm w-full" value={filters.to ?? ''} min={filters.from} onChange={(e) => update({ to: e.target.value })} aria-label="To date" title="Payroll periods overlapping until" />
              </div>
              <select className="select select-sm w-full" value={filters.employeeId ?? ''} onChange={(e) => update({ employeeId: e.target.value })} aria-label="Employee">
                <option value="">All employees</option>
                {employees.map((e) => (
                  <option key={e.id} value={e.id}>
                    {e.fullName} ({e.employeeCode})
                  </option>
                ))}
              </select>
              <select className="select select-sm w-full" value={filters.departmentId ?? ''} onChange={(e) => update({ departmentId: e.target.value })} aria-label="Department">
                <option value="">All departments</option>
                {departments.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name}
                  </option>
                ))}
              </select>
              <select className="select select-sm w-full" value={filters.designationId ?? ''} onChange={(e) => update({ designationId: e.target.value })} aria-label="Designation">
                <option value="">All designations</option>
                {designations.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name}
                  </option>
                ))}
              </select>
              <label className="input input-sm w-full">
                <Search className="size-4 opacity-50" />
                <input type="search" placeholder="Search name or employee ID" value={searchInput} onChange={(e) => setSearchInput(e.target.value)} aria-label="Search" />
              </label>
              <button type="button" className="btn btn-sm btn-ghost justify-self-start" onClick={reset} disabled={!filtered}>
                <RotateCcw className="size-4" /> Reset filters
              </button>
            </div>
            <p className="text-xs text-base-content/60">Scope: {scope}. Department and designation filter on the employee&apos;s current assignment.</p>
          </div>
        </div>

        {summaryError ? (
          <div className="card bg-base-100 shadow-sm"><ErrorState error={summaryError} onRetry={reloadSummary} /></div>
        ) : (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 2xl:grid-cols-5">
            <StatCard label="Total payroll" value={totals ? String(totals.recordCount) : '…'} hint={summary ? `records in ${summary.rows.length} payroll period${summary.rows.length === 1 ? '' : 's'}` : undefined} icon={BarChart3} tone="bg-primary/10 text-primary" />
            <StatCard label="Employees" value={totals ? String(totals.employeeCount) : '…'} icon={Users} tone="bg-secondary/10 text-secondary" />
            <StatCard label="Total gross" value={totals ? formatAmount(totals.grossSalary) : '…'} hint="Salary expense" icon={Banknote} tone="bg-info/10 text-info" />
            <StatCard label="Total deduction" value={totals ? formatAmount(totals.totalDeduction) : '…'} hint={totals ? `Tax ${formatAmount(totals.tax)}` : undefined} icon={MinusCircle} tone="bg-error/10 text-error" />
            <StatCard label="Total net salary" value={totals ? formatAmount(totals.netSalary) : '…'} hint={totals ? `Paid ${formatAmount(totals.paidNetSalary)} · outstanding ${formatAmount(totals.unpaidNetSalary)}` : undefined} icon={Wallet} tone="bg-success/10 text-success" />
          </div>
        )}

        <div role="tablist" className="tabs tabs-box w-max max-w-full overflow-x-auto bg-base-100 shadow-sm">
          {TABS.map(({ id, label }) => (
            <button key={id} type="button" role="tab" aria-selected={tab === id} className={`tab whitespace-nowrap ${tab === id ? 'tab-active' : ''}`} onClick={() => update({ tab: id === 'summary' ? undefined : id })}>
              {label}
            </button>
          ))}
        </div>

        {tab === 'summary' && (
          <ReportCard title="Monthly payroll summary" description="Totals per payroll period, with what has been paid and what is outstanding (salary expense)." actions={<ExportButtons label="Export monthly summary" onExport={exportCurrent} />}>
            <SummaryReport
              summary={summary}
              render={(report) => (
                <>
                  <Table columns={summaryColumns} rows={withTotal(report)} rowKey={(r) => r.payrollPeriodId ?? r.label} compact />
                  <div className="flex flex-col gap-2">
                    <h3 className="text-sm font-semibold">Salary expense by period</h3>
                    {report.rows.map((r) => (
                      <div key={r.payrollPeriodId ?? r.label} className="grid grid-cols-[minmax(0,10rem)_1fr] items-center gap-3 text-sm sm:grid-cols-[14rem_1fr_8rem]">
                        <span className="truncate">{r.label}</span>
                        <ShareBar value={r.grossSalary} total={report.totals.grossSalary} tone="bg-info" />
                        <span className="hidden text-right tabular-nums sm:block">{formatAmount(r.grossSalary)}</span>
                      </div>
                    ))}
                  </div>
                </>
              )}
            />
          </ReportCard>
        )}

        {tab === 'departments' && (
          <ReportCard title="Department-wise payroll" description="Employees, gross, deductions and net salary per department (as recorded on the payroll)." actions={<ExportButtons label="Export department report" onExport={exportCurrent} />}>
            <GroupedReport
              kind="departments"
              filters={filters}
              render={(report) => (
                <Table
                  columns={[
                    labelColumn('Department'),
                    { key: 'employees', header: 'Employees', render: (r) => r.employeeCount, className: 'text-right' },
                    amountColumn('grossSalary', 'Gross', 'hidden sm:table-cell text-right'),
                    amountColumn('totalDeduction', 'Total deduction', 'hidden md:table-cell text-right'),
                    amountColumn('netSalary', 'Net salary'),
                    { key: 'share', header: 'Share of net', render: (r) => (r.isTotal ? null : <ShareBar value={r.netSalary} total={report.totals.netSalary} />), className: 'hidden lg:table-cell' },
                  ]}
                  rows={withTotal(report)}
                  rowKey={(r) => (r.isTotal ? '__total' : r.label)}
                  compact
                />
              )}
            />
          </ReportCard>
        )}

        {tab === 'employees' && (
          <ReportCard title="Employee payroll report" description="One row per employee and payroll period." actions={<ExportButtons label="Export employee report" onExport={exportCurrent} />}>
            {employeeError ? (
              <ErrorState error={employeeError} onRetry={reloadEmployees} />
            ) : employeeLoading && !employeePage ? (
              <Loading label="Loading employee report…" />
            ) : !employeePage || employeePage.items.length === 0 ? (
              <EmptyState icon={Users} title="No payroll matches these filters" description="Try other filters or reset them." />
            ) : (
              <>
                <p className="text-sm text-base-content/60">{employeePage.totalCount} payroll records</p>
                <Table columns={employeeColumns} rows={employeePage.items} rowKey={(r) => r.id} compact />
                <Pagination
                  page={employeePage.page}
                  pageCount={Math.max(1, employeePage.totalPages)}
                  pageSize={employeePage.pageSize}
                  total={employeePage.totalCount}
                  onPageChange={(next) => update({ page: next })}
                  onPageSizeChange={(size) => update({ pageSize: size, page: undefined })}
                />
              </>
            )}
          </ReportCard>
        )}

        {tab === 'deductions' && (
          <ReportCard
            title="Deduction summary"
            description="Tax, provident fund, unpaid leave, advance, loan and other deductions."
            actions={
              <div className="flex flex-wrap gap-2">
                <GroupToggle value={groupBy} onChange={(value) => update({ groupBy: value === 'period' ? undefined : value })} />
                <ExportButtons label="Export deduction summary" onExport={exportCurrent} />
              </div>
            }
          >
            <GroupedReport
              kind="deductions"
              filters={filters}
              groupBy={groupBy}
              render={(report) => (
                <Table
                  columns={[
                    labelColumn(groupBy === 'department' ? 'Department' : 'Payroll period'),
                    amountColumn('tax', 'Tax'),
                    amountColumn('providentFund', 'Provident fund', 'hidden sm:table-cell text-right'),
                    amountColumn('leaveDeduction', 'Unpaid leave', 'hidden md:table-cell text-right'),
                    amountColumn('advanceDeduction', 'Advance', 'hidden lg:table-cell text-right'),
                    amountColumn('loanDeduction', 'Loan', 'hidden lg:table-cell text-right'),
                    amountColumn('otherDeduction', 'Other', 'hidden xl:table-cell text-right'),
                    amountColumn('totalDeduction', 'Total deduction'),
                  ]}
                  rows={withTotal(report)}
                  rowKey={(r) => (r.isTotal ? '__total' : (r.payrollPeriodId ?? r.label))}
                  compact
                />
              )}
            />
          </ReportCard>
        )}

        {tab === 'allowances' && (
          <ReportCard
            title="Allowance and bonus summary"
            description="House rent, medical, transport and other allowances, overtime and bonus."
            actions={
              <div className="flex flex-wrap gap-2">
                <GroupToggle value={groupBy} onChange={(value) => update({ groupBy: value === 'period' ? undefined : value })} />
                <ExportButtons label="Export allowance summary" onExport={exportCurrent} />
              </div>
            }
          >
            <GroupedReport
              kind="allowances"
              filters={filters}
              groupBy={groupBy}
              render={(report) => (
                <Table
                  columns={[
                    labelColumn(groupBy === 'department' ? 'Department' : 'Payroll period'),
                    amountColumn('houseRent', 'House rent', 'hidden sm:table-cell text-right'),
                    amountColumn('medicalAllowance', 'Medical', 'hidden md:table-cell text-right'),
                    amountColumn('transportAllowance', 'Transport', 'hidden lg:table-cell text-right'),
                    amountColumn('otherAllowance', 'Other', 'hidden xl:table-cell text-right'),
                    amountColumn('totalAllowances', 'Total allowances'),
                    amountColumn('overtime', 'Overtime', 'hidden md:table-cell text-right'),
                    amountColumn('bonus', 'Bonus'),
                  ]}
                  rows={withTotal(report)}
                  rowKey={(r) => (r.isTotal ? '__total' : (r.payrollPeriodId ?? r.label))}
                  compact
                />
              )}
            />
          </ReportCard>
        )}

        <p className="flex items-center gap-2 text-xs text-base-content/50">
          <CircleDollarSign className="size-4" /> Amounts are the payroll values calculated and stored by the server; nothing is recalculated in the browser.
        </p>
      </div>
    </>
  )
}
