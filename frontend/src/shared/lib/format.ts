/** Shared, locale-aware formatting helpers (ru-RU). */

const MONTHS_SHORT = [
  'янв', 'фев', 'мар', 'апр', 'май', 'июн',
  'июл', 'авг', 'сен', 'окт', 'ноя', 'дек',
]

const MONTHS_LONG = [
  'январь', 'февраль', 'март', 'апрель', 'май', 'июнь',
  'июль', 'август', 'сентябрь', 'октябрь', 'ноябрь', 'декабрь',
]

/**
 * Currency-aware whole-unit amount in the user's base currency (Epic 8), e.g.
 * `1 234 ₽` or `1 234 $`. Falls back to the ISO code for currencies `Intl` has
 * no symbol for. Used by aggregate views once amounts are converted to base.
 */
export function formatMoney(value: number, currency = 'RUB'): string {
  try {
    return new Intl.NumberFormat('ru-RU', {
      style: 'currency',
      currency,
      maximumFractionDigits: 0,
      currencyDisplay: 'narrowSymbol',
    }).format(value)
  } catch {
    // Invalid/unknown ISO code — degrade gracefully to "<amount> <code>".
    return `${value.toLocaleString('ru-RU', { maximumFractionDigits: 0 })} ${currency}`
  }
}

/**
 * Currency-aware compact amount for chart axes/labels in the base currency
 * (Epic 8), e.g. `12 тыс. ₽` or `12K $`. Falls back to the ISO code for unknown
 * currencies.
 */
export function formatMoneyCompact(value: number, currency = 'RUB'): string {
  try {
    return new Intl.NumberFormat('ru-RU', {
      style: 'currency',
      currency,
      notation: 'compact',
      maximumFractionDigits: 1,
      currencyDisplay: 'narrowSymbol',
    }).format(value)
  } catch {
    return `${value.toLocaleString('ru-RU', { notation: 'compact', maximumFractionDigits: 1 })} ${currency}`
  }
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
