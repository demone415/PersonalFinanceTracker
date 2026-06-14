import { supabase } from '@/shared/api/supabase'
import { Button } from '@/shared/ui/button'

/** Signs the user out; the AuthProvider listener clears the session store. */
export function LogoutButton() {
  return (
    <Button variant="outline" onClick={() => void supabase.auth.signOut()}>
      Выйти
    </Button>
  )
}
