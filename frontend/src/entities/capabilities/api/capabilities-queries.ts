import { useQuery } from '@tanstack/react-query'
import { capabilitiesApi } from './capabilities-api'

export const capabilitiesKeys = { all: ['capabilities'] as const }

/** Server feature flags. Cached generously — capabilities change only on redeploy. */
export function useCapabilities() {
  return useQuery({
    queryKey: capabilitiesKeys.all,
    queryFn: capabilitiesApi.get,
    staleTime: 5 * 60_000,
  })
}
