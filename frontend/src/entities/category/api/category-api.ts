import { http } from '@/shared/api/http'
import type { Category, CategoryInput } from '../model/types'

export const categoryApi = {
  list: () => http<Category[]>('/api/v1/categories'),
  create: (input: CategoryInput) =>
    http<Category>('/api/v1/categories', { method: 'POST', body: JSON.stringify(input) }),
  update: (id: string, input: CategoryInput) =>
    http<Category>(`/api/v1/categories/${id}`, { method: 'PUT', body: JSON.stringify(input) }),
  remove: (id: string) => http<void>(`/api/v1/categories/${id}`, { method: 'DELETE' }),
}
