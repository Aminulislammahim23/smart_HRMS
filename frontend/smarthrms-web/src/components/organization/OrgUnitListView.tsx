import { Pencil, Plus, Power, PowerOff, Search } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useApi } from '../../hooks/useApi'
import { usePagination } from '../../hooks/usePagination'
import { useToast } from '../../hooks/useToast'
import { errorMessage } from '../../utils/errors'
import { formatDateTime } from '../../utils/formatters'
import { ConfirmDialog } from '../common/ConfirmDialog'
import { EmptyState } from '../common/EmptyState'
import { ErrorState } from '../common/ErrorState'
import { Loading } from '../common/Loading'
import { PageHeader } from '../common/PageHeader'
import { Pagination } from '../common/Pagination'
import { ActiveBadge } from '../common/StatusBadge'
import { Table, type Column } from '../common/Table'
import type { OrgUnit, OrgUnitModule } from './orgUnits'

type StatusFilter = 'all' | 'active' | 'inactive'

export function OrgUnitListView({ module }: { module: OrgUnitModule }) {
  const { notify } = useToast()
  const { data, error, loading, reload } = useApi(module.list)
  const [search, setSearch] = useState('')
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all')
  const [pendingDeactivate, setPendingDeactivate] = useState<OrgUnit | null>(null)
  const [activatingId, setActivatingId] = useState<string | null>(null)

  const filtered = useMemo(() => {
    const term = search.trim().toLowerCase()
    return (data ?? []).filter(
      (unit) =>
        (statusFilter === 'all' || unit.isActive === (statusFilter === 'active')) &&
        (!term || unit.name.toLowerCase().includes(term) || unit.description?.toLowerCase().includes(term)),
    )
  }, [data, search, statusFilter])

  const pagination = usePagination(filtered)
  const noun = module.singular.toLowerCase()

  const activate = async (unit: OrgUnit) => {
    setActivatingId(unit.id)
    try {
      await module.update(unit.id, { name: unit.name, description: unit.description, isActive: true })
      notify('success', `${module.singular} "${unit.name}" activated.`)
      reload()
    } catch (caught) {
      notify('error', errorMessage(caught))
    } finally {
      setActivatingId(null)
    }
  }

  const columns: Column<OrgUnit>[] = [
    {
      key: 'name',
      header: 'Name',
      render: (unit) => (
        <div className="min-w-48">
          <p className="font-medium">{unit.name}</p>
          {unit.description && <p className="line-clamp-1 text-xs text-base-content/60">{unit.description}</p>}
        </div>
      ),
    },
    { key: 'employees', header: 'Employees', className: 'text-center', render: (unit) => unit.employeeCount },
    { key: 'status', header: 'Status', render: (unit) => <ActiveBadge isActive={unit.isActive} /> },
    {
      key: 'updated',
      header: 'Last updated',
      className: 'hidden md:table-cell whitespace-nowrap text-sm text-base-content/70',
      render: (unit) => formatDateTime(unit.updatedAt ?? unit.createdAt),
    },
    {
      key: 'actions',
      header: <span className="sr-only">Actions</span>,
      className: 'text-right',
      render: (unit) => (
        <div className="flex justify-end gap-1">
          <Link to={`${module.basePath}/${unit.id}/edit`} className="btn btn-ghost btn-xs" aria-label={`Edit ${unit.name}`}>
            <Pencil className="size-3.5" /> Edit
          </Link>
          {unit.isActive ? (
            <button type="button" className="btn btn-ghost btn-xs text-error" onClick={() => setPendingDeactivate(unit)}>
              <PowerOff className="size-3.5" /> Deactivate
            </button>
          ) : (
            <button type="button" className="btn btn-ghost btn-xs text-success" onClick={() => activate(unit)} disabled={activatingId === unit.id}>
              {activatingId === unit.id ? <span className="loading loading-spinner loading-xs" /> : <Power className="size-3.5" />} Activate
            </button>
          )}
        </div>
      ),
    },
  ]

  return (
    <>
      <PageHeader
        title={module.plural}
        description={`Manage the ${module.plural.toLowerCase()} employees can be assigned to.`}
        actions={
          <Link to={`${module.basePath}/create`} className="btn btn-primary btn-sm">
            <Plus className="size-4" /> New {noun}
          </Link>
        }
      />

      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-4 p-4 sm:p-6">
          <div className="flex flex-col gap-2 sm:flex-row">
            <label className="input w-full sm:max-w-xs">
              <Search className="size-4 opacity-50" />
              <input type="search" placeholder={`Search ${module.plural.toLowerCase()}`} value={search} onChange={(e) => setSearch(e.target.value)} />
            </label>
            <select className="select w-full sm:w-44" value={statusFilter} onChange={(e) => setStatusFilter(e.target.value as StatusFilter)} aria-label="Status">
              <option value="all">All statuses</option>
              <option value="active">Active</option>
              <option value="inactive">Inactive</option>
            </select>
          </div>

          {error ? (
            <ErrorState error={error} onRetry={reload} />
          ) : loading && !data ? (
            <Loading />
          ) : filtered.length === 0 ? (
            <EmptyState
              title={data?.length ? `No ${module.plural.toLowerCase()} match your filters` : `No ${module.plural.toLowerCase()} yet`}
              description={data?.length ? 'Try a different search or status.' : `Create the first ${noun} to get started.`}
            />
          ) : (
            <>
              <Table columns={columns} rows={pagination.pageItems} rowKey={(unit) => unit.id} />
              <Pagination
                page={pagination.page}
                pageCount={pagination.pageCount}
                pageSize={pagination.pageSize}
                total={pagination.total}
                onPageChange={pagination.setPage}
                onPageSizeChange={pagination.setPageSize}
              />
            </>
          )}
        </div>
      </div>

      <ConfirmDialog
        open={pendingDeactivate !== null}
        title={`Deactivate ${noun}?`}
        message={`"${pendingDeactivate?.name}" will be marked inactive and can no longer be assigned to employees. It is not deleted and can be activated again.`}
        confirmLabel="Deactivate"
        onCancel={() => setPendingDeactivate(null)}
        onConfirm={async () => {
          if (!pendingDeactivate) return
          const message = await module.deactivate(pendingDeactivate.id)
          notify('success', message)
          setPendingDeactivate(null)
          reload()
        }}
      />
    </>
  )
}
