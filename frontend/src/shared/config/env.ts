/**
 * Centralised, typed access to Vite environment variables.
 * All app config flows through here — never read import.meta.env directly elsewhere.
 */
export const env = {
  apiUrl: import.meta.env.VITE_API_URL ?? 'http://localhost:5000',
} as const
