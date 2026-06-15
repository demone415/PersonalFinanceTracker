/** Shared, locale-aware formatting helpers (ru-RU). */

const MONTHS_SHORT = [
  'янв', 'фев', 'мар', 'апр', 'май', 'июн',
  'июл', 'авг', 'сен', 'окт', 'ноя', 'дек',
]

const MONTHS_LONG = [
  'январь', 'февраль', 'март', 'апрель', 'май', 'июнь',
  'июль', 'август', 'сентябрь', 'октябрь', 'ноябрь', 'декабрь',
]

/** Whole-ruble amount, e.g. `1 234 ₽`. */
export function formatRub(value: number): string {
  return `${value.toLocaleString('ru-RU', { maximumFractionDigits: 0 })} ₽`
}

/** Compact ruble amount for chart axes/labels, e.g. `12,3 тыс ₽`. */
export function formatRubCompact(value: number): string {
  const abs = Math.abs(value)
  if (abs >= 1_000_000)
    return `${(value / 1_000_000).toLocaleString('ru-RU', { maximumFractionDigits: 1 })} млн ₽`
  if (abs >= 10_000)
    return `${Math.round(value / 1_000).toLocaleString('ru-RU')} тыс ₽`
  return formatRub(value)
}

/** Short month label, e.g. `июн` (or `июн 25` when crossing a year boundary). */
export function formatMonthShort(year: number, month: number, withYear = false): string {
  const name = MONTHS_SHORT[month - 1] ?? ''
  return withYear ? `${name} ${String(year).slice(2)}` : name
}

/** Capitalised full month name, e.g. `Июнь 2026`. */
export function formatMonthLong(year: number, month: number): string {
  const name = MONTHS_LONG[month - 1] ?? ''
  return `${name.charAt(0).toUpperCase()}${name.slice(1)} ${year}`
}
