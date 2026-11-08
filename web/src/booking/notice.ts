import { useCallback, useState } from 'react'

/**
 * Erfolgsmeldung nach einer Aktion. Jede Meldung bekommt eine neue Nummer, damit dieselbe Meldung beim zweiten Mal wieder
 * erscheint, den Fokus bekommt und vorgelesen wird (als `key` an `Status` geben).
 */
export function useNotice() {
  const [notice, setNotice] = useState<{ text: string; n: number } | null>(null)
  const show = useCallback((text: string) => setNotice((p) => ({ text, n: (p?.n ?? 0) + 1 })), [])
  return [notice, show] as const
}
