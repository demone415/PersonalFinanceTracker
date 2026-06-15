import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { categoryApi } from './category-api'
import type { Category, CategoryInput } from '../model/types'

const categoriesKey = ['categories'] as const

export function useCategories() {
  return useQuery({ queryKey: categoriesKey, queryFn: categoryApi.list })
}

export function useCreateCategory() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: CategoryInput) => categoryApi.create(input),
    // The server assigns the id, so refetch on completion rather than guessing.
    onSettled: () => queryClient.invalidateQueries({ queryKey: categoriesKey }),
  })
}

export function useUpdateCategory() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, input }: { id: string; input: CategoryInput }) => categoryApi.update(id, input),
    onMutate: async ({ id, input }) => {
      await queryClient.cancelQueries({ queryKey: categoriesKey })
      const previous = queryClient.getQueryData<Category[]>(categoriesKey) ?? []
      queryClient.setQueryData<Category[]>(
        categoriesKey,
        previous.map((c) => (c.id === id ? { ...c, ...input } : c)),
      )
      return { previous }
    },
    onError: (_error, _vars, context) => {
      if (context) queryClient.setQueryData(categoriesKey, context.previous)
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: categoriesKey }),
  })
}

export function useDeleteCategory() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (id: string) => categoryApi.remove(id),
    onMutate: async (id) => {
      await queryClient.cancelQueries({ queryKey: categoriesKey })
      const previous = queryClient.getQueryData<Category[]>(categoriesKey) ?? []
      queryClient.setQueryData<Category[]>(
        categoriesKey,
        previous.filter((c) => c.id !== id),
      )
      return { previous }
    },
    onError: (_error, _vars, context) => {
      if (context) queryClient.setQueryData(categoriesKey, context.previous)
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: categoriesKey }),
  })
}
