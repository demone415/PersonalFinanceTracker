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

  // 204 No Content (e.g. DELETE) has no body to parse.
  if (response.status === 204 || response.headers.get('content-length') === '0') {
    return undefined as T
  }

  return response.json() as Promise<T>
}
