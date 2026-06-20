/** Lifecycle of an async import/export job (mirrors backend BackgroundTaskStatus). */
export type JobStatusValue = 'Pending' | 'Running' | 'Done' | 'Failed'

/** Status of an async import/export job (GET /api/v1/jobs/{id}). */
export interface JobStatus {
  id: string
  type: string
  status: JobStatusValue
  progress: number
  error?: string
  createdAt: string
  completedAt?: string
  hasResult: boolean
}

/** Job states past which the status no longer changes. */
export const TERMINAL_JOB_STATUSES: ReadonlySet<JobStatusValue> = new Set<JobStatusValue>([
  'Done',
  'Failed',
])

/** Whether a job's status is terminal (Done/Failed). */
export function isTerminalStatus(status: JobStatusValue): boolean {
  return TERMINAL_JOB_STATUSES.has(status)
}

/** The two kinds of async job the frontend tracks (cf. backend BackgroundTaskType). */
export type BgTaskKind = 'export' | 'import'

/**
 * Result of a finished FNS import (the JSON body at GET /jobs/{id}/result),
 * mirrors the backend `ImportSummary`.
 */
export interface ImportSummary {
  receiptsTotal: number
  receiptsImported: number
  receiptsSkippedDuplicate: number
  rowsFailed: number
  warnings: string[]
}

/**
 * A background job the user kicked off, tracked globally (Story 6.3) so its
 * outcome (toast, file download, import summary) is delivered on any route and
 * survives a tab reload — not tied to the page that started it. Status/summary
 * are refreshed by polling; only the identity fields strictly need persisting.
 */
export interface TrackedTask {
  id: string
  /** Id of the user who started the job — a task is only ever shown to its owner. */
  userId: string
  kind: BgTaskKind
  /** Human label for the indicator/toast, e.g. «Экспорт CSV» / «Импорт ФНС». */
  label: string
  /** Epoch ms when the job was started (or first restored). */
  startedAt: number
  /** Last status seen from polling; starts optimistic at Pending. */
  status: JobStatusValue
  /** Failure detail, when status is Failed. */
  error?: string
  /** Import-only: the parsed summary once the job is Done. */
  summary?: ImportSummary
  /**
   * Whether the terminal side effects (download / summary toast) already fired.
   * Persisted so a reload doesn't re-download a file already delivered, while a
   * job that finished while the tab was closed (handled=false) still fires once.
   */
  handled: boolean
}
