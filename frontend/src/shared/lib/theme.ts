import { create } from 'zustand'

export type Theme = 'dark' | 'light'

const STORAGE_KEY = 'ft-theme'

function readInitial(): Theme {
  const stored = localStorage.getItem(STORAGE_KEY)
  return stored === 'light' ? 'light' : 'dark' // dark by default (CLAUDE.md)
}

function apply(theme: Theme) {
  document.documentElement.classList.toggle('dark', theme === 'dark')
}

interface ThemeState {
  theme: Theme
  toggle: () => void
  setTheme: (theme: Theme) => void
}

export const useThemeStore = create<ThemeState>((set, get) => ({
  theme: readInitial(),
  toggle: () => get().setTheme(get().theme === 'dark' ? 'light' : 'dark'),
  setTheme: (theme) => {
    localStorage.setItem(STORAGE_KEY, theme)
    apply(theme)
    set({ theme })
  },
}))

// Apply the persisted theme synchronously on module load (before first paint),
// so there is no light/dark flash on refresh.
apply(readInitial())
