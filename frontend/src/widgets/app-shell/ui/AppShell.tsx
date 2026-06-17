import { useState } from 'react'
import { Link, NavLink, Outlet } from 'react-router-dom'
import {
  LayoutDashboard, PieChart, ArrowLeftRight, Shapes, Target, History,
  QrCode, Shield, LogOut, X, Wallet, Settings, type LucideIcon,
} from 'lucide-react'
import { supabase } from '@/shared/api/supabase'
import { useSessionStore } from '@/entities/session'
import { useCapabilities } from '@/entities/capabilities'
import { cn } from '@/shared/lib/utils'
import { ThemeToggle } from '@/shared/ui/theme-toggle'
import { Button } from '@/shared/ui/button'

interface NavItem {
  to: string
  label: string
  icon: LucideIcon
  end?: boolean
}

const PRIMARY_NAV: NavItem[] = [
  { to: '/', label: 'Главная', icon: LayoutDashboard, end: true },
  { to: '/dashboard', label: 'Аналитика', icon: PieChart },
  { to: '/accruals', label: 'Начисления', icon: ArrowLeftRight },
  { to: '/categories', label: 'Категории', icon: Shapes },
  { to: '/budgets', label: 'Бюджеты', icon: Target },
  { to: '/journal', label: 'Журнал', icon: History },
  { to: '/settings', label: 'Настройки', icon: Settings },
]

// Mobile bottom-bar tabs (the central scan FAB is rendered separately).
const MOBILE_TABS: NavItem[] = [
  { to: '/', label: 'Главная', icon: LayoutDashboard, end: true },
  { to: '/accruals', label: 'Начисления', icon: ArrowLeftRight },
  { to: '/categories', label: 'Категории', icon: Shapes },
]

function displayName(email: string | null | undefined): string {
  if (!email) return 'Пользователь'
  const local = email.split('@')[0]
  return local.charAt(0).toUpperCase() + local.slice(1)
}

export function AppShell() {
  const isAdmin = useSessionStore((s) => s.isAdmin)
  const email = useSessionStore((s) => s.session?.user?.email)
  const [moreOpen, setMoreOpen] = useState(false)

  // Receipt scanning is off when the backend has no provider token. Stay optimistic
  // while the flag loads (undefined) and only disable once it's explicitly false.
  const { data: capabilities } = useCapabilities()
  const scanEnabled = capabilities?.receiptScanning !== false

  const name = displayName(email)
  const initials = name.slice(0, 2).toUpperCase()

  return (
    <div className="flex min-h-svh bg-background">
      {/* ── Desktop sidebar ───────────────────────────────────────────── */}
      <aside className="sticky top-0 hidden h-svh w-56 shrink-0 flex-col border-r bg-card/40 p-3 md:flex">
        <Link to="/" className="flex items-center gap-2 px-2 py-3 text-base font-semibold">
          <Wallet className="size-5 text-primary" />
          Финансы
        </Link>

        <nav className="mt-2 flex flex-col gap-1">
          {PRIMARY_NAV.map((item) => (
            <SidebarLink key={item.to} item={item} />
          ))}
          {isAdmin && (
            <SidebarLink item={{ to: '/admin', label: 'Администрирование', icon: Shield }} />
          )}
        </nav>

        <div className="mt-auto flex items-center gap-2 border-t pt-3">
          <div className="flex size-8 items-center justify-center rounded-full bg-accent text-xs font-medium">
            {initials}
          </div>
          <span className="flex-1 truncate text-sm">{name}</span>
          <Button
            variant="ghost"
            size="icon-sm"
            aria-label="Выйти"
            onClick={() => void supabase.auth.signOut()}
          >
            <LogOut />
          </Button>
        </div>
      </aside>

      {/* ── Main column ───────────────────────────────────────────────── */}
      <div className="flex min-w-0 flex-1 flex-col">
        {/* Mobile top bar */}
        <header className="flex items-center justify-between border-b px-4 py-2 md:hidden">
          <Link to="/" className="flex items-center gap-2 font-semibold">
            <Wallet className="size-5 text-primary" />
            Финансы
          </Link>
          <div className="flex items-center gap-1">
            <ThemeToggle />
            <Button
              variant="ghost"
              size="icon"
              aria-label="Выйти"
              onClick={() => void supabase.auth.signOut()}
            >
              <LogOut />
            </Button>
          </div>
        </header>

        {/* Desktop top bar */}
        <header className="hidden items-center justify-end gap-2 border-b px-6 py-2 md:flex">
          <ThemeToggle />
        </header>

        {/* Page content. Bottom padding leaves room for the mobile tab bar.
            Each routed page owns its own <main> landmark. */}
        <div className="flex-1 pb-24 md:pb-0">
          <Outlet />
        </div>
      </div>

      {/* ── Mobile bottom tab bar + central scan FAB ──────────────────── */}
      <nav className="fixed inset-x-0 bottom-0 z-30 flex items-end justify-around border-t bg-card px-2 pb-[max(0.5rem,env(safe-area-inset-bottom))] pt-2 md:hidden">
        <TabLink item={MOBILE_TABS[0]} />
        <TabLink item={MOBILE_TABS[1]} />

        {scanEnabled ? (
          <Link
            to="/scan"
            aria-label="Сканировать QR"
            className="-mt-6 flex size-14 shrink-0 items-center justify-center rounded-full bg-primary text-primary-foreground shadow-lg ring-4 ring-background transition-transform active:scale-95"
          >
            <QrCode className="size-6" />
          </Link>
        ) : (
          <button
            type="button"
            disabled
            aria-label="Сканирование чеков недоступно"
            title="Загрузка чеков недоступна: не настроен токен провайдера"
            className="-mt-6 flex size-14 shrink-0 cursor-not-allowed items-center justify-center rounded-full bg-muted text-muted-foreground shadow-lg ring-4 ring-background"
          >
            <QrCode className="size-6" />
          </button>
        )}

        <TabLink item={MOBILE_TABS[2]} />
        <button
          type="button"
          onClick={() => setMoreOpen(true)}
          className="flex flex-1 flex-col items-center gap-0.5 py-1 text-[10px] text-muted-foreground"
        >
          <Target className="size-5" />
          Ещё
        </button>
      </nav>

      {/* ── Mobile "More" sheet ───────────────────────────────────────── */}
      {moreOpen && (
        <div
          className="fixed inset-0 z-40 bg-black/50 md:hidden"
          onClick={() => setMoreOpen(false)}
        >
          <div
            className="absolute inset-x-0 bottom-0 space-y-1 rounded-t-2xl border-t bg-card p-4 pb-[max(1rem,env(safe-area-inset-bottom))]"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="mb-2 flex items-center justify-between">
              <span className="text-sm font-medium">Меню</span>
              <Button variant="ghost" size="icon-sm" aria-label="Закрыть" onClick={() => setMoreOpen(false)}>
                <X />
              </Button>
            </div>
            <SheetLink to="/dashboard" icon={PieChart} label="Аналитика" onClick={() => setMoreOpen(false)} />
            <SheetLink to="/budgets" icon={Target} label="Бюджеты" onClick={() => setMoreOpen(false)} />
            <SheetLink to="/journal" icon={History} label="Журнал" onClick={() => setMoreOpen(false)} />
            <SheetLink to="/settings" icon={Settings} label="Настройки" onClick={() => setMoreOpen(false)} />
            {isAdmin && (
              <SheetLink to="/admin" icon={Shield} label="Администрирование" onClick={() => setMoreOpen(false)} />
            )}
          </div>
        </div>
      )}
    </div>
  )
}

function SidebarLink({ item }: { item: NavItem }) {
  const Icon = item.icon
  return (
    <NavLink
      to={item.to}
      end={item.end}
      className={({ isActive }) =>
        cn(
          'flex items-center gap-3 rounded-md px-3 py-2 text-sm transition-colors',
          isActive
            ? 'bg-accent font-medium text-accent-foreground'
            : 'text-muted-foreground hover:bg-accent/50 hover:text-foreground',
        )
      }
    >
      <Icon className="size-[18px]" />
      {item.label}
    </NavLink>
  )
}

function TabLink({ item }: { item: NavItem }) {
  const Icon = item.icon
  return (
    <NavLink
      to={item.to}
      end={item.end}
      className={({ isActive }) =>
        cn(
          'flex flex-1 flex-col items-center gap-0.5 py-1 text-[10px]',
          isActive ? 'text-primary' : 'text-muted-foreground',
        )
      }
    >
      <Icon className="size-5" />
      {item.label}
    </NavLink>
  )
}

function SheetLink({
  to, icon: Icon, label, onClick,
}: {
  to: string
  icon: LucideIcon
  label: string
  onClick: () => void
}) {
  return (
    <Link
      to={to}
      onClick={onClick}
      className="flex items-center gap-3 rounded-md px-3 py-3 text-sm hover:bg-accent"
    >
      <Icon className="size-[18px] text-muted-foreground" />
      {label}
    </Link>
  )
}
