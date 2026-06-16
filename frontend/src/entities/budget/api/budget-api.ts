import { http } from '@/shared/api/http'
import type { Budget, BudgetInput, BudgetProgress, BudgetUpdateInput } from '../model/types'

function periodQuery(year?: number, month?: number): string {
  const params = new URLSearchParams()
  if (year != null) params.set('year', String(year))
  if (month != null) params.set('month', String(month))
  const qs = params.toString()
  return qs ? `?${qs}` : ''
}

export const budgetApi = {
  list: (year?: number, month?: number) =>
    http<Budget[]>(`/api/v1/budgets${periodQuery(year, month)}`),
  progress: (year?: number, month?: number) =>
    http<BudgetProgress[]>(`/api/v1/budgets/progress${periodQuery(year, month)}`),
  create: (input: BudgetInput) =>
    http<Budget>('/api/v1/budgets', { method: 'POST', body: JSON.stringify(input) }),
  update: (id: string, input: BudgetUpdateInput) =>
    http<Budget>(`/api/v1/budgets/${id}`, { method: 'PUT', body: JSON.stringify(input) }),
  remove: (id: string) => http<void>(`/api/v1/budgets/${id}`, { method: 'DELETE' }),
}
