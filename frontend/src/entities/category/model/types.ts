export interface Category {
  id: string
  name: string
  icon: string
  color: string
  isSystem: boolean
}

/** Create/update payload for a user category. */
export interface CategoryInput {
  name: string
  icon: string
  color: string
}
