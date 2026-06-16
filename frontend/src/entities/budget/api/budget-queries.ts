import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { budgetApi } from './budget-api'
import type { BudgetInput, BudgetUpdateInput } from '../model/types'

const budgetsKey = ['budgets'] as const

/** Progress for a given month. Pass an explicit year/month or omit for the current month. */
export function useBudgetProgress(year?: number, month?: number) {
  return useQuery({
    queryKey: [...budgetsKey, 'progress', year ?? null, month ?? null] as const,
    queryFn: () => budgetApi.progress(year, month),
  })
}

/** Invalidates every budget query (list + progress for all months) after a write. */
function useInvalidateBudgets() {
  const queryClient = useQueryClient()
  return () => queryClient.invalidateQueries({ queryKey: budgetsKey })
}

export function useCreateBudget() {
  const invalidate = useInvalidateBudgets()
  return useMutation({
    mutationFn: (input: BudgetInput) => budgetApi.create(input),
    onSettled: () => invalidate(),
  })
}

export function useUpdateBudget() {
  const invalidate = useInvalidateBudgets()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: BudgetUpdateInput }) =>
      budgetApi.update(id, input),
    onSettled: () => invalidate(),
  })
}

export function useDeleteBudget() {
  const invalidate = useInvalidateBudgets()
  return useMutation({
    mutationFn: (id: string) => budgetApi.remove(id),
    onSettled: () => invalidate(),
  })
}
