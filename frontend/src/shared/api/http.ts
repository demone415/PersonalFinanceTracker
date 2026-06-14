import { env } from '@/shared/config/env'
import { supabase } from './supabase'

/**
 * Thin fetch wrapper around the backend API. Attaches the current GoTrue access
 * token as a Bearer header when a session exists; the backend validates it.
 */
export async function http<T>(path: string, init?: RequestInit): Promise<T> {
  const { data } = await supabase.auth.getSession()
  const token = data.session?.access_token

  const response = await fetch(`${env.apiUrl}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...init?.headers,
    },
  })

  if (!response.ok) {
    throw new Error(`Request failed: ${response.status} ${response.statusText}`)
  }

  return response.json() as Promise<T>
}
