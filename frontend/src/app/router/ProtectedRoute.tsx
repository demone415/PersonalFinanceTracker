import { Navigate, Outlet } from 'react-router-dom'
import { useSessionStore } from '@/entities/session'

/** Gate for authenticated routes — redirects to /login when there is no session. */
export function ProtectedRoute() {
  const status = useSessionStore((s) => s.status)

  if (status === 'loading') return null
  if (status !== 'authenticated') return <Navigate to="/login" replace />

  return <Outlet />
}
