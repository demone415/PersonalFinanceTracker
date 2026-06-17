import { create } from 'zustand'

export type ToastVariant = 'success' | 'error' | 'info'

export interface Toast {
  id: string
  message: string
  variant: ToastVariant
}

/** Auto-dismiss delay; errors linger longer so they aren't missed. */
const DISMISS_MS: Record<ToastVariant, number> = {
  success: 4000,
  info: 4000,
  error: 7000,
}

interface ToastState {
  toasts: Toast[]
  show: (message: string, variant?: ToastVariant) => void
  dismiss: (id: string) => void
}

export const useToastStore = create<ToastState>((set, get) => ({
  toasts: [],
  show: (message, variant = 'info') => {
    const id = crypto.randomUUID()
    set((state) => ({ toasts: [...state.toasts, { id, message, variant }] }))
    setTimeout(() => get().dismiss(id), DISMISS_MS[variant])
  },
  dismiss: (id) => set((state) => ({ toasts: state.toasts.filter((t) => t.id !== id) })),
}))
