import { Link } from 'react-router-dom'
import { Construction } from 'lucide-react'
import { Button } from '@/shared/ui/button'

/** Generic placeholder for sections not yet implemented (budgets, journal, scan). */
export function ComingSoonPage({ title }: { title: string }) {
  return (
    <main className="flex min-h-[60svh] flex-col items-center justify-center gap-4 p-8 text-center">
      <div className="flex size-16 items-center justify-center rounded-full bg-accent">
        <Construction className="size-8 text-muted-foreground" />
      </div>
      <div className="space-y-1">
        <h1 className="text-xl font-semibold tracking-tight">{title}</h1>
        <p className="text-sm text-muted-foreground">Раздел в разработке — скоро появится.</p>
      </div>
      <Button variant="outline" asChild>
        <Link to="/">На главную</Link>
      </Button>
    </main>
  )
}
