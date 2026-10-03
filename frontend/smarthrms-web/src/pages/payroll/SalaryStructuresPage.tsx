import { zodResolver } from '@hookform/resolvers/zod'
import { AlertTriangle, Pencil, Search, Wallet } from 'lucide-react'
import { useCallback, useMemo, useState } from 'react'
import { useForm } from 'react-hook-form'
import { z } from 'zod'
import { ApiErrorAlert } from '../../components/common/ApiErrorAlert'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { FormField } from '../../components/common/FormField'
import { Loading } from '../../components/common/Loading'
import { Modal } from '../../components/common/Modal'
import { PageHeader } from '../../components/common/PageHeader'
import { Pagination } from '../../components/common/Pagination'
import { Table, type Column } from '../../components/common/Table'
import { amountSchema } from '../../components/payroll/amountSchema'
import { useApi } from '../../hooks/useApi'
import { usePagination } from '../../hooks/usePagination'
import { useToast } from '../../hooks/useToast'
import { getSalaryStructures, updateSalaryStructure } from '../../services/payrollService'
import type { SalaryStructure } from '../../types/payroll'
import { enumLabel, formatAmount } from '../../utils/formatters'

const amount = amountSchema

const schema = z.object({
  basicSalary: amount,
  houseRent: amount,
  medicalAllowance: amount,
  transportAllowance: amount,
  otherAllowance: amount,
  monthlyTax: amount,
  monthlyProvidentFund: amount,
})
type FormInput = z.input<typeof schema>
type FormOutput = z.output<typeof schema>

const FIELDS: { name: keyof FormOutput; label: string }[] = [
  { name: 'basicSalary', label: 'Basic salary' },
  { name: 'houseRent', label: 'House rent' },
  { name: 'medicalAllowance', label: 'Medical allowance' },
  { name: 'transportAllowance', label: 'Transport allowance' },
  { name: 'otherAllowance', label: 'Other allowance' },
  { name: 'monthlyTax', label: 'Monthly tax' },
  { name: 'monthlyProvidentFund', label: 'Provident fund (monthly)' },
]

function SalaryModal({ structure, onClose, onSaved }: { structure: SalaryStructure; onClose: () => void; onSaved: () => void }) {
  const { notify } = useToast()
  const [error, setError] = useState<unknown>(null)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormInput, unknown, FormOutput>({
    resolver: zodResolver(schema),
    defaultValues: {
      basicSalary: structure.basicSalary ?? '',
      houseRent: structure.houseRent,
      medicalAllowance: structure.medicalAllowance,
      transportAllowance: structure.transportAllowance,
      otherAllowance: structure.otherAllowance,
      monthlyTax: structure.monthlyTax,
      monthlyProvidentFund: structure.monthlyProvidentFund,
    },
  })

  const submit = handleSubmit(async (values) => {
    setError(null)
    try {
      await updateSalaryStructure(structure.employeeId, values)
      notify('success', `Salary of ${structure.employeeName} saved. It applies from the next payroll calculation.`)
      onSaved()
      onClose()
    } catch (caught) {
      setError(caught)
    }
  })

  return (
    <Modal open title={`Salary — ${structure.employeeName}`} onClose={onClose} busy={isSubmitting}>
      <form className="flex flex-col gap-3" onSubmit={submit} noValidate>
        <p className="text-sm text-base-content/70">Monthly amounts; tax and provident fund are withheld from salary. Payroll that is already calculated keeps its numbers until it is recalculated.</p>
        {error !== null && <ApiErrorAlert error={error} />}
        <div className="grid gap-3 sm:grid-cols-2">
          {FIELDS.map(({ name, label }) => (
            <FormField key={name} label={label} htmlFor={`ss-${name}`} error={errors[name]?.message} required={name === 'basicSalary'}>
              <input id={`ss-${name}`} type="number" min="0" step="0.01" inputMode="decimal" className="input w-full" {...register(name)} />
            </FormField>
          ))}
        </div>
        <div className="modal-action">
          <button type="button" className="btn btn-ghost" onClick={onClose} disabled={isSubmitting}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
            {isSubmitting && <span className="loading loading-spinner loading-sm" />} Save salary
          </button>
        </div>
      </form>
    </Modal>
  )
}

/** Basic salary, allowances and tax of every employee (HR/Admin). Employees without a basic salary are skipped by payroll. */
export default function SalaryStructuresPage() {
  const [search, setSearch] = useState('')
  const [onlyMissing, setOnlyMissing] = useState(false)
  const [editing, setEditing] = useState<SalaryStructure | null>(null)
  const load = useCallback((signal: AbortSignal) => getSalaryStructures(signal), [])
  const { data, error, loading, reload } = useApi(load)

  const missing = (data ?? []).filter((s) => s.basicSalary === null && (s.employeeStatus === 'Active' || s.employeeStatus === 'OnLeave')).length
  const rows = useMemo(() => {
    const term = search.trim().toLowerCase()
    return (data ?? []).filter(
      (s) =>
        (!onlyMissing || s.basicSalary === null) &&
        (!term || s.employeeName.toLowerCase().includes(term) || s.employeeCode.toLowerCase().includes(term)),
    )
  }, [data, search, onlyMissing])
  const pagination = usePagination(rows, 25)

  const columns: Column<SalaryStructure>[] = [
    {
      key: 'employee',
      header: 'Employee',
      render: (s) => (
        <div className="min-w-0">
          <p className="truncate font-medium">{s.employeeName}</p>
          <p className="text-xs text-base-content/60">
            {s.employeeCode} · {enumLabel(s.employeeStatus)}
          </p>
        </div>
      ),
    },
    { key: 'department', header: 'Department', render: (s) => s.departmentName ?? '—', className: 'hidden lg:table-cell' },
    {
      key: 'basic',
      header: 'Basic',
      className: 'text-right',
      render: (s) => (s.basicSalary === null ? <span className="badge badge-sm badge-soft badge-warning">Not set</span> : <span className="tabular-nums">{formatAmount(s.basicSalary)}</span>),
    },
    {
      key: 'allowances',
      header: 'Allowances',
      className: 'hidden md:table-cell text-right',
      render: (s) => <span className="tabular-nums">{formatAmount(s.houseRent + s.medicalAllowance + s.transportAllowance + s.otherAllowance)}</span>,
    },
    { key: 'tax', header: 'Tax + PF', className: 'hidden sm:table-cell text-right', render: (s) => <span className="tabular-nums">{formatAmount(s.monthlyTax + s.monthlyProvidentFund)}</span> },
    { key: 'gross', header: 'Monthly gross', className: 'text-right', render: (s) => <span className="font-semibold tabular-nums">{formatAmount(s.monthlyGross)}</span> },
    {
      key: 'actions',
      header: <span className="sr-only">Actions</span>,
      className: 'text-right',
      render: (s) => (
        <button type="button" className="btn btn-ghost btn-xs btn-square" onClick={() => setEditing(s)} title="Edit salary" aria-label={`Edit salary of ${s.employeeName}`}>
          <Pencil className="size-4" />
        </button>
      ),
    },
  ]

  return (
    <>
      <PageHeader title="Salary structures" description="Monthly basic salary, allowances and tax used by payroll." />

      {missing > 0 && (
        <div role="note" className="alert alert-warning alert-soft mb-6">
          <AlertTriangle className="size-5 shrink-0" />
          <span className="text-sm">
            {missing} current employees have no basic salary and will be skipped by payroll.{' '}
            <button type="button" className="link" onClick={() => setOnlyMissing(true)}>
              Show them
            </button>
          </span>
        </div>
      )}

      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-4 p-4 sm:p-6">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
            <label className="input input-sm w-full sm:max-w-xs">
              <Search className="size-4 opacity-50" />
              <input type="search" placeholder="Search name or employee ID" value={search} onChange={(e) => setSearch(e.target.value)} aria-label="Search" />
            </label>
            <label className="label cursor-pointer gap-2 text-sm">
              <input type="checkbox" className="checkbox checkbox-sm" checked={onlyMissing} onChange={(e) => setOnlyMissing(e.target.checked)} />
              Only without basic salary
            </label>
          </div>

          {error ? (
            <ErrorState error={error} onRetry={reload} />
          ) : loading && !data ? (
            <Loading label="Loading salaries…" />
          ) : rows.length === 0 ? (
            <EmptyState icon={Wallet} title={data?.length ? 'No employees match' : 'No employees yet'} />
          ) : (
            <>
              <Table columns={columns} rows={pagination.pageItems} rowKey={(s) => s.employeeId} compact />
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

      {editing && <SalaryModal structure={editing} onClose={() => setEditing(null)} onSaved={reload} />}
    </>
  )
}
