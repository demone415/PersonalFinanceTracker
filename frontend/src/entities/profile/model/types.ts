/** The caller's application profile (Epic 8, T8.1.2). */
export interface Profile {
  id: string
  displayName?: string
  /** Base currency (ISO 4217) — the unit dashboards and budgets are shown in. */
  currency: string
}

/** Editable profile fields. */
export interface ProfileUpdateInput {
  displayName?: string | null
  currency: string
}
