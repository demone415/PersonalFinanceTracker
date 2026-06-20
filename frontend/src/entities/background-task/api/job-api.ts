import { env } from '@/shared/config/env'
import { supabase } from '@/shared/api/supabase'
import { http } from '@/shared/api/http'
import type { JobStatus } from '../model/types'

/** Reads the download filename from a `Content-Disposition` header, if present. */
function fileNameFromDisposition(disposition: string | null): string | undefined {
  if (!disposition) return undefined
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)
  return match ? decodeURIComponent(match[1]) : undefined
}

/** Saves a blob to disk by clicking a transient object-URL anchor. */
function saveBlob(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = fileName
  document.body.appendChild(anchor)
  anchor.click()
  anchor.remove()
  URL.revokeObjectURL(url)
}

export const jobApi = {
  getStatus: (id: string) => http<JobStatus>(`/api/v1/jobs/${id}`),

  /**
   * Fetches a finished job's result as parsed JSON (used by the FNS import, whose
   * result is an `ImportSummary` rather than a downloadable file). Streams through
   * the API with the Bearer token, like {@link jobApi.downloadResult}.
   */
  getResultJson: async <T>(id: string): Promise<T> => {
    const { data } = await supabase.auth.getSession()
    const token = data.session?.access_token

    const response = await fetch(`${env.apiUrl}/api/v1/jobs/${id}/result`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    })

    if (!response.ok) {
      throw new Error(`Result fetch failed: ${response.status} ${response.statusText}`)
    }

    return (await response.json()) as T
  },

  /**
   * Streams a finished job's result through the API (never a presigned URL) and
   * saves it to disk. Uses a raw `fetch` rather than the JSON `http` wrapper
   * because the response is a binary file, attaching the same Bearer token.
   */
  downloadResult: async (id: string, fallbackName = `export-${id}.csv`): Promise<void> => {
    const { data } = await supabase.auth.getSession()
    const token = data.session?.access_token

    const response = await fetch(`${env.apiUrl}/api/v1/jobs/${id}/result`, {
      headers: token ? { Authorization: `Bearer ${token}` } : {},
    })

    if (!response.ok) {
      throw new Error(`Download failed: ${response.status} ${response.statusText}`)
    }

    const blob = await response.blob()
    const fileName = fileNameFromDisposition(response.headers.get('content-disposition')) ?? fallbackName
    saveBlob(blob, fileName)
  },
}
