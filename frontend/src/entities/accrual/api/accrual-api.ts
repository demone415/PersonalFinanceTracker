import { http } from '@/shared/api/http'
import { env } from '@/shared/config/env'
import { supabase } from '@/shared/api/supabase'
import type {
  Accrual,
  AccrualFilter,
  AccrualInput,
  AccrualListItem,
  PagedResult,
  Receipt,
  ReceiptItem,
  ReceiptItemInput,
  ReceiptStatus,
  ScanQrResult,
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

  /**
   * Queues an async CSV export of the accruals matching `filter` (pagination is
   * dropped — an export covers every matching row). Returns the job id to poll.
   */
  exportCsv: (filter: AccrualFilter = {}) => {
    // Drop pagination — an export covers every matching row, not a page.
    const query = toQueryString({ ...filter, page: undefined, pageSize: undefined })
    return http<{ jobId: string }>(`/api/v1/accruals/export${query}`, { method: 'POST' })
  },

  /**
   * Uploads an FNS «Налоги ФЛ» Excel export to start an async import (T6.1.4).
   * Sent as `multipart/form-data` (field `file`), so it bypasses the JSON `http`
   * wrapper and uses a raw fetch with the Bearer token (the browser sets the
   * multipart boundary itself). Returns the job id to poll.
   */
  importFns: async (file: File): Promise<{ jobId: string }> => {
    const { data } = await supabase.auth.getSession()
    const token = data.session?.access_token

    const form = new FormData()
    form.append('file', file)

    const response = await fetch(`${env.apiUrl}/api/v1/accruals/import`, {
      method: 'POST',
      headers: token ? { Authorization: `Bearer ${token}` } : {},
      body: form,
    })

    if (!response.ok) {
      throw new Error(`Request failed: ${response.status} ${response.statusText}`)
    }

    return (await response.json()) as { jobId: string }
  },

  create: (input: AccrualInput) =>
    http<Accrual>('/api/v1/accruals', { method: 'POST', body: JSON.stringify(input) }),

  update: (id: string, input: AccrualInput) =>
    http<Accrual>(`/api/v1/accruals/${id}`, { method: 'PUT', body: JSON.stringify(input) }),

  remove: (id: string) => http<void>(`/api/v1/accruals/${id}`, { method: 'DELETE' }),

  /** Submit a scanned QR string → creates an accrual + queues the receipt fetch. */
  scanQr: (qrRaw: string) =>
    http<ScanQrResult>('/api/v1/accruals/scan-qr', {
      method: 'POST',
      body: JSON.stringify({ qrRaw }),
    }),

  /** Poll the async receipt-fetch progress for an accrual. */
  getReceiptStatus: (accrualId: string) =>
    http<ReceiptStatus>(`/api/v1/accruals/${accrualId}/receipt-status`),

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
