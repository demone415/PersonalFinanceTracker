import { PieChart, Pie, Cell, ResponsiveContainer, Tooltip } from 'recharts'
import { useExpensesByCategory, type PeriodParams } from '@/entities/dashboard'
import { useBaseCurrency } from '@/entities/profile'
import { formatMoney } from '@/shared/lib/format'
import { ChartCard } from './ChartCard'

/** Expenses-by-category donut for the selected month (T2.2.2). */
export function ExpensesPieChart({ period }: { period?: PeriodParams }) {
  const { data, isPending, isError } = useExpensesByCategory(period)
  const base = useBaseCurrency()
  const items = data ?? []
  const total = items.reduce((sum, i) => sum + i.amount, 0)

  return (
    <ChartCard
      title="Расходы по категориям"
      loading={isPending}
      error={isError}
      isEmpty={items.length === 0}
      emptyText="Нет расходов за этот месяц"
    >
      <div className="flex flex-col items-center gap-4 sm:flex-row">
        <div className="relative h-56 w-full sm:w-56">
          <ResponsiveContainer width="100%" height="100%">
            <PieChart>
              <Pie
                data={items}
                dataKey="amount"
                nameKey="categoryName"
                innerRadius="60%"
                outerRadius="100%"
                paddingAngle={1}
                stroke="var(--color-card)"
                strokeWidth={2}
              >
                {items.map((item) => (
                  <Cell key={item.categoryId ?? 'none'} fill={item.color} />
                ))}
              </Pie>
              <Tooltip content={<PieTooltip currency={base} />} />
            </PieChart>
          </ResponsiveContainer>
          <div className="pointer-events-none absolute inset-0 flex flex-col items-center justify-center">
            <span className="text-xs text-muted-foreground">Всего</span>
            <span className="text-base font-semibold tabular-nums">{formatMoney(total, base)}</span>
          </div>
        </div>

        <ul className="w-full flex-1 space-y-1.5">
          {items.slice(0, 8).map((item) => (
            <li
              key={item.categoryId ?? 'none'}
              className="flex items-center gap-2 text-sm"
            >
              <span
                className="size-2.5 shrink-0 rounded-full"
                style={{ backgroundColor: item.color }}
              />
              <span className="min-w-0 flex-1 truncate">{item.categoryName}</span>
              <span className="shrink-0 tabular-nums text-muted-foreground">
                {item.percentage.toLocaleString('ru-RU')}%
              </span>
              <span className="w-20 shrink-0 text-right tabular-nums">
                {formatMoney(item.amount, base)}
              </span>
            </li>
          ))}
        </ul>
      </div>
    </ChartCard>
  )
}

interface TooltipPayload {
  payload: { categoryName: string; amount: number; percentage: number }
}

function PieTooltip({
  active, payload, currency = 'RUB',
}: { active?: boolean; payload?: TooltipPayload[]; currency?: string }) {
  if (!active || !payload?.length) return null
  const { categoryName, amount, percentage } = payload[0].payload
  return (
    <div className="rounded-lg border bg-popover px-3 py-2 text-xs shadow-md">
      <p className="font-medium">{categoryName}</p>
      <p className="tabular-nums text-muted-foreground">
        {formatMoney(amount, currency)} · {percentage.toLocaleString('ru-RU')}%
      </p>
    </div>
  )
}
