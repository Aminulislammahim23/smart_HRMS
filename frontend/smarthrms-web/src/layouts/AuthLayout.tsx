import { Suspense } from 'react'
import { Outlet } from 'react-router-dom'
import { Loading } from '../components/common/Loading'
import { APP_NAME } from '../utils/constants'

export default function AuthLayout() {
  return (
    <div className="flex min-h-screen items-center justify-center p-4">
      <div className="w-full max-w-md">
        <div className="mb-6 flex items-center justify-center gap-2">
          <div className="grid size-10 place-items-center rounded-xl bg-primary text-lg font-bold text-primary-content">S</div>
          <span className="text-2xl font-semibold tracking-tight">{APP_NAME}</span>
        </div>
        <Suspense fallback={<Loading />}>
          <Outlet />
        </Suspense>
      </div>
    </div>
  )
}
