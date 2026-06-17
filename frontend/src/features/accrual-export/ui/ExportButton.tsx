import { Download, Loader2 } from 'lucide-react'
import type { AccrualFilter } from '@/entities/accrual'
import { Button } from '@/shared/ui/button'
import { useAccrualExport } from '../model/use-accrual-export'

/**
 * «Экспорт CSV» button (T6.2.3): starts an async export of the accruals matching
 * the current `filter` and downloads the file when it is ready. The active
 * filters carry over so the export matches what the user is looking at.
 */
export function ExportButton({ filter }: { filter: AccrualFilter }) {
  const { start, isExporting } = useAccrualExport()

  return (
    <Button
      variant="outline"
      disabled={isExporting}
      onClick={() => start(filter)}
      title="Экспортировать текущий список в CSV"
    >
      {isExporting ? <Loader2 className="animate-spin" /> : <Download />}
      {isExporting ? 'Экспорт…' : 'Экспорт CSV'}
    </Button>
  )
}
