import { useCallback, useState, type RefObject } from 'react'
import { useFocusFirstInvalid } from './useFocusFirstInvalid'

export type FieldErrors = Record<string, string>

/**
 * Fehler am Feld für Formulare mit eigener Prüfung (`noValidate`, masken.md R8): `fail` setzt die gefundenen Fehler und
 * springt ins erste ungültige Feld, `clear` nimmt den Fehler eines Feldes zurück, sobald es sich ändert.
 */
export function useFormErrors(form: RefObject<HTMLElement | null>) {
  const [errors, setErrors] = useState<FieldErrors>({})
  const focusFirstInvalid = useFocusFirstInvalid(form)
  const clear = useCallback(
    (key: string) =>
      setErrors((current) => {
        if (!(key in current)) return current
        const { [key]: _removed, ...rest } = current
        return rest
      }),
    [],
  )
  const fail = useCallback(
    (found: FieldErrors) => {
      setErrors(found)
      focusFirstInvalid()
    },
    [focusFirstInvalid],
  )
  return { errors, clear, fail }
}
