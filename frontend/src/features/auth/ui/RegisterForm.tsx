import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { useNavigate } from 'react-router-dom'
import { supabase } from '@/shared/api/supabase'
import { Button } from '@/shared/ui/button'
import { Input } from '@/shared/ui/input'
import { Label } from '@/shared/ui/label'
import { registerSchema, type RegisterValues } from '../model/register-schema'

/**
 * Email/password registration via the Supabase SDK (GoTrue signup).
 * Email confirmation is disabled, so a successful signup returns a session and
 * the user is signed in immediately; AuthProvider picks up the auth event and
 * the route guards let them through.
 */
export function RegisterForm() {
  const navigate = useNavigate()
  const [authError, setAuthError] = useState<string | null>(null)
  const [needsConfirmation, setNeedsConfirmation] = useState(false)
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<RegisterValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: { email: '', password: '', confirmPassword: '' },
  })

  async function onSubmit(values: RegisterValues) {
    setAuthError(null)
    const { data, error } = await supabase.auth.signUp({
      email: values.email,
      password: values.password,
    })
    if (error) {
      const alreadyExists = /already|registered|exists/i.test(error.message)
      setAuthError(
        alreadyExists
          ? 'Пользователь с таким email уже зарегистрирован'
          : 'Не удалось зарегистрироваться. Попробуйте ещё раз',
      )
      return
    }

    // With email confirmation disabled GoTrue returns a live session; otherwise
    // the user must confirm via email before they can sign in.
    if (data.session) {
      navigate('/', { replace: true })
    } else {
      setNeedsConfirmation(true)
    }
  }

  if (needsConfirmation) {
    return (
      <div className="space-y-4 text-center">
        <p className="text-sm text-muted-foreground">
          Аккаунт создан. Подтвердите email по ссылке из письма, затем войдите.
        </p>
        <Button className="w-full" onClick={() => navigate('/login')}>
          Перейти ко входу
        </Button>
      </div>
    )
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
          autoComplete="new-password"
          {...register('password')}
        />
        {errors.password && <p className="text-sm text-destructive">{errors.password.message}</p>}
      </div>

      <div className="space-y-2">
        <Label htmlFor="confirmPassword">Повторите пароль</Label>
        <Input
          id="confirmPassword"
          type="password"
          autoComplete="new-password"
          {...register('confirmPassword')}
        />
        {errors.confirmPassword && (
          <p className="text-sm text-destructive">{errors.confirmPassword.message}</p>
        )}
      </div>

      {authError && <p className="text-sm text-destructive">{authError}</p>}

      <Button type="submit" className="w-full" disabled={isSubmitting}>
        {isSubmitting ? 'Регистрация…' : 'Зарегистрироваться'}
      </Button>
    </form>
  )
}
