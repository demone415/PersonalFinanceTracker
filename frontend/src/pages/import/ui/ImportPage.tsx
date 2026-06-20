import { useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import {
  AlertCircle,
  CheckCircle2,
  FileSpreadsheet,
  Loader2,
  Upload,
} from 'lucide-react'
import { useAccrualImport } from '@/features/accrual-import'
import { useBackgroundTasks, isTerminalStatus } from '@/entities/background-task'
import { Button } from '@/shared/ui/button'

const ACCEPT = '.xlsx'

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} Б`
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(0)} КБ`
  return `${(bytes / (1024 * 1024)).toFixed(1)} МБ`
}

export function ImportPage() {
  const inputRef = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [jobId, setJobId] = useState<string | null>(null)
  const { start, isStarting } = useAccrualImport()

  // The job we just launched — read live from the global registry so progress
  // and the result summary keep updating even though polling is app-wide.
  const task = useBackgroundTasks((s) => s.tasks.find((t) => t.id === jobId))

  function pickFile(selected: File | undefined) {
    if (selected) setFile(selected)
  }

  function submit() {
    if (!file) return
    start(file, (id) => setJobId(id))
  }

  const summary = task?.kind === 'import' ? task.summary : undefined
  const running = task != null && !isTerminalStatus(task.status)

  return (
    <main className="mx-auto max-w-2xl space-y-6 p-4 md:p-8">
      <header className="space-y-1">
        <h1 className="flex items-center gap-2 text-xl font-semibold tracking-tight">
          <FileSpreadsheet className="size-5 text-primary" /> Импорт чеков из ФНС
        </h1>
        <Link to="/accruals" className="text-sm text-muted-foreground hover:underline">
          ← К начислениям
        </Link>
      </header>

      <section className="space-y-3 rounded-lg border bg-card p-4 text-sm text-muted-foreground">
        <p>
          Загрузите выгрузку чеков из приложения «Налоги ФЛ» в формате Excel
          (<code className="text-foreground">.xlsx</code>). Каждый чек станет расходным
          начислением со связанным чеком и позициями.
        </p>
        <ul className="list-inside list-disc space-y-1">
          <li>Импорт идёт в фоне — можно уйти со страницы, результат придёт уведомлением.</li>
          <li>Повторные чеки (по номеру, ИНН и дате) пропускаются автоматически.</li>
          <li>Суммы импортируются в рублях; максимальный размер файла — 20&nbsp;МБ.</li>
        </ul>
      </section>

      {/* ── File picker ─────────────────────────────────────────────────── */}
      <section className="space-y-3">
        <button
          type="button"
          onClick={() => inputRef.current?.click()}
          className="flex w-full flex-col items-center gap-2 rounded-lg border border-dashed p-8 text-center transition-colors hover:border-primary/60 hover:bg-accent/30"
        >
          <Upload className="size-8 text-muted-foreground" />
          {file ? (
            <span className="text-sm">
              <span className="font-medium text-foreground">{file.name}</span>{' '}
              <span className="text-muted-foreground">({formatSize(file.size)})</span>
            </span>
          ) : (
            <span className="text-sm text-muted-foreground">
              Нажмите, чтобы выбрать файл <span className="text-foreground">.xlsx</span>
            </span>
          )}
        </button>
        <input
          ref={inputRef}
          type="file"
          accept={ACCEPT}
          className="hidden"
          onChange={(e) => pickFile(e.target.files?.[0])}
        />

        <Button disabled={!file || isStarting} onClick={submit} className="w-full">
          {isStarting ? <Loader2 className="animate-spin" /> : <Upload />}
          Импортировать
        </Button>
      </section>

      {/* ── Live status / result ────────────────────────────────────────── */}
      {task && (
        <section className="rounded-lg border bg-card p-4">
          {running && (
            <p className="flex items-center gap-2 text-sm">
              <Loader2 className="size-4 animate-spin text-primary" />
              Обработка файла…
            </p>
          )}

          {task.status === 'Failed' && (
            <div className="flex items-start gap-2 text-sm">
              <AlertCircle className="mt-0.5 size-4 shrink-0 text-destructive" />
              <div>
                <p className="font-medium">Импорт не выполнен</p>
                <p className="text-muted-foreground">
                  {task.error ?? 'Не удалось обработать файл. Проверьте формат и попробуйте снова.'}
                </p>
              </div>
            </div>
          )}

          {task.status === 'Done' && summary && (
            <div className="space-y-3">
              <p className="flex items-center gap-2 text-sm font-medium">
                <CheckCircle2 className="size-4 text-green-500" /> Импорт завершён
              </p>
              <dl className="grid grid-cols-2 gap-2 text-sm sm:grid-cols-4">
                <Stat label="Всего чеков" value={summary.receiptsTotal} />
                <Stat label="Добавлено" value={summary.receiptsImported} />
                <Stat label="Дубликаты" value={summary.receiptsSkippedDuplicate} />
                <Stat label="С ошибками" value={summary.rowsFailed} />
              </dl>
              {summary.warnings.length > 0 && (
                <details className="text-sm">
                  <summary className="cursor-pointer text-muted-foreground hover:text-foreground">
                    Предупреждения ({summary.warnings.length})
                  </summary>
                  <ul className="mt-2 max-h-48 space-y-1 overflow-auto rounded-md bg-muted/40 p-2 text-xs text-muted-foreground">
                    {summary.warnings.map((w, i) => (
                      <li key={i}>{w}</li>
                    ))}
                  </ul>
                </details>
              )}
              <Button variant="outline" asChild>
                <Link to="/accruals">Перейти к начислениям</Link>
              </Button>
            </div>
          )}
        </section>
      )}
    </main>
  )
}

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div className="rounded-md border bg-background p-2">
      <dt className="text-xs text-muted-foreground">{label}</dt>
      <dd className="text-lg font-semibold tabular-nums">{value}</dd>
    </div>
  )
}
