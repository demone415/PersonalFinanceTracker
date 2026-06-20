import type { ReactNode } from 'react'
import { QueryClientProvider } from '@tanstack/react-query'
import { queryClient } from './query-client'
import { AuthProvider } from './AuthProvider'
import { Toaster } from '@/shared/ui/toaster'
import { BackgroundTaskTracker } from '@/widgets/background-tasks'

/**
 * Composes all global providers. New providers (theme, etc.) are added here so
 * the rest of the app stays unaware of the wiring order.
 */
export function AppProviders({ children }: { children: ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>{children}</AuthProvider>
      {/* Polls + delivers async import/export outcomes on any route (Story 6.3). */}
      <BackgroundTaskTracker />
      <Toaster />
    </QueryClientProvider>
  )
}
