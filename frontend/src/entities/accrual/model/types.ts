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
