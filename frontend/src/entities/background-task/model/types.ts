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
