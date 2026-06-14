import { QueryClient } from '@tanstack/react-query'

/**
 * Shared TanStack Query client. Defaults tuned for a finance dashboard:
 * data is reasonably fresh for a minute and retried once on failure.
 */
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 60_000,
      retry: 1,
      refetchOnWindowFocus: false,
    },
  },
})
