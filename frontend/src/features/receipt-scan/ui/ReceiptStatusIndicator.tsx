import { CheckCircle2, Clock, Loader2, XCircle } from 'lucide-react'
import { useReceiptStatus, type ReceiptFetchStatus } from '@/entities/accrual'

/**
 * Live indicator of the background receipt fetch (T4.3.3). Polls
 * GET /accruals/{id}/receipt-status and shows: queue position while pending, then
 * the terminal outcome — received / failed / retry-limit reached.
 */
export function ReceiptStatusIndicator({ accrualId }: { accrualId: string }) {
  const { data, isPending, isError } = useReceiptStatus(accrualId)

  if (isPending) {
    return (
      <StatusRow
        icon={<Loader2 className="size-5 animate-spin text-muted-foreground" />}
        title="Запрашиваем статус…"
        tone="muted"
      />
    )
  }

  if (isError || !data) {
    return (
      <StatusRow
        icon={<XCircle className="size-5 text-destructive" />}
        title="Не удалось получить статус загрузки чека"
        tone="error"
      />
    )
  }

  return <StatusRow {...presentation(data.fetchStatus, data.queuePosition)} />
}

interface Presentation {
  icon: React.ReactNode
  title: string
  detail?: string
  tone: 'muted' | 'success' | 'error'
}

function presentation(status: ReceiptFetchStatus, queuePosition?: number): Presentation {
  switch (status) {
    case 'Pending':
      return {
        icon: <Clock className="size-5 animate-pulse text-primary" />,
        title: 'Чек в очереди на загрузку',
        detail:
          queuePosition && queuePosition > 0
            ? `Позиция в очереди: ${queuePosition}`
            : 'Состав чека подгрузится в фоне — можно закрыть страницу.',
        tone: 'muted',
      }
    case 'Fetched':
      return {
        icon: <CheckCircle2 className="size-5 text-green-500" />,
        title: 'Чек получен',
        detail: 'Состав чека подгружен и привязан к начислению.',
        tone: 'success',
      }
    case 'Failed':
      return {
        icon: <XCircle className="size-5 text-destructive" />,
        title: 'Не удалось получить чек',
        detail: 'Данные чека пока недоступны в системе ФНС.',
        tone: 'error',
      }
    case 'RetryLimit':
      return {
        icon: <XCircle className="size-5 text-destructive" />,
        title: 'Превышен лимит попыток',
        detail: 'Чек не появился в базе ФНС после нескольких попыток.',
        tone: 'error',
      }
  }
}

function StatusRow({ icon, title, detail, tone }: Presentation) {
  const border =
    tone === 'success'
      ? 'border-green-500/30 bg-green-500/5'
      : tone === 'error'
        ? 'border-destructive/30 bg-destructive/5'
        : 'border-border bg-muted/30'

  return (
    <div className={`flex items-start gap-3 rounded-lg border p-3 ${border}`}>
      <span className="mt-0.5 shrink-0">{icon}</span>
      <div className="min-w-0">
        <p className="text-sm font-medium">{title}</p>
        {detail && <p className="text-xs text-muted-foreground">{detail}</p>}
      </div>
    </div>
  )
}
