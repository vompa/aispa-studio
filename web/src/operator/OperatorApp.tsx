import { type FormEvent, useCallback, useEffect, useId, useRef, useState } from 'react'
import { api, type BookingChannel, type IntakeRouteDto, type OperatorSpaDto, type SubscriptionPlan } from '../api/client'
import { ConfirmDialog } from '../components/ConfirmDialog'
import { FormField } from '../components/FormField'
import { useFormErrors } from '../components/useFormErrors'

const KEY_STORE = 'aispa.betreiber.schluessel'

const CHANNELS: { value: BookingChannel; label: string; placeholder: string }[] = [
  { value: 'Sms', label: 'SMS', placeholder: '+43 660 1234567' },
  { value: 'WhatsApp', label: 'WhatsApp', placeholder: '+43 660 1234567' },
  { value: 'Phone', label: 'Telefon', placeholder: '+43 1 2345678' },
  { value: 'Email', label: 'Mail', placeholder: 'termine@spa.at' },
]

/** Abo-Stufen (ADR 0050); höhere Stufen enthalten alle Module der niedrigeren. */
const PLANS: { value: SubscriptionPlan; label: string; modules: string }[] = [
  { value: 'Start', label: 'Start', modules: 'Online-Buchung, Erinnerungen' },
  { value: 'Spa', label: 'Spa', modules: 'dazu Assistent, Wiederbuchung' },
  { value: 'SpaPlus', label: 'Spa Plus', modules: 'dazu Terminannahme, Benchmark' },
]

const channelLabel = (channel: BookingChannel) => CHANNELS.find((c) => c.value === channel)?.label ?? channel

/** Nur für diesen Tab: der Schlüssel ist ein Geheimnis und soll nicht dauerhaft im Browser liegen. */
function storedKey(): string {
  try {
    return sessionStorage.getItem(KEY_STORE) ?? ''
  } catch {
    return ''
  }
}

function storeKey(key: string | null) {
  try {
    if (key) sessionStorage.setItem(KEY_STORE, key)
    else sessionStorage.removeItem(KEY_STORE)
  } catch {
    // Privater Modus: dann bei jedem Neuladen neu eingeben.
  }
}

function problemText(error: unknown, fallback: string): string {
  const e = error as { errors?: Record<string, string[]>; detail?: string; title?: string } | undefined
  if (e?.errors) return Object.values(e.errors).flat().join(' ')
  return e?.detail ?? e?.title ?? fallback
}

/** Spas laden; ein falscher oder abgeschalteter Schlüssel wird aus dem Tab entfernt. */
async function fetchSpas(key: string): Promise<State> {
  let result
  try {
    result = await api.GET('/api/operator/spas', { headers: { 'X-Aispa-Operator-Key': key } })
  } catch {
    return { kind: 'failed', text: 'Der Server ist nicht erreichbar.' }
  }
  const status = result.response.status
  if (status === 403 || status === 404) {
    storeKey(null)
    return {
      kind: 'signedOut',
      error:
        status === 403 ? 'Der Schlüssel stimmt nicht.' : 'Der Betreiber-Zugang ist auf diesem Server nicht eingeschaltet (Operator:ApiKey).',
    }
  }
  return result.data ? { kind: 'ready', spas: result.data } : { kind: 'failed', text: 'Die Spas konnten nicht geladen werden.' }
}

type State =
  | { kind: 'signedOut'; error: string | null }
  | { kind: 'loading' }
  | { kind: 'ready'; spas: OperatorSpaDto[] }
  | { kind: 'failed'; text: string }

/**
 * Betreiber-Seite unter /betreiber/: alle Spas mit Postfach-Stand und Nummern; Nummern zuordnen und wieder lösen.
 * Zugang mit dem Betreiber-Schlüssel (Operator:ApiKey) statt einer Anmeldung, weil der Betreiber zu keinem Spa gehört.
 */
export function OperatorApp() {
  const [key, setKey] = useState(storedKey)
  const [state, setState] = useState<State>(() => (storedKey() ? { kind: 'loading' } : { kind: 'signedOut', error: null }))
  const [filter, setFilter] = useState('')
  const headers = { 'X-Aispa-Operator-Key': key }

  const apply = useCallback((next: State) => {
    if (next.kind === 'signedOut') setKey('')
    setState(next)
  }, [])
  const load = useCallback((current: string) => void fetchSpas(current).then(apply), [apply])

  useEffect(() => {
    const initial = storedKey()
    if (!initial) return
    let live = true
    void fetchSpas(initial).then((next) => live && apply(next))
    return () => {
      live = false
    }
  }, [apply])

  function signIn(given: string) {
    storeKey(given)
    setKey(given)
    setState({ kind: 'loading' })
    load(given)
  }

  function signOut() {
    storeKey(null)
    setKey('')
    setState({ kind: 'signedOut', error: null })
  }

  const shown =
    state.kind === 'ready'
      ? state.spas.filter((s) => s.name.toLocaleLowerCase('de').includes(filter.trim().toLocaleLowerCase('de')))
      : []

  return (
    <>
      <header className="bar">
        <h1>Betreiber</h1>
        {state.kind !== 'signedOut' && (
          <button type="button" className="ghost" onClick={signOut}>
            Abmelden
          </button>
        )}
      </header>
      <main>
        {state.kind === 'signedOut' && <SignIn error={state.error} onSubmit={signIn} />}
        {state.kind === 'loading' && <p role="status">Lade Spas …</p>}
        {state.kind === 'failed' && (
          <div role="alert">
            <p>{state.text}</p>
            <button type="button" onClick={() => load(key)}>
              Erneut versuchen
            </button>
          </div>
        )}
        {state.kind === 'ready' && (
          <>
            <p className="muted">
              {state.spas.length === 1 ? 'Ein Spa' : `${state.spas.length} Spas`}. Eine Nummer gehört genau einem Spa; Nachrichten an
              sie landen dort, Antworten und Erinnerungen gehen von ihr aus.
            </p>
            {state.spas.length > 5 && (
              <FormField label="Spa suchen" className="field">
                {(p) => <input {...p} type="search" value={filter} onChange={(e) => setFilter(e.target.value)} />}
              </FormField>
            )}
            {shown.map((spa) => (
              <SpaCard key={spa.id} spa={spa} headers={headers} onChanged={() => load(key)} />
            ))}
            {shown.length === 0 && <p>Kein Spa gefunden.</p>}
          </>
        )}
      </main>
    </>
  )
}

function SignIn({ error, onSubmit }: { error: string | null; onSubmit: (key: string) => void }) {
  const [value, setValue] = useState('')
  const formRef = useRef<HTMLFormElement>(null)
  const { errors, clear, fail } = useFormErrors(formRef)
  const errorId = useId()
  return (
    <form
      className="panel"
      ref={formRef}
      noValidate
      onSubmit={(e) => {
        e.preventDefault()
        if (!value.trim()) return fail({ key: 'Der Betreiber-Schlüssel fehlt. Gib ihn ein.' })
        onSubmit(value.trim())
      }}
    >
      <h2>Anmelden</h2>
      <p className="muted">Mit dem Betreiber-Schlüssel aus dem Geheimnisspeicher. Er bleibt nur in diesem Tab gespeichert.</p>
      <FormField label="Betreiber-Schlüssel" required className="field" error={errors.key}>
        {(p) => (
          <input
            {...p}
            type="password"
            autoComplete="current-password"
            value={value}
            // Die Ablehnung durch den Server kommt als Ansage (role="alert") und hängt zusätzlich am Feld.
            aria-invalid={p['aria-invalid'] || !!error}
            aria-describedby={[p['aria-describedby'], error ? errorId : undefined].filter(Boolean).join(' ') || undefined}
            onChange={(e) => {
              setValue(e.target.value)
              clear('key')
            }}
          />
        )}
      </FormField>
      {error && (
        <p id={errorId} role="alert" className="error">
          {error}
        </p>
      )}
      <button type="submit">Anmelden</button>
    </form>
  )
}

function SpaCard({ spa, headers, onChanged }: { spa: OperatorSpaDto; headers: Record<string, string>; onChanged: () => void }) {
  const headingId = useId()
  return (
    <section className="panel" aria-labelledby={headingId}>
      <h2 id={headingId}>{spa.name}</h2>
      <p className="muted">
        {spa.country} · {spa.locations === 1 ? 'ein Standort' : `${spa.locations} Standorte`} · seit{' '}
        {new Date(spa.createdAt).toLocaleDateString('de-AT')}
      </p>
      <LockControl spa={spa} headers={headers} onChanged={onChanged} />
      <PlanControl spa={spa} headers={headers} onChanged={onChanged} />
      <p className={spa.mailboxError ? 'error' : undefined}>
        {!spa.mailboxAddress
          ? 'Kein eigenes Postfach.'
          : spa.mailboxError
            ? `Postfach ${spa.mailboxAddress}: ${spa.mailboxError}`
            : `Postfach ${spa.mailboxAddress}${spa.mailboxEnabled ? '' : ' (abgeschaltet)'}${
                spa.mailboxCheckedAt ? `, zuletzt abgerufen ${new Date(spa.mailboxCheckedAt).toLocaleString('de-AT')}` : ''
              }.`}
      </p>
      <h3>Nummern und Adressen</h3>
      {spa.routes.length === 0 ? (
        <p className="muted">Noch keine. Nachrichten erreichen dieses Spa nur über die Webhook-Adresse mit ?spa=.</p>
      ) : (
        <ul className="routes">
          {spa.routes.map((route) => (
            <RouteItem key={route.id} route={route} headers={headers} onChanged={onChanged} />
          ))}
        </ul>
      )}
      <AddRoute tenantId={spa.id} spaName={spa.name} headers={headers} onAdded={onChanged} />
    </section>
  )
}

/**
 * Abo-Stufe wechseln; wirkt nach spätestens 30 Sekunden, der Kern bleibt immer an. Die Auswahl allein ändert nichts
 * (Pfeiltasten wechseln bei geschlossenem Feld den Wert), erst „Stufe ändern“ und die Rückfrage.
 */
function PlanControl({ spa, headers, onChanged }: { spa: OperatorSpaDto; headers: Record<string, string>; onChanged: () => void }) {
  const current = spa.plan ?? 'SpaPlus'
  const [selected, setSelected] = useState<SubscriptionPlan>(current)
  const [confirming, setConfirming] = useState(false)
  const [message, setMessage] = useState<{ error: boolean; text: string } | null>(null)
  const id = useId()
  const selectRef = useRef<HTMLSelectElement>(null)
  const wasConfirming = useRef(false)

  // Nach der Rückfrage ist „Stufe ändern“ gesperrt und nimmt den Fokus nicht zurück; er geht ins Auswahlfeld. Erst im
  // Effekt, weil das Feld hinter dem offenen Modal nicht fokussierbar ist.
  useEffect(() => {
    if (wasConfirming.current && !confirming) selectRef.current?.focus()
    wasConfirming.current = confirming
  }, [confirming])

  async function change() {
    setConfirming(false)
    const { response, error: problem } = await api.PUT('/api/operator/spas/{tenantId}/plan', {
      params: { path: { tenantId: spa.id } },
      headers,
      body: { plan: selected },
    })
    if (response.ok) {
      setMessage({ error: false, text: 'Stufe geändert.' })
      onChanged()
    } else {
      setMessage({ error: true, text: problemText(problem, 'Die Abo-Stufe konnte nicht geändert werden.') })
    }
  }

  return (
    <div className="field">
      <label htmlFor={id}>Abo-Stufe</label>
      <div className="plan">
        <select
          id={id}
          ref={selectRef}
          value={selected}
          onChange={(e) => {
            setSelected(e.target.value as SubscriptionPlan)
            setMessage(null)
          }}
        >
          {PLANS.map((p) => (
            <option key={p.value} value={p.value}>
              {p.label} ({p.modules})
            </option>
          ))}
        </select>
        <button type="button" disabled={selected === current} onClick={() => setConfirming(true)}>
          Stufe ändern
        </button>
      </div>
      {message && (
        <p role={message.error ? 'alert' : 'status'} className={message.error ? 'error' : 'success'}>
          {message.text}
        </p>
      )}
      {confirming && (
        <ChangePlanDialog
          spaName={spa.name}
          from={current}
          to={selected}
          onConfirm={() => void change()}
          onCancel={() => {
            setConfirming(false)
            setSelected(current)
          }}
        />
      )}
    </div>
  )
}

const planOf = (plan: SubscriptionPlan) => PLANS.find((p) => p.value === plan)

/** Rückfrage vor dem Stufenwechsel: Abbrechen hat den Fokus, Esc ist Abbrechen; die Stufe bestimmt Kosten und Module. */
function ChangePlanDialog({
  spaName,
  from,
  to,
  onConfirm,
  onCancel,
}: {
  spaName: string
  from: SubscriptionPlan
  to: SubscriptionPlan
  onConfirm: () => void
  onCancel: () => void
}) {
  return (
    <ConfirmDialog
      title="Abo-Stufe ändern?"
      text={`${spaName} wechselt von ${planOf(from)?.label} auf ${planOf(to)?.label}: ${planOf(to)?.modules}. Die Module wirken nach spätestens 30 Sekunden.`}
      confirmLabel="Stufe ändern"
      danger={false}
      onConfirm={onConfirm}
      onCancel={onCancel}
    />
  )
}

/** Sperren mit Grund (das Team sieht ihn) und Rückfrage; Entsperren sofort, weil es nichts kaputt macht. */
function LockControl({ spa, headers, onChanged }: { spa: OperatorSpaDto; headers: Record<string, string>; onChanged: () => void }) {
  const [open, setOpen] = useState(false)
  const [reason, setReason] = useState('')
  const [error, setError] = useState<string | null>(null)
  const formRef = useRef<HTMLFormElement>(null)
  const { errors, clear, fail } = useFormErrors(formRef)
  const params = { params: { path: { tenantId: spa.id } }, headers }
  // Das Formular ersetzt den Auslöser: ins Feld „Grund“ springen und beim Schließen zurück zum Auslöser (B59).
  const triggerRef = useRef<HTMLButtonElement>(null)
  const reasonRef = useRef<HTMLInputElement>(null)
  const wasOpen = useRef(false)
  useEffect(() => {
    if (open) reasonRef.current?.focus()
    else if (wasOpen.current) triggerRef.current?.focus()
    wasOpen.current = open
  }, [open])

  async function lock(e: FormEvent) {
    e.preventDefault()
    if (!reason.trim()) return fail({ reason: 'Der Grund fehlt. Trage ihn ein, das Team sieht ihn.' })
    const { response, error: problem } = await api.POST('/api/operator/spas/{tenantId}/lock', { ...params, body: { reason } })
    if (response.ok) {
      setOpen(false)
      setReason('')
      setError(null)
      clear('reason')
      onChanged()
    } else {
      fail({ reason: problemText(problem, 'Sperren hat nicht geklappt.') })
    }
  }

  async function unlock() {
    const { response } = await api.DELETE('/api/operator/spas/{tenantId}/lock', params)
    if (response.ok) onChanged()
    else setError('Entsperren hat nicht geklappt.')
  }

  if (spa.lockedAt) {
    return (
      <div className="locked">
        <p className="error">
          Gesperrt seit {new Date(spa.lockedAt).toLocaleString('de-AT')}
          {spa.lockReason ? `: ${spa.lockReason}` : ''}
        </p>
        <button type="button" onClick={() => void unlock()}>
          Entsperren
        </button>
        {error && <p role="alert">{error}</p>}
      </div>
    )
  }

  return open ? (
    <form className="add" ref={formRef} noValidate onSubmit={(e) => void lock(e)} aria-label={`${spa.name} sperren`}>
      <p className="muted">
        Das Team sieht und exportiert weiter alles, kann aber nichts ändern. Online sind keine neuen Buchungen möglich.
      </p>
      <FormField label="Grund (sieht das Team)" required className="field" error={errors.reason}>
        {(p) => (
          <input
            {...p}
            ref={reasonRef}
            maxLength={500}
            value={reason}
            onChange={(e) => {
              setReason(e.target.value)
              clear('reason')
            }}
          />
        )}
      </FormField>
      <button type="submit" className="danger">
        Wirklich sperren
      </button>
      <button
        type="button"
        className="ghost"
        onClick={() => {
          setOpen(false)
          clear('reason')
        }}
      >
        Abbrechen
      </button>
    </form>
  ) : (
    <button ref={triggerRef} type="button" className="ghost" onClick={() => setOpen(true)}>
      Spa sperren
    </button>
  )
}

function RouteItem({ route, headers, onChanged }: { route: IntakeRouteDto; headers: Record<string, string>; onChanged: () => void }) {
  const [confirming, setConfirming] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const label = `${channelLabel(route.channel)} ${route.address}`
  // „Lösen“ wird durch die Rückfrage ersetzt: Fokus auf „Wirklich lösen“, beim Abbrechen zurück auf „Lösen“ (B59).
  const releaseRef = useRef<HTMLButtonElement>(null)
  const confirmRef = useRef<HTMLButtonElement>(null)
  const wasConfirming = useRef(false)
  useEffect(() => {
    if (confirming) confirmRef.current?.focus()
    else if (wasConfirming.current) releaseRef.current?.focus()
    wasConfirming.current = confirming
  }, [confirming])

  async function remove() {
    const { response } = await api.DELETE('/api/operator/routes/{routeId}', { params: { path: { routeId: route.id } }, headers })
    if (response.ok || response.status === 404) onChanged()
    else setError('Entfernen hat nicht geklappt.')
  }

  return (
    <li>
      <span>
        <strong>{channelLabel(route.channel)}</strong> {route.address}
        {route.senderId && <span className="muted"> · Kennung {route.senderId}</span>}
      </span>
      {confirming ? (
        <span className="confirm">
          <button ref={confirmRef} type="button" className="danger" onClick={() => void remove()}>
            Wirklich lösen
          </button>
          <button type="button" className="ghost" onClick={() => setConfirming(false)}>
            Abbrechen
          </button>
        </span>
      ) : (
        <button ref={releaseRef} type="button" className="ghost" aria-label={`${label} lösen`} onClick={() => setConfirming(true)}>
          Lösen
        </button>
      )}
      {error && <p role="alert">{error}</p>}
    </li>
  )
}

function AddRoute({
  tenantId,
  spaName,
  headers,
  onAdded,
}: {
  tenantId: string
  spaName: string
  headers: Record<string, string>
  onAdded: () => void
}) {
  const [channel, setChannel] = useState<BookingChannel>('Sms')
  const [address, setAddress] = useState('')
  const [senderId, setSenderId] = useState('')
  const [message, setMessage] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const formRef = useRef<HTMLFormElement>(null)
  const { errors, clear, fail } = useFormErrors(formRef)
  const placeholder = CHANNELS.find((c) => c.value === channel)?.placeholder
  const addressLabel = channel === 'Email' ? 'Adresse' : 'Nummer'

  async function submit(e: FormEvent) {
    e.preventDefault()
    setMessage(null)
    if (!address.trim()) return fail({ address: `Die ${addressLabel} fehlt. Trage sie ein.` })
    if (channel === 'Email' && !address.includes('@')) return fail({ address: 'Die E-Mail-Adresse ist unvollständig. Sie braucht ein @.' })
    setBusy(true)
    const { data, error } = await api.POST('/api/operator/routes', {
      body: { tenantId, channel, address, senderId: channel === 'WhatsApp' && senderId.trim() ? senderId.trim() : null },
      headers,
    })
    setBusy(false)
    if (data) {
      setAddress('')
      setSenderId('')
      setMessage(`${channelLabel(data.channel)} ${data.address} gehört jetzt zu ${spaName}.`)
      onAdded()
    } else {
      fail({ address: problemText(error, 'Zuordnen hat nicht geklappt.') })
    }
  }

  return (
    <form className="add" ref={formRef} noValidate onSubmit={(e) => void submit(e)} aria-label={`Nummer für ${spaName} zuordnen`}>
      <FormField label="Kanal" className="field">
        {(p) => (
          <select {...p} value={channel} onChange={(e) => setChannel(e.target.value as BookingChannel)}>
            {CHANNELS.map((c) => (
              <option key={c.value} value={c.value}>
                {c.label}
              </option>
            ))}
          </select>
        )}
      </FormField>
      <FormField label={addressLabel} required className="field" error={errors.address}>
        {(p) => (
          <input
            {...p}
            type={channel === 'Email' ? 'email' : 'tel'}
            placeholder={placeholder}
            value={address}
            onChange={(e) => {
              setAddress(e.target.value)
              clear('address')
            }}
          />
        )}
      </FormField>
      {channel === 'WhatsApp' && (
        <FormField label="Phone-Number-ID (Meta)" className="field">
          {(p) => <input {...p} value={senderId} onChange={(e) => setSenderId(e.target.value)} />}
        </FormField>
      )}
      <button type="submit" disabled={busy}>
        Zuordnen
      </button>
      {message && (
        <p role="status" className="success">
          {message}
        </p>
      )}
    </form>
  )
}
