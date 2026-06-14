import { Navigate, Outlet } from 'react-router-dom'
import { useSessionStore } from '@/entities/session'

/** Gate for admin-only routes — non-admins are sent home, anonymous to /login. */
export function AdminRoute() {
  const status = useSessionStore((s) => s.status)
  const isAdmin = useSessionStore((s) => s.isAdmin)

  if (status === 'loading') return null
  if (status !== 'authenticated') return <Navigate to="/login" replace />
  if (!isAdmin) return <Navigate to="/" replace />

  return <Outlet />
}
