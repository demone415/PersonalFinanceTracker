import { z } from 'zod'

export const categorySchema = z.object({
  name: z.string().min(1, 'Введите название').max(100, 'Максимум 100 символов'),
  icon: z.string().min(1, 'Выберите иконку'),
  color: z.string().regex(/^#([0-9A-Fa-f]{6})$/, 'HEX-цвет, например #22c55e'),
})

export type CategoryFormValues = z.infer<typeof categorySchema>
