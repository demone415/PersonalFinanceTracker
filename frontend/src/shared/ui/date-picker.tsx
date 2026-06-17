import { CalendarDays, X } from 'lucide-react'
import { useState } from 'react'

import { cn } from '@/shared/lib/utils'
import { Button } from './button'
import { Calendar } from './calendar'
import { Popover, PopoverContent, PopoverTrigger } from './popover'

interface DatePickerProps {
  /** Selected date as an ISO `YYYY-MM-DD` string, or '' when unset. */
  value: string
  /** Emits the new `YYYY-MM-DD` string ('' when cleared). */
  onChange: (value: string) => void
  placeholder?: string
  className?: string
}

/** Local `YYYY-MM-DD` ⇆ Date helpers — no UTC shift (the value is a calendar day). */
function parse(value: string): Date | undefined {
  if (!value) return undefined
  const [y, m, d] = value.split('-').map(Number)
  if (!y || !m || !d) return undefined
  return new Date(y, m - 1, d)
}

function format(date: Date): string {
  const y = date.getFullYear()
  const m = String(date.getMonth() + 1).padStart(2, '0')
  const d = String(date.getDate()).padStart(2, '0')
  return `${y}-${m}-${d}`
}

/**
 * Date picker replacing the native `<input type="date">`: a popover calendar with
 * month/year dropdowns and full theme support. Keeps the same `YYYY-MM-DD` string
 * contract so callers (filter URL, etc.) are unchanged.
 */
export function DatePicker({ value, onChange, placeholder = 'Выберите дату', className }: DatePickerProps) {
  const [open, setOpen] = useState(false)
  const selected = parse(value)

  return (
    <Popover open={open} onOpenChange={setOpen}>
      <PopoverTrigger asChild>
        <Button
          type="button"
          variant="outline"
          className={cn(
            'w-full justify-start gap-2 font-normal',
            !selected && 'text-muted-foreground',
            className,
          )}
        >
          <CalendarDays className="size-4 shrink-0 opacity-70" />
          <span className="flex-1 text-left">
            {selected ? selected.toLocaleDateString('ru-RU') : placeholder}
          </span>
          {selected && (
            <span
              role="button"
              tabIndex={-1}
              aria-label="Очистить дату"
              className="rounded-sm p-0.5 opacity-60 hover:opacity-100"
              onClick={(e) => {
                e.stopPropagation()
                onChange('')
              }}
            >
              <X className="size-3.5" />
            </span>
          )}
        </Button>
      </PopoverTrigger>
      <PopoverContent align="start">
        <Calendar
          mode="single"
          selected={selected}
          defaultMonth={selected}
          onSelect={(date) => {
            onChange(date ? format(date) : '')
            setOpen(false)
          }}
        />
      </PopoverContent>
    </Popover>
  )
}
