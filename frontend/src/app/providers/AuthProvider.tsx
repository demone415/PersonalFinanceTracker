import { useEffect, type ReactNode } from 'react'
import { supabase } from '@/shared/api/supabase'
import { useSessionStore } from '@/entities/session'

/**
 * Bootstraps the auth session (T1.2.8): loads any persisted session on mount and
 * keeps the Zustand store in sync with Supabase auth events (login, logout,
 * token refresh). Renders nothing until the initial session is resolved, so
 * route guards never flash the login screen for an already-signed-in user.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const setSession = useSessionStore((s) => s.setSession)
  const status = useSessionStore((s) => s.status)

  useEffect(() => {
    void supabase.auth.getSession().then(({ data }) => setSession(data.session))

    const { data } = supabase.auth.onAuthStateChange((_event, session) => {
      setSession(session)
    })

    return () => data.subscription.unsubscribe()
  }, [setSession])

  if (status === 'loading') return null

  return <>{children}</>
}
