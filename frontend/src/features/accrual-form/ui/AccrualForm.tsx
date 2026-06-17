import { useEffect, useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useCategories } from '@/entities/category'
import { useBaseCurrency } from '@/entities/profile'
import { Button } from '@/shared/ui/button'
import { DateTimePicker } from '@/shared/ui/datetime-picker'
import { Input } from '@/shared/ui/input'
import { Label } from '@/shared/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/ui/select'
import { formatMoney } from '@/shared/lib/format'
import { CURRENCIES } from '@/shared/lib/currencies'
import { accrualSchema, type AccrualFormValues } from '../model/accrual-schema'

/** Sentinel for the "no category" option — Radix Select forbids an empty value. */
const NO_CATEGORY = '__none__'

const ACCRUAL_TYPES = [
  { value: 'Expense', label: 'Расход' },
  { value: 'Income', label: 'Доход' },
  { value: 'ReturnExpense', label: 'Возврат расхода' },
  { value: 'ReturnIncome', label: 'Возврат дохода' },
] as const

interface Props {
  defaultValues?: Partial<AccrualFormValues>
  submitting: boolean
  submitLabel: string
  onSubmit: (values: AccrualFormValues) => void
  onCancel: () => void
}

export function AccrualForm({ defaultValues, submitting, submitLabel, onSubmit, onCancel }: Props) {
  const { data: categories } = useCategories()
  const baseCurrency = useBaseCurrency()
  const [tagInput, setTagInput] = useState('')

  const {
    register,
    control,
    handleSubmit,
    watch,
    setValue,
    setError,
    clearErrors,
    formState: { errors, dirtyFields },
  } = useForm<AccrualFormValues>({
    resolver: zodResolver(accrualSchema),
    defaultValues: {
      currency: baseCurrency,
      includeInStats: true,
      type: 'Expense',
      tags: [],
      ...defaultValues,
    },
  })

  const tags = watch('tags') ?? []

  // In create mode the transaction currency should follow the user's base
  // currency, which may still be loading on first render; keep it in sync until
  // the user edits the field. In edit mode defaultValues carries the saved
  // currency, so leave it untouched.
  const isEdit = defaultValues?.currency != null
  useEffect(() => {
    if (!isEdit && !dirtyFields.currency) {
      setValue('currency', baseCurrency)
    }
  }, [isEdit, baseCurrency, dirtyFields.currency, setValue])

  // Multi-currency (Epic 8): a foreign transaction needs the rate to the base
  // currency captured at entry time so aggregates can convert it (T8.1.3).
  const currency = (watch('currency') ?? '').trim().toUpperCase()
  const isForeign = currency.length === 3 && currency !== baseCurrency.toUpperCase()
  const amount = watch('amount')
  const exchangeRate = watch('exchangeRate')

  // Drop a stale rate when the currency reverts to the base one, so a base-
  // currency accrual is never sent with a leftover multiplier.
  useEffect(() => {
    if (!isForeign && exchangeRate != null) {
      setValue('exchangeRate', undefined)
      clearErrors('exchangeRate')
    }
  }, [isForeign, exchangeRate, setValue, clearErrors])

  function submit(values: AccrualFormValues) {
    if (isForeign && !values.exchangeRate) {
      setError('exchangeRate', { type: 'manual', message: `Укажите курс к ${baseCurrency}` })
      return
    }
    onSubmit(isForeign ? values : { ...values, exchangeRate: null })
  }

  function addTag() {
    const t = tagInput.trim()
    if (t && !tags.includes(t) && tags.length < 20) {
      setValue('tags', [...tags, t])
      setTagInput('')
    }
  }

  function removeTag(tag: string) {
    setValue('tags', tags.filter((t) => t !== tag))
  }

  return (
    <form onSubmit={handleSubmit(submit)} className="space-y-4">
      <div className="grid grid-cols-2 gap-4">
        {/* Amount */}
        <div className="space-y-1">
          <Label>Сумма *</Label>
          <Input
            type="number"
            step="0.01"
            min="0.01"
            placeholder="0.00"
            {...register('amount', { valueAsNumber: true })}
          />
          {errors.amount && <p className="text-xs text-destructive">{errors.amount.message}</p>}
        </div>

        {/* Date */}
        <div className="space-y-1">
          <Label>Дата *</Label>
          <Controller
            control={control}
            name="date"
            render={({ field }) => (
              <DateTimePicker value={field.value ?? ''} onChange={field.onChange} />
            )}
          />
          {errors.date && <p className="text-xs text-destructive">{errors.date.message}</p>}
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        {/* Type */}
        <div className="space-y-1">
          <Label>Тип *</Label>
          <Controller
            control={control}
            name="type"
            render={({ field }) => (
              <Select value={field.value} onValueChange={field.onChange}>
                <SelectTrigger>
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  {ACCRUAL_TYPES.map((t) => (
                    <SelectItem key={t.value} value={t.value}>
                      {t.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            )}
          />
          {errors.type && <p className="text-xs text-destructive">{errors.type.message}</p>}
        </div>

        {/* Currency */}
        <div className="space-y-1">
          <Label>Валюта *</Label>
          <Controller
            control={control}
            name="currency"
            render={({ field }) => {
              const code = (field.value ?? '').toUpperCase()
              const hasPreset = CURRENCIES.some((c) => c.code === code)
              return (
                <Select value={code || undefined} onValueChange={field.onChange}>
                  <SelectTrigger>
                    <SelectValue placeholder="Валюта" />
                  </SelectTrigger>
                  <SelectContent>
                    {!hasPreset && code && <SelectItem value={code}>{code}</SelectItem>}
                    {CURRENCIES.map((c) => (
                      <SelectItem key={c.code} value={c.code}>
                        {c.label}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )
            }}
          />
          {errors.currency && <p className="text-xs text-destructive">{errors.currency.message}</p>}
        </div>
      </div>

      {/* Exchange rate — only for a currency other than the base one (Epic 8) */}
      {isForeign && (
        <div className="space-y-1 rounded-md border border-dashed p-3">
          <Label>Курс к {baseCurrency} *</Label>
          <Input
            type="number"
            step="0.000001"
            min="0"
            placeholder={`1 ${currency} = ? ${baseCurrency}`}
            {...register('exchangeRate', {
              setValueAs: (v) => {
                if (v === '' || v === null || v === undefined) return undefined
                const n = Number(v)
                return Number.isNaN(n) ? undefined : n
              },
            })}
          />
          {amount && exchangeRate ? (
            <p className="text-xs text-muted-foreground">
              ≈ {formatMoney(amount * exchangeRate, baseCurrency)}
            </p>
          ) : (
            <p className="text-xs text-muted-foreground">
              Курс на момент операции для пересчёта в основную валюту
            </p>
          )}
          {errors.exchangeRate && (
            <p className="text-xs text-destructive">{errors.exchangeRate.message}</p>
          )}
        </div>
      )}

      {/* Category */}
      <div className="space-y-1">
        <Label>Категория</Label>
        <Controller
          control={control}
          name="categoryId"
          render={({ field }) => (
            <Select
              value={field.value || NO_CATEGORY}
              onValueChange={(v) => field.onChange(v === NO_CATEGORY ? '' : v)}
            >
              <SelectTrigger>
                <SelectValue placeholder="— без категории —" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={NO_CATEGORY}>— без категории —</SelectItem>
                {categories?.map((c) => (
                  <SelectItem key={c.id} value={c.id}>
                    {c.name}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
        />
      </div>

      {/* Description */}
      <div className="space-y-1">
        <Label>Описание</Label>
        <Input placeholder="Необязательно" {...register('description')} />
        {errors.description && (
          <p className="text-xs text-destructive">{errors.description.message}</p>
        )}
      </div>

      {/* Tags */}
      <div className="space-y-1">
        <Label>Теги</Label>
        <div className="flex gap-2">
          <Input
            placeholder="Добавить тег"
            value={tagInput}
            onChange={(e) => setTagInput(e.target.value)}
            onKeyDown={(e) => {
              if (e.key === 'Enter') {
                e.preventDefault()
                addTag()
              }
            }}
          />
          <Button type="button" variant="outline" size="sm" onClick={addTag}>
            +
          </Button>
        </div>
        {tags.length > 0 && (
          <div className="flex flex-wrap gap-1 pt-1">
            {tags.map((tag) => (
              <span
                key={tag}
                className="flex items-center gap-1 rounded-full bg-muted px-2 py-0.5 text-xs"
              >
                {tag}
                <button
                  type="button"
                  onClick={() => removeTag(tag)}
                  className="text-muted-foreground hover:text-foreground"
                >
                  ×
                </button>
              </span>
            ))}
          </div>
        )}
      </div>

      {/* Include in stats */}
      <div className="flex items-center gap-2">
        <input type="checkbox" id="includeInStats" {...register('includeInStats')} />
        <Label htmlFor="includeInStats">Включать в статистику</Label>
      </div>

      <div className="flex justify-end gap-2 pt-2">
        <Button type="button" variant="outline" onClick={onCancel}>
          Отмена
        </Button>
        <Button type="submit" disabled={submitting}>
          {submitting ? 'Сохранение…' : submitLabel}
        </Button>
      </div>
    </form>
  )
}
