import { Suspense, useState } from 'react'
import { Outlet } from 'react-router-dom'
import { Loading } from '../components/common/Loading'
import { Sidebar } from '../components/layout/Sidebar'
import { Topbar } from '../components/layout/Topbar'

/** Sidebar is fixed on large screens and slides in as a drawer on tablets and phones. */
export default function DashboardLayout() {
  const [menuOpen, setMenuOpen] = useState(false)

  return (
    <div className="drawer lg:drawer-open">
      <input
        id="app-drawer"
        type="checkbox"
        className="drawer-toggle"
        checked={menuOpen}
        onChange={(event) => setMenuOpen(event.target.checked)}
      />
      <div className="drawer-content flex min-h-screen flex-col bg-base-200/60">
        <Topbar onMenuClick={() => setMenuOpen(true)} />
        <main className="mx-auto w-full max-w-7xl flex-1 p-4 lg:p-6">
          <Suspense fallback={<Loading />}>
            <Outlet />
          </Suspense>
        </main>
      </div>
      <div className="drawer-side z-40">
        <label htmlFor="app-drawer" aria-label="Close menu" className="drawer-overlay" />
        <Sidebar onNavigate={() => setMenuOpen(false)} />
      </div>
    </div>
  )
}
