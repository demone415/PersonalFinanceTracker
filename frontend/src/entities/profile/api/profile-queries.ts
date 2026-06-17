import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { profileApi } from './profile-api'
import type { Profile, ProfileUpdateInput } from '../model/types'

const profileKey = ['profile'] as const

/** The caller's profile. Created server-side on first read, so this rarely errors. */
export function useProfile() {
  return useQuery({
    queryKey: profileKey,
    queryFn: profileApi.get,
    staleTime: 5 * 60_000,
  })
}

/**
 * The base currency to display amounts in (Epic 8). Defaults to RUB while the
 * profile loads so the UI never flashes an empty symbol.
 */
export function useBaseCurrency(): string {
  const { data } = useProfile()
  return data?.currency ?? 'RUB'
}

export function useUpdateProfile() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: ProfileUpdateInput) => profileApi.update(input),
    onSuccess: (profile: Profile) => queryClient.setQueryData(profileKey, profile),
  })
}
