import { AlertCircle, CheckCircle2, Loader2, ListChecks, Trash2 } from 'lucide-react'
import {
  useBackgroundTasks,
  isTerminalStatus,
  tasksForUser,
  type JobStatusValue,
  type TrackedTask,
} from '@/entities/background-task'
import { useSessionStore } from '@/entities/session'
import { Button } from '@/shared/ui/button'
import { Popover, PopoverContent, PopoverTrigger } from '@/shared/ui/popover'
import { cn } from '@/shared/lib/utils'

const STATUS_LABEL: Record<JobStatusValue, string> = {
  Pending: 'В очереди',
  Running: 'Выполняется',
  Done: 'Готово',
  Failed: 'Ошибка',
}

function TaskRow({ task }: { task: TrackedTask }) {
  const running = !isTerminalStatus(task.status)
  const Icon = running ? Loader2 : task.status === 'Failed' ? AlertCircle : CheckCircle2
  const accent =
    task.status === 'Failed'
      ? 'text-destructive'
      : task.status === 'Done'
        ? 'text-green-500'
        : 'text-primary'

  return (
    <li className="flex items-start gap-2 py-2">
      <Icon className={cn('mt-0.5 size-4 shrink-0', accent, running && 'animate-spin')} />
      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-medium">{task.label}</p>
        <p className="text-xs text-muted-foreground">{STATUS_LABEL[task.status]}</p>
        {task.kind === 'import' && task.summary && (
          <p className="mt-0.5 text-xs text-muted-foreground">
            Добавлено {task.summary.receiptsImported}
            {task.summary.receiptsSkippedDuplicate > 0 &&
              `, дубликатов ${task.summary.receiptsSkippedDuplicate}`}
            {task.summary.rowsFailed > 0 && `, с ошибками ${task.summary.rowsFailed}`}
          </p>
        )}
        {task.status === 'Failed' && task.error && (
          <p className="mt-0.5 text-xs text-destructive">{task.error}</p>
        )}
      </div>
    </li>
  )
}

/**
 * Compact header affordance for global background tasks (Story 6.3): a spinner +
 * active count while jobs run, opening a popover that lists every tracked
 * import/export with its status and (for imports) result counts. Renders nothing
 * when there are no tasks. Independent of the current route.
 */
export function BackgroundTasksIndicator() {
  const allTasks = useBackgroundTasks((s) => s.tasks)
  const clearFinished = useBackgroundTasks((s) => s.clearFinished)
  const userId = useSessionStore((s) => s.userId)

  // Only show the signed-in user's own jobs (the store is shared across users).
  const tasks = tasksForUser(allTasks, userId)
  if (tasks.length === 0) return null

  const active = tasks.filter((t) => !isTerminalStatus(t.status))
  const hasFinished = tasks.some((t) => isTerminalStatus(t.status))
  // Newest first.
  const ordered = [...tasks].sort((a, b) => b.startedAt - a.startedAt)

  return (
    <Popover>
      <PopoverTrigger asChild>
        <Button variant="ghost" size="icon" className="relative" aria-label="Фоновые задачи">
          {active.length > 0 ? <Loader2 className="animate-spin" /> : <ListChecks />}
          {active.length > 0 && (
            <span className="absolute -right-0.5 -top-0.5 flex size-4 items-center justify-center rounded-full bg-primary text-[10px] font-medium text-primary-foreground">
              {active.length}
            </span>
          )}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="end" className="w-80 p-3">
        <div className="mb-1 flex items-center justify-between">
          <span className="text-sm font-medium">Фоновые задачи</span>
          {hasFinished && (
            <button
              type="button"
              onClick={clearFinished}
              className="flex items-center gap-1 text-xs text-muted-foreground transition-colors hover:text-foreground"
            >
              <Trash2 className="size-3.5" /> Очистить
            </button>
          )}
        </div>
        <ul className="divide-y">
          {ordered.map((task) => (
            <TaskRow key={task.id} task={task} />
          ))}
        </ul>
      </PopoverContent>
    </Popover>
  )
}
