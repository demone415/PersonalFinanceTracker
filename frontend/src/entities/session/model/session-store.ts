import { create } from 'zustand'
import type { Session, User } from '@supabase/supabase-js'

export type AuthStatus = 'loading' | 'authenticated' | 'unauthenticated'
export type UserRole = 'user' | 'admin'

interface SessionState {
  status: AuthStatus
  session: Session | null
  userId: string | null
  role: UserRole | null
  isAdmin: boolean
  /** Replaces the session from a Supabase auth event (or initial load). */
  setSession: (session: Session | null) => void
}

/**
 * Role is read strictly from `app_metadata.role` (server-controlled), mirroring
 * the backend (§11.1). `user_metadata` is user-editable and never trusted.
 */
function roleFromUser(user: User | null): UserRole | null {
  if (!user) return null
  const role = (user.app_metadata as { role?: string } | undefined)?.role
  return role === 'admin' ? 'admin' : 'user'
}

export const useSessionStore = create<SessionState>((set) => ({
  status: 'loading',
  session: null,
  userId: null,
  role: null,
  isAdmin: false,
  setSession: (session) => {
    const role = roleFromUser(session?.user ?? null)
    set({
      session,
      userId: session?.user?.id ?? null,
      role,
      isAdmin: role === 'admin',
      status: session ? 'authenticated' : 'unauthenticated',
    })
  },
}))
