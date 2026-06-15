import { Controller, useForm, useWatch } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { Button } from '@/shared/ui/button'
import { Input } from '@/shared/ui/input'
import { Label } from '@/shared/ui/label'
import { IconPicker } from './IconPicker'
import { ColorPicker } from './ColorPicker'
import { categorySchema, type CategoryFormValues } from '../model/category-schema'

export function CategoryForm({
  defaultValues,
  submitting = false,
  submitLabel = 'Сохранить',
  onSubmit,
  onCancel,
}: {
  defaultValues?: CategoryFormValues
  submitting?: boolean
  submitLabel?: string
  onSubmit: (values: CategoryFormValues) => void
  onCancel: () => void
}) {
  const {
    register,
    handleSubmit,
    control,
    formState: { errors },
  } = useForm<CategoryFormValues>({
    resolver: zodResolver(categorySchema),
    defaultValues: defaultValues ?? { name: '', icon: 'shopping-cart', color: '#22c55e' },
  })

  const color = useWatch({ control, name: 'color' })

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
      <div className="space-y-2">
        <Label htmlFor="name">Название</Label>
        <Input id="name" autoComplete="off" {...register('name')} />
        {errors.name && <p className="text-sm text-destructive">{errors.name.message}</p>}
      </div>

      <div className="space-y-2">
        <Label>Иконка</Label>
        <Controller
          control={control}
          name="icon"
          render={({ field }) => (
            <IconPicker value={field.value} color={color} onChange={field.onChange} />
          )}
        />
        {errors.icon && <p className="text-sm text-destructive">{errors.icon.message}</p>}
      </div>

      <div className="space-y-2">
        <Label>Цвет</Label>
        <Controller
          control={control}
          name="color"
          render={({ field }) => <ColorPicker value={field.value} onChange={field.onChange} />}
        />
        {errors.color && <p className="text-sm text-destructive">{errors.color.message}</p>}
      </div>

      <div className="flex justify-end gap-2 pt-2">
        <Button type="button" variant="outline" onClick={onCancel}>
          Отмена
        </Button>
        <Button type="submit" disabled={submitting}>
          {submitLabel}
        </Button>
      </div>
    </form>
  )
}
