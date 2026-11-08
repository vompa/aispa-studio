import { useState } from 'react'
import { passkeysSupported, signInWithPasskey } from '../auth/passkeys'

/** Anmeldung am Handy: Passkey direkt in der App, sonst der Weg über die Anmeldeseite. */
export function SignIn({ onSignedIn }: { onSignedIn: () => void }) {
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function passkey() {
    if (busy) return
    setBusy(true)
    setError(null)
    let ok = false
    try {
      ok = await signInWithPasskey()
    } catch {
      ok = false
    }
    setBusy(false)
    if (ok) onSignedIn()
    else setError('Die Anmeldung mit Passkey hat nicht geklappt. Bitte noch einmal versuchen oder mit Passwort anmelden.')
  }

  return (
    <>
      <h2>Bitte anmelden</h2>
      <p>Die App zeigt die eigenen Termine. Dafür braucht es die Anmeldung.</p>
      {passkeysSupported() && (
        <div className="actions">
          <button type="button" aria-disabled={busy || undefined} onClick={() => void passkey()}>
            Mit Passkey anmelden
          </button>
        </div>
      )}
      {error && <p role="alert">{error}</p>}
      <p>
        <a href="/anmelden/?zurueck=/mein-tag/">Mit Passwort anmelden</a>
      </p>
      <p className="muted">Nach dem Anmelden mit Passwort geht es von selbst hierher zurück.</p>
    </>
  )
}
