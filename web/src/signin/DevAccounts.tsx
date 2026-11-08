import { useEffect, useState } from 'react'
import type { StaffRole } from '../api/client'
import { ROLE_LABEL } from './roles'

type DevAccount = { email: string; displayName: string; role: StaffRole; locations: string[] }

/**
 * Anmeldung mit den Testkonten des Beispiel-Spas, ohne Passkey. Die Endpunkte gibt es nur in der Entwicklungsumgebung der API
 * (auch im Docker-Testbetrieb) und deshalb nicht im OpenAPI-Dokument; anderswo antwortet die API mit 404 und hier erscheint nichts.
 */
export function DevAccounts({ onSignedIn, onFailed }: { onSignedIn: (message: string) => void; onFailed: (message: string) => void }) {
  const [accounts, setAccounts] = useState<DevAccount[]>([])
  const [email, setEmail] = useState('')

  useEffect(() => {
    fetch('/api/v1/auth/dev/accounts')
      .then((response) => (response.ok ? (response.json() as Promise<DevAccount[]>) : []))
      .then((list) => {
        setAccounts(list)
        setEmail(list[0]?.email ?? '')
      })
      .catch(() => setAccounts([]))
  }, [])

  if (accounts.length === 0) {
    return null
  }

  async function signIn() {
    const response = await fetch('/api/v1/auth/dev/login', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email }),
    })
    if (!response.ok) return onFailed('Die Anmeldung mit dem Testkonto hat nicht geklappt. Läuft die API in der Entwicklungsumgebung?')
    const account = accounts.find((a) => a.email === email)
    onSignedIn(`Angemeldet als ${account?.displayName ?? email}.`)
  }

  return (
    <div className="actions">
      <select aria-label="Testkonto des Beispiel-Spas" value={email} onChange={(e) => setEmail(e.target.value)}>
        {accounts.map((a) => (
          <option key={a.email} value={a.email}>
            {a.displayName} · {ROLE_LABEL[a.role]} · {a.locations.join(', ')}
          </option>
        ))}
      </select>
      <button type="button" onClick={signIn}>Als Testkonto anmelden (nur Entwicklung)</button>
    </div>
  )
}
