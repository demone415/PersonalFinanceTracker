import { useState } from 'react'
import type { AccrualFilter, AccrualType } from '@/entities/accrual'
import { useCategories } from '@/entities/category'
import { Button } from '@/shared/ui/button'
import { Input } from '@/shared/ui/input'
import { Label } from '@/shared/ui/label'
import { countActiveFilters } from '../lib/filter-url'

const TYPE_OPTIONS: { value: AccrualType; label: string }[] = [
  { value: 'Expense', label: 'Расход' },
  { value: 'Income', label: 'Доход' },
  { value: 'ReturnExpense', label: 'Возврат расхода' },
  { value: 'ReturnIncome', label: 'Возврат дохода' },
]

const SELECT_CLASS =
  'flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm'

/** Mutable, non-pagination fields the panel edits as a local draft. */
type Draft = {
  dateFrom: string
  dateTo: string
  categoryId: string
  amountMin: string
  amountMax: string
  type: string
}

function toDraft(filter: AccrualFilter): Draft {
  return {
    dateFrom: filter.dateFrom ?? '',
    dateTo: filter.dateTo ?? '',
    categoryId: filter.categoryId ?? '',
    amountMin: filter.amountMin != null ? String(filter.amountMin) : '',
    amountMax: filter.amountMax != null ? String(filter.amountMax) : '',
    type: filter.type ?? '',
  }
}

const EMPTY_DRAFT: Draft = {
  dateFrom: '',
  dateTo: '',
  categoryId: '',
  amountMin: '',
  amountMax: '',
  type: '',
}

interface Props {
  /** Current filter, derived from the URL (source of truth). */
  filter: AccrualFilter
  /** Applies the chosen filter fields; resets to page 1 implicitly. */
  onApply: (fields: Omit<AccrualFilter, 'page' | 'pageSize'>) => void
}

export function FilterPanel({ filter, onApply }: Props) {
  const { data: categories } = useCategories()
  const [open, setOpen] = useState(false)
  const [draft, setDraft] = useState<Draft>(() => toDraft(filter))

  // Re-sync the draft when the URL-derived filter changes externally (back/
  // forward navigation, reset elsewhere). `filter` is referentially stable
  // between renders, so this only fires on a real change.
  const [syncedFilter, setSyncedFilter] = useState(filter)
  if (filter !== syncedFilter) {
    setSyncedFilter(filter)
    setDraft(toDraft(filter))
  }

  const activeCount = countActiveFilters(filter)

  function update<K extends keyof Draft>(key: K, value: Draft[K]) {
    setDraft((d) => ({ ...d, [key]: value }))
  }

  function handleApply() {
    const min = draft.amountMin.trim() === '' ? undefined : Number(draft.amountMin)
    const max = draft.amountMax.trim() === '' ? undefined : Number(draft.amountMax)
    onApply({
      dateFrom: draft.dateFrom || undefined,
      dateTo: draft.dateTo || undefined,
      categoryId: draft.categoryId || undefined,
      amountMin: Number.isFinite(min) ? min : undefined,
      amountMax: Number.isFinite(max) ? max : undefined,
      type: (draft.type || undefined) as AccrualType | undefined,
    })
  }

  function handleReset() {
    setDraft(EMPTY_DRAFT)
    onApply({})
  }

  return (
    <section className="rounded-lg border bg-card">
      <button
        type="button"
        onClick={() => setOpen((o) => !o)}
        className="flex w-full items-center justify-between px-4 py-3 text-sm font-medium"
      >
        <span className="flex items-center gap-2">
          Фильтры
          {activeCount > 0 && (
            <span className="rounded-full bg-primary px-2 py-0.5 text-xs text-primary-foreground">
              {activeCount}
            </span>
          )}
        </span>
        <span className="text-muted-foreground">{open ? '▲' : '▼'}</span>
      </button>

      {open && (
        <div className="space-y-4 border-t p-4">
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            {/* Date range */}
            <div className="space-y-1">
              <Label>Дата с</Label>
              <Input
                type="date"
                value={draft.dateFrom}
                onChange={(e) => update('dateFrom', e.target.value)}
              />
            </div>
            <div className="space-y-1">
              <Label>Дата по</Label>
              <Input
                type="date"
                value={draft.dateTo}
                onChange={(e) => update('dateTo', e.target.value)}
              />
            </div>

            {/* Amount range */}
            <div className="space-y-1">
              <Label>Сумма от</Label>
              <Input
                type="number"
                step="0.01"
                min="0"
                placeholder="0.00"
                value={draft.amountMin}
                onChange={(e) => update('amountMin', e.target.value)}
              />
            </div>
            <div className="space-y-1">
              <Label>Сумма до</Label>
              <Input
                type="number"
                step="0.01"
                min="0"
                placeholder="0.00"
                value={draft.amountMax}
                onChange={(e) => update('amountMax', e.target.value)}
              />
            </div>

            {/* Category */}
            <div className="space-y-1">
              <Label>Категория</Label>
              <select
                className={SELECT_CLASS}
                value={draft.categoryId}
                onChange={(e) => update('categoryId', e.target.value)}
              >
                <option value="">— любая —</option>
                {categories?.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </select>
            </div>

            {/* Type */}
            <div className="space-y-1">
              <Label>Тип</Label>
              <select
                className={SELECT_CLASS}
                value={draft.type}
                onChange={(e) => update('type', e.target.value)}
              >
                <option value="">— любой —</option>
                {TYPE_OPTIONS.map((t) => (
                  <option key={t.value} value={t.value}>
                    {t.label}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <div className="flex justify-end gap-2">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={handleReset}
              disabled={activeCount === 0}
            >
              Сбросить
            </Button>
            <Button type="button" size="sm" onClick={handleApply}>
              Применить
            </Button>
          </div>
        </div>
      )}
    </section>
  )
}
