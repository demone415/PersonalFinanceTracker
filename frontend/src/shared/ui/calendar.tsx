import { DayPicker, type DropdownProps } from 'react-day-picker'
import { ru } from 'react-day-picker/locale'
import 'react-day-picker/style.css'

import { cn } from '@/shared/lib/utils'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from './select'

export type CalendarProps = React.ComponentProps<typeof DayPicker>

/**
 * Month/year selector rendered with the themed Radix {@link Select} instead of a
 * native `<select>`, whose option list ignores the dark theme in some browsers.
 * Bridges Radix's string `onValueChange` back to the change-event handler
 * react-day-picker expects.
 */
function CalendarDropdown({ options, value, onChange, 'aria-label': ariaLabel }: DropdownProps) {
  return (
    <Select
      value={value != null ? String(value) : undefined}
      onValueChange={(next) =>
        onChange?.({ target: { value: next } } as React.ChangeEvent<HTMLSelectElement>)
      }
    >
      <SelectTrigger className="h-8 w-auto gap-1 border-0 px-2 font-medium shadow-none focus:ring-0" aria-label={ariaLabel}>
        <SelectValue />
      </SelectTrigger>
      <SelectContent className="max-h-72">
        {options?.map((option) => (
          <SelectItem key={option.value} value={String(option.value)} disabled={option.disabled}>
            {option.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  )
}

/**
 * Themed wrapper around react-day-picker. Defaults to a Russian locale and a
 * month/year dropdown caption so any year is one click away (the painful part of
 * the native picker). Colours bind to the app's CSS tokens via the `.rdp-themed`
 * rules in index.css, so it follows light/dark automatically.
 */
export function Calendar({
  className,
  captionLayout = 'dropdown',
  startMonth,
  endMonth,
  components,
  ...props
}: CalendarProps) {
  // Bound the year dropdown to a useful range (≈30 years back to next year) —
  // without explicit bounds the dropdown only offers the current year.
  const currentYear = new Date().getFullYear()
  return (
    <DayPicker
      locale={ru}
      captionLayout={captionLayout}
      startMonth={startMonth ?? new Date(currentYear - 30, 0)}
      endMonth={endMonth ?? new Date(currentYear + 1, 11)}
      animate
      className={cn('rdp-themed', className)}
      components={{ Dropdown: CalendarDropdown, ...components }}
      {...props}
    />
  )
}
