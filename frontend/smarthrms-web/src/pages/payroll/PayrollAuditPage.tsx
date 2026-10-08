import { RotateCcw, ScrollText, Search } from 'lucide-react'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { Loading } from '../../components/common/Loading'
import { PageHeader } from '../../components/common/PageHeader'
import { Table, type Column } from '../../components/common/Table'
import { useApi } from '../../hooks/useApi'
import { getAuditLog } from '../../services/auditService'
import { PAYROLL_AUDIT_ACTIONS, type AuditLogEntry, type AuditLogQuery } from '../../types/audit'
import { enumLabel, formatDateTime } from '../../utils/formatters'

const DATE = /^\d{4}-\d{2}-\d{2}$/
const TAKES = [50, 100, 250, 500]

function queryFromParams(params: URLSearchParams): AuditLogQuery {
  const action = params.get('action') ?? ''
  const from = params.get('from') ?? ''
  const to = params.get('to') ?? ''
  const take = Number(params.get('take'))
  return {
    category: 'payroll',
    action: PAYROLL_AUDIT_ACTIONS.some((a) => a === action) ? action : undefined,
    username: params.get('username') || undefined,
    entityId: params.get('entityId') || undefined,
    from: DATE.test(from) ? from : undefined,
    to: DATE.test(to) ? to : undefined,
    take: TAKES.includes(take) ? take : 100,
  }
}

/** Links an entry to the page of the thing it is about, where one exists. */
function entityLink(entry: AuditLogEntry) {
  if (!entry.entityId) return enumLabel(entry.entityType)
  const to =
    entry.entityType === 'PayrollPeriod' ? `/payroll/${entry.entityId}` : entry.entityType === 'Payslip' ? `/payroll/payslips/${entry.entityId}` : entry.entityType === 'PaymentBatch' ? `/payments/batches/${entry.entityId}` : null
  return to ? (
    <Link to={to} className="link">
      {enumLabel(entry.entityType)}
    </Link>
  ) : (
    enumLabel(entry.entityType)
  )
}

/**
 * Payroll audit trail (Admin): who did what to payroll, payslips, salary structures, payments and reports, and when.
 * Read from the existing audit log; entries never contain salary amounts.
 */
export default function PayrollAuditPage() {
  const [params, setParams] = useSearchParams()
  const query = useMemo(() => queryFromParams(params), [params])
  const [userInput, setUserInput] = useState(query.username ?? '')

  const update = useCallback(
    (patch: Record<string, string | number | undefined>) =>
      setParams(
        (current) => {
          const next = new URLSearchParams(current)
          for (const [key, value] of Object.entries(patch)) {
            if (value === undefined || value === '') next.delete(key)
            else next.set(key, String(value))
          }
          return next
        },
        { replace: true },
      ),
    [setParams],
  )

  useEffect(() => {
    if (userInput.trim() === (query.username ?? '')) return
    const timer = window.setTimeout(() => update({ username: userInput.trim() }), 350)
    return () => window.clearTimeout(timer)
  }, [userInput, query.username, update])

  const load = useCallback((signal: AbortSignal) => getAuditLog(query, signal), [query])
  const { data, error, loading, reload } = useApi(load)
  const filtered = !!(query.action || query.username || query.entityId || query.from || query.to)

  const reset = () => {
    setUserInput('')
    setParams(new URLSearchParams(), { replace: true })
  }

  const columns: Column<AuditLogEntry>[] = [
    { key: 'time', header: 'Time', render: (e) => <span className="whitespace-nowrap">{formatDateTime(e.occurredAt)}</span> },
    { key: 'user', header: 'User', render: (e) => e.username ?? '—' },
    { key: 'action', header: 'Action', render: (e) => <span className="badge badge-sm badge-outline whitespace-nowrap">{enumLabel(e.action)}</span> },
    { key: 'entity', header: 'Entity', render: entityLink, className: 'hidden md:table-cell' },
    { key: 'details', header: 'Details', render: (e) => <span className="text-base-content/80">{e.details ?? '—'}</span> },
  ]

  return (
    <>
      <PageHeader title="Payroll audit trail" description="Payroll, payslip, salary, payment and report actions: who, what and when (newest first)." />

      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-4 p-4 sm:p-6">
          <div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
            <select className="select select-sm w-full" value={query.action ?? ''} onChange={(e) => update({ action: e.target.value })} aria-label="Action">
              <option value="">All payroll actions</option>
              {PAYROLL_AUDIT_ACTIONS.map((a) => (
                <option key={a} value={a}>
                  {enumLabel(a)}
                </option>
              ))}
            </select>
            <label className="input input-sm w-full">
              <Search className="size-4 opacity-50" />
              <input type="search" placeholder="User name" value={userInput} onChange={(e) => setUserInput(e.target.value)} aria-label="User" />
            </label>
            <div className="grid grid-cols-2 gap-2">
              <input type="date" className="input input-sm w-full" value={query.from ?? ''} max={query.to} onChange={(e) => update({ from: e.target.value })} aria-label="From date" />
              <input type="date" className="input input-sm w-full" value={query.to ?? ''} min={query.from} onChange={(e) => update({ to: e.target.value })} aria-label="To date" />
            </div>
            <div className="flex gap-2">
              <select className="select select-sm w-full" value={query.take} onChange={(e) => update({ take: e.target.value })} aria-label="Entries">
                {TAKES.map((t) => (
                  <option key={t} value={t}>
                    Latest {t}
                  </option>
                ))}
              </select>
              <button type="button" className="btn btn-sm btn-ghost" onClick={reset} disabled={!filtered}>
                <RotateCcw className="size-4" /> Reset
              </button>
            </div>
          </div>
          {query.entityId && (
            <p className="text-sm text-base-content/60">
              Showing one entity only.{' '}
              <button type="button" className="link" onClick={() => update({ entityId: undefined })}>
                Show all
              </button>
            </p>
          )}

          {error ? (
            <ErrorState error={error} onRetry={reload} />
          ) : loading && !data ? (
            <Loading label="Loading audit trail…" />
          ) : !data || data.length === 0 ? (
            <EmptyState icon={ScrollText} title={filtered ? 'No entries match these filters' : 'No payroll activity yet'} description="Payroll, payslip, salary, payment and report actions are recorded here." />
          ) : (
            <>
              <p className="text-sm text-base-content/60">
                {data.length} entries{data.length === query.take ? ` (latest ${query.take}; narrow the filters to see older ones)` : ''}
                {loading && <span className="loading loading-spinner loading-xs ml-2 align-middle" />}
              </p>
              <Table columns={columns} rows={data} rowKey={(e) => e.id} compact />
            </>
          )}
        </div>
      </div>
    </>
  )
}
