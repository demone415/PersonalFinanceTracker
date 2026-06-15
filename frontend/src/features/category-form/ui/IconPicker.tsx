import { cn } from '@/shared/lib/utils'
import { CATEGORY_ICON_NAMES } from '@/shared/lib/category-icons'
import { LucideIcon } from '@/shared/ui/lucide-icon'

/** Grid of selectable Lucide icons (T1.3.4). Stores the kebab-case icon code. */
export function IconPicker({
  value,
  color,
  onChange,
}: {
  value: string
  color?: string
  onChange: (icon: string) => void
}) {
  return (
    <div className="grid grid-cols-8 gap-1.5">
      {CATEGORY_ICON_NAMES.map((name) => {
        const selected = name === value
        return (
          <button
            key={name}
            type="button"
            aria-label={name}
            aria-pressed={selected}
            onClick={() => onChange(name)}
            className={cn(
              'flex aspect-square items-center justify-center rounded-md border transition-colors',
              selected ? 'border-primary ring-1 ring-primary' : 'border-input hover:bg-accent',
            )}
          >
            <LucideIcon name={name} className="size-4" color={selected ? color : undefined} />
          </button>
        )
      })}
    </div>
  )
}
