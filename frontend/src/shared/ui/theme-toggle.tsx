import { Moon, Sun } from 'lucide-react'
import { useThemeStore } from '@/shared/lib/theme'
import { Button } from './button'

/** Toggles dark/light theme; persisted in Zustand + localStorage. */
export function ThemeToggle({ className }: { className?: string }) {
  const theme = useThemeStore((s) => s.theme)
  const toggle = useThemeStore((s) => s.toggle)

  return (
    <Button
      variant="ghost"
      size="icon"
      className={className}
      onClick={toggle}
      aria-label={theme === 'dark' ? 'Светлая тема' : 'Тёмная тема'}
    >
      {theme === 'dark' ? <Sun /> : <Moon />}
    </Button>
  )
}
