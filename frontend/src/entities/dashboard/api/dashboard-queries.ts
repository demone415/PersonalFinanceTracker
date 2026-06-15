import { useQuery } from '@tanstack/react-query'
import { dashboardApi } from './dashboard-api'
import type { PeriodParams } from '../model/types'

const KEYS = {
  all: ['dashboard'] as const,
  summary: (p: PeriodParams) => ['dashboard', 'summary', p] as const,
  expensesByCategory: (p: PeriodParams) => ['dashboard', 'expenses-by-category', p] as const,
  monthlyDynamics: (months: number) => ['dashboard', 'monthly-dynamics', months] as const,
  topCategories: (limit: number, p: PeriodParams) =>
    ['dashboard', 'top-categories', limit, p] as const,
}

export const dashboardKeys = KEYS

export function useDashboardSummary(params: PeriodParams = {}) {
  return useQuery({
    queryKey: KEYS.summary(params),
    queryFn: () => dashboardApi.summary(params),
  })
}

export function useExpensesByCategory(params: PeriodParams = {}) {
  return useQuery({
    queryKey: KEYS.expensesByCategory(params),
    queryFn: () => dashboardApi.expensesByCategory(params),
  })
}

export function useMonthlyDynamics(months = 6) {
  return useQuery({
    queryKey: KEYS.monthlyDynamics(months),
    queryFn: () => dashboardApi.monthlyDynamics(months),
  })
}

export function useTopCategories(limit = 5, params: PeriodParams = {}) {
  return useQuery({
    queryKey: KEYS.topCategories(limit, params),
    queryFn: () => dashboardApi.topCategories(limit, params),
  })
}
