import { useCallback, useEffect, useState, type RefObject } from 'react'

/**
 * Nach fehlgeschlagenem Speichern springt der Fokus ins erste ungültige Feld (masken.md R8).
 * Der Aufruf gehört in denselben Handler wie das Setzen der Fehler; der Fokus folgt, sobald `aria-invalid` im DOM steht.
 */
export function useFocusFirstInvalid(container: RefObject<HTMLElement | null>): () => void {
  const [requests, setRequests] = useState(0)
  useEffect(() => {
    if (requests > 0) container.current?.querySelector<HTMLElement>('[aria-invalid="true"]')?.focus()
  }, [requests, container])
  return useCallback(() => setRequests((n) => n + 1), [])
}
