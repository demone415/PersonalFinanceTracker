import { useState } from 'react'
import type { AccrualFilter, AccrualType } from '@/entities/accrual'
import { useCategories } from '@/entities/category'
import { Button } from '@/shared/ui/button'
import { DatePicker } from '@/shared/ui/date-picker'
import { Input } from '@/shared/ui/input'
import { Label } from '@/shared/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/ui/select'
import { countActiveFilters } from '../lib/filter-url'

/** Sentinel for the "any" option — Radix Select forbids an empty-string value. */
const ANY = '__any__'

const TYPE_OPTIONS: { value: AccrualType; label: string }[] = [
  { value: 'Expense', label: 'Расход' },
  { value: 'Income', label: 'Доход' },
  { value: 'ReturnExpense', label: 'Возврат расхода' },
  { value: 'ReturnIncome', label: 'Возврат дохода' },
]

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
              <DatePicker
                value={draft.dateFrom}
                onChange={(v) => update('dateFrom', v)}
                placeholder="dd.mm.yyyy"
              />
            </div>
            <div className="space-y-1">
              <Label>Дата по</Label>
              <DatePicker
                value={draft.dateTo}
                onChange={(v) => update('dateTo', v)}
                placeholder="dd.mm.yyyy"
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
              <Select
                value={draft.categoryId || ANY}
                onValueChange={(v) => update('categoryId', v === ANY ? '' : v)}
              >
                <SelectTrigger>
                  <SelectValue placeholder="— любая —" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ANY}>— любая —</SelectItem>
                  {categories?.map((c) => (
                    <SelectItem key={c.id} value={c.id}>
                      {c.name}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            {/* Type */}
            <div className="space-y-1">
              <Label>Тип</Label>
              <Select
                value={draft.type || ANY}
                onValueChange={(v) => update('type', v === ANY ? '' : v)}
              >
                <SelectTrigger>
                  <SelectValue placeholder="— любой —" />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value={ANY}>— любой —</SelectItem>
                  {TYPE_OPTIONS.map((t) => (
                    <SelectItem key={t.value} value={t.value}>
                      {t.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
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
