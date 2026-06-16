export type AccrualType = 'Income' | 'ReturnIncome' | 'Expense' | 'ReturnExpense'

export interface Accrual {
  id: string
  userId: string
  amount: number
  date: string
  type: AccrualType
  currency: string
  exchangeRate?: number
  categoryId?: string
  categoryName?: string
  categoryColor?: string
  categoryIcon?: string
  description?: string
  includeInStats: boolean
  groupId?: string
  receiptId?: string
  tags: string[]
  createdAt: string
}

export interface AccrualListItem {
  id: string
  amount: number
  date: string
  type: AccrualType
  currency: string
  categoryId?: string
  categoryName?: string
  categoryColor?: string
  categoryIcon?: string
  description?: string
  includeInStats: boolean
  tags: string[]
}

export interface PagedResult<T> {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export interface AccrualInput {
  amount: number
  date: string
  type: AccrualType
  currency: string
  categoryId?: string | null
  description?: string
  includeInStats: boolean
  groupId?: string | null
  exchangeRate?: number | null
  tags: string[]
}

export interface AccrualFilter {
  page?: number
  pageSize?: number
  dateFrom?: string
  dateTo?: string
  categoryId?: string
  amountMin?: number
  amountMax?: number
  type?: AccrualType
}

export interface ReceiptItem {
  id: string
  name: string
  price: number
  quantity: number
  sum: number
}

export interface ReceiptItemInput {
  name: string
  price: number
  quantity: number
  sum: number
}

export interface Receipt {
  id: string
  amountInKopecks: number
  date: string
  organization?: string
  address?: string
  inn?: string
  fetchStatus: string
  fetchAttempts: number
  items: ReceiptItem[]
}

/** Async receipt-fetch lifecycle (mirrors backend ReceiptFetchStatus enum). */
export type ReceiptFetchStatus = 'Pending' | 'Fetched' | 'Failed' | 'RetryLimit'

/** Result of POST /api/v1/accruals/scan-qr — the created accrual + queued receipt. */
export interface ScanQrResult {
  accrualId: string
  receiptId: string
  fetchStatus: ReceiptFetchStatus
}

/**
 * Progress of the background receipt fetch (GET /accruals/{id}/receipt-status).
 * While `Pending`, `queuePosition` is the 1-based slot in the global FIFO queue.
 */
export interface ReceiptStatus {
  accrualId: string
  receiptId?: string
  fetchStatus: ReceiptFetchStatus
  fetchAttempts: number
  queuePosition?: number
}
