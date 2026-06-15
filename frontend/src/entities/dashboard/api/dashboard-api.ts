import { http } from '@/shared/api/http'
import type {
  DashboardSummary,
  ExpenseByCategory,
  MonthlyDynamicsPoint,
  PeriodParams,
  TopCategory,
} from '../model/types'

function periodQuery(params: PeriodParams = {}, extra: Record<string, number> = {}): string {
  const search = new URLSearchParams()
  if (params.year != null) search.set('year', String(params.year))
  if (params.month != null) search.set('month', String(params.month))
  for (const [k, v] of Object.entries(extra)) search.set(k, String(v))
  const qs = search.toString()
  return qs ? `?${qs}` : ''
}

export const dashboardApi = {
  summary: (params: PeriodParams = {}) =>
    http<DashboardSummary>(`/api/v1/dashboard/summary${periodQuery(params)}`),

  expensesByCategory: (params: PeriodParams = {}) =>
    http<ExpenseByCategory[]>(`/api/v1/dashboard/expenses-by-category${periodQuery(params)}`),

  monthlyDynamics: (months = 6) =>
    http<MonthlyDynamicsPoint[]>(`/api/v1/dashboard/monthly-dynamics?months=${months}`),

  topCategories: (limit = 5, params: PeriodParams = {}) =>
    http<TopCategory[]>(`/api/v1/dashboard/top-categories${periodQuery(params, { limit })}`),
}
