import { z } from 'zod'

/**
 * Registration form schema. Mirrors GoTrue's signup requirements; the password
 * length floor matches GoTrue's default minimum (6) — keep in lockstep if the
 * server's `GOTRUE_PASSWORD_MIN_LENGTH` is changed.
 */
export const registerSchema = z
  .object({
    email: z.email('Введите корректный email'),
    password: z.string().min(6, 'Минимум 6 символов'),
    confirmPassword: z.string().min(1, 'Повторите пароль'),
  })
  .refine((v) => v.password === v.confirmPassword, {
    message: 'Пароли не совпадают',
    path: ['confirmPassword'],
  })

export type RegisterValues = z.infer<typeof registerSchema>
