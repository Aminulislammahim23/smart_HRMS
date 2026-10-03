import { zodResolver } from '@hookform/resolvers/zod'
import { Info, LogIn } from 'lucide-react'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { Navigate, useLocation, useNavigate, type Location } from 'react-router-dom'
import { z } from 'zod'
import { ApiErrorAlert } from '../../components/common/ApiErrorAlert'
import { FormField } from '../../components/common/FormField'
import { useAuth } from '../../hooks/useAuth'

const schema = z.object({
  username: z.string().trim().min(1, 'Enter your username.').max(100),
  password: z.string().min(1, 'Enter your password.').max(128),
})
type LoginForm = z.infer<typeof schema>

/** Sign-in. After success the user returns to the page they first asked for. */
export default function Login() {
  const { status, login, signedOutReason } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const from = (location.state as { from?: Location } | null)?.from
  const target = from && from.pathname !== '/login' ? `${from.pathname}${from.search}` : '/dashboard'
  const [error, setError] = useState<unknown>(null)

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginForm>({ resolver: zodResolver(schema), defaultValues: { username: '', password: '' } })

  if (status === 'signedIn') return <Navigate to={target} replace />

  const submit = handleSubmit(async (values) => {
    setError(null)
    try {
      await login(values)
      navigate(target, { replace: true })
    } catch (caught) {
      setError(caught)
    }
  })

  return (
    <div className="card bg-base-100 shadow-md">
      <form className="card-body gap-4" onSubmit={submit} noValidate>
        <div>
          <h1 className="text-xl font-semibold">Sign in</h1>
          <p className="text-sm text-base-content/60">Access the SmartHRMS workspace.</p>
        </div>

        {signedOutReason && !error && (
          <div role="status" className="alert alert-info alert-soft">
            <Info className="size-5 shrink-0" />
            <span className="text-sm">{signedOutReason}</span>
          </div>
        )}
        {error !== null && <ApiErrorAlert error={error} />}

        <FormField label="Username" htmlFor="login-username" error={errors.username?.message} required>
          <input id="login-username" className="input w-full" autoComplete="username" autoFocus {...register('username')} />
        </FormField>
        <FormField label="Password" htmlFor="login-password" error={errors.password?.message} required>
          <input id="login-password" type="password" className="input w-full" autoComplete="current-password" {...register('password')} />
        </FormField>

        <button type="submit" className="btn btn-primary" disabled={isSubmitting}>
          {isSubmitting ? <span className="loading loading-spinner loading-sm" /> : <LogIn className="size-4" />}
          Sign in
        </button>
        <p className="text-center text-xs text-base-content/50">Forgot your password? Ask an administrator to reset it.</p>
      </form>
    </div>
  )
}
