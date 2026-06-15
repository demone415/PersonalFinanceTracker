import { useState } from 'react'
import { Link } from 'react-router-dom'
import {
  useCategories,
  useCreateCategory,
  useDeleteCategory,
  useUpdateCategory,
  type Category,
} from '@/entities/category'
import { CategoryForm, type CategoryFormValues } from '@/features/category-form'
import { Button } from '@/shared/ui/button'
import { Skeleton } from '@/shared/ui/skeleton'
import { LucideIcon } from '@/shared/ui/lucide-icon'

type Editing = 'new' | Category | null

export function CategoriesPage() {
  const { data: categories, isPending } = useCategories()
  const createMutation = useCreateCategory()
  const updateMutation = useUpdateCategory()
  const deleteMutation = useDeleteCategory()

  const [editing, setEditing] = useState<Editing>(null)

  function handleSubmit(values: CategoryFormValues) {
    if (editing === 'new') {
      createMutation.mutate(values, { onSuccess: () => setEditing(null) })
    } else if (editing) {
      updateMutation.mutate({ id: editing.id, input: values }, { onSuccess: () => setEditing(null) })
    }
  }

  function handleDelete(category: Category) {
    if (confirm(`Удалить категорию «${category.name}»?`)) {
      deleteMutation.mutate(category.id)
    }
  }

  return (
    <main className="mx-auto max-w-3xl space-y-6 p-8">
      <header className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold tracking-tight">Категории</h1>
          <Link to="/" className="text-sm text-muted-foreground hover:underline">
            ← На главную
          </Link>
        </div>
        {editing === null && <Button onClick={() => setEditing('new')}>Добавить</Button>}
      </header>

      {editing !== null && (
        <section className="rounded-lg border bg-card p-4">
          <h2 className="mb-3 text-sm font-medium">
            {editing === 'new' ? 'Новая категория' : 'Редактирование категории'}
          </h2>
          <CategoryForm
            defaultValues={
              editing === 'new'
                ? undefined
                : { name: editing.name, icon: editing.icon, color: editing.color }
            }
            submitting={createMutation.isPending || updateMutation.isPending}
            submitLabel={editing === 'new' ? 'Создать' : 'Сохранить'}
            onSubmit={handleSubmit}
            onCancel={() => setEditing(null)}
          />
        </section>
      )}

      {isPending ? (
        <ul className="space-y-2">
          {Array.from({ length: 6 }).map((_, i) => (
            <li key={i}>
              <Skeleton className="h-14 w-full" />
            </li>
          ))}
        </ul>
      ) : (
        <ul className="space-y-2">
          {categories?.map((category) => (
            <li
              key={category.id}
              className="flex items-center justify-between rounded-lg border bg-card p-3"
            >
              <div className="flex items-center gap-3">
                <span
                  className="flex size-9 items-center justify-center rounded-md"
                  style={{ backgroundColor: `${category.color}22` }}
                >
                  <LucideIcon name={category.icon} color={category.color} className="size-5" />
                </span>
                <span className="font-medium">{category.name}</span>
                {category.isSystem && (
                  <span className="rounded-full bg-muted px-2 py-0.5 text-xs text-muted-foreground">
                    системная
                  </span>
                )}
              </div>

              {!category.isSystem && (
                <div className="flex gap-2">
                  <Button variant="outline" size="sm" onClick={() => setEditing(category)}>
                    Изменить
                  </Button>
                  <Button variant="outline" size="sm" onClick={() => handleDelete(category)}>
                    Удалить
                  </Button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
    </main>
  )
}
