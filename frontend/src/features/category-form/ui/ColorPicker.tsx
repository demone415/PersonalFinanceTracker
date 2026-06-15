import { cn } from '@/shared/lib/utils'

const PRESETS = [
  '#22c55e', '#f97316', '#3b82f6', '#8b5cf6', '#ef4444', '#ec4899',
  '#14b8a6', '#06b6d4', '#eab308', '#d946ef', '#10b981', '#64748b',
]

/** Preset swatches plus a native picker for an arbitrary HEX colour (T1.3.4). */
export function ColorPicker({ value, onChange }: { value: string; onChange: (color: string) => void }) {
  return (
    <div className="flex flex-wrap items-center gap-1.5">
      {PRESETS.map((preset) => (
        <button
          key={preset}
          type="button"
          aria-label={preset}
          aria-pressed={preset.toLowerCase() === value.toLowerCase()}
          onClick={() => onChange(preset)}
          className={cn(
            'size-6 rounded-full border',
            preset.toLowerCase() === value.toLowerCase()
              ? 'ring-2 ring-offset-2 ring-ring'
              : 'border-input',
          )}
          style={{ backgroundColor: preset }}
        />
      ))}
      <label className="ml-1 inline-flex size-6 cursor-pointer items-center justify-center rounded-full border border-dashed border-input">
        <input
          type="color"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          className="size-5 cursor-pointer rounded-full border-0 bg-transparent p-0"
          aria-label="Свой цвет"
        />
      </label>
    </div>
  )
}
