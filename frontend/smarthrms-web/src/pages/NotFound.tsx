import { SearchX } from 'lucide-react'
import { Link } from 'react-router-dom'
import { EmptyState } from '../components/common/EmptyState'

export default function NotFound() {
  return (
    <div className="card bg-base-100 shadow-sm">
      <EmptyState
        icon={SearchX}
        title="Page not found"
        description="The page you are looking for does not exist or has moved."
        action={
          <Link to="/dashboard" className="btn btn-primary btn-sm">
            Go to dashboard
          </Link>
        }
      />
    </div>
  )
}
