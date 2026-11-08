import { useRef, useState } from 'react'
import { api } from '../api/client'
import { FormField } from '../components/FormField'
import { useFormErrors, type FieldErrors } from '../components/useFormErrors'

const GENERIC_PROBLEM = 'Die Anmeldung hat nicht geklappt. Verbindung prüfen und noch einmal versuchen.'

function problemText(error: unknown): string {
  const e = error as { errors?: Record<string, string[]>; detail?: string; title?: string } | undefined
  if (e?.errors) return Object.values(e.errors).flat().join(' ')
  return e?.detail ?? e?.title ?? GENERIC_PROBLEM
}

/**
 * Einzelplatz-Betrieb (ADR 0041): Anmeldung mit Benutzername und Passwort. Einrichten, Wiederherstellen und Passwort ändern
 * gibt es nur in der Windows-App (Slice 103).
 */
export function PasswordLogin({ onSignedIn }: { onSignedIn: () => Promise<void> }) {
  const [userName, setUserName] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const formRef = useRef<HTMLFormElement>(null)
  const { errors, clear, fail } = useFormErrors(formRef)

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    if (busy) return
    const found: FieldErrors = {
      ...(!userName.trim() && { userName: 'Der Benutzername fehlt.' }),
      ...(!password && { password: 'Das Passwort fehlt.' }),
    }
    if (Object.keys(found).length > 0) return fail(found)
    setBusy(true)
    setError(null)
    try {
      const { response, error: problem } = await api.POST('/api/v1/auth/standalone/login', { body: { userName, password, persistent: true } })
      if (response.status === 401) return setError('Benutzername oder Passwort stimmt nicht. Beides prüfen und noch einmal versuchen.')
      if (!response.ok) return setError(problemText(problem))
    } catch {
      return setError(GENERIC_PROBLEM)
    } finally {
      setBusy(false)
    }
    await onSignedIn()
  }

  return (
    <form className="signin-form" ref={formRef} noValidate onSubmit={submit} aria-labelledby="login-title">
      <h2 id="login-title">Anmeldung</h2>
      <FormField label="Benutzername" required error={errors.userName}>
        {(p) => (
          <input
            {...p}
            name="username"
            value={userName}
            onChange={(e) => {
              setUserName(e.target.value)
              clear('userName')
            }}
            autoComplete="username"
            autoCapitalize="none"
            autoCorrect="off"
            spellCheck={false}
            autoFocus
          />
        )}
      </FormField>
      <FormField label="Passwort" required error={errors.password}>
        {(p) => (
          <input
            {...p}
            name="password"
            type="password"
            value={password}
            onChange={(e) => {
              setPassword(e.target.value)
              clear('password')
            }}
            autoComplete="current-password"
            enterKeyHint="done"
          />
        )}
      </FormField>
      <div className="actions">
        <button className="primary" type="submit" aria-disabled={busy || undefined}>
          Anmelden
        </button>
      </div>
      {error && <p role="alert">{error}</p>}
      <p className="muted">Passwort vergessen? Ein neues Passwort vergibt die Leitung in der Windows-App im Bereich „Team“.</p>
    </form>
  )
}
