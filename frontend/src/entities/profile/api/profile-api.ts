import { http } from '@/shared/api/http'
import type { Profile, ProfileUpdateInput } from '../model/types'

export const profileApi = {
  get: () => http<Profile>('/api/v1/profile'),
  update: (input: ProfileUpdateInput) =>
    http<Profile>('/api/v1/profile', { method: 'PUT', body: JSON.stringify(input) }),
}
