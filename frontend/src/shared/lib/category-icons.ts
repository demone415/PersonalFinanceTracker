import {
  ShoppingCart, Utensils, Car, House, HeartPulse, Gamepad2, Shirt, Wifi,
  GraduationCap, Gift, Wallet, Ellipsis, Dumbbell, Plane, Coffee, Book,
  Briefcase, Heart, Music, Smartphone, Fuel, Pizza, Baby, PawPrint,
  type LucideIcon as LucideIconType,
} from 'lucide-react'

/**
 * Curated kebab-case icon code → Lucide component map. Codes are persisted on
 * the Category (e.g. "shopping-cart") and shared by the picker and the renderer.
 */
export const CATEGORY_ICONS: Record<string, LucideIconType> = {
  'shopping-cart': ShoppingCart,
  utensils: Utensils,
  car: Car,
  house: House,
  'heart-pulse': HeartPulse,
  'gamepad-2': Gamepad2,
  shirt: Shirt,
  wifi: Wifi,
  'graduation-cap': GraduationCap,
  gift: Gift,
  wallet: Wallet,
  ellipsis: Ellipsis,
  dumbbell: Dumbbell,
  plane: Plane,
  coffee: Coffee,
  book: Book,
  briefcase: Briefcase,
  heart: Heart,
  music: Music,
  smartphone: Smartphone,
  fuel: Fuel,
  pizza: Pizza,
  baby: Baby,
  'paw-print': PawPrint,
}

/** All selectable icon codes (for the picker). */
export const CATEGORY_ICON_NAMES = Object.keys(CATEGORY_ICONS)
