import { useState } from 'react'
import { Link } from 'react-router-dom'
import {
  useBudgetProgress,
  useCreateBudget,
  useUpdateBudget,
  useDeleteBudget,
  type BudgetProgress,
} from '@/entities/budget'
import { BudgetForm, type BudgetFormValues } from '@/features/budget-form'
import { Button } from '@/shared/ui/button'
import { Skeleton } from '@/shared/ui/skeleton'
import { LucideIcon } from '@/shared/ui/lucide-icon'
import { formatRub, formatMonthLong } from '@/shared/lib/format'

type Editing = 'new' | BudgetProgress | null

const now = new Date()
const CURRENT_YEAR = now.getFullYear()
const CURRENT_MONTH = now.getMonth() + 1

/** Progress-bar fill colour: green under 80%, amber 80–100%, red on overrun. */
function barColor(pct: number): string {
  if (pct > 100) return 'bg-destructive'
  if (pct >= 80) return 'bg-amber-500'
  return 'bg-emerald-500'
}

export function BudgetsPage() {
  const { data: budgets, isPending } = useBudgetProgress(CURRENT_YEAR, CURRENT_MONTH)
  const createMutation = useCreateBudget()
  const updateMutation = useUpdateBudget()
  const deleteMutation = useDeleteBudget()

  const [editing, setEditing] = useState<Editing>(null)
  const [error, setError] = useState<string | null>(null)

  function handleSubmit(values: BudgetFormValues) {
    setError(null)
    if (editing === 'new') {
      createMutation.mutate(values, {
        onSuccess: () => setEditing(null),
        onError: () =>
          setError('Не удалось создать бюджет. Возможно, он уже задан для этой категории и месяца.'),
      })
    } else if (editing) {
      updateMutation.mutate(
        { id: editing.budgetId, input: { limitAmount: values.limitAmount, currency: values.currency } },
        {
          onSuccess: () => setEditing(null),
          onError: () => setError('Не удалось сохранить изменения.'),
        },
      )
    }
  }

  function handleDelete(budget: BudgetProgress) {
    if (confirm(`Удалить бюджет «${budget.categoryName}»?`)) {
      deleteMutation.mutate(budget.budgetId)
    }
  }

  function openEdit(budget: BudgetProgress) {
    setError(null)
    setEditing(budget)
  }

  return (
    <main className="mx-auto max-w-3xl space-y-6 p-8">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Бюджеты</h1>
          <p className="text-sm text-muted-foreground">{formatMonthLong(CURRENT_YEAR, CURRENT_MONTH)}</p>
          <Link to="/" className="text-sm text-muted-foreground hover:underline">
            ← На главную
          </Link>
        </div>
        {editing === null && (
          <Button onClick={() => { setError(null); setEditing('new') }}>Добавить</Button>
        )}
      </header>

      {editing !== null && (
        <section className="rounded-lg border bg-card p-4">
          <h2 className="mb-3 text-sm font-medium">
            {editing === 'new' ? 'Новый бюджет' : `Бюджет: ${editing.categoryName}`}
          </h2>
          <BudgetForm
            mode={editing === 'new' ? 'create' : 'edit'}
            defaultValues={
              editing === 'new'
                ? { year: CURRENT_YEAR, month: CURRENT_MONTH }
                : {
                    categoryId: editing.categoryId,
                    year: editing.year,
                    month: editing.month,
                    limitAmount: editing.limitAmount,
                    currency: editing.currency,
                  }
            }
            submitting={createMutation.isPending || updateMutation.isPending}
            submitLabel={editing === 'new' ? 'Создать' : 'Сохранить'}
            error={error}
            onSubmit={handleSubmit}
            onCancel={() => { setError(null); setEditing(null) }}
          />
        </section>
      )}

      {isPending ? (
        <ul className="space-y-3">
          {Array.from({ length: 3 }).map((_, i) => (
            <li key={i}>
              <Skeleton className="h-20 w-full" />
            </li>
          ))}
        </ul>
      ) : budgets && budgets.length > 0 ? (
        <ul className="space-y-3">
          {budgets.map((b) => {
            const pct = Math.max(0, b.percentage)
            const over = b.percentage > 100
            return (
              <li key={b.budgetId} className="rounded-lg border bg-card p-4">
                <div className="flex items-center justify-between gap-3">
                  <div className="flex items-center gap-3">
                    <span
                      className="flex size-9 items-center justify-center rounded-md"
                      style={{ backgroundColor: `${b.categoryColor}22` }}
                    >
                      <LucideIcon name={b.categoryIcon} color={b.categoryColor} className="size-5" />
                    </span>
                    <span className="font-medium">{b.categoryName}</span>
                  </div>
                  <div className="flex gap-2">
                    <Button variant="outline" size="sm" onClick={() => openEdit(b)}>
                      Изменить
                    </Button>
                    <Button variant="outline" size="sm" onClick={() => handleDelete(b)}>
                      Удалить
                    </Button>
                  </div>
                </div>

                <div className="mt-3 space-y-1.5">
                  <div className="h-2.5 w-full overflow-hidden rounded-full bg-muted">
                    <div
                      className={`h-full rounded-full transition-all ${barColor(b.percentage)}`}
                      style={{ width: `${Math.min(100, pct)}%` }}
                    />
                  </div>
                  <div className="flex items-center justify-between text-sm">
                    <span className={over ? 'font-medium text-destructive' : 'text-muted-foreground'}>
                      {formatRub(b.spentAmount)} из {formatRub(b.limitAmount)}
                    </span>
                    <span className={over ? 'font-medium text-destructive' : 'text-muted-foreground'}>
                      {b.percentage.toLocaleString('ru-RU', { maximumFractionDigits: 1 })}%
                      {over
                        ? ` · превышение на ${formatRub(-b.remainingAmount)}`
                        : ` · осталось ${formatRub(b.remainingAmount)}`}
                    </span>
                  </div>
                </div>
              </li>
            )
          })}
        </ul>
      ) : (
        <p className="rounded-lg border border-dashed bg-card p-8 text-center text-sm text-muted-foreground">
          На этот месяц бюджеты не заданы. Нажмите «Добавить», чтобы установить лимит по категории.
        </p>
      )}
    </main>
  )
}
