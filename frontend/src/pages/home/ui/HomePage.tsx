import { Link } from 'react-router-dom'
import { QrCode, Plus, ArrowRight } from 'lucide-react'
import {
  useAccruals,
  type AccrualListItem,
  type AccrualType,
} from '@/entities/accrual'
import { useSessionStore } from '@/entities/session'
import { Button } from '@/shared/ui/button'
import { Skeleton } from '@/shared/ui/skeleton'
import { LucideIcon } from '@/shared/ui/lucide-icon'

const TYPE_LABELS: Record<AccrualType, string> = {
  Income: 'Доход',
  ReturnIncome: 'Возврат дохода',
  Expense: 'Расход',
  ReturnExpense: 'Возврат расхода',
}

function isInflow(type: AccrualType) {
  return type === 'Income' || type === 'ReturnExpense'
}

function formatRub(value: number) {
  return `${value.toLocaleString('ru-RU', { maximumFractionDigits: 0 })} ₽`
}

function greeting() {
  const h = new Date().getHours()
  if (h < 6) return 'Доброй ночи'
  if (h < 12) return 'Доброе утро'
  if (h < 18) return 'Добрый день'
  return 'Добрый вечер'
}

function displayName(email: string | null | undefined) {
  if (!email) return ''
  const local = email.split('@')[0]
  return local.charAt(0).toUpperCase() + local.slice(1)
}

function monthRange() {
  const now = new Date()
  const from = new Date(now.getFullYear(), now.getMonth(), 1)
  const to = new Date(now.getFullYear(), now.getMonth() + 1, 0, 23, 59, 59)
  return { from: from.toISOString(), to: to.toISOString() }
}

interface MonthStats {
  income: number
  expense: number
  balance: number
}

function computeStats(items: AccrualListItem[]): MonthStats {
  let income = 0
  let expense = 0
  for (const a of items) {
    if (!a.includeInStats) continue
    switch (a.type) {
      case 'Income': income += a.amount; break
      case 'ReturnIncome': income -= a.amount; break
      case 'Expense': expense += a.amount; break
      case 'ReturnExpense': expense -= a.amount; break
    }
  }
  return { income, expense, balance: income - expense }
}

export function HomePage() {
  const email = useSessionStore((s) => s.session?.user?.email)
  const { from, to } = monthRange()

  const monthQuery = useAccruals({ dateFrom: from, dateTo: to, pageSize: 500 })
  const recentQuery = useAccruals({ pageSize: 5 })

  const stats = monthQuery.data ? computeStats(monthQuery.data.items) : null
  const name = displayName(email)

  return (
    <main className="mx-auto max-w-4xl space-y-6 p-4 md:p-8">
      {/* Greeting + quick actions */}
      <header className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <p className="text-sm text-muted-foreground">{greeting()},</p>
          <h1 className="text-2xl font-semibold tracking-tight">{name || 'обзор'} 👋</h1>
        </div>
        <div className="grid grid-cols-2 gap-3 sm:flex sm:gap-2">
          <Button asChild className="h-14 sm:h-9">
            <Link to="/scan">
              <QrCode className="size-5 sm:size-4" /> Скан QR
            </Link>
          </Button>
          <Button variant="outline" asChild className="h-14 sm:h-9">
            <Link to="/accruals">
              <Plus className="size-5 sm:size-4" /> Вручную
            </Link>
          </Button>
        </div>
      </header>

      {/* Month summary */}
      <section className="grid grid-cols-3 gap-3">
        <StatCard
          label="Баланс за месяц"
          value={stats ? formatRub(stats.balance) : undefined}
          loading={monthQuery.isPending}
          accent={stats && stats.balance >= 0 ? 'text-green-500' : 'text-red-500'}
        />
        <StatCard
          label="Доходы"
          value={stats ? formatRub(stats.income) : undefined}
          loading={monthQuery.isPending}
        />
        <StatCard
          label="Расходы"
          value={stats ? formatRub(stats.expense) : undefined}
          loading={monthQuery.isPending}
          accent="text-red-500"
        />
      </section>

      {/* Recent accruals */}
      <section className="space-y-3">
        <div className="flex items-center justify-between">
          <h2 className="text-sm font-medium text-muted-foreground">Последние начисления</h2>
          <Link
            to="/accruals"
            className="flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
          >
            Все <ArrowRight className="size-3.5" />
          </Link>
        </div>

        {recentQuery.isPending ? (
          <ul className="space-y-2">
            {Array.from({ length: 5 }).map((_, i) => (
              <li key={i}><Skeleton className="h-14 w-full" /></li>
            ))}
          </ul>
        ) : recentQuery.data && recentQuery.data.items.length > 0 ? (
          <ul className="space-y-2">
            {recentQuery.data.items.map((item) => (
              <RecentRow key={item.id} item={item} />
            ))}
          </ul>
        ) : (
          <div className="rounded-lg border border-dashed p-8 text-center text-sm text-muted-foreground">
            Пока нет начислений. Добавьте первое — вручную или сканом QR.
          </div>
        )}
      </section>
    </main>
  )
}

function StatCard({
  label, value, loading, accent,
}: {
  label: string
  value?: string
  loading: boolean
  accent?: string | null
}) {
  return (
    <div className="rounded-lg bg-card p-3 md:p-4">
      <p className="truncate text-xs text-muted-foreground">{label}</p>
      {loading || value === undefined ? (
        <Skeleton className="mt-1 h-7 w-20" />
      ) : (
        <p className={`mt-0.5 text-lg font-semibold tabular-nums md:text-2xl ${accent ?? ''}`}>
          {value}
        </p>
      )}
    </div>
  )
}

function RecentRow({ item }: { item: AccrualListItem }) {
  const sign = isInflow(item.type) ? '+' : '−'
  const color = isInflow(item.type) ? 'text-green-500' : 'text-red-500'
  return (
    <li>
      <Link
        to={`/accruals/${item.id}`}
        className="flex items-center gap-3 rounded-lg border bg-card p-3 transition-colors hover:bg-accent/40"
      >
        <span
          className="flex size-9 shrink-0 items-center justify-center rounded-md"
          style={{
            backgroundColor: item.categoryColor ? `${item.categoryColor}22` : 'var(--accent)',
            color: item.categoryColor ?? 'var(--muted-foreground)',
          }}
        >
          <LucideIcon name={item.categoryIcon ?? 'ellipsis'} className="size-4" />
        </span>
        <div className="min-w-0 flex-1">
          <p className="truncate text-sm">
            {item.description || item.categoryName || TYPE_LABELS[item.type]}
          </p>
          <p className="text-xs text-muted-foreground">
            {item.categoryName ? `${item.categoryName} · ` : ''}
            {new Date(item.date).toLocaleDateString('ru-RU', { day: 'numeric', month: 'long' })}
          </p>
        </div>
        <span className={`shrink-0 text-sm font-semibold tabular-nums ${color}`}>
          {sign}{formatRub(item.amount)}
        </span>
      </Link>
    </li>
  )
}
