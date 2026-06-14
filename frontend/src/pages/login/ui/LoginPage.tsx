import { Navigate } from 'react-router-dom'
import { LoginForm } from '@/features/auth'
import { useSessionStore } from '@/entities/session'

export function LoginPage() {
  const status = useSessionStore((s) => s.status)

  // Already signed in → no reason to show the login form.
  if (status === 'authenticated') return <Navigate to="/" replace />

  return (
    <main className="flex min-h-svh items-center justify-center p-8">
      <div className="w-full max-w-sm space-y-6 rounded-lg border bg-card p-6 shadow-sm">
        <div className="space-y-1 text-center">
          <h1 className="text-2xl font-semibold tracking-tight">Finance Tracker</h1>
          <p className="text-sm text-muted-foreground">Войдите в свой аккаунт</p>
        </div>
        <LoginForm />
      </div>
    </main>
  )
}
