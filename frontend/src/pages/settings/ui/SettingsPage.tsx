import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useProfile, useUpdateProfile, type Profile } from '@/entities/profile'
import { Button } from '@/shared/ui/button'
import { Input } from '@/shared/ui/input'
import { Label } from '@/shared/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/shared/ui/select'
import { Skeleton } from '@/shared/ui/skeleton'
import { CURRENCIES } from '@/shared/lib/currencies'

export function SettingsPage() {
  const { data: profile, isPending } = useProfile()

  return (
    <main className="mx-auto max-w-xl space-y-6 p-8">
      <header>
        <h1 className="text-2xl font-semibold tracking-tight">Настройки</h1>
        <Link to="/" className="text-sm text-muted-foreground hover:underline">
          ← На главную
        </Link>
      </header>

      {isPending || !profile ? (
        <Skeleton className="h-48 w-full" />
      ) : (
        // Keyed by profile so the form re-initialises from fresh server state
        // (avoids syncing props into state via an effect).
        <ProfileForm key={profile.id} profile={profile} />
      )}
    </main>
  )
}

function ProfileForm({ profile }: { profile: Profile }) {
  const updateMutation = useUpdateProfile()
  const [displayName, setDisplayName] = useState(profile.displayName ?? '')
  const [currency, setCurrency] = useState(profile.currency)
  const [saved, setSaved] = useState(false)

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault()
    setSaved(false)
    updateMutation.mutate(
      { displayName: displayName.trim() || null, currency },
      { onSuccess: () => setSaved(true) },
    )
  }

  // A custom code the user may already have that isn't in the preset list.
  const hasPreset = CURRENCIES.some((c) => c.code === currency)

  return (
    <form onSubmit={handleSubmit} className="space-y-4 rounded-lg border bg-card p-6">
      <div className="space-y-1">
        <Label htmlFor="displayName">Отображаемое имя</Label>
        <Input
          id="displayName"
          value={displayName}
          maxLength={100}
          placeholder="Имя"
          onChange={(e) => { setDisplayName(e.target.value); setSaved(false) }}
        />
      </div>

      <div className="space-y-1">
        <Label htmlFor="currency">Основная валюта</Label>
        <Select
          value={currency}
          onValueChange={(v) => { setCurrency(v); setSaved(false) }}
        >
          <SelectTrigger id="currency">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            {!hasPreset && <SelectItem value={currency}>{currency}</SelectItem>}
            {CURRENCIES.map((c) => (
              <SelectItem key={c.code} value={c.code}>
                {c.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <p className="text-xs text-muted-foreground">
          В этой валюте показываются дашборд и прогресс бюджетов. Суммы операций в
          других валютах пересчитываются по курсу, указанному при вводе.
        </p>
      </div>

      <div className="flex items-center gap-3 pt-2">
        <Button type="submit" disabled={updateMutation.isPending}>
          {updateMutation.isPending ? 'Сохранение…' : 'Сохранить'}
        </Button>
        {saved && !updateMutation.isPending && (
          <span className="text-sm text-green-500">Сохранено</span>
        )}
        {updateMutation.isError && (
          <span className="text-sm text-destructive">Не удалось сохранить</span>
        )}
      </div>
    </form>
  )
}
