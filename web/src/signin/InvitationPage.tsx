import { useEffect, useState } from 'react'
import { api, type InvitationPreview } from '../api/client'
import { ROLE_HINT, ROLE_LABEL } from './roles'
import { addPasskey } from '../auth/passkeys'

/** Einladung annehmen: Konto anlegen und direkt im Anschluss den Passkey auf diesem Gerät speichern. */
export function InvitationPage({ token, onAccepted }: { token: string; onAccepted: (warning: string | null) => Promise<void> }) {
  const [preview, setPreview] = useState<InvitationPreview | null | undefined>(undefined)
  const [message, setMessage] = useState<string | null>(null)
  const [failed, setFailed] = useState(false)
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    void api
      .GET('/api/v1/auth/invitations/{token}', { params: { path: { token } } })
      .then(
        ({ data }) => data,
        () => undefined,
      )
      .then((data) => {
        // Eine ungültige Einladung soll beim Neuladen nicht wieder auftauchen und nicht im Verlauf stehen bleiben.
        if (!data) window.history.replaceState(null, '', '/anmelden/')
        setPreview(data ?? null)
      })
  }, [token])

  async function accept() {
    if (busy) return
    setBusy(true)
    setFailed(false)
    const { response } = await api.POST('/api/v1/auth/invitations/{token}/accept', { params: { path: { token } } })
    if (!response.ok) {
      setBusy(false)
      setFailed(true)
      return setMessage('Die Einladung konnte nicht angenommen werden.')
    }

    // Ohne Passkey wäre das Konto nach dem Abmelden nicht mehr erreichbar; deshalb gleich hier anlegen.
    const added = await addPasskey().catch(() => false)
    window.history.replaceState(null, '', '/anmelden/')
    // Die Seite verschwindet mit dem Annehmen; die Warnung zeigt deshalb die Seite „Anmelden“ an.
    await onAccepted(added ? null : 'Konto angelegt. Der Passkey ist noch nicht gespeichert. Vor dem Abmelden „Passkey hinzufügen“ wählen.')
  }

  if (preview === undefined) return <p className="muted">Lade Einladung …</p>
  if (preview === null)
    return (
      <section>
        <h2>Einladung</h2>
        <p role="alert">Diese Einladung ist abgelaufen, wurde zurückgezogen oder schon angenommen. Die Person, die eingeladen hat, um einen neuen Link bitten.</p>
      </section>
    )

  return (
    <section aria-labelledby="invitation-title">
      <h2 id="invitation-title">Willkommen, {preview.displayName}</h2>
      <p>
        Eingeladen als <strong>{ROLE_LABEL[preview.role]}</strong> für {preview.locationNames.join(', ')}.
      </p>
      <p className="muted">{ROLE_HINT[preview.role]}</p>
      <p className="muted">
        Angemeldet wird mit einem Passkey auf diesem Gerät (Fingerabdruck, Gesicht oder Geräte-PIN), ohne Passwort. Konto: {preview.email}
      </p>
      <div className="actions">
        <button className="primary" type="button" onClick={accept} aria-disabled={busy || undefined}>
          Einladung annehmen und Passkey anlegen
        </button>
      </div>
      {message && <p role={failed ? 'alert' : 'status'}>{message}</p>}
    </section>
  )
}
