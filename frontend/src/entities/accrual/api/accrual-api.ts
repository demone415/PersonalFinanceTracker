import { http } from '@/shared/api/http'
import type {
  Accrual,
  AccrualFilter,
  AccrualInput,
  AccrualListItem,
  PagedResult,
  Receipt,
  ReceiptItem,
  ReceiptItemInput,
} from '../model/types'

/**
 * A `<input type="date">` yields a date-only string (`2026-06-16`) that the
 * backend binds to midnight, which would exclude same-day accruals. Expand a
 * date-only upper bound to the end of that day so the range stays inclusive.
 */
function inclusiveDateTo(value: string): string {
  return /^\d{4}-\d{2}-\d{2}$/.test(value) ? `${value}T23:59:59.999` : value
}

function toQueryString(filter: AccrualFilter): string {
  const params = new URLSearchParams()
  if (filter.page) params.set('page', String(filter.page))
  if (filter.pageSize) params.set('pageSize', String(filter.pageSize))
  if (filter.dateFrom) params.set('dateFrom', filter.dateFrom)
  if (filter.dateTo) params.set('dateTo', inclusiveDateTo(filter.dateTo))
  if (filter.categoryId) params.set('categoryId', filter.categoryId)
  if (filter.amountMin != null) params.set('amountMin', String(filter.amountMin))
  if (filter.amountMax != null) params.set('amountMax', String(filter.amountMax))
  if (filter.type) params.set('type', filter.type)
  const qs = params.toString()
  return qs ? `?${qs}` : ''
}

export const accrualApi = {
  list: (filter: AccrualFilter = {}) =>
    http<PagedResult<AccrualListItem>>(`/api/v1/accruals${toQueryString(filter)}`),

  getById: (id: string) => http<Accrual>(`/api/v1/accruals/${id}`),

  create: (input: AccrualInput) =>
    http<Accrual>('/api/v1/accruals', { method: 'POST', body: JSON.stringify(input) }),

  update: (id: string, input: AccrualInput) =>
    http<Accrual>(`/api/v1/accruals/${id}`, { method: 'PUT', body: JSON.stringify(input) }),

  remove: (id: string) => http<void>(`/api/v1/accruals/${id}`, { method: 'DELETE' }),

  getReceipt: (accrualId: string) =>
    http<Receipt>(`/api/v1/accruals/${accrualId}/receipt`),

  addReceiptItem: (accrualId: string, input: ReceiptItemInput) =>
    http<ReceiptItem>(`/api/v1/accruals/${accrualId}/receipt/items`, {
      method: 'POST',
      body: JSON.stringify(input),
    }),

  updateReceiptItem: (accrualId: string, itemId: string, input: ReceiptItemInput) =>
    http<ReceiptItem>(`/api/v1/accruals/${accrualId}/receipt/items/${itemId}`, {
      method: 'PUT',
      body: JSON.stringify(input),
    }),

  deleteReceiptItem: (accrualId: string, itemId: string) =>
    http<void>(`/api/v1/accruals/${accrualId}/receipt/items/${itemId}`, { method: 'DELETE' }),
}
