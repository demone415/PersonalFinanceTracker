import { CATEGORY_ICONS } from '@/shared/lib/category-icons'

/** Renders a Lucide icon by its kebab-case code, falling back to a neutral glyph. */
export function LucideIcon({
  name,
  color,
  className,
}: {
  name: string
  color?: string
  className?: string
}) {
  const Icon = CATEGORY_ICONS[name] ?? CATEGORY_ICONS.ellipsis
  return <Icon className={className} color={color} aria-hidden />
}
