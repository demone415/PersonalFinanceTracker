import { useEffect, useRef } from 'react'
import {
  jobApi,
  useJobStatus,
  useBackgroundTasks,
  isTerminalStatus,
  type ImportSummary,
  type TrackedTask,
} from '@/entities/background-task'
import { useToastStore } from '@/shared/lib/toast'

/** Builds the completion toast for a finished import from its summary. */
function importDoneMessage(label: string, s: ImportSummary): string {
  const parts = [`добавлено ${s.receiptsImported}`]
  if (s.receiptsSkippedDuplicate > 0) parts.push(`дубликатов ${s.receiptsSkippedDuplicate}`)
  if (s.rowsFailed > 0) parts.push(`с ошибками ${s.rowsFailed}`)
  return `${label}: ${parts.join(', ')}`
}

/**
 * Invisible per-job driver (Story 6.3): polls one tracked job and, on a terminal
 * state, fires its side effects exactly once — download the CSV for an export, or
 * fetch + store the summary and toast the counts for an import. Mounted by
 * `BackgroundTaskTracker` for every job that still needs watching, so the outcome
 * is delivered regardless of which route is open (or after a tab reload).
 */
export function JobWatcher({ task }: { task: TrackedTask }) {
  const { data: status, error } = useJobStatus(task.id)
  const update = useBackgroundTasks((s) => s.update)
  const remove = useBackgroundTasks((s) => s.remove)
  const show = useToastStore((s) => s.show)
  // Guards the side effects against the brief window between firing and the
  // store flipping `handled`, while this component is still mounted.
  const firedRef = useRef(false)

  // A job that can't be read (e.g. a stale persisted id, or a deleted job) would
  // poll forever — drop it from the registry instead.
  useEffect(() => {
    if (error) remove(task.id)
  }, [error, task.id, remove])

  useEffect(() => {
    if (!status) return

    // Keep the store in sync so the indicator and the import page see live state.
    if (status.status !== task.status || (status.error ?? undefined) !== task.error) {
      update(task.id, { status: status.status, error: status.error })
    }

    if (!isTerminalStatus(status.status)) return
    if (task.handled || firedRef.current) return
    firedRef.current = true

    if (status.status === 'Failed') {
      update(task.id, { handled: true })
      show(status.error ? `${task.label}: ошибка — ${status.error}` : `${task.label}: ошибка`, 'error')
      return
    }

    // Done.
    if (task.kind === 'export') {
      show(`${task.label}: файл готов, скачивание…`, 'success')
      void jobApi.downloadResult(task.id).catch(() => show('Не удалось скачать файл', 'error'))
      update(task.id, { handled: true })
    } else {
      void jobApi
        .getResultJson<ImportSummary>(task.id)
        .then((summary) => {
          update(task.id, { handled: true, summary })
          show(importDoneMessage(task.label, summary), summary.rowsFailed > 0 ? 'info' : 'success')
        })
        .catch(() => {
          update(task.id, { handled: true })
          show(`${task.label}: импорт завершён`, 'success')
        })
    }
  }, [status, task, update, show])

  return null
}
