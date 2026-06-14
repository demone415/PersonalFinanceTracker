import { Button } from '@/shared/ui/button'

export function HomePage() {
  return (
    <main className="flex min-h-svh flex-col items-center justify-center gap-6 p-8">
      <div className="space-y-2 text-center">
        <h1 className="text-3xl font-semibold tracking-tight">Finance Tracker</h1>
        <p className="text-muted-foreground">
          Frontend scaffold — Vite · React 19 · Tailwind v4 · shadcn/ui
        </p>
      </div>
      <Button>Get started</Button>
    </main>
  )
}
