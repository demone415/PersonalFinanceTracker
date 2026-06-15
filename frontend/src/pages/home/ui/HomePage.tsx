import { Link } from 'react-router-dom'
import { LogoutButton } from '@/features/auth'
import { useSessionStore } from '@/entities/session'
import { Button } from '@/shared/ui/button'

export function HomePage() {
  const userId = useSessionStore((s) => s.userId)
  const role = useSessionStore((s) => s.role)
  const isAdmin = useSessionStore((s) => s.isAdmin)

  return (
    <main className="flex min-h-svh flex-col items-center justify-center gap-6 p-8">
      <div className="space-y-2 text-center">
        <h1 className="text-3xl font-semibold tracking-tight">Finance Tracker</h1>
        <p className="text-muted-foreground">
          Вы вошли как <span className="font-medium">{role}</span>
          <br />
          <span className="text-xs">{userId}</span>
        </p>
      </div>
      <div className="flex items-center gap-3">
        <Button asChild>
          <Link to="/categories">Категории</Link>
        </Button>
        {isAdmin && (
          <Button variant="secondary" asChild>
            <Link to="/admin">Администрирование</Link>
          </Button>
        )}
        <LogoutButton />
      </div>
    </main>
  )
}
