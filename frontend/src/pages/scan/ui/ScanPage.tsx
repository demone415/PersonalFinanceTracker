import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Loader2, QrCode, ScanLine } from 'lucide-react'
import { useScanQr, type ScanQrResult } from '@/entities/accrual'
import { useCapabilities } from '@/entities/capabilities'
import { QrScanner, ReceiptStatusIndicator } from '@/features/receipt-scan'
import { Button } from '@/shared/ui/button'

/** Maps a failed scan-qr request to a user-facing message by HTTP status. */
function scanErrorMessage(error: Error): string {
  const status = Number(error.message.match(/Request failed: (\d+)/)?.[1])
  if (status === 400) return 'QR-код не похож на фискальный чек. Проверьте код и попробуйте снова.'
  if (status === 429) return 'Слишком много сканирований. Подождите немного и попробуйте позже.'
  if (status === 503) return 'Загрузка чеков сейчас недоступна на сервере.'
  return 'Не удалось отправить чек. Попробуйте ещё раз.'
}

export function ScanPage() {
  const { data: capabilities } = useCapabilities()
  const scanMutation = useScanQr()
  const [result, setResult] = useState<ScanQrResult | null>(null)

  // Stay optimistic while the flag loads; only block once it's explicitly false.
  const scanningDisabled = capabilities?.receiptScanning === false

  function handleDecode(qrRaw: string) {
    scanMutation.mutate(qrRaw, { onSuccess: setResult })
  }

  function reset() {
    scanMutation.reset()
    setResult(null)
  }

  return (
    <main className="mx-auto max-w-md space-y-6 p-4 md:p-8">
      <header className="space-y-1">
        <h1 className="flex items-center gap-2 text-xl font-semibold tracking-tight">
          <ScanLine className="size-5 text-primary" /> Сканирование чека
        </h1>
        <p className="text-sm text-muted-foreground">
          Отсканируйте QR-код с фискального чека — начисление создастся сразу, а состав
          чека подгрузится в фоне.
        </p>
      </header>

      {scanningDisabled ? (
        <div className="rounded-lg border border-dashed p-8 text-center">
          <QrCode className="mx-auto size-10 text-muted-foreground" />
          <p className="mt-3 text-sm font-medium">Загрузка чеков недоступна</p>
          <p className="mt-1 text-xs text-muted-foreground">
            На сервере не настроен токен провайдера чеков. Добавить начисление можно вручную.
          </p>
          <Button variant="outline" asChild className="mt-4">
            <Link to="/accruals">Добавить вручную</Link>
          </Button>
        </div>
      ) : result ? (
        // ── Post-scan: created accrual + live fetch status (T4.3.3) ──────────
        <section className="space-y-4">
          <ReceiptStatusIndicator accrualId={result.accrualId} />
          <div className="flex gap-2">
            <Button asChild className="flex-1">
              <Link to={`/accruals/${result.accrualId}`}>Открыть начисление</Link>
            </Button>
            <Button variant="outline" className="flex-1" onClick={reset}>
              Сканировать ещё
            </Button>
          </div>
        </section>
      ) : (
        // ── Scanning (T4.3.1 / T4.3.2) ──────────────────────────────────────
        <section className="space-y-3">
          <QrScanner onDecode={handleDecode} disabled={scanMutation.isPending} />

          {scanMutation.isPending && (
            <p className="flex items-center gap-2 text-sm text-muted-foreground">
              <Loader2 className="size-4 animate-spin" /> Отправляем чек…
            </p>
          )}
          {scanMutation.isError && (
            <p className="text-sm text-destructive">
              {scanErrorMessage(scanMutation.error as Error)}
            </p>
          )}
        </section>
      )}
    </main>
  )
}
