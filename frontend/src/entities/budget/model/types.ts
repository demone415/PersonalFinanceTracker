export interface Budget {
  id: string
  categoryId: string
  categoryName: string
  categoryColor: string
  categoryIcon: string
  year: number
  month: number
  limitAmount: number
  currency: string
}

/** Create payload for a budget. */
export interface BudgetInput {
  categoryId: string
  year: number
  month: number
  limitAmount: number
  currency: string
}

/** Update payload — only the limit and currency are mutable. */
export interface BudgetUpdateInput {
  limitAmount: number
  currency: string
}

/** Spend progress for one budget in a month. */
export interface BudgetProgress {
  budgetId: string
  categoryId: string
  categoryName: string
  categoryColor: string
  categoryIcon: string
  year: number
  month: number
  limitAmount: number
  spentAmount: number
  remainingAmount: number
  /** spent / limit × 100; may exceed 100 on overrun. */
  percentage: number
  currency: string
}
