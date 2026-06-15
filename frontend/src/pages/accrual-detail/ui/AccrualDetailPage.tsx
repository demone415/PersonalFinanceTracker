import { useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import {
  useAccrual,
  useUpdateAccrual,
  useReceipt,
  useAddReceiptItem,
  useUpdateReceiptItem,
  useDeleteReceiptItem,
  type ReceiptItem,
  type ReceiptItemInput,
} from '@/entities/accrual'
import { AccrualForm, type AccrualFormValues } from '@/features/accrual-form'
import { Button } from '@/shared/ui/button'
import { Input } from '@/shared/ui/input'
import { Label } from '@/shared/ui/label'
import { Skeleton } from '@/shared/ui/skeleton'

function ReceiptItemRow({
  item,
  onUpdate,
  onDelete,
}: {
  item: ReceiptItem
  onUpdate: (input: ReceiptItemInput) => void
  onDelete: () => void
}) {
  const [editing, setEditing] = useState(false)
  const [form, setForm] = useState<ReceiptItemInput>({
    name: item.name,
    price: item.price,
    quantity: item.quantity,
    sum: item.sum,
  })

  if (editing) {
    return (
      <li className="rounded-lg border bg-card p-3 space-y-2">
        <div className="grid grid-cols-2 gap-2">
          <div className="col-span-2 space-y-1">
            <Label className="text-xs">Наименование</Label>
            <Input
              value={form.name}
              onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
            />
          </div>
          <div className="space-y-1">
            <Label className="text-xs">Цена</Label>
            <Input
              type="number"
              step="0.01"
              value={form.price}
              onChange={(e) => setForm((f) => ({ ...f, price: parseFloat(e.target.value) || 0 }))}
            />
          </div>
          <div className="space-y-1">
            <Label className="text-xs">Кол-во</Label>
            <Input
              type="number"
              step="0.001"
              value={form.quantity}
              onChange={(e) => setForm((f) => ({ ...f, quantity: parseFloat(e.target.value) || 0 }))}
            />
          </div>
          <div className="space-y-1">
            <Label className="text-xs">Сумма</Label>
            <Input
              type="number"
              step="0.01"
              value={form.sum}
              onChange={(e) => setForm((f) => ({ ...f, sum: parseFloat(e.target.value) || 0 }))}
            />
          </div>
        </div>
        <div className="flex gap-2">
          <Button size="sm" onClick={() => { onUpdate(form); setEditing(false) }}>Сохранить</Button>
          <Button size="sm" variant="outline" onClick={() => setEditing(false)}>Отмена</Button>
        </div>
      </li>
    )
  }

  return (
    <li className="flex items-center justify-between rounded-lg border bg-card p-3">
      <div>
        <p className="text-sm font-medium">{item.name}</p>
        <p className="text-xs text-muted-foreground">
          {item.quantity} × {item.price.toLocaleString('ru-RU', { minimumFractionDigits: 2 })} ₽ ={' '}
          <span className="font-medium">{item.sum.toLocaleString('ru-RU', { minimumFractionDigits: 2 })} ₽</span>
        </p>
      </div>
      <div className="flex gap-2">
        <Button size="sm" variant="outline" onClick={() => setEditing(true)}>Изм.</Button>
        <Button size="sm" variant="outline" onClick={onDelete}>Удалить</Button>
      </div>
    </li>
  )
}

function AddItemForm({ onAdd }: { onAdd: (input: ReceiptItemInput) => void }) {
  const [form, setForm] = useState<ReceiptItemInput>({ name: '', price: 0, quantity: 1, sum: 0 })

  function autoSum() {
    setForm((f) => ({ ...f, sum: parseFloat((f.price * f.quantity).toFixed(2)) }))
  }

  return (
    <div className="rounded-lg border bg-muted/30 p-3 space-y-2">
      <p className="text-sm font-medium">Добавить позицию</p>
      <div className="grid grid-cols-2 gap-2">
        <div className="col-span-2 space-y-1">
          <Label className="text-xs">Наименование *</Label>
          <Input
            placeholder="Название товара"
            value={form.name}
            onChange={(e) => setForm((f) => ({ ...f, name: e.target.value }))}
          />
        </div>
        <div className="space-y-1">
          <Label className="text-xs">Цена</Label>
          <Input
            type="number" step="0.01"
            value={form.price}
            onChange={(e) => setForm((f) => ({ ...f, price: parseFloat(e.target.value) || 0 }))}
            onBlur={autoSum}
          />
        </div>
        <div className="space-y-1">
          <Label className="text-xs">Кол-во</Label>
          <Input
            type="number" step="0.001"
            value={form.quantity}
            onChange={(e) => setForm((f) => ({ ...f, quantity: parseFloat(e.target.value) || 0 }))}
            onBlur={autoSum}
          />
        </div>
        <div className="space-y-1">
          <Label className="text-xs">Сумма</Label>
          <Input
            type="number" step="0.01"
            value={form.sum}
            onChange={(e) => setForm((f) => ({ ...f, sum: parseFloat(e.target.value) || 0 }))}
          />
        </div>
      </div>
      <Button
        size="sm"
        disabled={!form.name.trim()}
        onClick={() => { onAdd(form); setForm({ name: '', price: 0, quantity: 1, sum: 0 }) }}
      >
        Добавить
      </Button>
    </div>
  )
}

export function AccrualDetailPage() {
  const { id } = useParams<{ id: string }>()
  const [editMode, setEditMode] = useState(false)

  const { data: accrual, isPending } = useAccrual(id!)
  const { data: receipt } = useReceipt(id!)
  const updateMutation = useUpdateAccrual()
  const addItemMutation = useAddReceiptItem()
  const updateItemMutation = useUpdateReceiptItem()
  const deleteItemMutation = useDeleteReceiptItem()

  if (isPending) {
    return (
      <main className="mx-auto max-w-3xl space-y-4 p-8">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-32 w-full" />
      </main>
    )
  }

  if (!accrual) return <p className="p-8 text-muted-foreground">Начисление не найдено</p>

  function handleUpdate(values: AccrualFormValues) {
    updateMutation.mutate(
      {
        id: accrual!.id,
        input: {
          ...values,
          categoryId: values.categoryId || null,
          groupId: values.groupId || null,
          exchangeRate: values.exchangeRate || null,
          tags: values.tags ?? [],
        },
      },
      { onSuccess: () => setEditMode(false) },
    )
  }

  const typeSign = accrual.type === 'Income' || accrual.type === 'ReturnIncome' ? '+' : '−'
  const amountColor = accrual.type === 'Income' || accrual.type === 'ReturnIncome'
    ? 'text-green-500' : 'text-red-500'

  return (
    <main className="mx-auto max-w-3xl space-y-6 p-8">
      <header className="flex items-center justify-between">
        <div>
          <Link to="/accruals" className="text-sm text-muted-foreground hover:underline">
            ← К списку
          </Link>
          <h1 className={`text-2xl font-semibold ${amountColor}`}>
            {typeSign}{accrual.amount.toLocaleString('ru-RU', { minimumFractionDigits: 2 })} {accrual.currency}
          </h1>
          <p className="text-sm text-muted-foreground">
            {new Date(accrual.date).toLocaleString('ru-RU')}
            {accrual.categoryName && <> · {accrual.categoryName}</>}
          </p>
        </div>
        {!editMode && (
          <Button variant="outline" onClick={() => setEditMode(true)}>Редактировать</Button>
        )}
      </header>

      {accrual.description && (
        <p className="text-sm text-muted-foreground">{accrual.description}</p>
      )}

      {accrual.tags.length > 0 && (
        <div className="flex flex-wrap gap-1">
          {accrual.tags.map((t) => (
            <span key={t} className="rounded-full bg-muted px-2 py-0.5 text-xs">#{t}</span>
          ))}
        </div>
      )}

      {editMode && (
        <section className="rounded-lg border bg-card p-4">
          <AccrualForm
            defaultValues={{
              amount: accrual.amount,
              date: accrual.date.slice(0, 16),
              type: accrual.type,
              currency: accrual.currency,
              categoryId: accrual.categoryId,
              description: accrual.description,
              includeInStats: accrual.includeInStats,
              groupId: accrual.groupId,
              exchangeRate: accrual.exchangeRate,
              tags: accrual.tags,
            }}
            submitting={updateMutation.isPending}
            submitLabel="Сохранить"
            onSubmit={handleUpdate}
            onCancel={() => setEditMode(false)}
          />
        </section>
      )}

      {/* Receipt items (T1.4.7) */}
      <section className="space-y-3">
        <h2 className="text-base font-semibold">Позиции чека</h2>

        {receipt && receipt.items.length > 0 ? (
          <ul className="space-y-2">
            {receipt.items.map((item) => (
              <ReceiptItemRow
                key={item.id}
                item={item}
                onUpdate={(input) =>
                  updateItemMutation.mutate({ accrualId: id!, itemId: item.id, input })
                }
                onDelete={() =>
                  deleteItemMutation.mutate({ accrualId: id!, itemId: item.id })
                }
              />
            ))}
          </ul>
        ) : (
          <p className="text-sm text-muted-foreground">Позиций нет</p>
        )}

        <AddItemForm
          onAdd={(input) => addItemMutation.mutate({ accrualId: id!, input })}
        />
      </section>
    </main>
  )
}
