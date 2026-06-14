import { env } from '@/shared/config/env'

/**
 * Thin fetch wrapper around the backend API. Auth headers and error mapping
 * are layered on in later auth/feature tasks; for now it just resolves the
 * base URL and parses JSON.
 */
export async function http<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${env.apiUrl}${path}`, {
    headers: { 'Content-Type': 'application/json', ...init?.headers },
    ...init,
  })

  if (!response.ok) {
    throw new Error(`Request failed: ${response.status} ${response.statusText}`)
  }

  return response.json() as Promise<T>
}
