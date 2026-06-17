import { useQuery } from '@tanstack/react-query'
import { jobApi } from './job-api'
import { TERMINAL_JOB_STATUSES } from '../model/types'

/**
 * Polls an async job's status every 1.5s while it is still running, then stops
 * once a terminal status (Done/Failed) is reached. Disabled when `jobId` is null.
 */
export function useJobStatus(jobId: string | null) {
  return useQuery({
    queryKey: ['jobs', jobId],
    queryFn: () => jobApi.getStatus(jobId!),
    enabled: !!jobId,
    refetchInterval: (query) =>
      query.state.data && TERMINAL_JOB_STATUSES.has(query.state.data.status) ? false : 1500,
  })
}
