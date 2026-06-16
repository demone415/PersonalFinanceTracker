import {
  BarChart, Bar, XAxis, YAxis, Cell, ResponsiveContainer, Tooltip,
} from 'recharts'
import { useTopCategories, type PeriodParams } from '@/entities/dashboard'
import { useBaseCurrency } from '@/entities/profile'
import { formatMoney, formatMoneyCompact } from '@/shared/lib/format'
import { ChartCard } from './ChartCard'

/** Top expense categories for the month as a horizontal bar chart (T2.2.4). */
export function TopCategoriesChart({
  limit = 5,
  period,
}: {
  limit?: number
  period?: PeriodParams
}) {
  const { data, isPending, isError } = useTopCategories(limit, period)
  const base = useBaseCurrency()
  const items = data ?? []

  return (
    <ChartCard
      title={`Топ-${limit} категорий расходов`}
      loading={isPending}
      error={isError}
      isEmpty={items.length === 0}
      emptyText="Нет расходов за этот месяц"
    >
      <div className="h-64 w-full">
        <ResponsiveContainer width="100%" height="100%">
          <BarChart
            data={items}
            layout="vertical"
            margin={{ top: 4, right: 16, left: 8, bottom: 4 }}
          >
            <XAxis
              type="number"
              tick={{ fontSize: 12, fill: 'var(--color-muted-foreground)' }}
              tickLine={false}
              axisLine={false}
              tickFormatter={(v: number) => formatMoneyCompact(v, base)}
            />
            <YAxis
              type="category"
              dataKey="categoryName"
              width={96}
              tick={{ fontSize: 12, fill: 'var(--color-muted-foreground)' }}
              tickLine={false}
              axisLine={false}
            />
            <Tooltip cursor={{ fill: 'var(--color-accent)' }} content={<TopTooltip currency={base} />} />
            <Bar dataKey="amount" radius={[0, 6, 6, 0]} barSize={22}>
              {items.map((item) => (
                <Cell key={item.categoryId ?? 'none'} fill={item.color} />
              ))}
            </Bar>
          </BarChart>
        </ResponsiveContainer>
      </div>
    </ChartCard>
  )
}

interface TopPayload {
  payload: { categoryName: string; amount: number }
}

function TopTooltip({
  active, payload, currency = 'RUB',
}: { active?: boolean; payload?: TopPayload[]; currency?: string }) {
  if (!active || !payload?.length) return null
  const { categoryName, amount } = payload[0].payload
  return (
    <div className="rounded-lg border bg-popover px-3 py-2 text-xs shadow-md">
      <p className="font-medium">{categoryName}</p>
      <p className="tabular-nums text-muted-foreground">{formatMoney(amount, currency)}</p>
    </div>
  )
}
