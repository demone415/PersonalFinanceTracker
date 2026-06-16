import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useCategories } from '@/entities/category'
import { Button } from '@/shared/ui/button'
import { Input } from '@/shared/ui/input'
import { Label } from '@/shared/ui/label'
import { formatMonthLong } from '@/shared/lib/format'
import { budgetSchema, type BudgetFormValues } from '../model/budget-schema'

const SELECT_CLASS =
  'flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm disabled:cursor-not-allowed disabled:opacity-60'

interface Props {
  /** In edit mode only the limit/currency are editable; category and period are fixed. */
  mode: 'create' | 'edit'
  defaultValues?: Partial<BudgetFormValues>
  submitting: boolean
  submitLabel: string
  error?: string | null
  onSubmit: (values: BudgetFormValues) => void
  onCancel: () => void
}

export function BudgetForm({
  mode,
  defaultValues,
  submitting,
  submitLabel,
  error,
  onSubmit,
  onCancel,
}: Props) {
  const { data: categories } = useCategories()
  const now = new Date()
  const isEdit = mode === 'edit'

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<BudgetFormValues>({
    resolver: zodResolver(budgetSchema),
    defaultValues: {
      categoryId: '',
      year: now.getFullYear(),
      month: now.getMonth() + 1,
      currency: 'RUB',
      ...defaultValues,
    },
  })

  const yearOptions = [now.getFullYear() - 1, now.getFullYear(), now.getFullYear() + 1]

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
      {/* Category */}
      <div className="space-y-1">
        <Label>Категория *</Label>
        <select className={SELECT_CLASS} disabled={isEdit} {...register('categoryId')}>
          <option value="">— выберите —</option>
          {categories?.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </select>
        {errors.categoryId && (
          <p className="text-xs text-destructive">{errors.categoryId.message}</p>
        )}
      </div>

      <div className="grid grid-cols-2 gap-4">
        {/* Month */}
        <div className="space-y-1">
          <Label>Месяц *</Label>
          <select className={SELECT_CLASS} disabled={isEdit} {...register('month', { valueAsNumber: true })}>
            {Array.from({ length: 12 }, (_, i) => i + 1).map((m) => (
              <option key={m} value={m}>
                {formatMonthLong(now.getFullYear(), m).split(' ')[0]}
              </option>
            ))}
          </select>
        </div>

        {/* Year */}
        <div className="space-y-1">
          <Label>Год *</Label>
          <select className={SELECT_CLASS} disabled={isEdit} {...register('year', { valueAsNumber: true })}>
            {yearOptions.map((y) => (
              <option key={y} value={y}>
                {y}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        {/* Limit */}
        <div className="space-y-1">
          <Label>Лимит *</Label>
          <Input
            type="number"
            step="0.01"
            min="0.01"
            placeholder="0.00"
            {...register('limitAmount', { valueAsNumber: true })}
          />
          {errors.limitAmount && (
            <p className="text-xs text-destructive">{errors.limitAmount.message}</p>
          )}
        </div>

        {/* Currency */}
        <div className="space-y-1">
          <Label>Валюта *</Label>
          <Input maxLength={3} placeholder="RUB" {...register('currency')} />
          {errors.currency && (
            <p className="text-xs text-destructive">{errors.currency.message}</p>
          )}
        </div>
      </div>

      {error && <p className="text-sm text-destructive">{error}</p>}

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
