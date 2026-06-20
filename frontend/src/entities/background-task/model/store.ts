import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { BgTaskKind, JobStatusValue, ImportSummary, TrackedTask } from './types'
import { isTerminalStatus } from './types'

interface BackgroundTasksState {
  tasks: TrackedTask[]
  /** Begins tracking a freshly-started job (replacing any entry with the same id). */
  track: (task: { id: string; kind: BgTaskKind; label: string }) => void
  /** Patches a tracked job (status sync, summary, handled flag). No-op if unknown. */
  update: (id: string, patch: Partial<Omit<TrackedTask, 'id'>>) => void
  /** Stops tracking a job. */
  remove: (id: string) => void
  /** Drops all jobs that have reached a terminal state. */
  clearFinished: () => void
  /** Drops everything (e.g. on sign-out — tasks are per-user). */
  clearAll: () => void
}

/**
 * Global registry of the user's active/finished async jobs (Story 6.3). Lives
 * outside any page and is persisted to `localStorage`, so polling, completion
 * toasts and file downloads keep working after navigation and across a tab
 * reload. The actual polling/side-effects are driven by the app-level
 * `BackgroundTaskTracker`; this store is just the shared source of truth.
 */
export const useBackgroundTasks = create<BackgroundTasksState>()(
  persist(
    (set) => ({
      tasks: [],

      track: ({ id, kind, label }) =>
        set((state) => ({
          tasks: [
            ...state.tasks.filter((t) => t.id !== id),
            {
              id,
              kind,
              label,
              startedAt: Date.now(),
              status: 'Pending' as JobStatusValue,
              handled: false,
            },
          ],
        })),

      update: (id, patch) =>
        set((state) => ({
          tasks: state.tasks.map((t) => (t.id === id ? { ...t, ...patch } : t)),
        })),

      remove: (id) =>
        set((state) => ({ tasks: state.tasks.filter((t) => t.id !== id) })),

      clearFinished: () =>
        set((state) => ({ tasks: state.tasks.filter((t) => !isTerminalStatus(t.status)) })),

      clearAll: () => set({ tasks: [] }),
    }),
    {
      name: 'ft.background-tasks',
      // Persist the whole task incl. `handled` so a reload neither re-downloads a
      // delivered file nor forgets a job that finished while the tab was closed.
    },
  ),
)

/** Convenience selector: jobs still in flight (Pending/Running). */
export function selectActiveTasks(state: BackgroundTasksState): TrackedTask[] {
  return state.tasks.filter((t) => !isTerminalStatus(t.status))
}

export type { ImportSummary }
