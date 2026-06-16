import { http } from '@/shared/api/http'
import type { Capabilities } from '../model/types'

export const capabilitiesApi = {
  get: () => http<Capabilities>('/api/v1/capabilities'),
}
