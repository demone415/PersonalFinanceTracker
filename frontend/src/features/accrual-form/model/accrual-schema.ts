import { z } from 'zod'

export const accrualSchema = z.object({
  amount: z.number().positive('Сумма должна быть больше 0'),
  date: z.string().min(1, 'Укажите дату'),
  type: z.enum(['Income', 'ReturnIncome', 'Expense', 'ReturnExpense']),
  currency: z.string().length(3, 'Код валюты — 3 символа'),
  categoryId: z.string().optional().nullable(),
  description: z.string().max(500).optional(),
  includeInStats: z.boolean(),
  groupId: z.string().optional().nullable(),
  exchangeRate: z.number().positive().optional().nullable(),
  tags: z.array(z.string().max(64)).max(20),
})

export type AccrualFormValues = z.infer<typeof accrualSchema>
