import { useEffect, useRef } from 'react'
import { useSessionStore } from '@/entities/session'
import { useBackgroundTasks, isTerminalStatus, tasksForUser } from '@/entities/background-task'
import { JobWatcher } from './JobWatcher'

/**
 * App-level driver of global background-task tracking (Story 6.3). Mounted once
 * near the root (see `AppProviders`) so job polling and completion side effects
 * (toast / download / import summary) run on every route — not only the page that
 * started the job — and resume after a tab reload from the persisted registry.
 *
 * Renders an invisible {@link JobWatcher} for each job that still needs watching:
 * anything not yet terminal, plus terminal-but-unhandled jobs that finished while
 * the tab was closed (so their outcome is delivered on next load).
 */
export function BackgroundTaskTracker() {
  const status = useSessionStore((s) => s.status)
  const userId = useSessionStore((s) => s.userId)
  const tasks = useBackgroundTasks((s) => s.tasks)
  const clearAll = useBackgroundTasks((s) => s.clearAll)
  const prevUser = useRef<string | null | undefined>(undefined)

  // Tasks are per-user and persisted in shared localStorage — drop them whenever
  // the signed-in user changes (including sign-out) so one user never polls or
  // sees another's jobs. Ignore the transient `loading` phase (userId is null
  // before the session resolves) so the first resolution doesn't wipe the user's
  // own restored tasks; the first real value (undefined → id) keeps them.
  useEffect(() => {
    if (status === 'loading') return
    if (prevUser.current !== undefined && prevUser.current !== userId) clearAll()
    prevUser.current = userId
  }, [status, userId, clearAll])

  if (status !== 'authenticated') return null

  // Only ever watch the signed-in user's own jobs (the store is shared across
  // users in one localStorage key).
  const watched = tasksForUser(tasks, userId).filter(
    (t) => !isTerminalStatus(t.status) || !t.handled,
  )

  return (
    <>
      {watched.map((task) => (
        <JobWatcher key={task.id} task={task} />
      ))}
    </>
  )
}
