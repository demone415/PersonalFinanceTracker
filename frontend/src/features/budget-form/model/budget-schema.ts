import { z } from 'zod'

export const budgetSchema = z.object({
  categoryId: z.string().min(1, 'Выберите категорию'),
  year: z.number().int().min(2000).max(2100),
  month: z.number().int().min(1).max(12),
  limitAmount: z.number({ message: 'Введите сумму' }).positive('Лимит должен быть больше нуля'),
  currency: z.string().length(3, 'Код из 3 букв, напр. RUB'),
})

export type BudgetFormValues = z.infer<typeof budgetSchema>
