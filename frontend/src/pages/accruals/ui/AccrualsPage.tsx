import { useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import {
  useAccruals,
  useCreateAccrual,
  useDeleteAccrual,
  type AccrualFilter,
  type AccrualListItem,
} from '@/entities/accrual'
import { AccrualForm, type AccrualFormValues } from '@/features/accrual-form'
import {
  FilterPanel,
  filterToParams,
  parseFilterFromParams,
} from '@/features/accrual-filter'
import { Button } from '@/shared/ui/button'
import { Skeleton } from '@/shared/ui/skeleton'

const TYPE_LABELS: Record<string, string> = {
  Income: 'Доход',
  ReturnIncome: 'Возврат дохода',
  Expense: 'Расход',
  ReturnExpense: 'Возврат расхода',
}

const TYPE_COLORS: Record<string, string> = {
  Income: 'text-green-500',
  ReturnIncome: 'text-green-400',
  Expense: 'text-red-500',
  ReturnExpense: 'text-red-400',
}

function formatAmount(item: AccrualListItem) {
  const sign = item.type === 'Income' || item.type === 'ReturnIncome' ? '+' : '−'
  return `${sign}${item.amount.toLocaleString('ru-RU', { minimumFractionDigits: 2 })} ${item.currency}`
}

export function AccrualsPage() {
  const [searchParams, setSearchParams] = useSearchParams()
  const filter = useMemo<AccrualFilter>(
    () => parseFilterFromParams(searchParams),
    [searchParams],
  )
  const [showForm, setShowForm] = useState(false)

  const { data, isPending } = useAccruals(filter)
  const createMutation = useCreateAccrual()
  const deleteMutation = useDeleteAccrual()

  /** Replace filter fields (clearing pagination back to page 1) and sync URL. */
  function applyFilter(fields: Omit<AccrualFilter, 'page' | 'pageSize'>) {
    setSearchParams(filterToParams({ ...fields, pageSize: filter.pageSize }))
  }

  function goToPage(page: number) {
    setSearchParams(filterToParams({ ...filter, page }))
  }

  function handleCreate(values: AccrualFormValues) {
    createMutation.mutate(
      {
        ...values,
        categoryId: values.categoryId || null,
        groupId: values.groupId || null,
        exchangeRate: values.exchangeRate || null,
        tags: values.tags ?? [],
      },
      { onSuccess: () => setShowForm(false) },
    )
  }

  function handleDelete(item: AccrualListItem) {
    if (confirm(`Удалить начисление на ${item.amount} ${item.currency}?`)) {
      deleteMutation.mutate(item.id)
    }
  }

  return (
    <main className="mx-auto max-w-4xl space-y-6 p-8">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Начисления</h1>
          <Link to="/" className="text-sm text-muted-foreground hover:underline">
            ← На главную
          </Link>
        </div>
        {!showForm && (
          <Button onClick={() => setShowForm(true)}>+ Добавить</Button>
        )}
      </header>

      {showForm && (
        <section className="rounded-lg border bg-card p-4">
          <h2 className="mb-3 text-sm font-medium">Новое начисление</h2>
          <AccrualForm
            submitting={createMutation.isPending}
            submitLabel="Создать"
            onSubmit={handleCreate}
            onCancel={() => setShowForm(false)}
          />
        </section>
      )}

      <FilterPanel filter={filter} onApply={applyFilter} />

      {isPending ? (
        <ul className="space-y-2">
          {Array.from({ length: 8 }).map((_, i) => (
            <li key={i}><Skeleton className="h-16 w-full" /></li>
          ))}
        </ul>
      ) : (
        <>
          <ul className="space-y-2">
            {data?.items.map((item) => (
              <li
                key={item.id}
                className="flex items-center justify-between rounded-lg border bg-card p-4"
              >
                <div className="flex items-center gap-4 min-w-0">
                  {item.categoryColor && (
                    <span
                      className="flex size-9 shrink-0 items-center justify-center rounded-md text-xs"
                      style={{ backgroundColor: `${item.categoryColor}22`, color: item.categoryColor }}
                    >
                      {item.categoryIcon ? '●' : '?'}
                    </span>
                  )}
                  <div className="min-w-0">
                    <p className={`font-semibold ${TYPE_COLORS[item.type] ?? ''}`}>
                      {formatAmount(item)}
                    </p>
                    <p className="truncate text-sm text-muted-foreground">
                      {item.description || item.categoryName || TYPE_LABELS[item.type]}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {new Date(item.date).toLocaleDateString('ru-RU')}
                      {item.tags.length > 0 && (
                        <span className="ml-2">{item.tags.map(t => `#${t}`).join(' ')}</span>
                      )}
                    </p>
                  </div>
                </div>

                <div className="flex shrink-0 gap-2">
                  <Link to={`/accruals/${item.id}`}>
                    <Button variant="outline" size="sm">Открыть</Button>
                  </Link>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => handleDelete(item)}
                  >
                    Удалить
                  </Button>
                </div>
              </li>
            ))}
          </ul>

          {/* Pagination */}
          {data && data.totalPages > 1 && (
            <div className="flex items-center justify-center gap-2 pt-2">
              <Button
                variant="outline"
                size="sm"
                disabled={(filter.page ?? 1) === 1}
                onClick={() => goToPage((filter.page ?? 1) - 1)}
              >
                ← Пред.
              </Button>
              <span className="text-sm text-muted-foreground">
                {filter.page} / {data.totalPages} (всего: {data.totalCount})
              </span>
              <Button
                variant="outline"
                size="sm"
                disabled={(filter.page ?? 1) === data.totalPages}
                onClick={() => goToPage((filter.page ?? 1) + 1)}
              >
                След. →
              </Button>
            </div>
          )}
        </>
      )}
    </main>
  )
}
