import { useImportAccruals } from '@/entities/accrual'
import { useBackgroundTasks } from '@/entities/background-task'
import { useToastStore } from '@/shared/lib/toast'

/** Maps a failed start request to a user-facing message by HTTP status. */
function startErrorMessage(error: Error): string {
  const status = Number(error.message.match(/Request failed: (\d+)/)?.[1])
  if (status === 400) return 'Файл не приложен или пуст. Выберите файл .xlsx.'
  if (status === 413) return 'Файл слишком большой (максимум 20 МБ).'
  return 'Не удалось запустить импорт. Попробуйте ещё раз.'
}

/**
 * Starts an async FNS Excel import and registers it with the global
 * background-task tracker (Story 6.3), so the result summary is delivered on any
 * route and survives a tab reload. `onStarted` hands the job id back to the page
 * so it can show live progress for the import the user just launched.
 */
export function useAccrualImport() {
  const show = useToastStore((s) => s.show)
  const track = useBackgroundTasks((s) => s.track)
  const startMutation = useImportAccruals()

  function start(file: File, onStarted?: (jobId: string) => void) {
    startMutation.mutate(file, {
      onSuccess: ({ jobId }) => {
        track({ id: jobId, kind: 'import', label: `Импорт ФНС: ${file.name}` })
        show('Импорт запущен — результат появится, когда файл обработается', 'info')
        onStarted?.(jobId)
      },
      onError: (error) => show(startErrorMessage(error as Error), 'error'),
    })
  }

  return {
    start,
    /** True only while the upload request is in flight (not the whole job). */
    isStarting: startMutation.isPending,
  }
}
