import { useEffect, useState } from 'react'
import { api } from './api'
import { Alert, Field, Heading } from './components'
import { useT } from './i18n'
import { passkeysSupported, signInWithPasskey } from './passkeys'
import { navigate } from './router'
import { useSession } from './session'

/** Anmeldung ohne Passwort: Code per E-Mail, Passkey oder – falls eingerichtet – Google und Apple. */
export function SignInPage({ navigatedHere }: { navigatedHere: boolean }) {
  const { t, lang } = useT()
  const { refresh } = useSession()
  const [email, setEmail] = useState('')
  const [codeSentTo, setCodeSentTo] = useState<string | null>(null)
  const [code, setCode] = useState('')
  // Rückkehr von Google/Apple mit Fehler (?anmeldung=…): Hinweis gleich zeigen.
  const [error, setError] = useState<string | null>(() =>
    new URLSearchParams(window.location.search).has('anmeldung') ? t.signInFailed : null,
  )
  const [busy, setBusy] = useState(false)
  // Zähler: jeder neue Wert setzt den Fokus ins jeweilige Feld.
  const [emailFocus, setEmailFocus] = useState(0)
  const [codeFocus, setCodeFocus] = useState(1)
  const [providers, setProviders] = useState<string[]>([])

  useEffect(() => {
    api.GET('/api/online/auth/providers').then(
      ({ data }) => setProviders(data?.external ?? []),
      () => setProviders([]),
    )
  }, [])

  async function done() {
    await refresh()
    navigate({ page: 'mine' })
  }

  async function sendCode(event: React.FormEvent) {
    event.preventDefault()
    if (busy) return
    setError(null)
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
      setEmailFocus((n) => n + 1)
      return setError(t.invalidEmail)
    }
    setBusy(true)
    try {
      const { response } = await api.POST('/api/online/auth/code', { body: { email: email.trim(), language: lang } })
      if (response.ok) setCodeSentTo(email.trim())
      else {
        setError(t.invalidEmail)
        setEmailFocus((n) => n + 1)
      }
    } catch {
      setError(t.errorOffline)
    } finally {
      setBusy(false)
    }
  }

  async function verify(event: React.FormEvent) {
    event.preventDefault()
    if (busy || code.replace(/\s/g, '').length !== 6) return
    setError(null)
    setBusy(true)
    try {
      const { response } = await api.POST('/api/online/auth/code/verify', { body: { email: codeSentTo!, code: code.replace(/\s/g, '') } })
      if (response.ok) await done()
      else {
        setError(t.codeInvalid)
        setCodeFocus((n) => n + 1)
      }
    } catch {
      setError(t.errorOffline)
    } finally {
      setBusy(false)
    }
  }

  async function passkey() {
    setError(null)
    try {
      if (await signInWithPasskey()) await done()
      else setError(t.passkeyFailed)
    } catch {
      setError(t.passkeyFailed)
    }
  }

  return (
    <>
      <Heading focus={navigatedHere}>{t.signInTitle}</Heading>
      <p>{t.signInIntro}</p>
      {error && error !== t.invalidEmail && error !== t.codeInvalid && <Alert>{error}</Alert>}

      {codeSentTo === null ? (
        <form onSubmit={sendCode} noValidate>
          <Field
            label={t.email}
            value={email}
            onChange={setEmail}
            type="email"
            autoComplete="email"
            required
            maxLength={254}
            error={error === t.invalidEmail ? error : undefined}
            focus={emailFocus}
          />
          <div className="actions">
            <button type="submit" aria-disabled={busy}>
              {busy ? t.sendingCode : t.sendCode}
            </button>
          </div>
        </form>
      ) : (
        <form onSubmit={verify} noValidate>
          {/* Der Hinweis hängt am Codefeld, das den Fokus bekommt: so wird er beim Wechsel vorgelesen. */}
          <Field
            label={t.code}
            value={code}
            onChange={setCode}
            autoComplete="one-time-code"
            inputMode="numeric"
            required
            maxLength={7}
            hint={t.codeSent(codeSentTo)}
            error={error === t.codeInvalid ? error : undefined}
            focus={codeFocus}
          />
          <div className="actions">
            <button type="submit" aria-disabled={busy || code.replace(/\s/g, '').length !== 6}>
              {t.confirmCode}
            </button>
            <button
              type="button"
              className="link"
              onClick={() => {
                setCodeSentTo(null)
                setCode('')
                setEmailFocus((n) => n + 1)
              }}
            >
              {t.otherEmail}
            </button>
          </div>
        </form>
      )}

      {(passkeysSupported() || providers.length > 0) && (
        <>
          <p className="divider">
            <span>{t.or}</span>
          </p>
          <div className="actions stacked">
            {passkeysSupported() && (
              <button type="button" className="secondary" onClick={passkey}>
                {t.withPasskey}
              </button>
            )}
            {providers.map((p) => (
              <a
                key={p}
                className="button secondary"
                href={`/api/online/auth/external/${encodeURIComponent(p)}?returnUrl=${encodeURIComponent('/buchen/#/termine')}`}
              >
                {t.withProvider(p)}
              </a>
            ))}
          </div>
        </>
      )}
    </>
  )
}
