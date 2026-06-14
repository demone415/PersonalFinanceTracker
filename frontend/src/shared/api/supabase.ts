import { createClient } from '@supabase/supabase-js'
import { env } from '@/shared/config/env'

/**
 * Supabase client used only for authentication against GoTrue (T1.2.6).
 * Login/refresh/logout go through this SDK; the .NET API validates the issued
 * JWT offline (see backend §11.1). The session is persisted so reloads stay
 * signed in, and tokens auto-refresh before expiry.
 */
export const supabase = createClient(env.supabaseUrl, env.supabaseAnonKey, {
  auth: {
    persistSession: true,
    autoRefreshToken: true,
    detectSessionInUrl: false,
    storageKey: 'finance-tracker-auth',
  },
})
