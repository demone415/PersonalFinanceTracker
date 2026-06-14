import { z } from 'zod'

/** Login form schema (T1.2.7). Mirrors GoTrue's password-grant requirements. */
export const loginSchema = z.object({
  email: z.email('Введите корректный email'),
  password: z.string().min(1, 'Введите пароль'),
})

export type LoginValues = z.infer<typeof loginSchema>
