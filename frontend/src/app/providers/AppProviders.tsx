import type { ReactNode } from 'react'
import { QueryClientProvider } from '@tanstack/react-query'
import { queryClient } from './query-client'
import { AuthProvider } from './AuthProvider'
import { Toaster } from '@/shared/ui/toaster'

/**
 * Composes all global providers. New providers (theme, etc.) are added here so
 * the rest of the app stays unaware of the wiring order.
 */
export function AppProviders({ children }: { children: ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>{children}</AuthProvider>
      <Toaster />
    </QueryClientProvider>
  )
}
