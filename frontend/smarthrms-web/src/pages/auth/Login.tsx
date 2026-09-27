import { Info, LogIn } from 'lucide-react'
import { Link } from 'react-router-dom'

/**
 * The SmartHRMS API has no login endpoint yet, so this page cannot sign anyone in. It is kept as the entry point
 * that real authentication will plug into, and it says plainly that sign-in is pending.
 */
export default function Login() {
  return (
    <div className="card bg-base-100 shadow-md">
      <div className="card-body gap-4">
        <div>
          <h1 className="text-xl font-semibold">Sign in</h1>
          <p className="text-sm text-base-content/60">Access the SmartHRMS workspace.</p>
        </div>

        <div role="alert" className="alert alert-info alert-soft items-start">
          <Info className="size-5 shrink-0" />
          <span className="text-sm">
            Sign-in is not available yet: the SmartHRMS API does not provide authentication. Until it does, the workspace
            is open without signing in.
          </span>
        </div>

        <fieldset className="fieldset" disabled>
          <label className="fieldset-legend" htmlFor="login-email">
            Email
          </label>
          <input id="login-email" type="email" className="input w-full" placeholder="you@company.com" />
          <label className="fieldset-legend" htmlFor="login-password">
            Password
          </label>
          <input id="login-password" type="password" className="input w-full" placeholder="••••••••" />
        </fieldset>

        <Link to="/dashboard" className="btn btn-primary">
          <LogIn className="size-4" />
          Continue to workspace
        </Link>
      </div>
    </div>
  )
}
