import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { accrualApi } from './accrual-api'
import type { AccrualFilter, AccrualInput, ReceiptItemInput } from '../model/types'

const KEYS = {
  all: ['accruals'] as const,
  list: (filter: AccrualFilter) => ['accruals', 'list', filter] as const,
  detail: (id: string) => ['accruals', id] as const,
  receipt: (id: string) => ['accruals', id, 'receipt'] as const,
}

export function useAccruals(filter: AccrualFilter = {}) {
  return useQuery({
    queryKey: KEYS.list(filter),
    queryFn: () => accrualApi.list(filter),
  })
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

export function useCreateAccrual() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (input: AccrualInput) => accrualApi.create(input),
    onSuccess: () => qc.invalidateQueries({ queryKey: KEYS.all }),
  })
}

export function useUpdateAccrual() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: AccrualInput }) =>
      accrualApi.update(id, input),
    onSuccess: (_data, { id }) => {
      qc.invalidateQueries({ queryKey: KEYS.all })
      qc.invalidateQueries({ queryKey: KEYS.detail(id) })
    },
  })
}

export function useDeleteAccrual() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => accrualApi.remove(id),
    onSuccess: () => qc.invalidateQueries({ queryKey: KEYS.all }),
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
