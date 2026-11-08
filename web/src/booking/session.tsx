import { createContext, useCallback, useContext, useEffect, useState } from 'react'
import { api, type CustomerSession } from './api'

type SessionState = {
  /** undefined = wird geladen, null = nicht angemeldet */
  session: CustomerSession | null | undefined
  refresh: () => Promise<void>
  signOut: () => Promise<void>
}

const SessionContext = createContext<SessionState>({
  session: null,
  refresh: async () => {},
  signOut: async () => {},
})

export function SessionProvider({ children }: { children: React.ReactNode }) {
  const [session, setSession] = useState<CustomerSession | null | undefined>(undefined)

  const refresh = useCallback(async () => {
    try {
      const { data } = await api.GET('/api/online/me')
      setSession(data ?? null)
    } catch {
      setSession(null)
    }
  }, [])

  const signOut = useCallback(async () => {
    await api.POST('/api/online/auth/logout')
    setSession(null)
  }, [])

  useEffect(() => {
    api.GET('/api/online/me').then(
      ({ data }) => setSession(data ?? null),
      () => setSession(null),
    )
  }, [])

  return <SessionContext.Provider value={{ session, refresh, signOut }}>{children}</SessionContext.Provider>
}

// eslint-disable-next-line react/only-export-components
export function useSession() {
  return useContext(SessionContext)
}
