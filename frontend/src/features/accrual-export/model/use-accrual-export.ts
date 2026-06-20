import { useExportAccruals, type AccrualFilter } from '@/entities/accrual'
import { useBackgroundTasks } from '@/entities/background-task'
import { useSessionStore } from '@/entities/session'
import { useToastStore } from '@/shared/lib/toast'

/**
 * Kicks off an async CSV export and hands it to the global background-task
 * registry (Story 6.3). Polling, the readiness toast and the file download are
 * driven app-wide by `BackgroundTaskTracker`, so leaving the page no longer drops
 * the job — the file still arrives, on whatever route the user is on, even after
 * a tab reload.
 */
export function useAccrualExport() {
  const show = useToastStore((s) => s.show)
  const track = useBackgroundTasks((s) => s.track)
  const userId = useSessionStore((s) => s.userId)
  const startMutation = useExportAccruals()

  function start(filter: AccrualFilter) {
    if (!userId) return
    startMutation.mutate(filter, {
      onSuccess: ({ jobId }) => {
        track({ id: jobId, userId, kind: 'export', label: 'Экспорт CSV' })
        show('Экспорт запущен — файл скачается, когда будет готов', 'info')
      },
      onError: () => show('Не удалось запустить экспорт', 'error'),
    })
  }

  return {
    start,
    /** True only while the start request is in flight (not the whole job). */
    isStarting: startMutation.isPending,
  }
}
