import type { AccrualFilter, AccrualType } from '@/entities/accrual'

const ACCRUAL_TYPES: readonly AccrualType[] = [
  'Income',
  'ReturnIncome',
  'Expense',
  'ReturnExpense',
]

/** Active filter fields, excluding pagination — used to count/clear filters. */
export type FilterFields = Omit<AccrualFilter, 'page' | 'pageSize'>

function parseNumber(value: string | null): number | undefined {
  if (value == null || value.trim() === '') return undefined
  const n = Number(value)
  return Number.isFinite(n) ? n : undefined
}

function parseType(value: string | null): AccrualType | undefined {
  return value != null && (ACCRUAL_TYPES as readonly string[]).includes(value)
    ? (value as AccrualType)
    : undefined
}

/** Reads an {@link AccrualFilter} out of URL search params (single source of truth). */
export function parseFilterFromParams(params: URLSearchParams): AccrualFilter {
  const filter: AccrualFilter = {
    page: parseNumber(params.get('page')) ?? 1,
    pageSize: parseNumber(params.get('pageSize')) ?? 20,
  }

  const dateFrom = params.get('dateFrom')
  const dateTo = params.get('dateTo')
  const categoryId = params.get('categoryId')
  const amountMin = parseNumber(params.get('amountMin'))
  const amountMax = parseNumber(params.get('amountMax'))
  const type = parseType(params.get('type'))

  if (dateFrom) filter.dateFrom = dateFrom
  if (dateTo) filter.dateTo = dateTo
  if (categoryId) filter.categoryId = categoryId
  if (amountMin != null) filter.amountMin = amountMin
  if (amountMax != null) filter.amountMax = amountMax
  if (type) filter.type = type

  return filter
}

/** Serialises a filter into a flat record suitable for `setSearchParams`. */
export function filterToParams(filter: AccrualFilter): Record<string, string> {
  const out: Record<string, string> = {}
  if (filter.page && filter.page !== 1) out.page = String(filter.page)
  if (filter.pageSize && filter.pageSize !== 20) out.pageSize = String(filter.pageSize)
  if (filter.dateFrom) out.dateFrom = filter.dateFrom
  if (filter.dateTo) out.dateTo = filter.dateTo
  if (filter.categoryId) out.categoryId = filter.categoryId
  if (filter.amountMin != null) out.amountMin = String(filter.amountMin)
  if (filter.amountMax != null) out.amountMax = String(filter.amountMax)
  if (filter.type) out.type = filter.type
  return out
}

/** Number of active (non-pagination) filter fields. */
export function countActiveFilters(filter: AccrualFilter): number {
  const fields: FilterFields = {
    dateFrom: filter.dateFrom,
    dateTo: filter.dateTo,
    categoryId: filter.categoryId,
    amountMin: filter.amountMin,
    amountMax: filter.amountMax,
    type: filter.type,
  }
  return Object.values(fields).filter((v) => v != null && v !== '').length
}
