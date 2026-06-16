import {
  LineChart, Line, XAxis, YAxis, CartesianGrid, ResponsiveContainer, Tooltip, Legend,
} from 'recharts'
import { useMonthlyDynamics } from '@/entities/dashboard'
import { useBaseCurrency } from '@/entities/profile'
import { formatMoney, formatMoneyCompact, formatMonthShort, formatMonthLong } from '@/shared/lib/format'
import { ChartCard } from './ChartCard'

const INCOME = 'oklch(0.7 0.15 150)'
const EXPENSE = 'oklch(0.65 0.2 25)'

/** Income vs. expense over the last N months (T2.2.3). */
export function MonthlyDynamicsChart({ months = 6 }: { months?: number }) {
  const { data, isPending, isError } = useMonthlyDynamics(months)
  const base = useBaseCurrency()
  const points = (data ?? []).map((p) => ({
    ...p,
    label: formatMonthShort(p.year, p.month),
  }))
  const hasData = points.some((p) => p.income !== 0 || p.expense !== 0)

  return (
    <ChartCard
      title={`Динамика за ${months} мес.`}
      loading={isPending}
      error={isError}
      isEmpty={!hasData}
    >
      <div className="h-64 w-full">
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={points} margin={{ top: 8, right: 8, left: 4, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="var(--color-border)" vertical={false} />
            <XAxis
              dataKey="label"
              tick={{ fontSize: 12, fill: 'var(--color-muted-foreground)' }}
              tickLine={false}
              axisLine={{ stroke: 'var(--color-border)' }}
            />
            <YAxis
              width={56}
              tick={{ fontSize: 12, fill: 'var(--color-muted-foreground)' }}
              tickLine={false}
              axisLine={false}
              tickFormatter={(v: number) => formatMoneyCompact(v, base)}
            />
            <Tooltip content={<DynamicsTooltip currency={base} />} />
            <Legend
              iconType="plainline"
              wrapperStyle={{ fontSize: 12 }}
              formatter={(value) => (value === 'income' ? 'Доходы' : 'Расходы')}
            />
            <Line
              type="monotone"
              dataKey="income"
              name="income"
              stroke={INCOME}
              strokeWidth={2}
              dot={{ r: 3 }}
              activeDot={{ r: 5 }}
            />
            <Line
              type="monotone"
              dataKey="expense"
              name="expense"
              stroke={EXPENSE}
              strokeWidth={2}
              dot={{ r: 3 }}
              activeDot={{ r: 5 }}
            />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </ChartCard>
  )
}

interface DynamicsPayload {
  payload: { year: number; month: number; income: number; expense: number }
}

function DynamicsTooltip({
  active, payload, currency = 'RUB',
}: { active?: boolean; payload?: DynamicsPayload[]; currency?: string }) {
  if (!active || !payload?.length) return null
  const { year, month, income, expense } = payload[0].payload
  return (
    <div className="rounded-lg border bg-popover px-3 py-2 text-xs shadow-md">
      <p className="mb-1 font-medium">{formatMonthLong(year, month)}</p>
      <p className="tabular-nums text-green-500">Доходы: {formatMoney(income, currency)}</p>
      <p className="tabular-nums text-red-500">Расходы: {formatMoney(expense, currency)}</p>
    </div>
  )
}
