import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useNavigate } from 'react-router-dom'
import { supabase } from '@/shared/api/supabase'
import { Button } from '@/shared/ui/button'
import { Input } from '@/shared/ui/input'
import { Label } from '@/shared/ui/label'
import { loginSchema, type LoginValues } from '../model/login-schema'

/** Email/password login via the Supabase SDK (T1.2.7). */
export function LoginForm() {
  const navigate = useNavigate()
  const [authError, setAuthError] = useState<string | null>(null)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginValues>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: '', password: '' },
  })

  async function onSubmit(values: LoginValues) {
    setAuthError(null)
    const { error } = await supabase.auth.signInWithPassword(values)
    if (error) {
      // Don't leak whether the email exists — generic message.
      setAuthError('Неверный email или пароль')
      return
    }
    navigate('/', { replace: true })
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4" noValidate>
      <div className="space-y-2">
        <Label htmlFor="email">Email</Label>
        <Input id="email" type="email" autoComplete="email" {...register('email')} />
        {errors.email && <p className="text-sm text-destructive">{errors.email.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="password">Пароль</Label>
        <Input
          id="password"
          type="password"
          autoComplete="current-password"
          {...register('password')}
        />
        {errors.password && <p className="text-sm text-destructive">{errors.password.message}</p>}
      </div>

      {authError && <p className="text-sm text-destructive">{authError}</p>}

      <Button type="submit" className="w-full" disabled={isSubmitting}>
        {isSubmitting ? 'Вход…' : 'Войти'}
      </Button>
    </form>
  )
}
