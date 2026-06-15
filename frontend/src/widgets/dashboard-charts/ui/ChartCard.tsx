import type { ReactNode } from 'react'
import { Skeleton } from '@/shared/ui/skeleton'

/**
 * Consistent card shell for a dashboard chart: title, fixed-height body, and
 * built-in loading / empty / error states so each widget stays self-contained.
 */
export function ChartCard({
  title,
  action,
  loading,
  isEmpty,
  error,
  emptyText = 'Нет данных за выбранный период',
  children,
}: {
  title: string
  action?: ReactNode
  loading?: boolean
  isEmpty?: boolean
  error?: boolean
  emptyText?: string
  children: ReactNode
}) {
  return (
    <section className="rounded-xl border bg-card p-4 md:p-5">
      <div className="mb-4 flex items-center justify-between gap-2">
        <h2 className="text-sm font-medium text-muted-foreground">{title}</h2>
        {action}
      </div>
      {loading ? (
        <Skeleton className="h-64 w-full" />
      ) : error ? (
        <div className="flex h-64 items-center justify-center text-center text-sm text-destructive">
          Не удалось загрузить данные
        </div>
      ) : isEmpty ? (
        <div className="flex h-64 items-center justify-center text-center text-sm text-muted-foreground">
          {emptyText}
        </div>
      ) : (
        children
      )}
    </section>
  )
}
