import { CalendarDays } from 'lucide-react'
import { useState } from 'react'

import { cn } from '@/shared/lib/utils'
import { formatDateTimeRu, toLocalDateTimeString } from '@/shared/lib/format'
import { Button } from './button'
import { Calendar } from './calendar'
import { Input } from './input'
import { Popover, PopoverContent, PopoverTrigger } from './popover'

interface DateTimePickerProps {
  /** Local date-time string, e.g. `2026-06-28T03:00` or `…:00:00` ('' when unset). */
  value: string
  /** Emits `YYYY-MM-DDTHH:mm:ss` ('' is never emitted — a day must be picked). */
  onChange: (value: string) => void
  placeholder?: string
  className?: string
}

function parse(value: string): Date | undefined {
  if (!value) return undefined
  const d = new Date(value)
  return Number.isNaN(d.getTime()) ? undefined : d
}

const pad = (n: number) => String(n).padStart(2, '0')

/**
 * Themed replacement for `<input type="datetime-local">`: a popover with the
 * month/year-dropdown calendar plus a time field. Keeps a local
 * `YYYY-MM-DDTHH:mm:ss` string contract and displays `dd.MM.yyyy HH:mm:ss`.
 */
export function DateTimePicker({
  value,
  onChange,
  placeholder = 'дд.мм.гггг чч:мм:сс',
  className,
}: DateTimePickerProps) {
  const [open, setOpen] = useState(false)
  const current = parse(value)
  const timeValue = current
    ? `${pad(current.getHours())}:${pad(current.getMinutes())}:${pad(current.getSeconds())}`
    : '00:00:00'

  function emitWithDay(day: Date) {
    const base = current ?? new Date()
    day.setHours(base.getHours(), base.getMinutes(), base.getSeconds(), 0)
    onChange(toLocalDateTimeString(day))
  }

  function emitWithTime(time: string) {
    const [h, m, s] = time.split(':').map(Number)
    const base = current ? new Date(current) : new Date()
    base.setHours(h || 0, m || 0, s || 0, 0)
    onChange(toLocalDateTimeString(base))
  }

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          className={cn('w-full justify-start gap-2 font-normal', !current && 'text-muted-foreground', className)}
        >
          <CalendarDays className="size-4 shrink-0 opacity-70" />
          <span className="flex-1 text-left">{current ? formatDateTimeRu(current) : placeholder}</span>
        </Button>
      </PopoverTrigger>
      <PopoverContent align="start" className="space-y-3">
        <Calendar
          mode="single"
          selected={current}
          defaultMonth={current}
          onSelect={(day) => day && emitWithDay(day)}
        />
        <div className="flex items-center gap-2 border-t pt-3">
          <span className="text-sm text-muted-foreground">Время</span>
          <Input
            type="time"
            step="1"
            value={timeValue}
            onChange={(e) => emitWithTime(e.target.value)}
            className="w-auto"
          />
        </div>
      </PopoverContent>
    </Popover>
  )
}
