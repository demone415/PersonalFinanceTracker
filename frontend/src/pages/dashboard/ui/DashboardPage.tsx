import { useState } from 'react'
import { ChevronLeft, ChevronRight } from 'lucide-react'
import {
  SummaryCards,
  ExpensesPieChart,
  MonthlyDynamicsChart,
  TopCategoriesChart,
} from '@/widgets/dashboard-charts'
import { Button } from '@/shared/ui/button'
import { formatMonthLong } from '@/shared/lib/format'

interface Period {
  year: number
  month: number
}

function currentPeriod(): Period {
  const now = new Date()
  return { year: now.getFullYear(), month: now.getMonth() + 1 }
}

function shift({ year, month }: Period, delta: number): Period {
  const idx = year * 12 + (month - 1) + delta
  return { year: Math.floor(idx / 12), month: (idx % 12) + 1 }
}

function isCurrent(p: Period): boolean {
  const c = currentPeriod()
  return p.year === c.year && p.month === c.month
}

/** Dashboard page: summary cards, expenses pie, 6-month dynamics, top categories (Story 2.2). */
export function DashboardPage() {
  const [period, setPeriod] = useState<Period>(currentPeriod)

  return (
    <main className="mx-auto max-w-5xl space-y-5 p-4 md:p-8">
      <header className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Аналитика</h1>
          <p className="text-sm text-muted-foreground">Обзор доходов и расходов</p>
        </div>

        {/* Month selector — drives the summary, pie and top-categories widgets. */}
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="icon-sm"
            aria-label="Предыдущий месяц"
            onClick={() => setPeriod((p) => shift(p, -1))}
          >
            <ChevronLeft />
          </Button>
          <span className="min-w-36 text-center text-sm font-medium tabular-nums">
            {formatMonthLong(period.year, period.month)}
          </span>
          <Button
            variant="outline"
            size="icon-sm"
            aria-label="Следующий месяц"
            disabled={isCurrent(period)}
            onClick={() => setPeriod((p) => shift(p, 1))}
          >
            <ChevronRight />
          </Button>
        </div>
      </header>

      <SummaryCards period={period} />

      <div className="grid gap-4 lg:grid-cols-2">
        <ExpensesPieChart period={period} />
        <TopCategoriesChart limit={5} period={period} />
      </div>

      <MonthlyDynamicsChart months={6} />
    </main>
  )
}
