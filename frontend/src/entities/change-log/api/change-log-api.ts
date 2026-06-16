import { http } from '@/shared/api/http'
import type { ChangeLogEntry, ChangeLogFilter, PagedResult } from '../model/types'

function toQueryString(filter: ChangeLogFilter): string {
  const params = new URLSearchParams()
  if (filter.page) params.set('page', String(filter.page))
  if (filter.pageSize) params.set('pageSize', String(filter.pageSize))
  if (filter.entityType) params.set('entityType', filter.entityType)
  if (filter.action) params.set('action', filter.action)
  const qs = params.toString()
  return qs ? `?${qs}` : ''
}

export const changeLogApi = {
  list: (filter: ChangeLogFilter = {}) =>
    http<PagedResult<ChangeLogEntry>>(`/api/v1/changelog${toQueryString(filter)}`),
}
