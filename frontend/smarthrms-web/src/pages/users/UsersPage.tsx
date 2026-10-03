import { zodResolver } from '@hookform/resolvers/zod'
import { KeyRound, Lock, Pencil, ShieldCheck, UserPlus } from 'lucide-react'
import { useCallback, useState } from 'react'
import { useForm, useWatch } from 'react-hook-form'
import { z } from 'zod'
import { ApiErrorAlert } from '../../components/common/ApiErrorAlert'
import { EmptyState } from '../../components/common/EmptyState'
import { ErrorState } from '../../components/common/ErrorState'
import { FormField } from '../../components/common/FormField'
import { Loading } from '../../components/common/Loading'
import { Modal } from '../../components/common/Modal'
import { PageHeader } from '../../components/common/PageHeader'
import { Table, type Column } from '../../components/common/Table'
import { passwordSchema } from '../../components/layout/passwordSchema'
import { useApi } from '../../hooks/useApi'
import { useAuth } from '../../hooks/useAuth'
import { useToast } from '../../hooks/useToast'
import { getEmployees } from '../../services/employeeService'
import { createUser, getUsers, resetPassword, updateUser } from '../../services/userService'
import { USER_ROLES, type UserRole } from '../../types/auth'
import type { Employee } from '../../types/employee'
import type { User } from '../../types/user'
import { formatDateTime } from '../../utils/formatters'

const ROLE_HELP: Record<UserRole, string> = {
  Employee: 'Own profile, attendance, leave and payslips.',
  Manager: 'Employee, plus reviewing the leave of direct reports.',
  HR: 'People, attendance, leave and payroll (cannot approve payroll).',
  Admin: 'Everything, including users and payroll approval.',
}

const needsEmployee = (role: UserRole) => role === 'Employee' || role === 'Manager'

const userSchema = z
  .object({
    mode: z.enum(['create', 'edit']),
    username: z.string().trim(),
    password: z.string(),
    role: z.enum(USER_ROLES),
    employeeId: z.string(),
    isActive: z.boolean(),
  })
  .superRefine((v, ctx) => {
    if (v.mode === 'create') {
      if (!/^[A-Za-z0-9._@-]{1,100}$/.test(v.username)) ctx.addIssue({ code: 'custom', path: ['username'], message: 'Letters, digits and . _ @ - only (max 100).' })
      const password = passwordSchema.safeParse(v.password)
      if (!password.success) ctx.addIssue({ code: 'custom', path: ['password'], message: password.error.issues[0].message })
    }
    if (needsEmployee(v.role) && !v.employeeId) ctx.addIssue({ code: 'custom', path: ['employeeId'], message: `A ${v.role} account must be linked to an employee.` })
  })
type UserForm = z.infer<typeof userSchema>

function UserModal({ user, employees, onClose, onSaved }: { user?: User; employees: Employee[]; onClose: () => void; onSaved: () => void }) {
  const { notify } = useToast()
  const [error, setError] = useState<unknown>(null)
  const {
    register,
    handleSubmit,
    control,
    formState: { errors, isSubmitting },
  } = useForm<UserForm>({
    resolver: zodResolver(userSchema),
    defaultValues: { mode: user ? 'edit' : 'create', username: user?.username ?? '', password: '', role: user?.role ?? 'Employee', employeeId: user?.employeeId ?? '', isActive: user?.isActive ?? true },
  })
  const role = useWatch({ control, name: 'role' })

  const submit = handleSubmit(async (values) => {
    setError(null)
    try {
      if (user) {
        await updateUser(user.id, { role: values.role, isActive: values.isActive, employeeId: values.employeeId || null })
        notify('success', `${user.username} updated. Their existing sessions have ended.`)
      } else {
        await createUser({ username: values.username, password: values.password, role: values.role, employeeId: values.employeeId || null })
        notify('success', `Account ${values.username} created.`)
      }
      onSaved()
      onClose()
    } catch (caught) {
      setError(caught)
    }
  })

  return (
    <Modal open title={user ? `Edit ${user.username}` : 'New user'} onClose={onClose} busy={isSubmitting}>
      <form className="flex flex-col gap-3" onSubmit={submit} noValidate>
        {error !== null && <ApiErrorAlert error={error} />}
        {!user && (
          <>
            <FormField label="Username" htmlFor="u-name" error={errors.username?.message} required>
              <input id="u-name" className="input w-full" autoComplete="off" {...register('username')} />
            </FormField>
            <FormField label="Initial password" htmlFor="u-pass" error={errors.password?.message} hint="At least 8 characters with a letter and a digit. Share it securely." required>
              <input id="u-pass" type="password" className="input w-full" autoComplete="new-password" {...register('password')} />
            </FormField>
          </>
        )}
        <FormField label="Role" htmlFor="u-role" hint={ROLE_HELP[role]} required>
          <select id="u-role" className="select w-full" {...register('role')}>
            {USER_ROLES.map((r) => (
              <option key={r} value={r}>
                {r}
              </option>
            ))}
          </select>
        </FormField>
        <FormField label="Employee" htmlFor="u-emp" error={errors.employeeId?.message} hint={needsEmployee(role) ? 'Required for this role.' : 'Optional for HR and Admin.'} required={needsEmployee(role)}>
          <select id="u-emp" className="select w-full" {...register('employeeId')}>
            <option value="">No employee</option>
            {employees.map((e) => (
              <option key={e.id} value={e.id}>
                {e.fullName} ({e.employeeCode})
              </option>
            ))}
          </select>
        </FormField>
        {user && (
          <label className="label cursor-pointer gap-2">
            <input type="checkbox" className="toggle toggle-sm toggle-success" {...register('isActive')} />
            <span className="text-sm">Account active</span>
          </label>
        )}
        <div className="modal-action">
          <button type="button" className="btn btn-ghost" onClick={onClose} disabled={isSubmitting}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
            {isSubmitting && <span className="loading loading-spinner loading-sm" />} {user ? 'Save' : 'Create user'}
          </button>
        </div>
      </form>
    </Modal>
  )
}

const resetSchema = z.object({ password: passwordSchema })

function ResetPasswordModal({ user, onClose, onSaved }: { user: User; onClose: () => void; onSaved: () => void }) {
  const { notify } = useToast()
  const [error, setError] = useState<unknown>(null)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<{ password: string }>({ resolver: zodResolver(resetSchema), defaultValues: { password: '' } })

  const submit = handleSubmit(async ({ password }) => {
    setError(null)
    try {
      await resetPassword(user.id, password)
      notify('success', `Password of ${user.username} reset; the account is unlocked.`)
      onSaved()
      onClose()
    } catch (caught) {
      setError(caught)
    }
  })

  return (
    <Modal open title={`Reset password — ${user.username}`} onClose={onClose} busy={isSubmitting}>
      <form className="flex flex-col gap-3" onSubmit={submit} noValidate>
        {error !== null && <ApiErrorAlert error={error} />}
        <FormField label="New password" htmlFor="r-pass" error={errors.password?.message} hint="Also unlocks the account and ends its sessions." required>
          <input id="r-pass" type="password" className="input w-full" autoComplete="new-password" {...register('password')} />
        </FormField>
        <div className="modal-action">
          <button type="button" className="btn btn-ghost" onClick={onClose} disabled={isSubmitting}>
            Cancel
          </button>
          <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
            {isSubmitting && <span className="loading loading-spinner loading-sm" />} Reset password
          </button>
        </div>
      </form>
    </Modal>
  )
}

/** Sign-in accounts and roles (Admin only). */
export default function UsersPage() {
  const { user: me } = useAuth()
  const [editing, setEditing] = useState<User | 'new' | null>(null)
  const [resetting, setResetting] = useState<User | null>(null)
  const load = useCallback((signal: AbortSignal) => Promise.all([getUsers(signal), getEmployees(signal)]), [])
  const { data, error, loading, reload } = useApi(load)
  const [users, employees] = data ?? [[], []]
  const linked = new Set(users.map((u) => u.employeeId).filter(Boolean))

  const columns: Column<User>[] = [
    {
      key: 'username',
      header: 'Username',
      render: (u) => (
        <div>
          <p className="font-medium">
            {u.username} {u.id === me?.id && <span className="badge badge-xs badge-outline ml-1">You</span>}
          </p>
          <p className="text-xs text-base-content/60">{u.employeeName ? `${u.employeeName} (${u.employeeCode})` : 'No employee'}</p>
        </div>
      ),
    },
    { key: 'role', header: 'Role', render: (u) => <span className="badge badge-sm badge-soft badge-primary">{u.role}</span> },
    {
      key: 'status',
      header: 'Status',
      render: (u) => (
        <div className="flex flex-wrap gap-1">
          <span className={`badge badge-sm ${u.isActive ? 'badge-soft badge-success' : 'badge-outline'}`}>{u.isActive ? 'Active' : 'Inactive'}</span>
          {u.lockoutEndAt && Date.parse(u.lockoutEndAt) > Date.now() && (
            <span className="badge badge-sm badge-soft badge-error gap-1" title={`Locked until ${formatDateTime(u.lockoutEndAt)}`}>
              <Lock className="size-3" /> Locked
            </span>
          )}
        </div>
      ),
    },
    { key: 'login', header: 'Last sign-in', render: (u) => formatDateTime(u.lastLoginAt), className: 'hidden md:table-cell' },
    {
      key: 'actions',
      header: <span className="sr-only">Actions</span>,
      className: 'text-right',
      render: (u) => (
        <div className="flex justify-end gap-1">
          <button type="button" className="btn btn-ghost btn-xs btn-square" onClick={() => setEditing(u)} title="Edit" aria-label={`Edit ${u.username}`}>
            <Pencil className="size-4" />
          </button>
          <button type="button" className="btn btn-ghost btn-xs btn-square" onClick={() => setResetting(u)} title="Reset password" aria-label={`Reset password of ${u.username}`}>
            <KeyRound className="size-4" />
          </button>
        </div>
      ),
    },
  ]

  const editingUser = editing === 'new' ? undefined : editing ?? undefined
  // An employee can only have one account: offer unlinked employees (plus the one already linked to this user).
  const selectable = employees.filter((e) => e.isActive && (!linked.has(e.id) || e.id === editingUser?.employeeId))

  return (
    <>
      <PageHeader
        title="Users & roles"
        description="Who can sign in, and what each role can do."
        actions={
          <button type="button" className="btn btn-primary btn-sm" onClick={() => setEditing('new')}>
            <UserPlus className="size-4" /> New user
          </button>
        }
      />

      <div className="card bg-base-100 shadow-sm">
        <div className="card-body gap-3 p-4 sm:p-6">
          {error ? (
            <ErrorState error={error} onRetry={reload} />
          ) : loading && !data ? (
            <Loading label="Loading users…" />
          ) : users.length === 0 ? (
            <EmptyState icon={ShieldCheck} title="No users yet" />
          ) : (
            <Table columns={columns} rows={users} rowKey={(u) => u.id} compact />
          )}
        </div>
      </div>

      {editing && <UserModal user={editingUser} employees={selectable} onClose={() => setEditing(null)} onSaved={reload} />}
      {resetting && <ResetPasswordModal user={resetting} onClose={() => setResetting(null)} onSaved={reload} />}
    </>
  )
}
