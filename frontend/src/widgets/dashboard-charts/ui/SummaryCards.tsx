import { TrendingUp, TrendingDown, Wallet, Scale } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import { useDashboardSummary, type PeriodParams } from '@/entities/dashboard'
import { useBaseCurrency } from '@/entities/profile'
import { Skeleton } from '@/shared/ui/skeleton'
import { formatMoney } from '@/shared/lib/format'

/** Headline metric cards: income, expense, month balance, all-time balance (T2.2.5). */
export function SummaryCards({ period }: { period?: PeriodParams }) {
  const { data, isPending, isError } = useDashboardSummary(period)
  // Aggregates arrive already converted to the base currency (Epic 8); label them so.
  const base = useBaseCurrency()

  return (
    <section className="grid grid-cols-2 gap-3 lg:grid-cols-4">
      <MetricCard
        label="Доходы за месяц"
        value={data ? formatMoney(data.monthIncome, base) : undefined}
        icon={TrendingUp}
        tone="text-green-500"
        loading={isPending}
        error={isError}
      />
      <MetricCard
        label="Расходы за месяц"
        value={data ? formatMoney(data.monthExpense, base) : undefined}
        icon={TrendingDown}
        tone="text-red-500"
        loading={isPending}
        error={isError}
      />
      <MetricCard
        label="Баланс за месяц"
        value={data ? formatMoney(data.monthBalance, base) : undefined}
        icon={Scale}
        tone={data && data.monthBalance < 0 ? 'text-red-500' : 'text-green-500'}
        loading={isPending}
        error={isError}
      />
      <MetricCard
        label="Баланс (всего)"
        value={data ? formatMoney(data.totalBalance, base) : undefined}
        icon={Wallet}
        tone={data && data.totalBalance < 0 ? 'text-red-500' : 'text-foreground'}
        loading={isPending}
        error={isError}
      />
    </section>
  )
}

function MetricCard({
  label, value, icon: Icon, tone, loading, error,
}: {
  label: string
  value?: string
  icon: LucideIcon
  tone: string
  loading: boolean
  error: boolean
}) {
  return (
    <div className="rounded-xl border bg-card p-4">
      <div className="flex items-center justify-between">
        <p className="truncate text-xs text-muted-foreground">{label}</p>
        <Icon className={`size-4 shrink-0 ${tone}`} />
      </div>
      {loading ? (
        <Skeleton className="mt-2 h-7 w-24" />
      ) : error ? (
        <p className="mt-1 text-lg font-semibold text-muted-foreground">—</p>
      ) : (
        <p className={`mt-1 text-xl font-semibold tabular-nums md:text-2xl ${tone}`}>{value}</p>
      )}
    </div>
  )
}
