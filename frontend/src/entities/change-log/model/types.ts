import type { PagedResult } from '@/entities/accrual'

export type { PagedResult }

/** Entities the change log tracks (mirrors backend ChangeLogInterceptor.TrackedTypes). */
export type ChangeLogEntityType = 'Accrual' | 'MonthlyBudget'

/** Action recorded for a change (mirrors backend ChangeLogInterceptor). */
export type ChangeLogAction = 'Create' | 'Update' | 'Delete'

/**
 * One audit-log row. `valuesBefore`/`valuesAfter` are raw JSON snapshots.
 *
 * `action` uses the closed {@link ChangeLogAction} union — the interceptor only ever
 * writes Create/Update/Delete. `entityType` stays a plain `string`: the set of tracked
 * entities grows with new epics, so the UI falls back to the raw value rather than
 * narrowing it away.
 */
export interface ChangeLogEntry {
  id: string
  userId: string
  entityType: string
  action: ChangeLogAction
  entityId: string
  timestamp: string
  valuesBefore?: string | null
  valuesAfter?: string | null
}

export interface ChangeLogFilter {
  page?: number
  pageSize?: number
  entityType?: string
  action?: string
}
