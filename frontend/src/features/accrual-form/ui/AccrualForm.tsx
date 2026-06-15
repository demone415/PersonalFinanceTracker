import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useCategories } from '@/entities/category'
import { Button } from '@/shared/ui/button'
import { Input } from '@/shared/ui/input'
import { Label } from '@/shared/ui/label'
import { accrualSchema, type AccrualFormValues } from '../model/accrual-schema'

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
  const [tagInput, setTagInput] = useState('')

  const {
    register,
    handleSubmit,
    watch,
    setValue,
    formState: { errors },
  } = useForm<AccrualFormValues>({
    resolver: zodResolver(accrualSchema),
    defaultValues: {
      currency: 'RUB',
      includeInStats: true,
      type: 'Expense',
      tags: [],
      ...defaultValues,
    },
  })

  const tags = watch('tags') ?? []

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
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
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
          <Input type="datetime-local" {...register('date')} />
          {errors.date && <p className="text-xs text-destructive">{errors.date.message}</p>}
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        {/* Type */}
        <div className="space-y-1">
          <Label>Тип *</Label>
          <select
            className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm"
            {...register('type')}
          >
            {ACCRUAL_TYPES.map((t) => (
              <option key={t.value} value={t.value}>
                {t.label}
              </option>
            ))}
          </select>
          {errors.type && <p className="text-xs text-destructive">{errors.type.message}</p>}
        </div>

        {/* Currency */}
        <div className="space-y-1">
          <Label>Валюта *</Label>
          <Input placeholder="RUB" maxLength={3} {...register('currency')} />
          {errors.currency && <p className="text-xs text-destructive">{errors.currency.message}</p>}
        </div>
      </div>

      {/* Category */}
      <div className="space-y-1">
        <Label>Категория</Label>
        <select
          className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm"
          {...register('categoryId')}
        >
          <option value="">— без категории —</option>
          {categories?.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
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

// useState needed in this file
import { useState } from 'react'
