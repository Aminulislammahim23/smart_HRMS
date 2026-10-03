import { ShieldAlert } from 'lucide-react'
import { Link } from 'react-router-dom'
import { EmptyState } from '../components/common/EmptyState'

/** Shown for a page the signed-in user's role may not open. */
export default function Forbidden({ reason = 'role' }: { reason?: 'role' | 'noEmployee' }) {
  return (
    <div className="card bg-base-100 shadow-sm">
      <EmptyState
        icon={ShieldAlert}
        title={reason === 'noEmployee' ? 'No employee record linked' : 'You do not have access to this page'}
        description={
          reason === 'noEmployee'
            ? 'This page shows your own employee data, but your account is not linked to an employee. Ask an administrator to link it.'
            : 'Your role does not include this area. If you think this is a mistake, contact HR or an administrator.'
        }
        action={
          <Link to="/dashboard" className="btn btn-sm">
            Go to dashboard
          </Link>
        }
      />
    </div>
  )
}
