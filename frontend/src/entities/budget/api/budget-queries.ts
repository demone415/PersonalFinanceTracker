import { useMutation, useQuery, useQueryClient, type QueryKey } from '@tanstack/react-query'
import { budgetApi } from './budget-api'
import type { BudgetInput, BudgetProgress, BudgetUpdateInput } from '../model/types'

const budgetsKey = ['budgets'] as const

/** Progress for a given month. Pass an explicit year/month or omit for the current month. */
export function useBudgetProgress(year?: number, month?: number) {
  return useQuery({
    queryKey: [...budgetsKey, 'progress', year ?? null, month ?? null] as const,
    queryFn: () => budgetApi.progress(year, month),
  })
}

/** Recomputes the derived progress fields after a limit change (mirrors the backend). */
function applyLimit(b: BudgetProgress, input: BudgetUpdateInput): BudgetProgress {
  const percentage =
    input.limitAmount > 0 ? Math.round((b.spentAmount / input.limitAmount) * 1000) / 10 : 0
  return {
    ...b,
    limitAmount: input.limitAmount,
    currency: input.currency,
    remainingAmount: input.limitAmount - b.spentAmount,
    percentage,
  }
}

/** Optimistically patches every cached progress list and returns a rollback snapshot. */
function patchProgressCaches(
  queryClient: ReturnType<typeof useQueryClient>,
  patch: (list: BudgetProgress[]) => BudgetProgress[],
): Array<[QueryKey, BudgetProgress[] | undefined]> {
  const snapshots = queryClient.getQueriesData<BudgetProgress[]>({ queryKey: budgetsKey })
  for (const [key, data] of snapshots) {
    if (data) queryClient.setQueryData<BudgetProgress[]>(key, patch(data))
  }
  return snapshots
}

export function useCreateBudget() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: BudgetInput) => budgetApi.create(input),
    // The server assigns the id and computes spend, so refetch on completion
    // rather than guessing the derived progress fields (same as category create).
    onSettled: () => queryClient.invalidateQueries({ queryKey: budgetsKey }),
  })
}

export function useUpdateBudget() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: BudgetUpdateInput }) =>
      budgetApi.update(id, input),
    onMutate: async ({ id, input }) => {
      await queryClient.cancelQueries({ queryKey: budgetsKey })
      const snapshots = patchProgressCaches(queryClient, (list) =>
        list.map((b) => (b.budgetId === id ? applyLimit(b, input) : b)),
      )
      return { snapshots }
    },
    onError: (_error, _vars, context) => {
      context?.snapshots.forEach(([key, data]) => queryClient.setQueryData(key, data))
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: budgetsKey }),
  })
}

export function useDeleteBudget() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => budgetApi.remove(id),
    onMutate: async (id) => {
      await queryClient.cancelQueries({ queryKey: budgetsKey })
      const snapshots = patchProgressCaches(queryClient, (list) =>
        list.filter((b) => b.budgetId !== id),
      )
      return { snapshots }
    },
    onError: (_error, _vars, context) => {
      context?.snapshots.forEach(([key, data]) => queryClient.setQueryData(key, data))
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: budgetsKey }),
  })
}
