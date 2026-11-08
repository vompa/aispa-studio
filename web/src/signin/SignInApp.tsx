import { useCallback, useEffect, useState } from 'react'
import { api, type CurrentUser } from '../api/client'
import { addPasskey, passkeysSupported, signInWithPasskey } from '../auth/passkeys'
import { DevAccounts } from './DevAccounts'
import { InvitationPage } from './InvitationPage'
import { PasswordLogin } from './PasswordLogin'
import { SignupForm } from './SignupForm'

type Notice = { text: string; error: boolean }
type DeviceCode = { code: string; until: string }

const params = () => new URLSearchParams(window.location.search)

const MUST_CHANGE_PASSWORD =
  'Das Startpasswort gilt noch. Zuerst in der Windows-App mit dem Startpasswort anmelden und dort ein eigenes Passwort wählen, dann geht es hier weiter.'

/**
 * Rückkehr nach der Anmeldung (`?zurueck=`): nur eigene Pfade, kein offener Redirect. Der Wert wird wie vom Browser gegen die eigene
 * Adresse aufgelöst (der URL-Parser entfernt Tab und Zeilenumbruch, aus „/\t/host“ wird „//host“) und gilt nur bei gleichem Ursprung.
 */
function ownReturnPath(value: string | null): string | null {
  if (!value || !value.startsWith('/') || value.startsWith('//') || value.startsWith('/\\')) return null
  let url: URL
  try {
    url = new URL(value, window.location.origin)
  } catch {
    return null
  }
  return url.origin === window.location.origin ? url.pathname + url.search + url.hash : null
}

/**
 * Seite „Anmelden“ (Slice 103): ersetzt die Anmeldung der früheren Web-Team-Oberfläche. Server: Passkey, Einladung, neues Spa und
 * in der Entwicklung die Testkonten; Einzelplatz: Benutzername und Passwort. Angemeldet gibt es den Code für die Windows-App.
 */
export function SignInApp({ navigate = (path: string) => window.location.assign(path) }: { navigate?: (path: string) => void }) {
  const [standalone, setStandalone] = useState<boolean | null>(null)
  const [user, setUser] = useState<CurrentUser | null | undefined>(undefined)
  const [invitation, setInvitation] = useState(() => params().get('einladung'))
  const [paymentsDone] = useState(() => params().get('zahlungen') === '1')
  const [returnPath] = useState(() => ownReturnPath(params().get('zurueck')))
  const [signup, setSignup] = useState(false)
  const [notice, setNotice] = useState<Notice | null>(null)
  const [deviceCode, setDeviceCode] = useState<DeviceCode | null>(null)
  const [busy, setBusy] = useState(false)

  const loadUser = useCallback(async (): Promise<CurrentUser | null> => {
    try {
      const { data } = await api.GET('/api/v1/auth/me')
      setUser(data ?? null)
      return data ?? null
    } catch {
      setUser(null)
      return null
    }
  }, [])

  useEffect(() => {
    api.GET('/api/v1/auth/me').then(
      ({ data }) => setUser(data ?? null),
      () => setUser(null),
    )
    api.GET('/api/v1/system/modules').then(
      ({ data }) => setStandalone(data?.standalone === true),
      () => setStandalone(false),
    )
  }, [])

  /**
   * Nach jeder Anmeldung: zurück an den eigenen Ausgangspunkt oder auf dieser Seite angemeldet bleiben. Gilt noch ein Startpasswort,
   * bleibt die Seite stehen; ändern lässt es sich nur in der Windows-App.
   */
  const signedIn = useCallback(async () => {
    const me = await loadUser()
    if (me && !me.mustChangePassword && returnPath) navigate(returnPath)
  }, [loadUser, navigate, returnPath])

  async function run(action: () => Promise<void>) {
    if (busy) return
    setBusy(true)
    try {
      await action()
    } finally {
      setBusy(false)
    }
  }

  const passkeySignIn = () =>
    run(async () => {
      setNotice(null)
      const ok = await signInWithPasskey().catch(() => false)
      if (ok) await signedIn()
      else setNotice({ text: 'Die Anmeldung mit Passkey hat nicht geklappt. Noch einmal versuchen oder einen anderen Passkey wählen.', error: true })
    })

  const showDeviceCode = () =>
    run(async () => {
      setNotice(null)
      const { data } = await api.POST('/api/v1/auth/device-code').catch(() => ({ data: undefined }))
      if (!data) {
        setDeviceCode(null)
        return setNotice({ text: 'Der Code wurde nicht erzeugt. Verbindung prüfen und noch einmal versuchen.', error: true })
      }
      const until = new Date(data.expiresAt).toLocaleTimeString('de-AT', { hour: '2-digit', minute: '2-digit' })
      setDeviceCode({ code: data.code, until })
    })

  const addOwnPasskey = () =>
    run(async () => {
      const ok = await addPasskey().catch(() => false)
      setNotice(ok ? { text: 'Passkey hinzugefügt.', error: false } : { text: 'Der Passkey wurde nicht gespeichert. Noch einmal versuchen.', error: true })
      await loadUser()
    })

  const signOut = () =>
    run(async () => {
      await api.POST('/api/v1/auth/logout').catch(() => undefined)
      setDeviceCode(null)
      setNotice(null)
      await loadUser()
    })

  const message = notice && (
    <p role={notice.error ? 'alert' : 'status'} className={notice.error ? 'notice error' : 'notice'}>
      {notice.text}
    </p>
  )

  let content
  if (user === undefined || standalone === null) {
    content = (
      <p role="status" className="muted">
        Wird geladen …
      </p>
    )
  } else if (invitation && !standalone) {
    content = (
      <InvitationPage
        token={invitation}
        onAccepted={async (text) => {
          setInvitation(null)
          if (text) setNotice({ text, error: true })
          await loadUser()
        }}
      />
    )
  } else if (user) {
    content = (
      <section aria-labelledby="signed-in-title" className="panel">
        <h2 id="signed-in-title">Angemeldet als {user.displayName}</h2>
        {user.access && <p className="muted">{user.access.tenantName}</p>}
        {user.mustChangePassword && (
          <p role="alert" className="notice error">
            {MUST_CHANGE_PASSWORD}
          </p>
        )}
        <p>Mit einem Code meldet sich die Windows-App mit diesem Zugang an. Der Code gilt einmal und nur kurz.</p>
        <div className="actions">
          <button type="button" className="primary" aria-disabled={busy || undefined} onClick={() => void showDeviceCode()}>
            Code für die Windows-App
          </button>
          {!standalone && (
            <button type="button" aria-disabled={busy || undefined} onClick={() => void addOwnPasskey()}>
              Passkey hinzufügen
            </button>
          )}
          <button type="button" className="ghost" aria-disabled={busy || undefined} onClick={() => void signOut()}>
            Abmelden
          </button>
        </div>
        {deviceCode && (
          <p role="status" className="notice device-code">
            Code: <strong className="code">{deviceCode.code}</strong> (gültig bis {deviceCode.until}, einmal verwendbar). In der Windows-App eintippen.
          </p>
        )}
        {message}
        <nav aria-label="Weiter" className="links">
          <a href="/mein-tag/">Mein Tag</a>
          <a href="/help/">Hilfe</a>
        </nav>
      </section>
    )
  } else if (standalone) {
    content = <PasswordLogin onSignedIn={signedIn} />
  } else {
    content = (
      <>
        <section aria-labelledby="auth-title" className="panel">
          <h2 id="auth-title">Anmeldung</h2>
          <p>Angemeldet wird mit einem Passkey auf diesem Gerät (Fingerabdruck, Gesicht oder Geräte-PIN).</p>
          <div className="actions">
            {passkeysSupported() && (
              <button type="button" className="primary" aria-disabled={busy || undefined} onClick={() => void passkeySignIn()}>
                Mit Passkey anmelden
              </button>
            )}
            <button type="button" className="ghost" aria-expanded={signup} onClick={() => setSignup(!signup)}>
              Neues Spa registrieren
            </button>
          </div>
          {!passkeysSupported() && <p className="muted">Dieser Browser kann keine Passkeys. Die Seite in einem aktuellen Browser öffnen.</p>}
          {message}
          <DevAccounts
            onSignedIn={async (text) => {
              setNotice({ text, error: false })
              await signedIn()
            }}
            onFailed={(text) => setNotice({ text, error: true })}
          />
        </section>
        {signup && (
          <SignupForm
            onSignedUp={async (text) => {
              setSignup(false)
              if (text) setNotice({ text, error: true })
              await signedIn()
            }}
          />
        )}
      </>
    )
  }

  return (
    <>
      <header className="bar">
        <h1>AISPA Studio</h1>
      </header>
      <main>
        {paymentsDone && (
          <p role="status" className="notice">
            Zahlungskonto verbunden. Zurück in der Windows-App: Verwaltung → Zahlungen.
          </p>
        )}
        {content}
      </main>
    </>
  )
}
