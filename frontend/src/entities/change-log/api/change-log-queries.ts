import { useQuery } from '@tanstack/react-query'
import { changeLogApi } from './change-log-api'
import type { ChangeLogFilter } from '../model/types'

const KEYS = {
  all: ['change-log'] as const,
  listFiltered: (filter: ChangeLogFilter) => ['change-log', 'list', filter] as const,
}

export function useChangeLog(filter: ChangeLogFilter = {}) {
  return useQuery({
    queryKey: KEYS.listFiltered(filter),
    queryFn: () => changeLogApi.list(filter),
  })
}
