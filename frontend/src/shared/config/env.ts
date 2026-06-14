/**
 * Centralised, typed access to Vite environment variables.
 * All app config flows through here — never read import.meta.env directly elsewhere.
 */
export const env = {
  apiUrl: import.meta.env.VITE_API_URL ?? 'http://localhost:5000',
  /** GoTrue base URL; supabase-js appends `/auth/v1`. */
  supabaseUrl: import.meta.env.VITE_SUPABASE_URL ?? 'http://localhost:9999',
  /**
   * Public anon key. Standalone GoTrue (no API gateway) does not validate it,
   * so a placeholder is fine locally; set a real value behind a gateway.
   */
  supabaseAnonKey: import.meta.env.VITE_SUPABASE_ANON_KEY ?? 'local-anon-key',
} as const
