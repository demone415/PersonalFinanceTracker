import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  ChevronRight, FilePlus2, Pencil, Trash2, Plus, Minus, ReceiptText, type LucideIcon,
} from 'lucide-react'
import { useChangeLog, type ChangeLogEntry } from '@/entities/change-log'
import { useCategories } from '@/entities/category'
import { Button } from '@/shared/ui/button'
import { Skeleton } from '@/shared/ui/skeleton'
import { formatDateTimeRu, formatMoney } from '@/shared/lib/format'

const PAGE_SIZE = 20

const ENTITY_FILTERS: ReadonlyArray<{ value: string | undefined; label: string }> = [
  { value: undefined, label: 'Все' },
  { value: 'Accrual', label: 'Начисления' },
  { value: 'MonthlyBudget', label: 'Бюджеты' },
]

/** Action presentation: Russian label, icon, and accent class (icon backs the colour). */
const ACTIONS: Record<string, { label: string; icon: LucideIcon; accent: string; dot: string }> = {
  Create: { label: 'Создание', icon: FilePlus2, accent: 'text-emerald-600 dark:text-emerald-400', dot: 'bg-emerald-500' },
  Update: { label: 'Изменение', icon: Pencil, accent: 'text-amber-600 dark:text-amber-400', dot: 'bg-amber-500' },
  Delete: { label: 'Удаление', icon: Trash2, accent: 'text-red-600 dark:text-red-400', dot: 'bg-red-500' },
}

const ENTITY_LABELS: Record<string, string> = {
  Accrual: 'Начисление',
  MonthlyBudget: 'Бюджет',
}

/** Human-readable Russian labels for the snapshot fields the user cares about. */
const FIELD_LABELS: Record<string, string> = {
  Amount: 'Сумма',
  Date: 'Дата',
  Type: 'Тип',
  Currency: 'Валюта',
  ExchangeRate: 'Курс',
  CategoryId: 'Категория',
  Description: 'Описание',
  IncludeInStats: 'Учитывать в статистике',
  Tags: 'Метки',
  // MonthlyBudget
  LimitAmount: 'Лимит',
  Year: 'Год',
  Month: 'Месяц',
}

const ACCRUAL_TYPE_LABELS: Record<string, string> = {
  Income: 'Доход',
  Expense: 'Расход',
  ReturnIncome: 'Возврат дохода',
  ReturnExpense: 'Возврат расхода',
}

const MONTHS = [
  'январь', 'февраль', 'март', 'апрель', 'май', 'июнь',
  'июль', 'август', 'сентябрь', 'октябрь', 'ноябрь', 'декабрь',
]

/** Technical / opaque fields that mean nothing to a user — never shown in the diff. */
const HIDDEN_KEYS = new Set([
  'xmin', 'Id', 'UserId', 'ReceiptId', 'GroupId', 'CreatedAt', 'AmountInBaseCurrency',
  'Category', 'Receipt', 'Items',
])

type Snapshot = Record<string, unknown>
type ItemLine = { Name?: string; Quantity?: number; Sum?: number }

/** Returned when a snapshot string is present but not valid JSON (vs. legitimately absent). */
const CORRUPT = Symbol('corrupt-snapshot')

function parse(json?: string | null): Snapshot | null | typeof CORRUPT {
  if (!json) return null
  try {
    return JSON.parse(json) as Snapshot
  } catch {
    return CORRUPT
  }
}

/** Renders one field value in Russian, given the surrounding snapshot for context. */
function formatValue(
  key: string,
  value: unknown,
  snapshot: Snapshot | null,
  categoryName: (id: string) => string,
): string {
  if (value == null || value === '') return '—'

  switch (key) {
    case 'Amount':
    case 'LimitAmount': {
      const currency = typeof snapshot?.Currency === 'string' ? snapshot.Currency : 'RUB'
      return formatMoney(Number(value), currency)
    }
    case 'Date':
      return formatDateTimeRu(String(value))
    case 'Type':
      return ACCRUAL_TYPE_LABELS[String(value)] ?? String(value)
    case 'IncludeInStats':
      return value ? 'Да' : 'Нет'
    case 'CategoryId':
      return categoryName(String(value))
    case 'Month': {
      const m = Number(value)
      return MONTHS[m - 1] ?? String(value)
    }
    case 'Tags':
      return Array.isArray(value) && value.length > 0 ? value.join(', ') : '—'
    case 'ExchangeRate':
      return Number(value).toLocaleString('ru-RU', { maximumFractionDigits: 4 })
    default:
      if (typeof value === 'object') return JSON.stringify(value)
      return String(value)
  }
}

/** Whether a value carries no information worth a row (null, empty string, empty array). */
function isEmpty(value: unknown): boolean {
  return value == null || value === '' || (Array.isArray(value) && value.length === 0)
}

/** Field-level rows: changed (before→after) on update, or the single-side value otherwise. */
function diffRows(before: Snapshot | null, after: Snapshot | null) {
  const keys = new Set([...Object.keys(before ?? {}), ...Object.keys(after ?? {})])
  const rows: Array<{ key: string; before: unknown; after: unknown; changed: boolean }> = []
  for (const key of keys) {
    if (HIDDEN_KEYS.has(key) || !(key in FIELD_LABELS)) continue
    const b = before?.[key]
    const a = after?.[key]
    const isUpdate = before != null && after != null
    const changed = isUpdate && JSON.stringify(b) !== JSON.stringify(a)
    // On an update, surface only the fields that actually changed.
    if (isUpdate && !changed) continue
    // On a create/delete, skip fields with no value (e.g. empty rate, no tags).
    if (!isUpdate && isEmpty(before != null ? b : a)) continue
    rows.push({ key, before: b, after: a, changed })
  }
  return rows
}

/** Receipt positions that appeared / disappeared, keyed by name+sum to dedupe overlaps. */
function itemDiff(before: Snapshot | null, after: Snapshot | null) {
  const toMap = (s: Snapshot | null) => {
    const list = Array.isArray(s?.Items) ? (s.Items as ItemLine[]) : []
    return new Map(list.map((i) => [`${i.Name}|${i.Sum}`, i]))
  }
  const beforeMap = toMap(before)
  const afterMap = toMap(after)
  const added = [...afterMap].filter(([k]) => !beforeMap.has(k)).map(([, v]) => v)
  const removed = [...beforeMap].filter(([k]) => !afterMap.has(k)).map(([, v]) => v)
  return { added, removed }
}

function ItemRow({ item, kind }: { item: ItemLine; kind: 'added' | 'removed' }) {
  const Icon = kind === 'added' ? Plus : Minus
  const cls = kind === 'added'
    ? 'text-emerald-600 dark:text-emerald-400'
    : 'text-red-600 dark:text-red-400'
  return (
    <li className="flex items-center gap-2 text-xs">
      <Icon className={`size-3.5 shrink-0 ${cls}`} aria-hidden />
      <span className="min-w-0 flex-1 truncate text-foreground">{item.Name || 'Без названия'}</span>
      {item.Quantity != null && item.Quantity !== 1 && (
        <span className="shrink-0 tabular-nums text-muted-foreground">×{item.Quantity}</span>
      )}
      {item.Sum != null && (
        <span className="shrink-0 tabular-nums text-muted-foreground">{formatMoney(Number(item.Sum))}</span>
      )}
    </li>
  )
}

function EntryDetails({
  entry,
  categoryName,
}: {
  entry: ChangeLogEntry
  categoryName: (id: string) => string
}) {
  const before = parse(entry.valuesBefore)
  const after = parse(entry.valuesAfter)

  if (before === CORRUPT || after === CORRUPT) {
    return <p className="text-xs text-red-500">Снимок повреждён — не удалось разобрать JSON.</p>
  }

  const rows = diffRows(before, after)
  const { added, removed } = itemDiff(before, after)
  const hasItems = added.length > 0 || removed.length > 0

  if (rows.length === 0 && !hasItems) {
    return <p className="text-xs text-muted-foreground">Нет полей для отображения.</p>
  }

  return (
    <div className="space-y-3">
      {rows.length > 0 && (
        <dl className="grid grid-cols-[minmax(7rem,auto)_1fr] gap-x-3 gap-y-1.5 text-xs">
          {rows.map((row) => (
            <div key={row.key} className="contents">
              <dt className="font-medium text-muted-foreground">{FIELD_LABELS[row.key]}</dt>
              <dd className="min-w-0">
                {entry.action === 'Update' ? (
                  <span className="flex flex-wrap items-center gap-1">
                    <span className="text-muted-foreground line-through decoration-red-400/60">
                      {formatValue(row.key, row.before, before, categoryName)}
                    </span>
                    <ChevronRight className="size-3 shrink-0 text-muted-foreground" aria-hidden />
                    <span className="font-medium text-foreground">
                      {formatValue(row.key, row.after, after, categoryName)}
                    </span>
                  </span>
                ) : (
                  <span className="text-foreground">
                    {formatValue(
                      row.key,
                      entry.action === 'Delete' ? row.before : row.after,
                      entry.action === 'Delete' ? before : after,
                      categoryName,
                    )}
                  </span>
                )}
              </dd>
            </div>
          ))}
        </dl>
      )}

      {hasItems && (
        <div className="rounded-md border border-border/60 bg-muted/30 p-2.5">
          <p className="mb-1.5 flex items-center gap-1.5 text-xs font-medium text-muted-foreground">
            <ReceiptText className="size-3.5" aria-hidden />
            Позиции чека
          </p>
          <ul className="space-y-1">
            {added.map((item, i) => <ItemRow key={`a${i}`} item={item} kind="added" />)}
            {removed.map((item, i) => <ItemRow key={`r${i}`} item={item} kind="removed" />)}
          </ul>
        </div>
      )}
    </div>
  )
}

function EntryCard({
  entry,
  categoryName,
}: {
  entry: ChangeLogEntry
  categoryName: (id: string) => string
}) {
  const action = ACTIONS[entry.action] ?? {
    label: entry.action, icon: Pencil, accent: 'text-muted-foreground', dot: 'bg-muted-foreground',
  }
  const ActionIcon = action.icon
  const entityLabel = ENTITY_LABELS[entry.entityType] ?? entry.entityType

  // An accrual that still exists (i.e. not a Delete) can be opened from its entry.
  const linkTo = entry.entityType === 'Accrual' && entry.action !== 'Delete'
    ? `/accruals/${entry.entityId}`
    : null

  const header = (
    <div className="flex items-start gap-3">
      <span className={`mt-0.5 flex size-7 shrink-0 items-center justify-center rounded-full bg-muted ${action.accent}`}>
        <ActionIcon className="size-4" aria-hidden />
      </span>
      <div className="min-w-0 flex-1">
        <p className="flex items-center gap-1.5 text-sm font-semibold">
          <span className={action.accent}>{action.label}</span>
          <span className="text-foreground">· {entityLabel}</span>
        </p>
        <p className="text-xs text-muted-foreground">{formatDateTimeRu(entry.timestamp)}</p>
      </div>
      {linkTo && (
        <span className="flex shrink-0 items-center gap-0.5 self-center text-xs text-muted-foreground transition-colors group-hover:text-foreground">
          Открыть
          <ChevronRight className="size-4" aria-hidden />
        </span>
      )}
    </div>
  )

  const body = (
    <div className="mt-3 border-t border-border/60 pt-3">
      <EntryDetails entry={entry} categoryName={categoryName} />
    </div>
  )

  if (linkTo) {
    return (
      <li>
        <Link
          to={linkTo}
          className="group block rounded-lg border bg-card p-4 transition-colors hover:border-primary/40 hover:bg-accent/40"
        >
          {header}
          {body}
        </Link>
      </li>
    )
  }

  return (
    <li className="rounded-lg border bg-card p-4">
      {header}
      {body}
    </li>
  )
}

export function JournalPage() {
  const [entityType, setEntityType] = useState<string | undefined>(undefined)
  const [page, setPage] = useState(1)

  const filter = useMemo(
    () => ({ page, pageSize: PAGE_SIZE, entityType }),
    [page, entityType],
  )
  const { data, isPending } = useChangeLog(filter)

  const { data: categories } = useCategories()
  const categoryName = useMemo(() => {
    const byId = new Map((categories ?? []).map((c) => [c.id, c.name]))
    return (id: string) => byId.get(id) ?? 'Категория удалена'
  }, [categories])

  function applyEntityFilter(value: string | undefined) {
    setEntityType(value)
    setPage(1)
  }

  return (
    <main className="mx-auto max-w-3xl space-y-6 p-8">
      <header>
        <h1 className="text-2xl font-semibold tracking-tight">Журнал изменений</h1>
        <Link to="/" className="text-sm text-muted-foreground hover:underline">
          ← На главную
        </Link>
      </header>

      <div className="flex flex-wrap gap-2">
        {ENTITY_FILTERS.map((f) => (
          <Button
            key={f.label}
            variant={entityType === f.value ? 'default' : 'outline'}
            size="sm"
            onClick={() => applyEntityFilter(f.value)}
          >
            {f.label}
          </Button>
        ))}
      </div>

      {isPending ? (
        <ul className="space-y-2">
          {Array.from({ length: 8 }).map((_, i) => (
            <li key={i}><Skeleton className="h-20 w-full" /></li>
          ))}
        </ul>
      ) : data && data.items.length > 0 ? (
        <>
          <ul className="space-y-2">
            {data.items.map((entry) => (
              <EntryCard key={entry.id} entry={entry} categoryName={categoryName} />
            ))}
          </ul>

          {data.totalPages > 1 && (
            <div className="flex items-center justify-center gap-2 pt-2">
              <Button
                variant="outline"
                size="sm"
                disabled={page === 1}
                onClick={() => setPage((p) => p - 1)}
              >
                ← Пред.
              </Button>
              <span className="text-sm text-muted-foreground">
                {page} / {data.totalPages} (всего: {data.totalCount})
              </span>
              <Button
                variant="outline"
                size="sm"
                disabled={page === data.totalPages}
                onClick={() => setPage((p) => p + 1)}
              >
                След. →
              </Button>
            </div>
          )}
        </>
      ) : (
        <p className="rounded-lg border bg-card p-8 text-center text-sm text-muted-foreground">
          Изменений пока нет.
        </p>
      )}
    </main>
  )
}
