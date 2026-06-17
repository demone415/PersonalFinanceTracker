import { CheckCircle2, AlertCircle, Info, X, type LucideIcon } from 'lucide-react'
import { useToastStore, type ToastVariant } from '@/shared/lib/toast'
import { cn } from '@/shared/lib/utils'

const ICONS: Record<ToastVariant, LucideIcon> = {
  success: CheckCircle2,
  error: AlertCircle,
  info: Info,
}

const ACCENT: Record<ToastVariant, string> = {
  success: 'text-green-500',
  error: 'text-destructive',
  info: 'text-primary',
}

/**
 * Global toast outlet. Mounted once near the app root; any code can raise a
 * toast via {@link useToastStore}. Used by the CSV export to announce readiness.
 */
export function Toaster() {
  const toasts = useToastStore((s) => s.toasts)
  const dismiss = useToastStore((s) => s.dismiss)

  if (toasts.length === 0) return null

  return (
    <div className="fixed inset-x-0 bottom-0 z-50 flex flex-col items-center gap-2 p-4 md:inset-x-auto md:right-4 md:items-end">
      {toasts.map((toast) => {
        const Icon = ICONS[toast.variant]
        return (
          <div
            key={toast.id}
            role="status"
            className="flex w-full max-w-sm items-start gap-3 rounded-lg border bg-card p-3 shadow-lg"
          >
            <Icon className={cn('mt-0.5 size-5 shrink-0', ACCENT[toast.variant])} />
            <p className="flex-1 text-sm">{toast.message}</p>
            <button
              type="button"
              aria-label="Закрыть"
              onClick={() => dismiss(toast.id)}
              className="text-muted-foreground transition-colors hover:text-foreground"
            >
              <X className="size-4" />
            </button>
          </div>
        )
      })}
    </div>
  )
}
