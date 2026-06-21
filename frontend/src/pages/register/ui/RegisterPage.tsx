import { Link, Navigate } from 'react-router-dom'
import { RegisterForm } from '@/features/auth'
import { useSessionStore } from '@/entities/session'

export function RegisterPage() {
  const status = useSessionStore((s) => s.status)

  // Already signed in → no reason to show the registration form.
  if (status === 'authenticated') return <Navigate to="/" replace />

  return (
    <main className="flex min-h-svh items-center justify-center p-8">
      <div className="w-full max-w-sm space-y-6 rounded-lg border bg-card p-6 shadow-sm">
        <div className="space-y-1 text-center">
          <h1 className="text-2xl font-semibold tracking-tight">Finance Tracker</h1>
          <p className="text-sm text-muted-foreground">Создайте аккаунт</p>
        </div>
        <RegisterForm />
        <p className="text-center text-sm text-muted-foreground">
          Уже есть аккаунт?{' '}
          <Link to="/login" className="font-medium text-foreground underline underline-offset-4">
            Войти
          </Link>
        </p>
      </div>
    </main>
  )
}
