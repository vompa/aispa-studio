import { useRef, useState } from 'react'
import { api } from '../api/client'
import { FormField } from '../components/FormField'
import { useFormErrors, type FieldErrors } from '../components/useFormErrors'
import { addPasskey } from '../auth/passkeys'

const COUNTRIES = [
  { code: 'AT', label: 'Österreich' },
  { code: 'DE', label: 'Deutschland' },
  { code: 'CH', label: 'Schweiz' },
] as const

/** Neues Spa registrieren: Unternehmen und Inhaber-Konto anlegen, danach gleich den Passkey speichern. */
export function SignupForm({ onSignedUp }: { onSignedUp: (message: string | null) => Promise<void> }) {
  const [spaName, setSpaName] = useState('')
  const [country, setCountry] = useState<string>('AT')
  const [ownerName, setOwnerName] = useState('')
  const [ownerEmail, setOwnerEmail] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const formRef = useRef<HTMLFormElement>(null)
  const { errors, clear, fail } = useFormErrors(formRef)

  async function submit(event: React.FormEvent) {
    event.preventDefault()
    if (busy) return
    const found: FieldErrors = {
      ...(!spaName.trim() && { spaName: 'Der Name des Spas fehlt.' }),
      ...(!ownerName.trim() && { ownerName: 'Der eigene Name fehlt.' }),
      ...(!ownerEmail.includes('@') && { ownerEmail: 'Die E-Mail-Adresse ist unvollständig. Sie braucht ein @.' }),
    }
    if (Object.keys(found).length > 0) return fail(found)
    setBusy(true)
    setError(null)
    const { response, error: problem } = await api.POST('/api/v1/tenant/signup', { body: { spaName, country, ownerName, ownerEmail } })
    if (!response.ok) {
      setBusy(false)
      const errors = (problem as { errors?: Record<string, string[]> } | undefined)?.errors
      return setError(errors ? Object.values(errors).flat().join(' ') : 'Das Spa konnte nicht registriert werden.')
    }

    // Ohne Passkey wäre das Konto nach dem Abmelden nicht mehr erreichbar; deshalb gleich hier anlegen.
    const added = await addPasskey().catch(() => false)
    await onSignedUp(added ? null : 'Spa angelegt. Der Passkey ist noch nicht gespeichert. Vor dem Abmelden „Passkey hinzufügen“ wählen.')
  }

  return (
    <form className="signup" ref={formRef} noValidate onSubmit={submit} aria-labelledby="signup-title">
      <h2 id="signup-title">Neues Spa registrieren</h2>
      <FormField label="Name des Spas" required error={errors.spaName}>
        {(p) => (
          <input
            {...p}
            value={spaName}
            onChange={(e) => {
              setSpaName(e.target.value)
              clear('spaName')
            }}
          />
        )}
      </FormField>
      <FormField label="Land">
        {(p) => (
          <select {...p} value={country} onChange={(e) => setCountry(e.target.value)}>
            {COUNTRIES.map((c) => (
              <option key={c.code} value={c.code}>
                {c.label}
              </option>
            ))}
          </select>
        )}
      </FormField>
      <FormField label="Ihr Name" required error={errors.ownerName}>
        {(p) => (
          <input
            {...p}
            value={ownerName}
            onChange={(e) => {
              setOwnerName(e.target.value)
              clear('ownerName')
            }}
          />
        )}
      </FormField>
      <FormField label="E-Mail" required width="short" error={errors.ownerEmail}>
        {(p) => (
          <input
            {...p}
            type="email"
            value={ownerEmail}
            onChange={(e) => {
              setOwnerEmail(e.target.value)
              clear('ownerEmail')
            }}
          />
        )}
      </FormField>
      <p className="muted">Die registrierende Person wird Inhaber:in und meldet sich danach mit einem Passkey an, ohne Passwort. Das Team lädt die Leitung in der Windows-App im Bereich „Team“ ein.</p>
      <div className="actions">
        <button className="primary" type="submit" aria-disabled={busy || undefined}>
          Spa registrieren und Passkey anlegen
        </button>
      </div>
      {error && <p role="alert">{error}</p>}
    </form>
  )
}
