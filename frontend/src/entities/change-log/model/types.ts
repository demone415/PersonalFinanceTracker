import type { PagedResult } from '@/entities/accrual'

export type { PagedResult }

/** Entities the change log tracks (mirrors backend ChangeLogInterceptor.TrackedTypes). */
export type ChangeLogEntityType = 'Accrual' | 'MonthlyBudget'

/** Action recorded for a change (mirrors backend ChangeLogInterceptor). */
export type ChangeLogAction = 'Create' | 'Update' | 'Delete'

/** One audit-log row. `valuesBefore`/`valuesAfter` are raw JSON snapshots. */
export interface ChangeLogEntry {
  id: string
  userId: string
  entityType: string
  entityId: string
  action: string
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
