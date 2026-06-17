import { useEffect, useRef, useState } from 'react'
import { useExportAccruals, type AccrualFilter } from '@/entities/accrual'
import { jobApi, useJobStatus } from '@/entities/background-task'
import { useToastStore } from '@/shared/lib/toast'

/**
 * Drives the async CSV export end to end: kicks off the job, polls its status,
 * and — once Done — streams the file down and raises a success toast (or an
 * error toast on failure). Self-contained so any list view can drop in an
 * export button (T6.2.3).
 */
export function useAccrualExport() {
  const [jobId, setJobId] = useState<string | null>(null)
  // The job whose terminal outcome we've already handled — guards against the
  // download/toast firing twice while polling settles.
  const handledJob = useRef<string | null>(null)
  const show = useToastStore((s) => s.show)

  const { data: status } = useJobStatus(jobId)
  const startMutation = useExportAccruals()

  function start(filter: AccrualFilter) {
    startMutation.mutate(filter, {
      onSuccess: ({ jobId: id }) => {
        handledJob.current = null
        setJobId(id)
      },
      onError: () => show('Не удалось запустить экспорт', 'error'),
    })
  }

  // React to the job reaching a terminal state. Polling stops on its own (see
  // useJobStatus), so we only fire side effects here — no state updates.
  useEffect(() => {
    if (!jobId || !status || handledJob.current === jobId) return
    if (status.status !== 'Done' && status.status !== 'Failed') return

    handledJob.current = jobId
    if (status.status === 'Done') {
      show('Экспорт готов — файл загружается', 'success')
      void jobApi
        .downloadResult(status.id)
        .catch(() => show('Не удалось скачать файл экспорта', 'error'))
    } else {
      show(status.error ? `Ошибка экспорта: ${status.error}` : 'Экспорт завершился ошибкой', 'error')
    }
  }, [status, jobId, show])

  const isTerminal = status?.status === 'Done' || status?.status === 'Failed'

  return {
    start,
    /** True while the job is being created or is still running. */
    isExporting: startMutation.isPending || (jobId !== null && !isTerminal),
  }
}
