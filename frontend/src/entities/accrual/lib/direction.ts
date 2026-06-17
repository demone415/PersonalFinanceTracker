import type { AccrualType } from '../model/types'

/**
 * Whether a transaction moves money *into* the account (приход). Income and a
 * returned expense (refund) are inflows; an expense and a returned income are
 * outflows. Single source of truth so list/detail/dashboard agree on direction.
 */
export function isInflow(type: AccrualType): boolean {
  return type === 'Income' || type === 'ReturnExpense'
}
