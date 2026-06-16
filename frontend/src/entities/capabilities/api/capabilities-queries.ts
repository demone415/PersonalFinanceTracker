import { useQuery } from '@tanstack/react-query'
import { capabilitiesApi } from './capabilities-api'

export const capabilitiesKeys = { all: ['capabilities'] as const }

/**
 * Server feature flags. The backend can gain/lose the provider token independently
 * of the frontend, so this must be queried at runtime rather than assumed. Cached
 * for a few minutes (changes only on a backend config change) but still refetched,
 * so a newly-configured token lights the receipt features up without a redeploy.
 */
export function useCapabilities() {
  return useQuery({
    queryKey: capabilitiesKeys.all,
    queryFn: capabilitiesApi.get,
    staleTime: 5 * 60_000,
  })
}
