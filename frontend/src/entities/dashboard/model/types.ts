/** Dashboard aggregate shapes returned by /api/v1/dashboard/* (Story 2.1). */

export interface DashboardSummary {
  totalBalance: number
  monthIncome: number
  monthExpense: number
  monthBalance: number
  year: number
  month: number
}

export interface ExpenseByCategory {
  categoryId?: string
  categoryName: string
  color: string
  icon: string
  amount: number
  percentage: number
}

export interface MonthlyDynamicsPoint {
  year: number
  month: number
  income: number
  expense: number
}

export interface TopCategory {
  categoryId?: string
  categoryName: string
  color: string
  icon: string
  amount: number
}

export interface PeriodParams {
  year?: number
  month?: number
}
