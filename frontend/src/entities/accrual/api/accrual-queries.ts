import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { accrualApi } from './accrual-api'
import type { AccrualFilter, AccrualInput, AccrualListItem, PagedResult, ReceiptItemInput } from '../model/types'

const KEYS = {
  all: ['accruals'] as const,
  list: () => ['accruals', 'list'] as const,
  listFiltered: (filter: AccrualFilter) => ['accruals', 'list', filter] as const,
  detail: (id: string) => ['accruals', id] as const,
  receipt: (id: string) => ['accruals', id, 'receipt'] as const,
  receiptStatus: (id: string) => ['accruals', id, 'receipt-status'] as const,
}

/** Terminal fetch states — once reached, the receipt-status no longer changes. */
const TERMINAL_STATUSES = new Set(['Fetched', 'Failed', 'RetryLimit'])

export function useAccruals(filter: AccrualFilter = {}) {
  return useQuery({
    queryKey: KEYS.listFiltered(filter),
    queryFn: () => accrualApi.list(filter),
  })
}

/**
 * Returns a callback that forces the accrual lists to refetch. Used by the filter
 * panel so pressing "Применить" re-runs the query even when the filter values are
 * unchanged (identical URL params would otherwise serve the cached result).
 */
export function useRefreshAccruals() {
  const qc = useQueryClient()
  return () => qc.invalidateQueries({ queryKey: KEYS.list() })
}

export function useAccrual(id: string) {
  return useQuery({
    queryKey: KEYS.detail(id),
    queryFn: () => accrualApi.getById(id),
    enabled: !!id,
  })
}

export function useReceipt(accrualId: string) {
  return useQuery({
    queryKey: KEYS.receipt(accrualId),
    queryFn: () => accrualApi.getReceipt(accrualId),
    enabled: !!accrualId,
  })
}

/**
 * Starts an async CSV export of the accruals matching `filter` (T6.2.1) and
 * returns the job id. Orchestration (polling + download) lives in the
 * accrual-export feature.
 */
export function useExportAccruals() {
  return useMutation({
    mutationFn: (filter: AccrualFilter) => accrualApi.exportCsv(filter),
  })
}

/**
 * Starts an async FNS Excel import (T6.1.4) and returns the job id. Orchestration
 * (global tracking + summary toast) lives in the accrual-import feature and the
 * app-level background-task tracker.
 */
export function useImportAccruals() {
  return useMutation({
    mutationFn: (file: File) => accrualApi.importFns(file),
  })
}

/** Submit a scanned QR string (T4.3.2). Refreshes lists so the new accrual appears. */
export function useScanQr() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (qrRaw: string) => accrualApi.scanQr(qrRaw),
    onSuccess: () => qc.invalidateQueries({ queryKey: KEYS.all }),
  })
}

/**
 * Polls the async receipt-fetch progress (T4.3.3). Refetches every few seconds
 * while `Pending`, then stops once a terminal status (Fetched/Failed/RetryLimit)
 * is reached so we don't poll the backend forever.
 */
export function useReceiptStatus(accrualId: string | undefined) {
  return useQuery({
    queryKey: KEYS.receiptStatus(accrualId ?? '_'),
    queryFn: () => accrualApi.getReceiptStatus(accrualId!),
    enabled: !!accrualId,
    refetchInterval: (query) =>
      query.state.data && TERMINAL_STATUSES.has(query.state.data.fetchStatus) ? false : 3000,
  })
}

export function useCreateAccrual() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (input: AccrualInput) => accrualApi.create(input),
    onMutate: async (input) => {
      await qc.cancelQueries({ queryKey: KEYS.list() })
      const snapshot = qc.getQueriesData<PagedResult<AccrualListItem>>({ queryKey: KEYS.list() })
      qc.setQueriesData<PagedResult<AccrualListItem>>(
        { queryKey: KEYS.list() },
        (old) => {
          if (!old) return old
          const optimistic: AccrualListItem = {
            id: `optimistic-${Date.now()}`,
            amount: input.amount,
            date: input.date,
            type: input.type,
            currency: input.currency,
            categoryId: input.categoryId ?? undefined,
            description: input.description,
            includeInStats: input.includeInStats,
            tags: input.tags,
          }
          return { ...old, items: [optimistic, ...old.items], totalCount: old.totalCount + 1 }
        }
      )
      return { snapshot }
    },
    onError: (_err, _input, ctx) => {
      ctx?.snapshot.forEach(([key, data]) => qc.setQueryData(key, data))
    },
    onSettled: () => qc.invalidateQueries({ queryKey: KEYS.all }),
  })
}

export function useUpdateAccrual() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: AccrualInput }) =>
      accrualApi.update(id, input),
    onMutate: async ({ id, input }) => {
      await qc.cancelQueries({ queryKey: KEYS.all })
      const listSnapshot = qc.getQueriesData<PagedResult<AccrualListItem>>({ queryKey: KEYS.list() })
      const detailSnapshot = qc.getQueryData(KEYS.detail(id))
      qc.setQueriesData<PagedResult<AccrualListItem>>(
        { queryKey: KEYS.list() },
        (old) => {
          if (!old) return old
          return {
            ...old,
            items: old.items.map((item) =>
              item.id === id
                ? { ...item, amount: input.amount, date: input.date, type: input.type,
                    currency: input.currency, categoryId: input.categoryId ?? undefined,
                    description: input.description, includeInStats: input.includeInStats,
                    tags: input.tags }
                : item
            ),
          }
        }
      )
      return { listSnapshot, detailSnapshot }
    },
    onError: (_err, { id }, ctx) => {
      ctx?.listSnapshot.forEach(([key, data]) => qc.setQueryData(key, data))
      if (ctx?.detailSnapshot !== undefined)
        qc.setQueryData(KEYS.detail(id), ctx.detailSnapshot)
    },
    onSettled: (_data, _err, { id }) => {
      qc.invalidateQueries({ queryKey: KEYS.all })
      qc.invalidateQueries({ queryKey: KEYS.detail(id) })
    },
  })
}

export function useDeleteAccrual() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => accrualApi.remove(id),
    onMutate: async (id) => {
      await qc.cancelQueries({ queryKey: KEYS.list() })
      const snapshot = qc.getQueriesData<PagedResult<AccrualListItem>>({ queryKey: KEYS.list() })
      qc.setQueriesData<PagedResult<AccrualListItem>>(
        { queryKey: KEYS.list() },
        (old) => {
          if (!old) return old
          return {
            ...old,
            items: old.items.filter((item) => item.id !== id),
            totalCount: Math.max(0, old.totalCount - 1),
          }
        }
      )
      return { snapshot }
    },
    onError: (_err, _id, ctx) => {
      ctx?.snapshot.forEach(([key, data]) => qc.setQueryData(key, data))
    },
    onSettled: () => qc.invalidateQueries({ queryKey: KEYS.all }),
  })
}

export function useAddReceiptItem() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ accrualId, input }: { accrualId: string; input: ReceiptItemInput }) =>
      accrualApi.addReceiptItem(accrualId, input),
    onSuccess: (_data, { accrualId }) => {
      qc.invalidateQueries({ queryKey: KEYS.receipt(accrualId) })
      qc.invalidateQueries({ queryKey: KEYS.detail(accrualId) })
    },
  })
}

export function useUpdateReceiptItem() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({
      accrualId,
      itemId,
      input,
    }: {
      accrualId: string
      itemId: string
      input: ReceiptItemInput
    }) => accrualApi.updateReceiptItem(accrualId, itemId, input),
    onSuccess: (_data, { accrualId }) =>
      qc.invalidateQueries({ queryKey: KEYS.receipt(accrualId) }),
  })
}

export function useDeleteReceiptItem() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ accrualId, itemId }: { accrualId: string; itemId: string }) =>
      accrualApi.deleteReceiptItem(accrualId, itemId),
    onSuccess: (_data, { accrualId }) =>
      qc.invalidateQueries({ queryKey: KEYS.receipt(accrualId) }),
  })
}
