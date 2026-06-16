import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { useChangeLog, type ChangeLogEntry } from '@/entities/change-log'
import { Button } from '@/shared/ui/button'
import { Skeleton } from '@/shared/ui/skeleton'

const PAGE_SIZE = 20

const ENTITY_FILTERS: ReadonlyArray<{ value: string | undefined; label: string }> = [
  { value: undefined, label: 'Все' },
  { value: 'Accrual', label: 'Начисления' },
  { value: 'MonthlyBudget', label: 'Бюджеты' },
]

const ACTION_LABELS: Record<string, string> = {
  Create: 'Создание',
  Update: 'Изменение',
  Delete: 'Удаление',
}

const ACTION_COLORS: Record<string, string> = {
  Create: 'text-green-500',
  Update: 'text-amber-500',
  Delete: 'text-red-500',
}

const ENTITY_LABELS: Record<string, string> = {
  Accrual: 'Начисление',
  MonthlyBudget: 'Бюджет',
}

/** Properties not worth showing in the diff — concurrency tokens / audit noise. */
const HIDDEN_KEYS = new Set(['xmin', 'Id', 'UserId'])

type Snapshot = Record<string, unknown>

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

function display(value: unknown): string {
  if (value == null) return '—'
  if (typeof value === 'object') return JSON.stringify(value)
  return String(value)
}

/** Field-level rows for an entry: changed (before→after), or the single-side snapshot. */
function diffRows(before: Snapshot | null, after: Snapshot | null) {
  const keys = new Set([...Object.keys(before ?? {}), ...Object.keys(after ?? {})])
  const rows: Array<{ key: string; before: unknown; after: unknown; changed: boolean }> = []
  for (const key of keys) {
    if (HIDDEN_KEYS.has(key)) continue
    const b = before?.[key]
    const a = after?.[key]
    const changed = before != null && after != null && display(b) !== display(a)
    // On an update, surface only the fields that actually changed.
    if (before != null && after != null && !changed) continue
    rows.push({ key, before: b, after: a, changed })
  }
  return rows
}

function EntryDetails({ entry }: { entry: ChangeLogEntry }) {
  const before = parse(entry.valuesBefore)
  const after = parse(entry.valuesAfter)

  if (before === CORRUPT || after === CORRUPT) {
    return <p className="text-xs text-red-500">Снимок повреждён — не удалось разобрать JSON.</p>
  }

  const rows = diffRows(before, after)

  if (rows.length === 0) {
    return <p className="text-xs text-muted-foreground">Нет полей для отображения.</p>
  }

  return (
    <table className="w-full text-xs">
      <tbody>
        {rows.map((row) => (
          <tr key={row.key} className="border-t border-border/50">
            <td className="py-1 pr-3 font-medium text-muted-foreground align-top">{row.key}</td>
            {entry.action === 'Update' ? (
              <td className="py-1">
                <span className="text-red-400 line-through">{display(row.before)}</span>
                <span className="mx-1 text-muted-foreground">→</span>
                <span className="text-green-400">{display(row.after)}</span>
              </td>
            ) : (
              <td className="py-1">
                {display(entry.action === 'Delete' ? row.before : row.after)}
              </td>
            )}
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function EntryRow({ entry }: { entry: ChangeLogEntry }) {
  const [open, setOpen] = useState(false)
  return (
    <li className="rounded-lg border bg-card p-4">
      <button
        type="button"
        className="flex w-full items-center justify-between gap-4 text-left"
        aria-expanded={open}
        onClick={() => setOpen((v) => !v)}
      >
        <div className="min-w-0">
          <p className="font-semibold">
            <span className={ACTION_COLORS[entry.action] ?? ''}>
              {ACTION_LABELS[entry.action] ?? entry.action}
            </span>
            <span className="ml-2 text-foreground">
              {ENTITY_LABELS[entry.entityType] ?? entry.entityType}
            </span>
          </p>
          <p className="text-xs text-muted-foreground">
            {new Date(entry.timestamp).toLocaleString('ru-RU')}
          </p>
        </div>
        <span className="shrink-0 text-xs text-muted-foreground">{open ? '▲' : '▼'}</span>
      </button>
      {open && (
        <div className="mt-3 border-t pt-3">
          <EntryDetails entry={entry} />
        </div>
      )}
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
            <li key={i}><Skeleton className="h-16 w-full" /></li>
          ))}
        </ul>
      ) : data && data.items.length > 0 ? (
        <>
          <ul className="space-y-2">
            {data.items.map((entry) => (
              <EntryRow key={entry.id} entry={entry} />
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
